using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.Calendar;

/// <summary>Mapeamento de <see cref="Holiday"/> (TDD 4.2).</summary>
public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays");
        builder.HasKey(holiday => holiday.Id);
        builder.Property(holiday => holiday.Id).ValueGeneratedNever();

        builder.Property(holiday => holiday.Date).HasColumnType("date");
        builder.Property(holiday => holiday.Description).HasMaxLength(Holiday.DescriptionMaxLength);

        // Dois feriados na mesma data não existem: garantido pelo banco.
        builder.HasIndex(holiday => holiday.Date).IsUnique();
    }
}
