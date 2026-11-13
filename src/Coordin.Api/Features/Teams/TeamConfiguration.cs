using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coordin.Api.Features.Teams;

/// <summary>Mapeamento de <see cref="Team"/> (TDD 4.2).</summary>
public sealed class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(team => team.Id);
        builder.Property(team => team.Id).ValueGeneratedNever(); // Guid v7 gerado pela aplicação (DT-15)

        builder.Property(team => team.Name).HasMaxLength(Team.NameMaxLength).IsRequired();
        builder.Property(team => team.NormalizedName).HasMaxLength(Team.NameMaxLength).IsRequired();

        // A regra "nome único sem diferenciar maiúsculas" é garantida aqui, pelo banco.
        builder.HasIndex(team => team.NormalizedName).IsUnique();
    }
}
