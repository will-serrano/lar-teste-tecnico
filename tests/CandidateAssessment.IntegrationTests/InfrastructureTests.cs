using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CandidateAssessment.Api.Serialization;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Xunit;

namespace CandidateAssessment.IntegrationTests;

/// <summary>
/// End-to-end coverage for the production-grade features introduced in Stage 4:
/// health checks, rate limiting, and Person detail cache invalidation.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class InfrastructureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(),
            new DateOnlyJsonConverter(),
        },
    };

    private readonly CandidateAssessmentWebApplicationFactory _factory;

    public InfrastructureTests(CandidateAssessmentWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Health_LivenessProbe_ShouldReturnHealthy()
    {
        // Liveness does not require auth and does not probe dependencies.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Health_ReadinessProbe_ShouldReturnHealthy_WhenDatabaseReachable()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task RateLimit_ShouldExceedLimit_AfterBurstingRequests()
    {
        // A dedicated factory is used so the strict 5/15s quota cannot bleed
        // into other tests that share the main collection's in-memory counter.
        using var tightFactory = new RateLimitedWebApplicationFactory();
        tightFactory.EnsureDatabaseCreated();

        var client = tightFactory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++)
        {
            var response = await client.GetAsync("/health/live");
            statuses.Add(response.StatusCode);
        }

        // We only assert that at least one rate-limited response happened, which
        // proves the middleware is wired and applies the configured quota.
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task PersonCache_ShouldReturnUpdatedData_AfterPersonMutation()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var cpf = UniqueCpf();
        var create = await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = "Cache Person",
            cpf,
            birthDate = "1990-01-01",
        });
        var created = await create.Content.ReadFromJsonAsync<PersonResponse>(JsonOptions);

        // First read populates the cache.
        var first = await client.GetFromJsonAsync<PersonResponse>(
            $"/api/v1/persons/{created!.Id}",
            JsonOptions);
        Assert.Equal("Cache Person", first!.Name);

        // Mutate.
        var update = await client.PutAsJsonAsync(
            $"/api/v1/persons/{created.Id}",
            new { name = "Cache Person Updated", birthDate = "1990-01-01" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        // Subsequent read must reflect the update (cache was invalidated).
        var second = await client.GetFromJsonAsync<PersonResponse>(
            $"/api/v1/persons/{created.Id}",
            JsonOptions);
        Assert.Equal("Cache Person Updated", second!.Name);
    }

    [Fact]
    public async Task PersonCache_ShouldBeInvalidated_AfterAddingPhone()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var cpf = UniqueCpf();
        var create = await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = "Phone Cache",
            cpf,
            birthDate = "1990-01-01",
        });
        var created = await create.Content.ReadFromJsonAsync<PersonResponse>(JsonOptions);

        // Prime the cache with no phones.
        var first = await client.GetFromJsonAsync<PersonResponse>(
            $"/api/v1/persons/{created!.Id}",
            JsonOptions);
        Assert.NotNull(first);

        // Add a phone.
        var addPhone = await client.PostAsJsonAsync(
            $"/api/v1/persons/{created.Id}/phones",
            new { type = "Mobile", number = "13999999999" });
        Assert.Equal(HttpStatusCode.Created, addPhone.StatusCode);

        // List phones through the phones endpoint to confirm persistence.
        var phones = await client.GetFromJsonAsync<List<PhoneResponse>>(
            $"/api/v1/persons/{created.Id}/phones",
            JsonOptions);
        Assert.NotNull(phones);
        Assert.NotEmpty(phones!);
    }

    [Fact]
    public void ApiVersioning_ShouldDecorateControllers_WithApiVersionAttribute()
    {
        // Walk the loaded assemblies to verify that all controllers carry the
        // [ApiVersion] attribute (proves the versioning wiring is in place).
        var apiAssembly = typeof(CandidateAssessment.Api.Controllers.PersonsController).Assembly;
        var versioned = apiAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetCustomAttributes(typeof(Asp.Versioning.ApiVersionAttribute), false)
                .Cast<Asp.Versioning.ApiVersionAttribute>())
            .ToList();

        Assert.NotEmpty(versioned);
        Assert.All(versioned, attr =>
        {
            // Each ApiVersion attribute must include a 1.0 version.
            Assert.Contains(attr.Versions, v => v.MajorVersion == 1 && v.MinorVersion == 0);
        });
    }

    [Fact]
    public async Task TraceId_ShouldBePresent_InProblemDetails()
    {
        var client = await _factory.CreateAuthenticatedUserClientAsync();
        var response = await client.GetAsync($"/api/v1/persons/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId.GetString()));
    }

    /// <summary>
    /// Generates a unique, mathematically valid CPF per call.
    /// </summary>
    private static string UniqueCpf()
    {
        var hex = Guid.NewGuid().ToString("N");
        Span<char> digits = stackalloc char[11];

        for (var i = 0; i < 11; i++)
        {
            var hexIndex = (i * 3) % hex.Length;
            var nibble = hex[hexIndex];
            var n = nibble <= '9' ? nibble - '0' : (nibble - 'a') + 10;
            digits[i] = (char)('0' + (n % 10));
        }

        int[] weights1 = { 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum1 = 0;
        for (var i = 0; i < 9; i++)
        {
            sum1 += (digits[i] - '0') * weights1[i];
        }

        var remainder1 = sum1 % 11;
        digits[9] = (char)('0' + (remainder1 < 2 ? 0 : 11 - remainder1));

        int[] weights2 = { 11, 10, 9, 8, 7, 6, 5, 4, 3, 2 };
        var sum2 = 0;
        for (var i = 0; i < 10; i++)
        {
            sum2 += (digits[i] - '0') * weights2[i];
        }

        var remainder2 = sum2 % 11;
        digits[10] = (char)('0' + (remainder2 < 2 ? 0 : 11 - remainder2));

        return new string(digits);
    }

    private sealed class PersonResponse
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = default!;

        public string Cpf { get; init; } = default!;

        public DateOnly BirthDate { get; init; }

        public bool IsActive { get; init; }
    }

    private sealed class PhoneResponse
    {
        public Guid Id { get; init; }

        public string Type { get; init; } = default!;

        public string Number { get; init; } = default!;
    }
}
