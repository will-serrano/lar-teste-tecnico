using CandidateAssessment.Application.Abstractions.Pagination;
using FluentValidation;

namespace CandidateAssessment.Application.Persons.GetDeleted;

public sealed class GetDeletedPersonsValidator : AbstractValidator<GetDeletedPersonsQuery>
{
    public GetDeletedPersonsValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be greater than or equal to 1.")
            .When(q => q.Page.HasValue);

        RuleFor(q => q.PageSize)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageSize must be greater than or equal to 1.")
            .LessThanOrEqualTo(PaginationOptions.MaxPageSize)
            .WithMessage($"PageSize cannot exceed {PaginationOptions.MaxPageSize}.")
            .When(q => q.PageSize.HasValue);
    }
}
