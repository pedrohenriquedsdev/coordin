using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Infrastructure.Persistence;

/// <summary>
/// Contexto do EF Core. Nesta change ele ainda não tem entidades; as do domínio chegam nas
/// changes 3 a 5 (TDD 4.2), cada uma com sua migration.
/// </summary>
public class CoordinDbContext(DbContextOptions<CoordinDbContext> options) : DbContext(options);
