# ADR 005 — Observabilidade sem dependência operacional do Collector

## Contexto e decisão

Preservar .NET 6, controllers/facades/handlers, Serilog e SQLite. Application
depende apenas de `ActivitySource` e `Meter` da BCL. A composição da API registra
o SDK OpenTelemetry; OTLP está desligado por padrão. Console e arquivos rolling
continuam funcionando sem a stack de observabilidade. Não há exportação de logs
para Grafana/Loki nem endpoint `/metrics` na API.

Pacotes fixados: `OpenTelemetry.Extensions.Hosting` e exporter OTLP **1.6.0**,
runtime instrumentation **1.7.0**. O restore/build em net6.0 valida a resolução
de assemblies compatíveis. Isso não estende o suporte da Microsoft ao .NET 6
(EOL). Versões 1.8.0–1.15.2 do exporter possuem
[GHSA-4625-4j76-fww9](https://github.com/advisories/GHSA-4625-4j76-fww9).
O SDK 1.15.3 trouxe dependências de .NET 10 e ambiguidades de Options em net6;
o grafo selecionado antecede o recurso de disk retry afetado. Atualizar o SDK
exige revalidar o grafo, auditoria de dependências e execução dos testes.

## Sinais e privacidade

- Middleware depois de routing e antes de autenticação/rate limiting cria um
  span servidor W3C, preserva parent `traceparent`/`tracestate` e mantém `TraceId`
  legado como `Activity.Id`, também usado em `ProblemDetails.traceId`. Todos os
  logs no escopo recebem `OtelTraceId` (32 hex), `SpanId` e `RequestId`. Replays
  recebem sua própria requisição/span, não os identificadores da resposta gravada.
  O ID completo do span servidor é capturado em `RequestCorrelation` antes dos
  spans filhos; erros MVC/idempotência mantêm igualdade exata com `TraceId`
  mesmo quando produzidos dentro de um span de sessão/handler.
- HTTP tem contador `candidate.http.request.count` e histograma em segundos
  `candidate.http.request.duration`; labels: método conhecido, rota template
  (ou `unmatched`) e status. Métodos desconhecidos viram `OTHER`. `/health*`
  e `/swagger*` não geram esses spans/métricas. Seu pipeline e rate limiting
  continuam iguais; `/health` inclui checks registrados/banco, `/health/ready`
  filtra `ready`, `/health/live` não verifica dependências.
- Handlers têm spans internos, duração e outcomes success/failed. SQLite tem
  spans client e duração/erros com operações limitadas `reader`, `scalar`,
  `nonquery`, cobrindo APIs síncronas e assíncronas do interceptor EF Core 6.
  A duração cobre execução do comando, não leitura/materialização posterior.
- Cache/idempotência/limpeza usam meters BCL próprios registrados na composição,
  `CandidateAssessment.Cache` e `CandidateAssessment.Idempotency`, com labels fechados.
  Nenhum label usa usuário, chave/hash, CPF, IDs ou paths concretos.
- Não ativamos instrumentação HTTP/EF automática que captura URLs ou SQL.
  Nenhum span inclui corpo, header de autenticação, parâmetros SQL, query string,
  mensagem/stack trace de exceção ou baggage propagado. Falhas indicam status e
  tipo, sem mensagem. Sampling não desliga os contadores.
- Um sink sanitizador comum aos destinos Serilog remove exceções e mascara
  propriedades fora de uma allowlist técnica. Isso também protege eventos dos
  loggers existentes de EF/exception middleware. Não configurar sinks externos
  por `Serilog:WriteTo`: os destinos são compostos explicitamente para evitar
  duplicação e bypass da sanitização. Templates de logs devem ser estáticos;
  jamais interpolar dados pessoais no template.

## Configuração

Seção `Observability` (nomes com `__` equivalem a variáveis de ambiente):

| Opção | Padrão |
|---|---|
| `ServiceName` | `CandidateAssessment.Api` |
| `SamplingRatio` | `1` (parent-based; ajustar proporção em produção) |
| `OtlpEnabled` | `false` |
| `OtlpEndpoint` | `http://localhost:4317` |
| `OtlpProtocol` | `grpc` (`http/protobuf` também permitido) |
| `ExportTimeoutMilliseconds` | `5000` |
| `MetricExportIntervalMilliseconds` | `15000` |
| `MaxQueueSize` / `MaxExportBatchSize` | `2048` / `512` |

As opções são validadas no startup, inclusive sem exportação: proporção [0,1],
limites positivos, lote não maior que fila, endpoint HTTP(S) sem credenciais,
query ou fragmento; gRPC exige URL raiz. HTTP/protobuf usa URL base; a composição
acrescenta `/v1/traces` e `/v1/metrics`. Exportação usa lotes de no máximo 512
spans por padrão, intervalo 5s, timeout limitado e fila em memória; não há
garantia de entrega em falha/quedas. Headers de autenticação OTLP não são
herdados implicitamente de ambiente. TLS/autenticação de uma implantação
produtiva precisam de desenho específico, não do Collector local sem TLS.

Resource inclui `service.name`, versão do assembly, instância/máquina e
`deployment.environment`. `Serilog:ConsoleJson=true` habilita JSON compacto sem
mudar o arquivo rolling. Falhas efetivas de transporte/execução do exporter geram
`candidate.telemetry.export.failure.count`; outros diagnósticos inesperados do
SDK usam `candidate.telemetry.sdk.diagnostic.count`. Logs incluem somente nome
do EventSource/evento, ID, nível e categoria, nunca payloads. Há no máximo um
aviso/minuto por categoria (SDK/exporter), evitando que um aviso do SDK esconda
uma falha do Collector. O SDK 1.6 também avisa quando ignora intencionalmente
instruments de meters não inscritos; somente esse motivo conhecido é rebaixado
a Debug, sem contador de falha. Outros motivos não são suprimidos.
Indisponibilidade do Collector não altera escritas ou readiness. Configuração
inválida, diferentemente de indisponibilidade, impede o startup.

O evento de início é registrado em `ApplicationStarted`, com ambiente capturado
antes de `RunAsync`. Não há anúncio de início nem acesso ao host descartado
depois do encerramento.

## Stack local opcional

Usar o Compose base com `docker-compose.observability.yml` como segundo arquivo.
Definir `GRAFANA_ADMIN_PASSWORD` no ambiente local antes de subir; o overlay
recusa senha ausente e não contém credencial versionada. A API mantém sua porta
e probes; nenhuma dependência de Collector é adicionada à API.

```powershell
# GRAFANA_ADMIN_PASSWORD deve ser injetada no ambiente antes deste comando.
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d --build
# Parar sem apagar os volumes existentes:
docker compose -f docker-compose.yml -f docker-compose.observability.yml down
```

Fluxo: API → OTLP Collector → traces Jaeger; métricas → exporter Prometheus do
Collector → Prometheus → dashboard Grafana. Collector/OTLP somente na rede
interna. UIs: Jaeger `http://127.0.0.1:16686`, Prometheus
`http://127.0.0.1:9090`, Grafana `http://127.0.0.1:3000`.
Dashboard **Candidate Assessment — API** é provisionado, com tráfego, frações
5xx/429/503, latência p50/p95/p99, cache, outcomes/duração/lock wait de
idempotência, remoções/falhas de limpeza e SQLite.
503 também integra 5xx; não somar as séries. Sem tráfego suficiente, quantis
e rates podem estar vazios. Não há SLO numérico presumido.

Imagens têm tags fixas. Jaeger usa memória demonstrativa e perde traces no
restart. Prometheus usa volume local com retenção máxima 24h/256MB (o primeiro
limite atingido prevalece); Grafana usa volume para estado local. Não são
garantias de retenção, backup ou configuração produtiva.

Para smoke test: executar uma escrita e seu replay com tracing conhecido,
aguardar exportação/scrape (até cerca de 30s), procurar `OtelTraceId` no Jaeger e
consultar `candidate_http_request_count_total` e
`candidateassessment_idempotency_operations_total` no Prometheus. Parar somente o Collector e
repetir a escrita: resposta/probes devem funcionar e avisos locais aparecer.
Os testes automatizados usam listeners e sinks isolados, sem exigir Docker.
