using CandidateAssessment.Application.Persons.Create;
using CandidateAssessment.Application.Persons.Delete;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Application.Persons.GetDeleted;
using CandidateAssessment.Application.Persons.Restore;
using CandidateAssessment.Application.Persons.Search;
using CandidateAssessment.Application.Persons.Update;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Application-layer use cases and validators.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<CreatePersonHandler>();
        services.AddScoped<GetPersonByIdHandler>();
        services.AddScoped<SearchPersonsHandler>();
        services.AddScoped<UpdatePersonHandler>();
        services.AddScoped<DeletePersonHandler>();
        services.AddScoped<RestorePersonHandler>();
        services.AddScoped<GetDeletedPersonsHandler>();

        services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>();

        return services;
    }
}

/// <summary>
/// Marker type used for FluentValidation assembly scanning.
/// </summary>
internal sealed class ApplicationAssemblyMarker
{
    private ApplicationAssemblyMarker()
    {
    }
}
