using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>
/// <see cref="CoordinApiFactory"/> no ambiente <c>Testing</c> que, opcionalmente, copia todos os logs da
/// aplicação para um <see cref="ListLoggerProvider"/>, para os testes provarem o que não pode ser escrito
/// (senhas, tokens).
/// </summary>
public sealed class LoggingApiFactory(
    ListLoggerProvider? logs = null,
    PostgresFixture? postgres = null,
    string? connectionString = null,
    IReadOnlyDictionary<string, string?>? configuration = null)
    : CoordinApiFactory("Testing", postgres, connectionString, configuration)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        if (logs is not null)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        }
    }
}
