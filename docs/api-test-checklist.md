# Checklist de testes da API

**Data da execução:** 2026-10-02  
**Ambiente:** testes de integração via `WebApplicationFactory` e SQLite em memória isolado. O banco local da aplicação não foi usado nem alterado.

## Resultado geral

- [x] API compilada durante a execução da suíte de integração.
- [x] Suíte de integração executada após a inclusão de cobertura para GET por ID e PUT de telefone.
- [x] Fluxos de criação, consulta, atualização e exclusão de pessoas e telefones exercitados por HTTP.
- [x] Autenticação, autorização, validação, conflitos, health checks, rate limiting e cache exercitados.
- [x] Dados de teste isolados; nenhuma alteração persistente no banco de desenvolvimento.

## Endpoints e fluxos

| Status | Método e endpoint | O que foi verificado |
|---|---|---|
| [x] OK | `POST /api/v1/auth/login` | Login válido retorna token e roles; senha inválida e usuário inexistente retornam `401`; campos ausentes/vazios retornam `400`. |
| [x] OK | `POST /api/v1/persons` | Criação com CPF válido retorna `201`, ID e timestamps; nome vazio e CPF malformado retornam `400`; CPF inválido ou duplicado retorna `409`; usuário sem role Admin recebe `403`. |
| [x] OK | `GET /api/v1/persons` | Listagem paginada, busca por nome, acesso com Admin/User e acesso anônimo (`401`). |
| [x] OK | `GET /api/v1/persons/{id}` | Consulta da pessoa criada, atualização visível após cache e pessoa inexistente/excluída retorna `404`. |
| [x] OK | `PUT /api/v1/persons/{id}` | Atualização de nome e data, `204`, timestamps e invalidação do cache; apenas Admin pode gerenciar. |
| [x] OK | `DELETE /api/v1/persons/{id}` | Exclusão lógica retorna `204`; pessoa deixa de aparecer na consulta de ativos. |
| [x] OK | `POST /api/v1/persons/{id}/restore` | Restauração retorna `204` e a pessoa volta a ser consultável. |
| [x] OK | `GET /api/v1/persons/deleted` | Lista pessoas excluídas para Admin; User recebe `403`. |
| [x] OK | `POST /api/v1/persons/{personId}/phones` | Criação retorna `201`; pessoa inexistente retorna `404`; telefone inválido retorna `400`; User recebe `403`; número repetido ou sexto telefone retorna `409`. |
| [x] OK | `GET /api/v1/persons/{personId}/phones` | Lista telefones associados à pessoa e reflete a exclusão. |
| [x] OK | `GET /api/v1/persons/{personId}/phones/{phoneId}` | Consulta por ID retorna o tipo e número persistidos, inclusive após atualização. |
| [x] OK | `PUT /api/v1/persons/{personId}/phones/{phoneId}` | Atualização de tipo e número retorna `204`; GET subsequente confirma os novos dados. |
| [x] OK | `DELETE /api/v1/persons/{personId}/phones/{phoneId}` | Exclusão retorna `204`; o telefone removido deixa de constar na listagem. |
| [x] OK | `GET /health` | Liveness retorna `200` e `Healthy`. |
| [x] OK | `GET /health/ready` | Readiness retorna `200` e `Healthy` quando o banco está disponível. |
| [x] OK | `GET /health/live` | Probe liveness usada no teste de rate limiting; quota restritiva produz `429` após rajada de chamadas. |

## Achados

- **Endpoints:** nenhum defeito funcional encontrado nos fluxos testados.
- **Cobertura adicionada nesta execução:** antes não havia teste de integração direto para `GET` de telefone por ID e atualização (`PUT`) de telefone. O teste `Should_GetAndUpdatePhone_When_PhoneExists` agora cobre ambos, verificando a persistência após a alteração.
- **Avisos do restore/build:** o NuGet reportou avisos `NU1903` de vulnerabilidades conhecidas para `SQLitePCLRaw.lib.e_sqlite3` 2.1.2, `Newtonsoft.Json` 9.0.1, `System.Net.Http` 4.3.0 e `System.Text.RegularExpressions` 4.3.0. Eles não falharam os testes, mas são pendências de dependências a avaliar separadamente.

## Execução

```powershell
dotnet test CandidateAssessment.sln --no-restore --verbosity minimal
```

**Resultado:** aprovado, 122 testes unitários e 38 testes de integração (160 no total), zero falhas e zero testes ignorados.

## Evolução: idempotência e observabilidade

Validação adicional em 2026-10-02, preservando a execução histórica acima:

- [x] Sete escritas com header opcional, replay de 201/204, corpo e Location.
- [x] Escopo por usuário, conflitos de chave, validação, limites e autorização.
- [x] Transação atômica, falhas de serialização/completion, reinício e TTL de 24h.
- [x] SQLite em arquivo com conexões independentes: concorrência, timeout e upgrade de migration preservando dados.
- [x] Cache transacional: nenhum snapshot não confirmado; invalidação após commit.
- [x] Kestrel real: headers mantidos na primeira entrega/replay e 204 sem escrita de corpo ou exceção posterior.
- [x] Correlação exata de traceId em erros/replays, sampling e privacidade dos sinais.
- [x] Imagem Docker com runtime .NET 6, usuário não root e migration demonstrativa opt-in.
- [x] Pipeline real Collector → Jaeger/Prometheus/Grafana: trace conhecido com 17 spans, outcomes executed/replayed e 10 painéis provisionados.
- [x] Collector indisponível: escrita/replay e readiness continuam funcionando; falha de exportação é logada sem payload sensível.

O build usa SDK .NET 6.0.428, fixado em `global.json`, com C# 10,
mantendo `net6.0` e runtime 6. Docker e CI usam o mesmo SDK.
Os finais de linha, newline final e imports foram normalizados.
O gate completo de formatação e analisadores foi aprovado com o SDK 6.

## Compatibilidade com o SDK .NET 6

Validação em 2026-10-03:

- [x] `dotnet --version`: SDK 6.0.428, selecionado por `global.json`.
- [x] Seis projetos com `net6.0`, referências válidas e inclusão em `CandidateAssessment.sln`.
- [x] `dotnet clean` seguido de `dotnet build CandidateAssessment.sln -c Release -warnaserror`: seis projetos compilados com C# 10, zero avisos e zero erros.
- [x] 178 testes unitários e 106 de integração aprovados em Release, com as configurações de cobertura da CI.
- [x] Tarefa `test` do VS Code: build Debug e os mesmos 284 testes aprovados.
- [x] `dotnet format CandidateAssessment.sln --verify-no-changes`: gate completo aprovado.
- [x] Resumo real da CI executado sobre os dois relatórios Cobertura, com contadores de linhas válidas/cobertas; ausência de relatórios rejeitada com código 1.
- [x] `dotnet-ef` 6.0.36 restaurado e as duas migrations listadas sem conexão com o banco.
- [x] Build Docker com SDK 6.0.428; container com runtime 6.0.36, usuário não root, health/readiness/liveness `Healthy` e Docker HEALTHCHECK `healthy`.
- [x] Solução XML removida; VS Code configurado para abrir `CandidateAssessment.sln`.
- [ ] Descoberta no Test Explorer após recarregar a janela do VS Code.

O diagnóstico CA1050 é suprimido somente na classe parcial global `Program`,
necessária ao hosting e ao `WebApplicationFactory<Program>`. Os demais
analisadores continuam habilitados.

As etapas locais foram verificadas; esta validação não representa uma execução
remota do GitHub Actions nem uma afirmação de cobertura de código de 100%.
