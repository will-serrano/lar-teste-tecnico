using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CandidateAssessment.Api.Serialization;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Xunit;

namespace CandidateAssessment.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class PersonsApiTests
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

    public PersonsApiTests(CandidateAssessmentWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Should_CreateAndRetrievePerson_When_RequestIsValid()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var cpf = UniqueCpf();
        var request = new
        {
            name = "Maria Souza",
            cpf,
            birthDate = "1990-05-20",
        };

        var createResponse = await client.PostAsJsonAsync("/api/v1/persons", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PersonResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal("Maria Souza", created.Name);
        Assert.True(created.IsActive);

        var getResponse = await client.GetAsync($"/api/v1/persons/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<PersonResponse>(JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_NameIsBlank()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var request = new
        {
            name = string.Empty,
            cpf = UniqueCpf(),
            birthDate = "1990-05-20",
        };

        var response = await client.PostAsJsonAsync("/api/v1/persons", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnBadRequest_When_CpfIsMalformed()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var request = new
        {
            name = "Maria",
            cpf = "123",
            birthDate = "1990-05-20",
        };

        var response = await client.PostAsJsonAsync("/api/v1/persons", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_CpfIsMathematicallyInvalid()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        // 00000000000 is rejected by the Cpf value object (all digits equal).
        var request = new
        {
            name = "Maria",
            cpf = "000.000.000-00",
            birthDate = "1990-05-20",
        };

        var response = await client.PostAsJsonAsync("/api/v1/persons", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnConflict_When_CpfAlreadyExists()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var cpf = UniqueCpf();
        var request = new
        {
            name = "Maria",
            cpf,
            birthDate = "1990-05-20",
        };

        var first = await client.PostAsJsonAsync("/api/v1/persons", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/persons", request with { name = "Other" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_PersonDoesNotExist()
    {
        var client = await _factory.CreateAuthenticatedUserClientAsync();
        var response = await client.GetAsync($"/api/v1/persons/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Should_SearchPersons_WithPagination()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();

        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/v1/persons", new
            {
                name = $"Paginated Person {Guid.NewGuid():N} {i:D2}",
                cpf = UniqueCpf(),
                birthDate = "1990-01-01",
            });
        }

        var response = await client.GetAsync("/api/v1/persons?page=1&pageSize=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<PersonResponse>>(JsonOptions);
        Assert.NotNull(paged);
        Assert.Equal(3, paged!.Items.Count);
        Assert.True(paged.TotalItems >= 5);
        Assert.True(paged.TotalPages >= 2);
    }

    [Fact]
    public async Task Should_SearchPersons_FilterByName()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var uniqueTag = Guid.NewGuid().ToString("N");
        var targetName = $"FilterTarget-{uniqueTag}";
        await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = targetName,
            cpf = UniqueCpf(),
            birthDate = "1990-01-01",
        });
        await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = $"Other-{uniqueTag}",
            cpf = UniqueCpf(),
            birthDate = "1990-01-01",
        });

        var response = await client.GetAsync($"/api/v1/persons?name={uniqueTag}");
        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<PersonResponse>>(JsonOptions);

        Assert.NotNull(paged);
        Assert.Equal(2, paged!.Items.Count);
        Assert.Contains(paged.Items, p => p.Name == targetName);
    }

    [Fact]
    public async Task Should_UpdatePerson_When_RequestIsValid()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var create = await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = "Maria",
            cpf = UniqueCpf(),
            birthDate = "1990-01-01",
        });
        var created = await create.Content.ReadFromJsonAsync<PersonResponse>(JsonOptions);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/persons/{created!.Id}",
            new
            {
                name = "Maria Updated",
                birthDate = "1991-02-02",
            });

        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var fetched = await client.GetFromJsonAsync<PersonResponse>(
            $"/api/v1/persons/{created.Id}",
            JsonOptions);
        Assert.NotNull(fetched);
        Assert.Equal("Maria Updated", fetched!.Name);
        Assert.Equal(new DateOnly(1991, 2, 2), fetched.BirthDate);
    }

    [Fact]
    public async Task Should_SoftDeleteAndRestorePerson()
    {
        var client = await _factory.CreateAuthenticatedAdminClientAsync();
        var create = await client.PostAsJsonAsync("/api/v1/persons", new
        {
            name = "Maria",
            cpf = UniqueCpf(),
            birthDate = "1990-01-01",
        });
        var created = await create.Content.ReadFromJsonAsync<PersonResponse>(JsonOptions);

        var deleteResponse = await client.DeleteAsync($"/api/v1/persons/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getAfterDelete = await client.GetAsync($"/api/v1/persons/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);

        var deletedList = await client.GetFromJsonAsync<PagedResponse<PersonResponse>>(
            "/api/v1/persons/deleted",
            JsonOptions);
        Assert.NotNull(deletedList);
        Assert.Contains(deletedList!.Items, p => p.Id == created.Id);

        var restoreResponse = await client.PostAsync(
            $"/api/v1/persons/{created.Id}/restore",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, restoreResponse.StatusCode);

        var getAfterRestore = await client.GetAsync($"/api/v1/persons/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getAfterRestore.StatusCode);
    }

    [Fact]
    public async Task Should_ReturnProblemDetails_OnException()
    {
        var client = await _factory.CreateAuthenticatedUserClientAsync();
        var response = await client.GetAsync($"/api/v1/persons/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.TryGetProperty("traceId", out _));
        Assert.True(problem.TryGetProperty("status", out var status));
        Assert.Equal(404, status.GetInt32());
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

        public DateTime CreatedAtUtc { get; init; }

        public DateTime UpdatedAtUtc { get; init; }

        public DateTime? DeletedAtUtc { get; init; }

        public DateTime? RestoredAtUtc { get; init; }
    }

    private sealed class PagedResponse<T>
    {
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        public int Page { get; init; }

        public int PageSize { get; init; }

        public int TotalItems { get; init; }

        public int TotalPages { get; init; }

        public bool HasNext { get; init; }

        public bool HasPrevious { get; init; }
    }
}
