using Coordin.Api.Features.Demands;
using Coordin.Engine.Planning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.Planning;

/// <summary>Mapeamento de <see cref="Forecast"/> (TDD 4.2).</summary>
public sealed class ForecastConfiguration : IEntityTypeConfiguration<Forecast>
{
    public void Configure(EntityTypeBuilder<Forecast> builder)
    {
        builder.ToTable(
            "Forecasts",
            table => table.HasCheckConstraint(
                "CK_Forecasts_EndAfterWithoutMargin",
                "\"ForecastEndDate\" IS NULL OR \"EndDateWithoutMargin\" IS NULL OR \"ForecastEndDate\" >= \"EndDateWithoutMargin\""));

        builder.HasKey(forecast => forecast.DemandId);
        builder.Property(forecast => forecast.UnpredictableReason).HasMaxLength(40);
        builder.Property(forecast => forecast.ForecastStartDate).HasColumnType("date");
        builder.Property(forecast => forecast.EndDateWithoutMargin).HasColumnType("date");
        builder.Property(forecast => forecast.ForecastEndDate).HasColumnType("date");
        builder.Property(forecast => forecast.BaselineEndDate).HasColumnType("date");
        builder.Property(forecast => forecast.CalculatedAt).HasColumnType("timestamptz");

        // Uma previsão por demanda; excluir a demanda remove a previsão.
        builder.HasOne<Demand>()
            .WithOne(demand => demand.Forecast)
            .HasForeignKey<Forecast>(forecast => forecast.DemandId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}

/// <summary>Mapeamento de <see cref="PlanningSettings"/>, com o registro inicial (DT-22, DT-13).</summary>
public sealed class PlanningSettingsConfiguration : IEntityTypeConfiguration<PlanningSettings>
{
    public const decimal InitialSafetyMarginPercent = 20m;

    public void Configure(EntityTypeBuilder<PlanningSettings> builder)
    {
        builder.ToTable(
            "PlanningSettings",
            table =>
            {
                table.HasCheckConstraint("CK_PlanningSettings_Singleton", $"\"Id\" = {PlanningSettings.SingletonId}");
                table.HasCheckConstraint("CK_PlanningSettings_SafetyMargin", "\"SafetyMarginPercent\" >= 0");
            });

        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.ExecutionOrderMode).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(settings => settings.SafetyMarginPercent).HasPrecision(6, 2);

        builder.HasData(new PlanningSettings
        {
            Id = PlanningSettings.SingletonId,
            ExecutionOrderMode = ExecutionOrderMode.FollowPriority,
            SafetyMarginPercent = InitialSafetyMarginPercent,
        });
    }
}
