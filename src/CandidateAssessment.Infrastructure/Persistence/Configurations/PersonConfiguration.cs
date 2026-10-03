using CandidateAssessment.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CandidateAssessment.Infrastructure.Persistence.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("Persons");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("Id")
            .ValueGeneratedNever();

        builder.Property(p => p.Name)
            .HasMaxLength(Person.MaxNameLength)
            .IsRequired();

        builder.Property(p => p.Cpf)
            .HasMaxLength(11)
            .IsRequired();

        builder.Property(p => p.BirthDate)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();

        builder.Property(p => p.DeletedAtUtc);

        builder.Property(p => p.RestoredAtUtc);

        builder.HasIndex(p => p.Cpf)
            .IsUnique()
            .HasDatabaseName("IX_Persons_Cpf");

        builder.HasIndex(p => p.Name)
            .HasDatabaseName("IX_Persons_Name");

        // Filtro global de consulta: a exclusão lógica oculta pessoas inativas das consultas comuns.
        builder.HasQueryFilter(p => p.IsActive);

        builder.HasMany(p => p.Phones)
            .WithOne()
            .HasForeignKey(phone => phone.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Phones)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
