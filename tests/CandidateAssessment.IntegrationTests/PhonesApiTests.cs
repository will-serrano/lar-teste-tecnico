using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CandidateAssessment.Api.Serialization;
using CandidateAssessment.IntegrationTests.Infrastructure;

namespace CandidateAssessment.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class PhonesApiTests
{
    private readonly CandidateAssessmentWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(),
            new DateOnlyJsonConverter(),
        },
    };

    public PhonesApiTests(CandidateAssessmentWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Should_AddPhone_When_PayloadIsValid_And_AdminIsAuthenticated()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var personId = await CreatePersonAsync(admin);

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new
            {
                type = "Mobile",
                number = "13999999999",
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var phone = await response.Content.ReadFromJsonAsync<PhoneResponse>(JsonOptions);
        Assert.NotNull(phone);
        Assert.Equal("13999999999", phone!.Number);
    }

    [Fact]
    public async Task Should_GetAndUpdatePhone_When_PhoneExists()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var personId = await CreatePersonAsync(admin);
        var create = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new { type = "Mobile", number = "13999999999" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PhoneResponse>(JsonOptions);
        Assert.NotNull(created);

        var get = await admin.GetAsync(
            $"/api/v1/persons/{personId}/phones/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<PhoneResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal("Mobile", fetched!.Type);
        Assert.Equal("13999999999", fetched.Number);

        var update = await admin.PutAsJsonAsync(
            $"/api/v1/persons/{personId}/phones/{created.Id}",
            new { type = "Residential", number = "1133334444" });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var updated = await admin.GetFromJsonAsync<PhoneResponse>(
            $"/api/v1/persons/{personId}/phones/{created.Id}",
            JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Residential", updated!.Type);
        Assert.Equal("1133334444", updated.Number);
    }

    [Fact]
    public async Task Should_ReturnForbidden_When_UserAttemptsToAddPhone()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var personId = await CreatePersonAsync(admin);

        var user = await _factory.CreateAuthenticatedUserClientAsync();
        var response = await user.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new
            {
                type = "Mobile",
                number = "13999999999",
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_DuplicateNumberForSamePerson()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var personId = await CreatePersonAsync(admin);

        var first = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new { type = "Mobile", number = "13999999999" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new { type = "Commercial", number = "13999999999" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_AddingSixthPhone()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var personId = await CreatePersonAsync(admin);

        for (var i = 0; i < 5; i++)
        {
            var suffix = i.ToString().PadLeft(2, '0');
            var response = await admin.PostAsJsonAsync(
                $"/api/v1/persons/{personId}/phones",
                new { type = "Mobile", number = $"139999999{suffix}" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        var overflow = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new { type = "Mobile", number = "13999999888" });

        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode);
    }

    [Fact]
    public async Task Should_ListAndDeletePhones()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var personId = await CreatePersonAsync(admin);

        var created = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new { type = "Mobile", number = "13999999999" });
        var phone = await created.Content.ReadFromJsonAsync<PhoneResponse>(JsonOptions);

        var list = await admin.GetAsync($"/api/v1/persons/{personId}/phones");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var phones = await list.Content.ReadFromJsonAsync<List<PhoneResponse>>(JsonOptions);
        Assert.NotNull(phones);
        Assert.Single(phones!);

        var delete = await admin.DeleteAsync($"/api/v1/persons/{personId}/phones/{phone!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var listAfter = await admin.GetAsync($"/api/v1/persons/{personId}/phones");
        var phonesAfter = await listAfter.Content.ReadFromJsonAsync<List<PhoneResponse>>(JsonOptions);
        Assert.Empty(phonesAfter!);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_AddingPhoneToNonExistingPerson()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{Guid.NewGuid()}/phones",
            new { type = "Mobile", number = "13999999999" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnUnauthorized_When_AccessingPhonesWithoutToken()
    {
        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/v1/persons/{Guid.NewGuid()}/phones");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_PhoneNumberIsInvalid()
    {
        var admin = await _factory.CreateAuthenticatedAdminClientAsync();
        var personId = await CreatePersonAsync(admin);

        var response = await admin.PostAsJsonAsync(
            $"/api/v1/persons/{personId}/phones",
            new { type = "Mobile", number = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Guid> CreatePersonAsync(HttpClient client)
    {
        var cpf = UniqueCpf();
        var response = await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = "Maria",
            cpf,
            birthDate = "1990-01-01",
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<PersonResponse>(JsonOptions);
        return payload!.Id;
    }

    private static string UniqueCpf()
    {
        var hex = Guid.NewGuid().ToString("N");
        var digits = new char[11];

        for (var i = 0; i < 11; i++)
        {
            var hexIndex = (i * 3) % hex.Length;
            var nibble = hex[hexIndex];
            var n = nibble <= '9' ? nibble - '0' : (nibble - 'a') + 10;
            digits[i] = (char)('0' + (n % 10));
        }

        int[] weights1 = [10, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum1 = 0;
        for (var i = 0; i < 9; i++)
        {
            sum1 += (digits[i] - '0') * weights1[i];
        }

        var remainder1 = sum1 % 11;
        digits[9] = (char)('0' + (remainder1 < 2 ? 0 : 11 - remainder1));

        int[] weights2 = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum2 = 0;
        for (var i = 0; i < 10; i++)
        {
            sum2 += (digits[i] - '0') * weights2[i];
        }

        var remainder2 = sum2 % 11;
        digits[10] = (char)('0' + (remainder2 < 2 ? 0 : 11 - remainder2));

        return new string(digits);
    }

    private sealed class PhoneResponse
    {
        public Guid Id { get; init; }

        public Guid PersonId { get; init; }

        public string Type { get; init; } = default!;

        public string Number { get; init; } = default!;
    }

    private sealed class PersonResponse
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = default!;

        public string Cpf { get; init; } = default!;

        public DateOnly BirthDate { get; init; }
    }
}
