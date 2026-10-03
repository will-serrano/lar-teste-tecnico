using CandidateAssessment.Domain.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CandidateAssessment.Infrastructure.Authentication;

/// <summary>
/// Garante que os dois papéis padrão e as contas iniciais existam. É idempotente — seguro
/// para executar a cada inicialização.
/// </summary>
public static class IdentityUserSeeder
{
    private const string SeedUsersSectionName = "SeedUsers";

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("CandidateAssessment.Infrastructure.Authentication.IdentityUserSeeder");
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await EnsureRoleAsync(roleManager, logger, ApplicationRoles.Admin);
        await EnsureRoleAsync(roleManager, logger, ApplicationRoles.User);

        var seedSection = configuration.GetSection(SeedUsersSectionName);

        var adminUserName = seedSection.GetValue<string>("Admin:Username");
        var adminPassword = seedSection.GetValue<string>("Admin:Password");
        var userUserName = seedSection.GetValue<string>("User:Username");
        var userPassword = seedSection.GetValue<string>("User:Password");

        if (!string.IsNullOrWhiteSpace(adminUserName) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            await EnsureUserWithRoleAsync(
                userManager,
                logger,
                adminUserName,
                adminPassword,
                ApplicationRoles.Admin);
        }

        if (!string.IsNullOrWhiteSpace(userUserName) && !string.IsNullOrWhiteSpace(userPassword))
        {
            await EnsureUserWithRoleAsync(
                userManager,
                logger,
                userUserName,
                userPassword,
                ApplicationRoles.User);
        }
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger,
        string role)
    {
        if (await roleManager.RoleExistsAsync(role))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(role));
        if (result.Succeeded)
        {
            logger.LogInformation("Identity role '{Role}' created.", role);
        }
        else
        {
            logger.LogError(
                "Failed to create Identity role '{Role}': {Errors}",
                role,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private static async Task EnsureUserWithRoleAsync(
        UserManager<IdentityUser> userManager,
        ILogger logger,
        string userName,
        string password,
        string role)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            user = new IdentityUser
            {
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                logger.LogError(
                    "Failed to create seed user '{UserName}': {Errors}",
                    userName,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Seed user '{UserName}' created.", userName);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addResult = await userManager.AddToRoleAsync(user, role);
            if (addResult.Succeeded)
            {
                logger.LogInformation(
                    "User '{UserName}' added to role '{Role}'.",
                    userName,
                    role);
            }
            else
            {
                logger.LogError(
                    "Failed to add user '{UserName}' to role '{Role}': {Errors}",
                    userName,
                    role,
                    string.Join(", ", addResult.Errors.Select(e => e.Description)));
            }
        }
    }
}
