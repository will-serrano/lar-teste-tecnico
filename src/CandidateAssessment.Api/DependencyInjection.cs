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
    /// Registra os serviços da camada da API (controllers, validadores, JSON, autenticação, Swagger,
    /// versionamento, limitação de taxa e verificações de integridade).
    /// </summary>
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddControllers(options =>
            {
                // Um único filtro global substitui o código repetitivo de validação por ação
                // que antes existia em cada controller. A validação continua sendo opt-in por
                // tipo — somente argumentos com um IValidator<T> registrado são verificados —
                // e, em caso de falha, o filtro interrompe a execução e retorna um
                // ValidationProblemDetails 400 com os mesmos erros indexados por propriedade
                // da versão manual.
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

        // Fachadas para os handlers da camada Application. Os controllers dependem apenas delas,
        // reduzindo os construtores extensos (11 dependências em PersonsController e 7 em
        // PhonesController) a uma. Cada handler continua registrado e testável de forma
        // independente, além de manter suas próprias dependências — a fachada apenas delega.
        services.AddScoped<PersonsFacade>();
        services.AddScoped<PhonesFacade>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedUsersOptions>(configuration.GetSection(SeedUsersOptions.SectionName));
        services.Configure<SerilogOptions>(configuration.GetSection(SerilogOptions.SectionName));
        services.AddApiObservability(configuration);
        services.AddApiIdempotency(configuration);

        AddIdentity(services);
        AddAuthentication(services, configuration);
        AddAuthorization(services);

        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddEndpointsApiExplorer();

        // A ordem importa: o versionamento deve ser registrado antes do Swagger para que o
        // explorador da API versionada preencha um documento Swagger "v1".
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
        // Vincula JwtOptions pelo padrão Options para que o middleware JwtBearer e o
        // JwtTokenService leiam a configuração combinada (incluindo substituições dos testes
        // aplicadas depois que Program.cs inicia o registro dos serviços).
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
