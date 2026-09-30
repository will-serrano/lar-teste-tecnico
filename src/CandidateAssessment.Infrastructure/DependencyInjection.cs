using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Application.Abstractions.Time;
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

        return services;
    }
}
