using System.Net;
using System.Net.Http.Json;
using System.Text;
using CandidateAssessment.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CandidateAssessment.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class ValidationApiTests
{
    private const string PhonesPath = "/api/v1/persons/11111111-1111-1111-1111-111111111111/phones";
    private const string PhonePath = PhonesPath + "/22222222-2222-2222-2222-222222222222";
    private readonly CandidateAssessmentWebApplicationFactory _factory;

    public ValidationApiTests(CandidateAssessmentWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Theory]
    [InlineData("/api/v1/persons?page=0", "Page", "Page must be greater than or equal to 1.")]
    [InlineData("/api/v1/persons?pageSize=0", "PageSize", "PageSize must be greater than or equal to 1.")]
    [InlineData("/api/v1/persons/deleted?page=0", "Page", "Page must be greater than or equal to 1.")]
    [InlineData("/api/v1/persons/deleted?pageSize=0", "PageSize", "PageSize must be greater than or equal to 1.")]
    [InlineData("/api/v1/persons?page=abc", "Page", "The value 'abc' is not valid.")]
    [InlineData("/api/v1/persons/deleted?page=abc", "Page", "The value 'abc' is not valid.")]
    public async Task Should_ReturnValidationProblemDetails_When_QueryIsInvalid(
        string path,
        string property,
        string message)
    {
        using var client = await _factory.CreateAuthenticatedAdminClientAsync();
        using var response = await client.GetAsync(path);

        await AssertValidationProblemAsync(response, property, message);
    }

    [Theory]
    [InlineData("POST", "/api/v1/persons", "{\"name\":\"Test\",\"cpf\":\"123\",\"birthDate\":\"1990-01-01\"}",
        "Cpf", "Cpf must contain 11 digits.")]
    [InlineData("PUT", "/api/v1/persons/11111111-1111-1111-1111-111111111111",
        "{\"name\":\" \",\"birthDate\":\"1990-01-01\"}", "Name", "Name is required.")]
    [InlineData("POST", "/api/v1/auth/login", "{\"username\":\" \",\"password\":\"test\"}",
        "Username", "Username is required.")]
    [InlineData("POST", PhonesPath, "{\"type\":\"Mobile\",\"number\":\"abc\"}",
        "Number", "Number must contain only digits, spaces, '(', ')', '+', or '-'.")]
    [InlineData("PUT", PhonePath, "{\"type\":\"Mobile\",\"number\":\" \"}",
        "Number", "Number is required.")]
    [InlineData("POST", PhonesPath, "{\"type\":\"Mobile\",\"number\":\"123\"}",
        "request", "Number is not valid for type Mobile.")]
    [InlineData("PUT", PhonePath, "{\"type\":\"Mobile\",\"number\":\"123\"}",
        "request", "Number is not valid for type Mobile.")]
    public async Task Should_ReturnValidationProblemDetails_When_BodyIsInvalid(
        string method,
        string path,
        string json,
        string property,
        string message)
    {
        using var client = await _factory.CreateAuthenticatedAdminClientAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        using var response = await client.SendAsync(request);

        await AssertValidationProblemAsync(response, property, message);
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        string property,
        string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem!.Status);
        Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.5.1", problem.Type);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.True(problem.Extensions.TryGetValue("traceId", out var traceId));
        Assert.False(string.IsNullOrWhiteSpace(traceId?.ToString()));
        Assert.True(problem.Errors.TryGetValue(property, out var errors));
        Assert.Contains(message, errors!);
    }
}
