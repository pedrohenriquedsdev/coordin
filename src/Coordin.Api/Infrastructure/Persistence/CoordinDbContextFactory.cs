using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Coordin.Api.Infrastructure.Persistence;

/// <summary>
/// Usada <b>somente</b> pelo <c>dotnet-ef</c> para gerar migrations. Assim a ferramenta não
/// depende de segredos nem de um banco no ar: gerar uma migration só precisa do modelo, e a
/// string abaixo não tem credencial nem é usada para conectar.
/// </summary>
public sealed class CoordinDbContextFactory : IDesignTimeDbContextFactory<CoordinDbContext>
{
    public CoordinDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CoordinDbContext>()
            .UseNpgsql("Host=localhost;Database=coordin_design_time")
            .Options;

        return new CoordinDbContext(options);
    }
}
