using CandidateAssessment.Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateAssessment.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => new { x.ScopeHash, x.KeyHash });
        builder.Property(x => x.ScopeHash).HasMaxLength(64);
        builder.Property(x => x.KeyHash).HasMaxLength(64);
        builder.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(256);
        builder.Property(x => x.Location).HasMaxLength(2048);
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
