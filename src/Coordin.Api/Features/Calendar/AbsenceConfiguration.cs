using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.Calendar;

/// <summary>Mapeamento de <see cref="Absence"/> (TDD 4.2).</summary>
public sealed class AbsenceConfiguration : IEntityTypeConfiguration<Absence>
{
    public void Configure(EntityTypeBuilder<Absence> builder)
    {
        builder.ToTable(
            "Absences",
            table => table.HasCheckConstraint(
                "CK_Absences_Period",
                "\"EndDate\" >= \"StartDate\""));

        builder.HasKey(absence => absence.Id);
        builder.Property(absence => absence.Id).ValueGeneratedNever();

        builder.Property(absence => absence.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(absence => absence.StartDate).HasColumnType("date");
        builder.Property(absence => absence.EndDate).HasColumnType("date");

        // CASCADE: excluir a pessoa remove as ausências dela (TDD 4.4).
        builder.HasOne(absence => absence.Person)
            .WithMany(person => person.Absences)
            .HasForeignKey(absence => absence.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
