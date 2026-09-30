using CandidateAssessment.Api.Contracts.Persons;
using CandidateAssessment.Api.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Api;

public static class DependencyInjection
{
    /// <summary>
    /// Registers API-layer services (controllers, request validators, JSON options).
    /// </summary>
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
            });

        services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
        services.AddScoped<IValidator<UpdatePersonRequest>, UpdatePersonRequestValidator>();

        return services;
    }
}

internal sealed class CreatePersonRequestValidator : AbstractValidator<CreatePersonRequest>
{
    public CreatePersonRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters.");

        RuleFor(r => r.Cpf)
            .NotEmpty()
            .WithMessage("Cpf is required.")
            .Must(HaveAtLeast11Digits)
            .WithMessage("Cpf must contain 11 digits.");

        RuleFor(r => r.BirthDate)
            .NotEqual(default(DateOnly))
            .WithMessage("BirthDate is required.");
    }

    private static bool HaveAtLeast11Digits(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return false;
        }

        var digitCount = 0;
        foreach (var ch in cpf)
        {
            if (char.IsDigit(ch))
            {
                digitCount++;
            }
        }

        return digitCount == 11;
    }
}

internal sealed class UpdatePersonRequestValidator : AbstractValidator<UpdatePersonRequest>
{
    public UpdatePersonRequestValidator()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name cannot exceed 100 characters.");

        RuleFor(r => r.BirthDate)
            .NotEqual(default(DateOnly))
            .WithMessage("BirthDate is required.");
    }
}
