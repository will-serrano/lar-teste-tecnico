# CandidateAssessment

Web API em .NET 6 demonstrando arquitetura Clean Architecture com foco em boas práticas de produção.

> ⚠️ **.NET 6** foi mantido por requisito da avaliação. Esta versão está fora do período de suporte oficial da Microsoft desde novembro de 2024. A arquitetura foi projetada para permitir migração futura para .NET 8+.

## Status

🚧 Em desenvolvimento

## Arquitetura

```
src/
├── CandidateAssessment.Domain           # Entidades, Value Objects, Enums
├── CandidateAssessment.Application      # Casos de uso, abstrações, validações
├── CandidateAssessment.Infrastructure   # EF Core, Identity, Cache, implementações
└── CandidateAssessment.Api              # Controllers, Middleware, configuração

tests/
├── CandidateAssessment.UnitTests        # Testes unitários (Domain + Application)
└── CandidateAssessment.IntegrationTests # Testes de integração (HTTP → DB)
```

## Stack

- ASP.NET Core 6 Web API
- Entity Framework Core 6 + SQLite
- ASP.NET Core Identity + JWT
- FluentValidation
- Serilog
- xUnit + Coverlet

## Como executar

*Instruções detalhadas serão adicionadas durante o desenvolvimento.*

## Licença

Este projeto foi desenvolvido como parte de um processo de avaliação técnica.
