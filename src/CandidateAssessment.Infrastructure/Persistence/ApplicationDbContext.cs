using CandidateAssessment.Application.Abstractions.Persistence;
using CandidateAssessment.Domain.Entities;
using CandidateAssessment.Infrastructure.Idempotency;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CandidateAssessment.Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<IdentityUser, IdentityRole, string>, IUnitOfWork
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Person> Persons => Set<Person>();

    public DbSet<Phone> Phones => Set<Phone>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
