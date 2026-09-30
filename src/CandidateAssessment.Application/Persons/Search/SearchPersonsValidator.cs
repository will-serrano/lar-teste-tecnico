using CandidateAssessment.Application.Abstractions.Pagination;
using FluentValidation;

namespace CandidateAssessment.Application.Persons.Search;

public sealed class SearchPersonsValidator : AbstractValidator<SearchPersonsQuery>
{
    public SearchPersonsValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThan(0)
            .When(q => q.Page.HasValue)
            .WithMessage("Page must be greater than zero.");

        RuleFor(q => q.PageSize)
            .GreaterThan(0)
            .When(q => q.PageSize.HasValue)
            .WithMessage("PageSize must be greater than zero.")
            .LessThanOrEqualTo(PaginationOptions.MaxPageSize)
            .When(q => q.PageSize.HasValue)
            .WithMessage($"PageSize cannot exceed {PaginationOptions.MaxPageSize}.");

        RuleFor(q => q.Name)
            .MaximumLength(PersonMaxName)
            .When(q => !string.IsNullOrEmpty(q.Name))
            .WithMessage("Name filter cannot exceed 100 characters.");

        RuleFor(q => q.Cpf)
            .MaximumLength(14)
            .When(q => !string.IsNullOrEmpty(q.Cpf))
            .WithMessage("Cpf filter is too long.");
    }

    private const int PersonMaxName = 100;
}
