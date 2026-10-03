using CandidateAssessment.Application.Abstractions.Authentication;
using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Infrastructure.Authentication;
using CandidateAssessment.Infrastructure.Caching;
using CandidateAssessment.Infrastructure.Persistence;
using CandidateAssessment.Infrastructure.Persistence.Repositories;
using CandidateAssessment.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CandidateAssessment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=candidateassessment.db";

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IPersonRepository, EfPersonRepository>();
        services.AddScoped<IPhoneRepository, EfPhoneRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<IUserAuthenticationService, IdentityUserAuthenticationService>();

        AddCaching(services, configuration);

        return services;
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        // Armazenamento do cache em memória. A abstração (ICacheService) é o único contrato
        // conhecido pela camada Application; futuramente, trocar por Redis exige alterar apenas esta linha.
        services.AddMemoryCache();

        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<IPersonCache, PersonCacheInvalidator>();
    }
}
