using Coordin.Api.Features.Auth;
using Coordin.Api.Features.Calendar;
using Coordin.Api.Features.People;
using Coordin.Api.Features.Teams;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Infrastructure.Persistence;

/// <summary>
/// Contexto do EF Core: a conta do gestor (tabelas do Identity), os refresh tokens (DT-28) e os
/// cadastros de capacidade (times, pessoas, ausências e feriados; TDD 4.2). As demais entidades do
/// domínio chegam nas changes 4 e 5, cada uma com sua migration.
/// </summary>
public class CoordinDbContext(DbContextOptions<CoordinDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Person> People => Set<Person>();

    public DbSet<Absence> Absences => Set<Absence>();

    public DbSet<Holiday> Holidays => Set<Holiday>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Os cadastros de capacidade têm uma configuração por entidade, na pasta da feature.
        builder.ApplyConfigurationsFromAssembly(typeof(CoordinDbContext).Assembly);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(token => token.Id);

            // O hash é a "chave de busca" do cookie: precisa ser único e rápido de achar.
            entity.Property(token => token.TokenHash).HasMaxLength(RefreshToken.TokenHashLength).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();

            // Apagar o usuário apaga as sessões dele.
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Cadeia da rotação: um token aponta para quem o substituiu.
            entity.HasOne<RefreshToken>()
                .WithMany()
                .HasForeignKey(token => token.ReplacedById)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
