using CandidateAssessment.Application.Abstractions.Authentication;
using CandidateAssessment.Application.Abstractions.Caching;
using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
using CandidateAssessment.Infrastructure.Authentication;
using CandidateAssessment.Infrastructure.Caching;
using CandidateAssessment.Infrastructure.Persistence;
using CandidateAssessment.Infrastructure.Persistence.Repositories;
using CandidateAssessment.Infrastructure.Time;
using Microsoft.AspNetCore.Identity;
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
        // Memory cache backing. The abstraction (ICacheService) is the only contract
        // Application knows about; switching to Redis later is a one-line swap here.
        services.AddMemoryCache();

        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.SectionName));

        services.AddSingleton<ICacheService, MemoryCacheService>();
        services.AddSingleton<IPersonCache, PersonCacheInvalidator>();
    }
}
