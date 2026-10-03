using CandidateAssessment.Application.Authentication.Login;
using CandidateAssessment.Application.Persons.Create;
using CandidateAssessment.Application.Persons.Delete;
using CandidateAssessment.Application.Persons.GetById;
using CandidateAssessment.Application.Persons.GetDeleted;
using CandidateAssessment.Application.Persons.Restore;
using CandidateAssessment.Application.Persons.Search;
using CandidateAssessment.Application.Persons.Update;
using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Application.Phones.Delete;
using CandidateAssessment.Application.Phones.GetById;
using CandidateAssessment.Application.Phones.List;
using CandidateAssessment.Application.Phones.Update;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra os casos de uso e validadores da camada Application.
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

        services.AddScoped<CreatePhoneHandler>();
        services.AddScoped<UpdatePhoneHandler>();
        services.AddScoped<DeletePhoneHandler>();
        services.AddScoped<GetPhoneByIdHandler>();
        services.AddScoped<ListPhonesHandler>();

        services.AddScoped<LoginHandler>();

        services.AddValidatorsFromAssemblyContaining<ApplicationAssemblyMarker>();

        return services;
    }
}

/// <summary>
/// Tipo marcador usado para localizar assemblies do FluentValidation.
/// </summary>
internal sealed class ApplicationAssemblyMarker
{
    private ApplicationAssemblyMarker()
    {
    }
}
