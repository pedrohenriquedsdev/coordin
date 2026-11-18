using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.Demands;

/// <summary>
/// Mapeamento dos históricos (RF-20, RF-21; DT-14). As regras de integridade vivem no banco: uma mudança registrada
/// tem de mudar de fato, e o histórico só é gravado com a demanda que ele descreve. A proteção contra atualização
/// e exclusão fica nos gatilhos da migração, porque o EF não as impede sozinho.
/// </summary>
public sealed class ForecastHistoryConfiguration : IEntityTypeConfiguration<ForecastHistory>
{
    public void Configure(EntityTypeBuilder<ForecastHistory> builder)
    {
        builder.ToTable(
            "ForecastHistory",
            table => table.HasCheckConstraint(
                "CK_ForecastHistory_Changed",
                "\"PreviousEndDate\" IS DISTINCT FROM \"NewEndDate\""));

        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).ValueGeneratedNever();
        builder.Property(history => history.PreviousEndDate).HasColumnType("date");
        builder.Property(history => history.NewEndDate).HasColumnType("date");
        builder.Property(history => history.Cause).HasMaxLength(32).IsRequired();
        builder.Property(history => history.ChangedAt).HasColumnType("timestamp with time zone");

        builder.HasOne<Demand>()
            .WithMany()
            .HasForeignKey(history => history.DemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(history => new { history.DemandId, history.ChangedAt });
    }
}

public sealed class EstimateHistoryConfiguration : IEntityTypeConfiguration<EstimateHistory>
{
    public void Configure(EntityTypeBuilder<EstimateHistory> builder)
    {
        builder.ToTable(
            "EstimateHistory",
            table => table.HasCheckConstraint(
                "CK_EstimateHistory_Changed",
                "\"PreviousHours\" IS DISTINCT FROM \"NewHours\""));

        builder.HasKey(history => history.Id);
        builder.Property(history => history.Id).ValueGeneratedNever();
        builder.Property(history => history.PreviousHours).HasPrecision(10, 2);
        builder.Property(history => history.NewHours).HasPrecision(10, 2);
        builder.Property(history => history.Reason).HasMaxLength(500);
        builder.Property(history => history.ChangedAt).HasColumnType("timestamp with time zone");

        builder.HasOne<Demand>()
            .WithMany()
            .HasForeignKey(history => history.DemandId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(history => new { history.DemandId, history.ChangedAt });
    }
}
