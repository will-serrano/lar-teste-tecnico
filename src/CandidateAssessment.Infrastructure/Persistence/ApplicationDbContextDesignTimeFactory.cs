using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CandidateAssessment.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef` tools to construct the DbContext
/// without bootstrapping the full host.
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
