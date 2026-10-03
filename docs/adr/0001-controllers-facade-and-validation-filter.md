# ADR 0001 — Façade + ValidationActionFilter para controllers

- **Status:** Aceito
- **Data:** 2026-10-02
- **Escopo:** camada `CandidateAssessment.Api`

## Contexto

O projeto tinha **3 controllers** consumindo **13 handlers** da `Application`:

| Controller | Dependências injetadas |
|---|---|
| `PersonsController` | 7 handlers + 4 validators = **11** |
| `PhonesController` | 5 handlers + 2 validators = **7** |
| `AuthController`   | 1 handler + 1 validator  = 2 |

Cada endpoint replicava o mesmo boilerplate de validação:

```csharp
var validation = await _validator.ValidateAsync(request, ct);
if (!validation.IsValid)
{
    foreach (var error in validation.Errors)
        ModelState.AddModelError(
            string.IsNullOrEmpty(error.PropertyName) ? "request" : error.PropertyName,
            error.ErrorMessage);
    return ValidationProblem(ModelState);
}
```

Esse trecho aparecia 7 vezes (5 em `Persons`, 2 em `Phones`).

A proposta inicial foi **unificar handlers por controller** (um `PersonsHandler` com 7 métodos) para reduzir o construtor. Essa alternativa foi avaliada e rejeitada (ver "Alternativa considerada").

## Decisão

Dois ajustes ortogonais:

1. **Façade fina por bounded context** (`PersonsFacade`, `PhonesFacade`) na camada Api. Cada método é um *forward* de uma linha ao handler correspondente. Vertical Slice Architecture na `Application` permanece intacta — os 13 handlers continuam existindo, com suas dependências, lifetime e testes individuais.
2. **`ValidationActionFilter`** global (`IAsyncActionFilter`) que resolve `IValidator<T>` para o tipo de cada action argument, executa `ValidateAsync`, popula `ModelState` com chaves por propriedade e devolve `ValidationProblemDetails` 400 em falha. A resposta usa a `ProblemDetailsFactory` do MVC, preservando `type`, `traceId` e as customizações da factory, assim como `ControllerBase.ValidationProblem`. Erros sem propriedade usam o nome do argumento (`request` ou `query`), preservando as chaves anteriores. Opt-in por tipo: argumentos sem validator registrado são ignorados (route ids, `CancellationToken`, etc.).

Controllers agora dependem apenas do façade e nada mais. As actions de query bindam o query object via model binding (`[FromQuery] SearchPersonsQuery query`), o que elimina a construção manual dentro do método e permite que o filtro valide o objeto construído pelo binder antes do action executar.

### Estrutura resultante

```
Api/
  Validation/
    ValidationActionFilter.cs       # IAsyncActionFilter global
  Facades/
    PersonsFacade.cs                # 7 forwards → 7 handlers
    PhonesFacade.cs                 # 5 forwards → 5 handlers
  Controllers/
    PersonsController.cs            # 1 dependência
    PhonesController.cs             # 1 dependência
    AuthController.cs               # 1 dependência
```

## Consequências

### Ganhos

| Aspecto | Antes | Depois |
|---|---|---|
| Construtor do `PersonsController` | 11 deps | 1 dep |
| Construtor do `PhonesController`  | 7 deps  | 1 dep |
| Linhas duplicadas de validação nos controllers | ~70 (7 cópias × ~10) | 0 |
| Handlers no `Application` | 13 | 13 (inalterados) |
| Testes granulares por handler | sim | sim |
| Adicionar novo endpoint | registrar validator + handler + inject | registrar validator + handler |
| Validação funciona antes de qualquer lógica do controller | não | sim |
| `[FromQuery] SearchPersonsQuery` (model binding) | não | sim |

### Trade-offs

- **Reflection no filter.** `ValidationActionFilter.InvokeValidateAsync` resolve `IValidator<T>.ValidateAsync(T, CancellationToken)` por reflexão na interface genérica fechada. O custo é desprezível (1 chamada por request), mas é uma dependência implícita no contrato de FluentValidation 11.x.
- **Façade = mais uma classe por bounded context.** Custo puramente mecânico; sem lógica de negócio. Se um dia o bounded context tiver só 1 handler, a fachada pode ser eliminada sem refatoração maior (basta injetar o handler no controller).
- **`ArgumentNullException.ThrowIfNull(request)` removido do controller.** A action recebe o objeto do model binder (que retorna 400 automaticamente se o body estiver ausente) ou do filter (que valida com 400 se falhar). O cheiro original some.

## Alternativa considerada (e rejeitada)

**Unificar os 7 handlers de Person em um único `PersonsHandler` (análogo para Phones)** — a sugestão original nesta thread.

| Critério | Façade + Filtro (escolhido) | Handlers unificados |
|---|---|---|
| Vertical Slice preservada | sim | não — vira "Vertical Layer" |
| Single Responsibility por classe | sim (façade é puro delegate) | não — Person vira Create + Get + Search + Update + Delete + Restore + GetDeleted |
| Testes granulares por caso de uso | sim | piora — cada teste monta o grafo inteiro |
| Construtor do "service" unificado | n/a (façade delega) | inflado novamente — Create precisa de repo+UoW+clock+cache, Search só de repo, etc. |
| Risco de regredir a VSA no futuro | baixo | alto — toda nova feature num arquivo de 600+ linhas |
| Compatibilidade com testes unitários existentes | nenhuma | alguma (todos os 9 `*HandlerTests.cs` precisariam ser reescritos) |
| Compatibilidade com integração contínua / refactor futuro | trivial | cara |

A unificação troca o inchaço do controller pelo inchaço de uma classe "façade/serviço" que é mais difícil de testar e reverter. Não resolve o problema real (a repetição do boilerplate de validação), apenas o move.

## Validação

- `dotnet build CandidateAssessment.sln --no-restore --configuration Release`: 0 erros, 0 warnings.
- `dotnet test CandidateAssessment.sln --no-build --no-restore --configuration Release`: todos os testes passando, nenhum ignorado.
- `CandidateAssessment.UnitTests`: 165/165 passando.
- `CandidateAssessment.IntegrationTests`: 52/52 passando.
- 13 cenários de regressão em `ValidationApiTests` verificam `type`, `traceId`, status 400 e as chaves/mensagens dos erros nos sete endpoints com validação, incluindo falhas do model binding e erros sem propriedade.

## Como estender

Adicionar um novo endpoint com validação hoje exige apenas:

1. Criar `CreateXHandler` em `Application/X/Create/`.
2. Criar `CreateXRequest` + `CreateXRequestValidator` em `Api/Contracts/X/`.
3. Adicionar `CreateXAsync` no façade.
4. Adicionar a action no controller.

Nenhuma das quatro etapas exige tocar construtor, `TryValidateModel` ou `ModelState`.