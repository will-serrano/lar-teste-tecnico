using Xunit;

namespace CandidateAssessment.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection that serializes integration tests across classes to avoid
/// concurrent access to the shared in-memory SQLite database and Identity seed gate.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<CandidateAssessmentWebApplicationFactory>
{
    public const string Name = "IntegrationTests";
}
