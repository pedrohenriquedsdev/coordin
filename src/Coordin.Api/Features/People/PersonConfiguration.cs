using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.People;

/// <summary>Mapeamento de <see cref="Person"/> (TDD 4.2).</summary>
public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable(
            "People",
            table => table.HasCheckConstraint(
                "CK_People_DailyCapacityHours_Range",
                $"\"DailyCapacityHours\" >= 0 AND \"DailyCapacityHours\" <= {Person.MaxDailyCapacityHours}"));

        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).ValueGeneratedNever();

        builder.Property(person => person.Name).HasMaxLength(Person.NameMaxLength).IsRequired();
        builder.Property(person => person.DailyCapacityHours).HasPrecision(5, 2);

        // RESTRICT: um time com pessoas não pode ser excluído (TDD 4.4). O banco garante, além da API.
        builder.HasOne(person => person.Team)
            .WithMany(team => team.People)
            .HasForeignKey(person => person.TeamId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
