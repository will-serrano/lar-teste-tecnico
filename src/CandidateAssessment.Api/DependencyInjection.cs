using System.Text;
using CandidateAssessment.Api.Authentication;
using CandidateAssessment.Api.Authorization;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Contracts.Auth;
using CandidateAssessment.Api.Contracts.Persons;
using CandidateAssessment.Api.Contracts.Phones;
using CandidateAssessment.Api.Extensions;
using CandidateAssessment.Api.Facades;
using CandidateAssessment.Api.Serialization;
using CandidateAssessment.Api.Validation;
using CandidateAssessment.Application.Abstractions.Authentication;
using CandidateAssessment.Domain.Roles;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CandidateAssessment.Api;

public static class DependencyInjection
{
    /// <summary>
    /// Registers API-layer services (controllers, validators, JSON, auth, swagger,
    /// versioning, rate limiting, health checks).
    /// </summary>
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddControllers(options =>
            {
                // Single global filter replaces the per-action validation boilerplate
                // that used to live in every controller. Validation is still opt-in by
                // type — only arguments with a registered IValidator<T> are checked —
                // and on failure the filter short-circuits with a ValidationProblemDetails
                // 400 carrying the same property-keyed errors as the manual version.
                options.Filters.Add<ValidationActionFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
                options.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        services.AddScoped<IValidator<CreatePersonRequest>, CreatePersonRequestValidator>();
        services.AddScoped<IValidator<UpdatePersonRequest>, UpdatePersonRequestValidator>();
        services.AddScoped<IValidator<CreatePhoneRequest>, CreatePhoneRequestValidator>();
        services.AddScoped<IValidator<UpdatePhoneRequest>, UpdatePhoneRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

        // Facades over the Application handlers. Controllers depend only on these so
        // the bloated constructor (11 deps on PersonsController, 7 on PhonesController)
        // collapses to one. Each handler is still independently registered, testable,
        // and owns its own dependencies — the façade is a pure delegate.
        services.AddScoped<PersonsFacade>();
        services.AddScoped<PhonesFacade>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedUsersOptions>(configuration.GetSection(SeedUsersOptions.SectionName));
        services.Configure<SerilogOptions>(configuration.GetSection(SerilogOptions.SectionName));

        AddIdentity(services);
        AddAuthentication(services, configuration);
        AddAuthorization(services);

        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddEndpointsApiExplorer();

        // Order matters: versioning must be registered before Swagger so the
        // versioned API explorer populates a "v1" Swagger document.
        services.AddApiVersioningWithExplorer();

        services.AddApiRateLimiting(configuration);
        services.AddApiHealthChecks();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Candidate Assessment API",
                Version = "v1",
                Description = "Clean Architecture Web API demonstrating Senior-level .NET 6 practices.",
            });

            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "JWT Bearer authentication. Copy the accessToken from /auth/login and paste it here.",
            });

            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    Array.Empty<string>()
                },
            });
        });

        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.Lockout.AllowedForNewUsers = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<CandidateAssessment.Infrastructure.Persistence.ApplicationDbContext>()
            .AddDefaultTokenProviders();
    }

    private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        // Bind JwtOptions through the options pattern so the JwtBearer middleware
        // and JwtTokenService both read the merged configuration (including
        // test overrides applied after Program.cs has begun wiring services).
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptionsSnapshot) =>
            {
                var jwtOptions = jwtOptionsSnapshot.Value;
                bearer.RequireHttpsMetadata = false;
                bearer.SaveToken = true;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
    }

    private static void AddAuthorization(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.CanReadPersons, policy =>
                policy.RequireRole(ApplicationRoles.Admin, ApplicationRoles.User));

            options.AddPolicy(AuthorizationPolicies.CanManagePersons, policy =>
                policy.RequireRole(ApplicationRoles.Admin));

            options.AddPolicy(AuthorizationPolicies.CanViewDeletedPersons, policy =>
                policy.RequireRole(ApplicationRoles.Admin));
        });
    }
}
