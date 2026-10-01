# ADR 002 — Entity Framework Core 6 + SQLite como provider padrão

- **Status:** Aceito
- **Data:** Etapa 1 do plano (camada de persistência)
- **Contexto:** A avaliação permite `Entity Framework Core`, `SQL Server`,
  `SQLite` ou outro banco relacional. Precisamos de um provider que
  funcione em ambiente de avaliação, demonstre migrations e permita testes
  determinísticos.

## Decisão

Utilizar **Entity Framework Core 6** com **SQLite** como provider padrão,
migrations versionadas no controle de versão e abstrações de repositório
específicas por entidade (`IPersonRepository`, `IPhoneRepository`,
`IUnitOfWork`).

## Razões

1. **Sem dependência externa.** SQLite é embarcado; o avaliador não
   precisa instalar SQL Server ou provisionar Postgres.
2. **Migrations reais.** EF Core gera SQL real aplicado em produção, e
   as migrations ficam versionadas em
   `src/CandidateAssessment.Infrastructure/Migrations/`.
3. **Testes de integração estáveis.** Cada teste pode usar um arquivo
   SQLite isolado (ou `:memory:` com connection string dedicada) com
   schema criado a partir das mesmas migrations — comportamento fiel ao
   produção, sem o `InMemoryProvider` que esconde bugs.
4. **Aplicação dos constraints de negócio.** As validações de
   `UNIQUE(CPF)` e `UNIQUE(PersonId, Number)` ficam no nível do schema
   (último recurso), depois do domínio (matemática do CPF) e da
   Application (regras de quantidade/duplicidade).

## Consequências

- **Boas:** A camada `Application` permanece independente do provider
  através de `Abstractions/Persistence`; trocar para SQL Server é um
  trabalho isolado de migrations + connection string.
- **Aceitáveis:** Algumas operações DDL do SQLite têm restrições (ex.:
  renomear colunas requer recriação). A migração entre providers exigirá
  testes manuais antes de promover migrations em produção.
- **Descartadas:** *EF Core InMemory provider* — esconde o
  comportamento real do SQLite/SQL Server, gera falsos positivos em
  testes.

## Configuração

- Provider: `Microsoft.EntityFrameworkCore.Sqlite` 6.0.36.
- Connection string default: `Data Source=candidateassessment.db` (em
  Docker: `/app/data/candidateassessment.db`, montado como volume).
- Migrations em
  `src/CandidateAssessment.Infrastructure/Migrations/`.
- Configurações EF Core isoladas em
  `Persistence/Configurations/PersonConfiguration.cs` e
  `PhoneConfiguration.cs` (em vez de um `OnModelCreating()` gigante).

## Verificação

```bash
dotnet tool restore                       # restaura dotnet-ef 6.0.36
dotnet ef migrations list \
    --project src/CandidateAssessment.Infrastructure \
    --startup-project src/CandidateAssessment.Api
dotnet ef database update \
    --project src/CandidateAssessment.Infrastructure \
    --startup-project src/CandidateAssessment.Api
```

A CI executa o `build` da `Infrastructure` com as migrations como
código compilado, garantindo que divergências de schema quebrem cedo.
