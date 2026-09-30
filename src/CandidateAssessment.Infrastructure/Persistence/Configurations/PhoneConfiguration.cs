using CandidateAssessment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateAssessment.Infrastructure.Persistence.Configurations;

public class PhoneConfiguration : IEntityTypeConfiguration<Phone>
{
    public void Configure(EntityTypeBuilder<Phone> builder)
    {
        builder.ToTable("Phones");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(p => p.PersonId)
            .IsRequired();

        builder.Property(p => p.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(p => p.Number)
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(p => new { p.PersonId, p.Number })
            .IsUnique()
            .HasDatabaseName("IX_Phones_PersonId_Number");

        builder.HasIndex(p => p.PersonId)
            .HasDatabaseName("IX_Phones_PersonId");
    }
}
