# ADR 004 - Idempotencia opt-in nas escritas

- Status: Aceito
- Data: 2026-10-02

## Contexto

Repetir uma chamada apos timeout pode criar um conflito de CPF/telefone ou
executar novamente uma atualizacao, exclusao ou restauracao. As constraints
existentes protegem regras de negocio, mas nao repetem a resposta original.

Os controllers delegam a facades e handlers; cada handler chama SaveChangesAsync.
Os POSTs consultam o recurso para produzir o DTO com timestamps e Location.

## Decisao

Adicionar metadado Idempotent nas sete escritas e middleware depois de routing,
autenticacao, autorizacao e rate limiting. Header Idempotency-Key opcional:
sem ele, o contrato existente e preservado. Login e leituras nao participam.

Chave de 1-128 caracteres ASCII `[A-Za-z0-9._:-]`, escopo por emissor JWT validado
e identificador do usuario; persistir hashes SHA-256, nunca a chave em claro.
Fingerprint inclui metodo, rota/IDs/query e JSON canonicalizado. Whitespace e
ordem de propriedades nao importam; valores e ordem de arrays importam.

Persistir apenas 201/204, incluindo bytes do DTO, Content-Type e Location.
Idempotency-Replayed indica replay. Mesma chave/usuario com outra entrada gera
409 IdempotencyKeyReuse; erros nao consomem a chave. Cada tentativa conserva sua
propria correlacao e precisa passar novamente pelas policies/rate limiting.

A resposta e a mutacao pertencem a mesma transacao nao diferida no SQLite,
usando o ApplicationDbContext scoped dos handlers. Bufferizar Stream,
BodyWriter e StartAsync: nenhuma resposta de sucesso sai antes do commit.
Queda antes de commit reverte ambos; depois de commit, manter a resposta
mesmo que a entrega falhe. Nao ha lease nem registro Pending confirmado.

O SQLite tem um escritor por banco. Configurar timeout real do provider
(inicialmente 2s); SQLITE_BUSY/LOCKED gera 503 IdempotencyStoreBusy com Retry-After.
Nao repetir automaticamente a mutacao nem usar um lock em memoria.

Cache em memoria permanece singleton; decorator ICacheService e IPersonCache
sao scoped. Durante a transacao, bypass de leitura/preenchimento e invalidacoes
adiadas; apos commit, invalidar antes de enviar. Rollback descarta invalidacoes.

TTL de 24h configuravel, contado da conclusao; expiracao permite reuso e nova
avaliacao de negocio. Limpeza em lotes transacionais evita apagar uma substituicao
recente. Limites de request/response sao 1 MiB com chave; sem chave nao mudam.

## Consequencias

- Sem Redis, MediatR ou modificacao da entidade Person/IUnitOfWork.
- UNIQUE composto do escopo/chave e transacao oferecem deduplicacao entre conexoes.
- Respostas antigas podem ser repetidas mesmo depois de alterar/excluir o recurso.
- Dados pessoais do DTO ficam duplicados temporariamente no mesmo SQLite:
  proteger o volume e aplicar retencao; nao exportar esses corpos para telemetria.
- Streaming nao e suportado nas operacoes com chave; erros de serializacao/limite
  revertem a transacao e usam o tratamento global de erros.
- Cache continua por instancia; replicas e efeitos externos futuros exigem outro
  desenho. Isto nao promete exactly-once universal nem deduplicacao apos o TTL.

## Verificacao

Testes HTTP em arquivo SQLite migrado com conexoes independentes: sete escritas,
replay, conflitos, escopo, autorizacao, concorrencia, timeout, TTL, reinicio e
falhas antes/depois do commit. Testes unitarios verificam buffering/BodyWriter e
cache transacional. Migrations sao aplicadas sem recriar dados existentes.
