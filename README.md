# CandidateAssessment

Web API em **.NET 6** demonstrando **Clean Architecture** com maturidade
de produção: autenticação, autorização baseada em policies, validação,
cache, observabilidade, versionamento, rate limiting, health checks,
Docker, testes em camadas e CI.

> ⚠️ **.NET 6** foi mantido por requisito da avaliação técnica. Esta
> versão está fora do período de suporte oficial da Microsoft desde
> novembro de 2024. A arquitetura foi desenhada para absorver uma
> migração futura para .NET 8+ sem reescrita de regras de negócio.

## Status

✅ Pronto para avaliação (Etapas 1–5 concluídas). Veja o roadmap em
[`docs/adr/`](./docs/adr/).

## Objetivo

Demonstrar organização de produção sem complexidade artificial: testes
significativos, contratos HTTP estáveis, separação de camadas
respeitada, e preocupações transversais isoladas.

## Requisitos

| Componente | Versão |
|---|---|
| .NET SDK | **6.0.x** (a CI fixa `actions/setup-dotnet@v3` com `6.0.x`) |
| Sistema operacional | Linux, macOS ou Windows |
| Docker | opcional (para execução containerizada) |
| dotnet-ef | instalado via `dotnet tool restore` (6.0.36) |

## Stack

- **ASP.NET Core 6** Web API + Controllers
- **Entity Framework Core 6** + SQLite
- **ASP.NET Core Identity** + **JWT Bearer**
- **FluentValidation**
- **Serilog** (console + arquivo com rolling diário)
- **IMemoryCache** atrás de `ICacheService`
- **AspNetCoreRateLimit**
- **Asp.Versioning.Mvc**
- **Swashbuckle / OpenAPI**
- **xUnit** + **Coverlet** para testes e coverage

## Arquitetura

```text
CandidateAssessment/
├── .github/workflows/ci.yml         # CI: build + format check + tests + coverage + Docker
├── docs/adr/                        # Architecture Decision Records
├── src/
│   ├── CandidateAssessment.Domain           # entidades, value objects, enums, exceptions
│   ├── CandidateAssessment.Application      # casos de uso + abstrações
│   ├── CandidateAssessment.Infrastructure   # EF Core, Identity, cache, time
│   └── CandidateAssessment.Api              # controllers, middleware, configuration
├── tests/
│   ├── CandidateAssessment.UnitTests        # Domain + Application (fakes)
│   └── CandidateAssessment.IntegrationTests # HTTP → EF → SQLite (WebApplicationFactory)
├── coverlet.runsettings             # cobertura consolidada para CI
├── Directory.Build.props            # analyzers centralizados
├── Directory.Build.targets          # supressões por escopo (testes, migrations)
├── docker-compose.yml
├── Dockerfile
├── .editorconfig
├── .dockerignore
└── .gitignore
```

Dependências entre camadas:

```text
Api ──────────► Application
Api ──────────► Infrastructure
Infrastructure ► Application
Infrastructure ► Domain
Application ───► Domain
Domain ────────► (apenas BCL)
```

Veja [`docs/adr/001-clean-architecture.md`](./docs/adr/001-clean-architecture.md).

## Decisões técnicas

1. **Clean Architecture com feature folders** na Application
   ([ADR 001](./docs/adr/001-clean-architecture.md)).
2. **EF Core 6 + SQLite** como provider padrão; `IUnitOfWork` +
   `IPersonRepository` + `IPhoneRepository` específicos por entidade
   ([ADR 002](./docs/adr/002-entity-framework-core.md)).
3. **Soft delete em `Person`** com filtro global e rota administrativa
   dedicada ([ADR 003](./docs/adr/003-soft-delete.md)).
4. **`AspNetCoreRateLimit`** em vez do middleware nativo do .NET 7+ (o
   projeto trava em .NET 6); isolado em
   `Api/Extensions/RateLimitingExtensions.cs` para troca futura.
5. **`Asp.Versioning.Mvc`** 6.0 para `Asp.Versioning.Mvc.ApiExplorer` —
   já oferece suporte explícito a `net6.0` ([docs
   oficiais](https://github.com/dotnet/aspnet-api-versioning)).
6. **Cache atrás de `ICacheService`** — `MemoryCacheService` é a única
   implementação hoje; trocar para Redis é uma substituição da
   `Infrastructure` sem tocar `Application`.

## Como executar

### Sem Docker (linha de comando)

```bash
git clone <repo>
cd <repo>

# 1. Restaurar ferramentas (dotnet-ef 6.0.36)
dotnet tool restore

# 2. Restaurar pacotes
dotnet restore CandidateAssessment.sln

# 3. Aplicar migrations (cria o SQLite a partir do schema versionado)
dotnet ef database update \
    --project src/CandidateAssessment.Infrastructure \
    --startup-project src/CandidateAssessment.Api

# 4. Executar a API
dotnet run --project src/CandidateAssessment.Api --launch-profile Development
```

A aplicação sobe em `http://localhost:5000` (perfil padrão do
`Properties/launchSettings.json`). Swagger fica disponível em
`http://localhost:5000/swagger`.

### Com Docker

```bash
docker compose up --build
```

A API escuta em `http://localhost:8080`. Os volumes `candidate-data` e
`candidate-logs` persistem o SQLite e os logs entre reinícios.

```bash
docker compose down            # para containers e remove rede
docker compose down -v         # também remove volumes (apaga dados)
```

## Configuração

Configurações tipadas via `Options Pattern`:

| Seção | Tipo | Origem padrão |
|---|---|---|
| `Jwt` | `JwtOptions` | `appsettings.json` (substitua `SigningKey` em produção) |
| `SeedUsers` | `SeedUsersOptions` | `appsettings.Development.json` |
| `Serilog` | `SerilogOptions` | `appsettings.json` |
| `Cache` | `CacheOptions` | `appsettings.json` |
| `IpRateLimiting` | (lib) | `appsettings.json` |

Sobrescreva via **variáveis de ambiente** com `__` (substituem
quaisquer pontos da hierarquia):

```bash
export Jwt__SigningKey="<sua chave de pelo menos 32 bytes>"
export ConnectionStrings__DefaultConnection="Data Source=/tmp/candidate.db"
export Serilog__MinimumLevel="Warning"
```

> 🚨 O `SigningKey` em `appsettings.json` é apenas um placeholder para
> desenvolvimento. Em produção, use secrets reais (Docker secrets, Azure
> Key Vault, AWS Secrets Manager, GitHub Actions secrets etc.).

## Migrations

```bash
# Listar migrations aplicadas/pendentes
dotnet ef migrations list \
    --project src/CandidateAssessment.Infrastructure \
    --startup-project src/CandidateAssessment.Api

# Criar nova migration a partir de mudança no modelo
dotnet ef migrations add <NomeSignificativo> \
    --project src/CandidateAssessment.Infrastructure \
    --startup-project src/CandidateAssessment.Api

# Aplicar migrations
dotnet ef database update \
    --project src/CandidateAssessment.Infrastructure \
    --startup-project src/CandidateAssessment.Api
```

> Em produção real, scripts controlados de migration são mais
> apropriados do que aplicar via tooling na inicialização. No escopo da
> avaliação, a CLI é suficiente.

## Credenciais de demonstração

Definidas em `appsettings.Development.json` (substituíveis por
variáveis de ambiente):

| Role | Username | Password |
|---|---|---|
| Admin | `admin` | `Admin@123` |
| User  | `user`  | `User@123` |

As senhas nunca são logadas. Para redefinir o banco: apague
`src/CandidateAssessment.Api/candidateassessment.db` e rode `dotnet ef
database update`.

## Swagger

Em **Development**, `http://localhost:<porta>/swagger` lista todos os
endpoints agrupados por controller. Use o botão **Authorize** e cole o
`accessToken` retornado por `/api/v1/auth/login` (esquema `Bearer`).

Em **Production**, o Swagger permanece apenas no `Dockerfile` se a env
`ASPNETCORE_ENVIRONMENT=Production` permitir — atualmente, ele só é
exposto em `Development` por design.

## Endpoints

### Autenticação

```http
POST /api/v1/auth/login
Content-Type: application/json

{ "username": "admin", "password": "Admin@123" }
```

Resposta `200`:

```json
{ "accessToken": "<jwt>", "expiresAt": "2026-09-30T23:00:00Z" }
```

### Persons

| Método | Rota | Roles | Resumo |
|---|---|---|---|
| POST | `/api/v1/persons` | Admin | Cria pessoa |
| GET | `/api/v1/persons` | Admin, User | Lista paginada (ativos) |
| GET | `/api/v1/persons/{id}` | Admin, User | Detalhe (cacheável) |
| PUT | `/api/v1/persons/{id}` | Admin | Atualiza |
| DELETE | `/api/v1/persons/{id}` | Admin | Soft delete |
| POST | `/api/v1/persons/{id}/restore` | Admin | Restaura |
| GET | `/api/v1/persons/deleted` | Admin | Lista inativos |

Filtros disponíveis: `?name=...&cpf=...`. Paginação: `?page=1&pageSize=20`
(máximo 100).

### Phones

| Método | Rota | Roles |
|---|---|---|
| POST | `/api/v1/persons/{personId}/phones` | Admin |
| GET | `/api/v1/persons/{personId}/phones` | Admin, User |
| GET | `/api/v1/persons/{personId}/phones/{phoneId}` | Admin, User |
| PUT | `/api/v1/persons/{personId}/phones/{phoneId}` | Admin |
| DELETE | `/api/v1/persons/{personId}/phones/{phoneId}` | Admin |

Regras: máximo 5 telefones por pessoa; `(PersonId, Number)` é UNIQUE.

## Roles e Policies

| Operação | User | Admin |
|---|:-:|:-:|
| Login | ✅ | ✅ |
| Consultar pessoa | ✅ | ✅ |
| Listar pessoas | ✅ | ✅ |
| Consultar telefones | ✅ | ✅ |
| Criar/Alterar/Excluir pessoa | ❌ | ✅ |
| Restaurar pessoa | ❌ | ✅ |
| Consultar `/persons/deleted` | ❌ | ✅ |
| Adicionar/Alterar/Excluir telefone | ❌ | ✅ |

Policies (em `Api/Authorization/AuthorizationPolicies.cs`):

- `CanReadPersons` → Admin ou User
- `CanManagePersons` → Admin
- `CanViewDeletedPersons` → Admin

## Testes

```bash
# Apenas unitários
dotnet test tests/CandidateAssessment.UnitTests/CandidateAssessment.UnitTests.csproj

# Apenas integração
dotnet test tests/CandidateAssessment.IntegrationTests/CandidateAssessment.IntegrationTests.csproj

# Solução completa (ambos)
dotnet test CandidateAssessment.sln
```

| Projeto | Quantidade | Tipo |
|---|---:|---|
| `CandidateAssessment.UnitTests` | 114 | Domain + Application (fakes) |
| `CandidateAssessment.IntegrationTests` | 35 | HTTP → EF → SQLite (WebApplicationFactory) |

Os testes de integração sobem a aplicação real em memória de processo e
apontam para um SQLite temporário. O `[Collection]`-based fan-out evita
conflitos de schema entre suítes.

## Coverage

```bash
dotnet test CandidateAssessment.sln --settings coverlet.runsettings \
    --collect:"XPlat Code Coverage"
```

Os relatórios `coverage.cobertura.xml` são gerados em
`tests/<projeto>/TestResults/<guid>/`. O `coverlet.runsettings` exclui
`Migrations/`, código gerado, e tipos `[Obsolete]`/`[GeneratedCode]`.

Resumo típico (medido localmente):

| Assembly | Line coverage |
|---|---:|
| `CandidateAssessment.Api` | ~94% |
| `CandidateAssessment.Infrastructure` | ~87% |
| `CandidateAssessment.Domain` | ~92% |
| `CandidateAssessment.Application` | ~65% |

A métrica é informativa — não usamos gate numérico rígido para evitar
testes inúteis apenas para inflar números.

## Health checks

```http
GET /health         # liveness: processo respondendo
GET /health/ready   # readiness: processo + banco respondendo
```

A rota `/health/ready` está fora do rate limiting.

## CI

Pipeline em `.github/workflows/ci.yml`:

1. Checkout
2. Setup .NET 6
3. Cache NuGet
4. `dotnet restore`
5. `dotnet build -c Release` (com `NETSDK1138` tratado como erro para
   evitar mudanças acidentais de target)
6. `dotnet format --verify-no-changes` (gate de formatação)
7. Testes unitários + integração com cobertura (`coverlet.runsettings`)
8. Resumo de cobertura (artefato)
9. Build + smoke-test da imagem Docker (readiness probe via curl)
10. Upload do `coverage-report` como artifact (30 dias)

## Formatação

```bash
# Aplicar correções de formatação
dotnet format

# Verificar sem aplicar (gate do CI)
dotnet format --verify-no-changes
```

A configuração reside em `.editorconfig`; `dotnet format` a respeita
integralmente.

## Estrutura do projeto

```text
src/CandidateAssessment.Domain/        # Person, Phone, Cpf, PhoneNumber, PhoneType, DomainException
src/CandidateAssessment.Application/   # Persons/Create, Persons/Search, ..., Phones/*, Authentication/Login
src/CandidateAssessment.Infrastructure/ # EF Core (DbContext, Configurations, Repositories, Migrations),
                                       #   Authentication (Identity seeder), Caching, Time
src/CandidateAssessment.Api/           # Controllers, Middleware (Exception, IdentitySeed),
                                       #   Extensions (ApiVersioning, RateLimiting, HealthChecks),
                                       #   Configuration, Authentication, Authorization
tests/CandidateAssessment.UnitTests/
tests/CandidateAssessment.IntegrationTests/
```

## Trade-offs

- **.NET 6:** exigência da avaliação; o plano reconhece que a versão
  está em EOL e recomenda .NET 8+ para evolução natural. A arquitetura
  evita APIs específicas do 6 onde possível.
- **`AspNetCoreRateLimit`** em vez do middleware nativo: a versão 6 do
  framework não traz o `Microsoft.AspNetCore.RateLimiting`. A
  biblioteca escolhida é a mais consolidada e fica isolada em um único
  arquivo de extensão para troca futura.
- **Sem CQRS formal / sem MediatR:** o tamanho do projeto não justifica
  o custo. Cada caso de uso é uma classe concreta única
  (`CreatePersonHandler`) — fácil de ler, fácil de testar.
- **Sem `IRepository<T>` genérico:** Person e Phone têm semânticas
  diferentes (filtro global, navegação 1:N, paginação), então
  contratos específicos expressam melhor a intenção.
- **Migrations EF Core com SQLite:** algumas alterações de schema
  exigem recriação manual em SQLite. Para o escopo da avaliação, o
  estado final permanece coerente.

## Possíveis evoluções

- Migrar para .NET 8+ quando possível (atualizar `Asp.Versioning`,
  substituir `AspNetCoreRateLimit` por `Microsoft.AspNetCore.RateLimiting`).
- Substituir `MemoryCacheService` por implementação `Redis` atrás da
  mesma `ICacheService`.
- Adicionar `RefreshToken` para reduzir janela de exposição do JWT.
- Endpoint `PATCH` para updates parciais.
- Audit log dedicado (separado da entidade).
- Health check para dependências externas (Redis, SMTP).
- OpenTelemetry para tracing distribuído (em vez de apenas `traceId`).
- CI publica artefato de cobertura em serviço externo (Codecov, Coveralls).

## Licença

Este projeto foi desenvolvido como parte de um processo de avaliação
técnica. Sem licença pública explícita — uso restrito ao contexto da
avaliação.
