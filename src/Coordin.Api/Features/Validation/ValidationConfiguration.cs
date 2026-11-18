using Coordin.Api.Features.Demands;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.Validation;

/// <summary>Mapeamento de <see cref="ValidationBaseline"/>: uma por demanda, apagada com a demanda (DT-37).</summary>
public sealed class ValidationBaselineConfiguration : IEntityTypeConfiguration<ValidationBaseline>
{
    public void Configure(EntityTypeBuilder<ValidationBaseline> builder)
    {
        builder.ToTable("ValidationBaselines");
        builder.HasKey(baseline => baseline.DemandId);
        builder.Property(baseline => baseline.BaselineDate).HasColumnType("date");

        builder.HasOne<Demand>()
            .WithMany()
            .HasForeignKey(baseline => baseline.DemandId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Mapeamento de <see cref="ValidationCurrentPlan"/>: um único registro, garantido pelo banco (DT-37).</summary>
public sealed class ValidationCurrentPlanConfiguration : IEntityTypeConfiguration<ValidationCurrentPlan>
{
    public void Configure(EntityTypeBuilder<ValidationCurrentPlan> builder)
    {
        builder.ToTable(
            "ValidationCurrentPlans",
            table => table.HasCheckConstraint("CK_ValidationCurrentPlans_Singleton", "\"Id\" = 1"));

        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();
        builder.Property(plan => plan.SnapshotJson).HasColumnType("jsonb");
        builder.Property(plan => plan.CapturedAt).HasColumnType("timestamptz");
    }
}
