using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Coordin.Api.Infrastructure.Auth;

/// <summary>
/// Limite de requisições de <c>login</c> e <c>refresh</c> (DT-28): 10 por minuto por IP. O orçamento é
/// único para os dois endpoints, porque são as portas de entrada de quem tenta adivinhar credenciais.
/// </summary>
public static class AuthRateLimiting
{
    public const string PolicyName = "auth";
    public const string PermitLimitKey = "Auth:RateLimit:PermitLimit";

    public const int DefaultPermitLimit = 10;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddCoordinAuthRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // O padrão do ASP.NET Core é 503; o correto para "muitas requisições" é 429.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;
        });

        // A política é montada quando as opções são criadas (primeiro uso), com a configuração final:
        // assim o limite pode ser ajustado por configuração, e os testes o elevam.
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var permitLimit = configuration.GetValue<int?>(PermitLimitKey) ?? DefaultPermitLimit;

            options.AddPolicy(PolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // Atrás da hospedagem, o UseForwardedHeaders (change 1) já trocou o IP do proxy pelo do cliente.
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = Window,
                        QueueLimit = 0, // acima do limite, recusa na hora em vez de enfileirar
                    }));
        });

        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // Mesmo formato dos demais erros da API (Problem Details em português, com traceId).
        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = http,
                ProblemDetails = { Status = StatusCodes.Status429TooManyRequests },
            });
    }
}
