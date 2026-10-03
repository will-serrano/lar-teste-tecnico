namespace CandidateAssessment.Api.Authorization;


/// <summary>
/// Políticas de autorização nomeadas. Centralizá-las aqui evita literais com nomes
/// de papéis nos controllers e torna auditável o mapeamento entre papéis e permissões.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Acesso de leitura a pessoas — papéis Admin e User.
    /// </summary>
    public const string CanReadPersons = nameof(CanReadPersons);

    /// <summary>
    /// Acesso para gerenciar pessoas (criar, atualizar, excluir, restaurar) — somente Admin.
    /// </summary>
    public const string CanManagePersons = nameof(CanManagePersons);

    /// <summary>
    /// Acesso ao endpoint administrativo que lista pessoas excluídas logicamente — somente Admin.
    /// </summary>
    public const string CanViewDeletedPersons = nameof(CanViewDeletedPersons);
}
