namespace CandidateAssessment.Api.Contracts;

/// <summary>
/// Estrutura genérica de paginação para endpoints de listagem.
/// Os nomes dos campos seguem o contrato definido na seção 21 de plan.md.
/// </summary>
public sealed class PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalItems { get; init; }

    public int TotalPages { get; init; }

    public bool HasNext { get; init; }

    public bool HasPrevious { get; init; }
}
