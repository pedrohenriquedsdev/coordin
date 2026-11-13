using Coordin.Api.Features.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Infrastructure.Persistence;

/// <summary>
/// Contexto do EF Core. Por enquanto guarda só a conta do gestor (tabelas do Identity) e os
/// refresh tokens (DT-28); as entidades do domínio chegam nas changes 3 a 5 (TDD 4.2), cada uma
/// com sua migration.
/// </summary>
public class CoordinDbContext(DbContextOptions<CoordinDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
