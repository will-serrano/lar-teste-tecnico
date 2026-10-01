# ADR 003 — Soft delete para `Person` com filtro global e rota administrativa

- **Status:** Aceito
- **Data:** Etapa 1 do plano (modelagem do domínio)
- **Contexto:** A regra de negócio exige preservar o histórico de Pessoas
  mesmo após exclusão; `Phone` é excluído fisicamente (relacionamento
  dependente, sem valor histórico próprio).

## Decisão

`Person` usa **soft delete** com filtro global do EF Core, e o
endpoint administrativo `GET /api/v1/persons/deleted` expõe apenas os
inativos (somente Admin). A restauração é uma operação explícita:

```http
POST /api/v1/persons/{id}/restore
```

Campos relevantes:

```text
IsActive      bool      // estado atual
DeletedAtUtc  DateTime? // última exclusão
RestoredAtUtc DateTime? // última restauração
CreatedAtUtc  DateTime  // imutável
UpdatedAtUtc  DateTime  // atualizado em qualquer mutação
```

## Razões

1. **CPF permanece reservado.** Um CPF cadastrado continua ocupando o
   índice UNIQUE mesmo após exclusão, então reativação ou recadastro
   exige restauração explícita.
2. **Auditoria simples sem tabela paralela.** `DeletedAtUtc` e
   `RestoredAtUtc` guardam o último ciclo sem precisar de uma tabela
   `AuditLog` separada.
3. **Filtro global.** Toda consulta padrão (`Persons`, `Phones`,
   navegação) ignora inativos; a listagem de excluídos usa
   `IgnoreQueryFilters()`.
4. **Endpoint administrativo claro.** `GET /api/v1/persons/deleted`
   (Admin) é separado de `GET /api/v1/persons` (Admin/User). Não
   usamos query string `?includeDeleted=true` porque misturaria
   contextos.

## Consequências

- **Boas:** Histórico preservado, consulta normal simples, sem ruído na
  rota principal.
- **Aceitáveis:** Esquecer `IgnoreQueryFilters()` em alguma consulta
  futura traria inativos sem querer. Para mitigar, testes de
  integração cobrem o fluxo `DELETE → GET (404) → /persons/deleted →
  RESTORE → GET (200)`.
- **Descartadas:**
  - Exclusão física — perde histórico e libera o CPF.
  - Histórico completo em `DeletedAtUtc1`, `DeletedAtUtc2`... —
    vira rapidamente um esquema frágil; uma tabela `AuditLog` é a
    evolução correta quando o histórico completo virar requisito.
  - `IncludeDeleted` na rota principal — mistura contextos, prejudica
    cache, dificulta autorização.

## Verificação

```bash
# Soft delete
curl -X DELETE -H "Authorization: Bearer $TOKEN" \
    http://localhost:8080/api/v1/persons/$ID

# GET normal -> 404
curl -H "Authorization: Bearer $TOKEN" \
    http://localhost:8080/api/v1/persons/$ID

# GET administrativo -> 200 com IsActive=false
curl -H "Authorization: Bearer $ADMIN_TOKEN" \
    http://localhost:8080/api/v1/persons/deleted

# Restore
curl -X POST -H "Authorization: Bearer $ADMIN_TOKEN" \
    http://localhost:8080/api/v1/persons/$ID/restore
```

Coberto por testes em `PersonsApiTests.Should_SoftDeleteAndRestorePerson`
e `GetDeletedPersonsHandlerTests`.
