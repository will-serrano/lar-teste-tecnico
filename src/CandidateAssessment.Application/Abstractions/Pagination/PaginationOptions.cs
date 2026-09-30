namespace CandidateAssessment.Application.Abstractions.Pagination;

/// <summary>
/// Default pagination contract used by Application use cases.
/// Controllers adapt incoming query strings to these values.
/// </summary>
public static class PaginationOptions
{
    public const int DefaultPage = 1;

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    public static int NormalizePage(int? page)
        => page is null or < 1 ? DefaultPage : page.Value;

    public static int NormalizePageSize(int? pageSize)
    {
        if (pageSize is null or < 1)
        {
            return DefaultPageSize;
        }

        return pageSize.Value > MaxPageSize ? MaxPageSize : pageSize.Value;
    }
}
