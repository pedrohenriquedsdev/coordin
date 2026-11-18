using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.Demands;

/// <summary>Mapeamento de <see cref="Demand"/> e de suas partes e alocações (TDD 4.2).</summary>
public sealed class DemandConfiguration : IEntityTypeConfiguration<Demand>
{
    public void Configure(EntityTypeBuilder<Demand> builder)
    {
        builder.ToTable(
            "Demands",
            table =>
            {
                table.HasCheckConstraint("CK_Demands_PriorityRank", "\"PriorityRank\" >= 1");
                table.HasCheckConstraint("CK_Demands_ExecutionRank", "\"ExecutionRank\" >= 1");
                table.HasCheckConstraint(
                    "CK_Demands_CompletedAfterStart",
                    "\"CompletedOn\" IS NULL OR \"StartDate\" IS NULL OR \"CompletedOn\" >= \"StartDate\"");
                table.HasCheckConstraint(
                    "CK_Demands_ActualEffortHours",
                    "\"ActualEffortHours\" IS NULL OR \"ActualEffortHours\" > 0");
            });

        builder.HasKey(demand => demand.Id);
        builder.Property(demand => demand.Id).ValueGeneratedNever();

        builder.Property(demand => demand.Title).HasMaxLength(Demand.TitleMaxLength).IsRequired();
        builder.Property(demand => demand.StartDate).HasColumnType("date");
        builder.Property(demand => demand.InformedEndDate).HasColumnType("date");
        builder.Property(demand => demand.CompletedOn).HasColumnType("date");
        builder.Property(demand => demand.ActualEffortHours).HasPrecision(10, 2);
        builder.Property(demand => demand.ForecastEndDateAtCompletion).HasColumnType("date");
        builder.Property(demand => demand.EstimatedEffortHoursAtCompletion).HasPrecision(10, 2);

        // Índices únicos: o banco garante rankings únicos mesmo com escritas simultâneas (design D6).
        builder.HasIndex(demand => demand.PriorityRank).IsUnique();
        builder.HasIndex(demand => demand.ExecutionRank).IsUnique();
    }
}

/// <summary>Mapeamento de <see cref="DemandTeam"/> (TDD 4.2).</summary>
public sealed class DemandTeamConfiguration : IEntityTypeConfiguration<DemandTeam>
{
    public void Configure(EntityTypeBuilder<DemandTeam> builder)
    {
        builder.ToTable(
            "DemandTeams",
            table => table.HasCheckConstraint(
                "CK_DemandTeams_EstimatedEffortHours",
                "\"EstimatedEffortHours\" IS NULL OR \"EstimatedEffortHours\" > 0"));

        builder.HasKey(part => new { part.DemandId, part.TeamId });
        builder.Property(part => part.EstimatedEffortHours).HasPrecision(10, 2);

        // CASCADE: excluir a demanda remove as partes dela. RESTRICT: um time com partes não é excluído.
        builder.HasOne(part => part.Demand)
            .WithMany(demand => demand.Teams)
            .HasForeignKey(part => part.DemandId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(part => part.Team)
            .WithMany()
            .HasForeignKey(part => part.TeamId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}

/// <summary>Mapeamento de <see cref="DemandAssignment"/> (TDD 4.2).</summary>
public sealed class DemandAssignmentConfiguration : IEntityTypeConfiguration<DemandAssignment>
{
    public void Configure(EntityTypeBuilder<DemandAssignment> builder)
    {
        builder.ToTable("DemandAssignments");

        builder.HasKey(assignment => new { assignment.DemandId, assignment.PersonId });

        // CASCADE nas duas pontas: excluir a demanda ou a pessoa remove a alocação (design D2).
        builder.HasOne(assignment => assignment.Demand)
            .WithMany(demand => demand.Assignments)
            .HasForeignKey(assignment => assignment.DemandId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(assignment => assignment.Person)
            .WithMany()
            .HasForeignKey(assignment => assignment.PersonId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}

/// <summary>Mapeamento de <see cref="DemandDependency"/> (RF-18; DT-23; design D6).</summary>
public sealed class DemandDependencyConfiguration : IEntityTypeConfiguration<DemandDependency>
{
    public void Configure(EntityTypeBuilder<DemandDependency> builder)
    {
        builder.ToTable(
            "DemandDependencies",
            table => table.HasCheckConstraint(
                "CK_DemandDependencies_NoSelfDependency",
                "\"DemandId\" <> \"PrerequisiteDemandId\""));

        builder.HasKey(dependency => new { dependency.DemandId, dependency.PrerequisiteDemandId });

        // CASCADE na dependente: excluir quem espera remove as suas arestas.
        builder.HasOne(dependency => dependency.Demand)
            .WithMany(demand => demand.Prerequisites)
            .HasForeignKey(dependency => dependency.DemandId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        // RESTRICT no pré-requisito: não se exclui uma demanda de que outra depende (demand-has-dependents).
        builder.HasOne(dependency => dependency.Prerequisite)
            .WithMany(demand => demand.Dependents)
            .HasForeignKey(dependency => dependency.PrerequisiteDemandId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(dependency => dependency.PrerequisiteDemandId);
    }
}
