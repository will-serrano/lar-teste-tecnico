using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CandidateAssessment.Infrastructure.Persistence;

/// <summary>
/// Fábrica de tempo de design usada pelas ferramentas `dotnet ef` para criar o DbContext
/// sem inicializar o host completo.
/// </summary>
public class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=candidateassessment.db")
            .Options;

        return new ApplicationDbContext(options);
    }
}
