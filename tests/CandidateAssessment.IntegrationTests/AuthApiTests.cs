using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Xunit;

namespace CandidateAssessment.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AuthApiTests
{
    private readonly CandidateAssessmentWebApplicationFactory _factory;

    public AuthApiTests(CandidateAssessmentWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Should_ReturnToken_When_CredentialsAreValid()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrEmpty(payload!.AccessToken));
        Assert.Contains("Admin", payload.Roles);
    }

    [Fact]
    public async Task Should_ReturnUnauthorized_When_PasswordIsWrong()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "admin",
            password = "WrongPassword1",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnUnauthorized_When_UserDoesNotExist()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "ghost",
            password = "Whatever123",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_RequestIsMissingFields()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "",
            password = "",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnUnauthorized_When_AccessingPersonsWithoutToken()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/persons");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_UserAttemptsToCreatePerson()
    {
        var client = await _factory.CreateAuthenticatedUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = "Should Be Forbidden",
            cpf = "123.456.789-09",
            birthDate = "1990-01-01",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnOk_When_AdminCreatesPerson()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = "Maria",
            cpf = UniqueCpf(),
            birthDate = "1990-01-01",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnOk_When_UserReadsPersons()
    {
        var client = await _factory.CreateAuthenticatedUserClientAsync();
        var response = await client.GetAsync("/api/v1/persons");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_UserReadsDeletedPersons()
    {
        var client = await _factory.CreateAuthenticatedUserClientAsync();
        var response = await client.GetAsync("/api/v1/persons/deleted");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

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

    private sealed class LoginResponse
    {
        public string AccessToken { get; init; } = default!;

        public DateTime ExpiresAtUtc { get; init; }

        public string Username { get; init; } = default!;

        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    }
}
