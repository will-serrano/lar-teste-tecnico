using System.Text;
using CandidateAssessment.Api.Authentication;
using CandidateAssessment.Api.Authorization;
using CandidateAssessment.Api.Configuration;
using CandidateAssessment.Api.Contracts.Auth;
using CandidateAssessment.Api.Contracts.Phones;
using CandidateAssessment.Api.Contracts.Persons;
using CandidateAssessment.Api.Serialization;
using CandidateAssessment.Application.Abstractions.Authentication;
using CandidateAssessment.Application.Phones.Create;
using CandidateAssessment.Application.Phones.Update;
using CandidateAssessment.Domain.Roles;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CandidateAssessment.Api;

public static class DependencyInjection
{
    /// <summary>
    /// Registers API-layer services (controllers, validators, JSON, auth, swagger).
    /// </summary>
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddControllers()
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

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedUsersOptions>(configuration.GetSection(SeedUsersOptions.SectionName));

        AddIdentity(services);
        AddAuthentication(services, configuration);
        AddAuthorization(services);

        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddEndpointsApiExplorer();
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

internal sealed class CreatePhoneRequestValidator : AbstractValidator<CreatePhoneRequest>
{
    public CreatePhoneRequestValidator()
    {
        RuleFor(r => r.Type)
            .IsInEnum()
            .WithMessage("PhoneType must be a valid value.");

        RuleFor(r => r.Number)
            .NotEmpty()
            .WithMessage("Number is required.")
            .Must(HaveOnlyAllowedCharacters)
            .WithMessage("Number must contain only digits, spaces, '(', ')', '+', or '-'.");

        RuleFor(r => r)
            .Must(r => CandidateAssessment.Domain.ValueObjects.PhoneNumber.TryCreate(
                r.Number,
                r.Type,
                out _))
            .WithMessage(r => $"Number is not valid for type {r.Type}.")
            .When(r => r.Type != 0);
    }

    private static bool HaveOnlyAllowedCharacters(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return false;
        }

        foreach (var ch in number)
        {
            var allowed = char.IsDigit(ch)
                || char.IsWhiteSpace(ch)
                || ch == '('
                || ch == ')'
                || ch == '+'
                || ch == '-';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed class UpdatePhoneRequestValidator : AbstractValidator<UpdatePhoneRequest>
{
    public UpdatePhoneRequestValidator()
    {
        RuleFor(r => r.Type)
            .IsInEnum()
            .WithMessage("PhoneType must be a valid value.");

        RuleFor(r => r.Number)
            .NotEmpty()
            .WithMessage("Number is required.");

        RuleFor(r => r)
            .Must(r => CandidateAssessment.Domain.ValueObjects.PhoneNumber.TryCreate(
                r.Number,
                r.Type,
                out _))
            .WithMessage(r => $"Number is not valid for type {r.Type}.")
            .When(r => r.Type != 0);
    }
}
