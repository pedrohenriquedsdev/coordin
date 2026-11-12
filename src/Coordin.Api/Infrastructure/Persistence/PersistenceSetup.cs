using Microsoft.EntityFrameworkCore;

namespace Coordin.Api.Infrastructure.Persistence;

/// <summary>PostgreSQL com EF Core (DT-07); a string de conexão nunca fica em arquivo versionado (DT-30).</summary>
public static class PersistenceSetup
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddCoordinPersistence(this IServiceCollection services)
    {
        // A string é lida quando o contexto é criado (e não aqui) para valer a configuração
        // final: user-secrets em desenvolvimento, variável de ambiente em produção.
        services.AddDbContext<CoordinDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    "A string de conexão 'ConnectionStrings:Default' não está configurada. " +
                    "Em desenvolvimento use 'dotnet user-secrets'; em produção, a variável ConnectionStrings__Default.");

            options.UseNpgsql(connectionString);
        });

        return services;
    }

    /// <summary>Aplica as migrations pendentes antes de a aplicação aceitar requisições (DT-07).</summary>
    public static async Task MigrateCoordinDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoordinDbContext>();
        await db.Database.MigrateAsync();
    }
}
