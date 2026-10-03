namespace CandidateAssessment.IntegrationTests.Infrastructure;

/// <summary>
/// Coleção do xUnit que serializa os testes de integração entre classes para evitar
/// acesso concorrente ao banco SQLite compartilhado em memória e ao controle de criação
/// dos dados iniciais do Identity.
/// </summary>
[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<CandidateAssessmentWebApplicationFactory>
{
    public const string Name = "IntegrationTests";
}
