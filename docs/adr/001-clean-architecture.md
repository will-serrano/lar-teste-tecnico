# ADR 001 — Clean Architecture com feature folders na Application

- **Status:** Aceito
- **Data:** Etapa 1 do plano (scaffolding inicial)
- **Contexto:** Avaliação técnica requer demonstrar organização de produção sem complexidade artificial.

## Decisão

Adotar **Clean Architecture** em quatro projetos (`Domain`, `Application`,
`Infrastructure`, `Api`) com a camada `Application` organizada por *feature
folder*:

```text
Application/
├── Abstractions/        # contratos puros (IPersonRepository, ICacheService, ...)
├── Persons/
│   ├── Create/
│   ├── Update/
│   ├── Delete/
│   ├── Restore/
│   ├── GetById/
│   ├── Search/
│   └── GetDeleted/
├── Phones/
│   ├── Create/
│   ├── Update/
│   ├── Delete/
│   ├── GetById/
│   └── List/
└── Authentication/
    └── Login/
```

Cada caso de uso concentra `Command/Query`, `Handler`, `Validator` e
`Response` num único diretório, evitando a clássica pasta horizontal
`Services/`, `DTOs/`, `Validators/` que espalha uma única feature por
vários lugares.

## Razões

1. **Independência do Domain.** O Domain não referencia Entity Framework,
   ASP.NET Core, Serilog ou qualquer detalhe externo — apenas tipos
   BCL/abstrações de infraestrutura.
2. **Independência da Application.** A Application conhece apenas
   `Abstractions/`. Trocar SQLite por SQL Server ou MemoryCache por
   Redis é uma substituição na `Infrastructure` sem alterar a regra de
   negócio.
3. **Coesão por feature.** Cada caso de uso (Create, Search, Restore...)
   é um arquivo autocontido. Revisar um requisito toca um diretório,
   não dez.
4. **Onboarding rápido.** Um novo desenvolvedor lê
   `Application/Persons/Create/CreatePersonHandler.cs` e entende a
   operação fim a fim.

## Consequências

- **Boas:** Regras de negócio testáveis sem I/O (fakes substituem
  repositórios). Migração para outras tecnologias exige apenas reescrever
  a `Infrastructure`.
- **Aceitáveis:** O número de arquivos cresce mais rápido que em uma
  estrutura monolítica, mas cada arquivo é menor e mais focado.
- **Descartadas:** *Vertical Slice Architecture* completa traria
  controllers/validadores/repositórios dentro de cada feature, o que
  viola a separação de camadas do Clean Architecture.

## Verificação

A separação é aplicada na prática por:

- `Domain` referencia apenas BCL.
- `Application` referencia apenas `Domain`.
- `Infrastructure` referencia `Application` e `Domain` (e ASP.NET
  Identity, EF Core).
- `Api` referencia `Application` e `Infrastructure`.

A análise é repetível via `dotnet build` e os testes unitários da
Application rodam sem qualquer provider real (todas as dependências são
substituídas por fakes).
