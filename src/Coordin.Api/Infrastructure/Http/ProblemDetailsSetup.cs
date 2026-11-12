using System.Diagnostics;
using System.Text.Json;

namespace Coordin.Api.Infrastructure.Http;

/// <summary>
/// Formato único de erro da API: Problem Details (RFC 9457) com mensagens em português
/// (DT-06, DT-18). Também liga a validação nativa de Minimal APIs.
/// </summary>
public static class ProblemDetailsSetup
{
    public static IServiceCollection AddCoordinProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);
        services.AddValidation();
        return services;
    }

    public static WebApplication UseCoordinProblemDetails(this WebApplication app)
    {
        // Exceções não tratadas viram 500 em Problem Details; respostas 4xx/5xx sem corpo
        // (por exemplo, um 404 de rota) também ganham o corpo no mesmo formato.
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    private static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var status = problem.Status ?? context.HttpContext.Response.StatusCode;

        if (ProblemTexts.Title(status) is { } title)
        {
            problem.Title = title;
        }

        if (context.Exception is not null)
        {
            // Nunca expor a mensagem, o tipo ou a pilha da exceção ao cliente.
            problem.Extensions.Clear();
            problem.Detail = ProblemTexts.Detail(StatusCodes.Status500InternalServerError);
        }

        if (problem is HttpValidationProblemDetails validation)
        {
            // O front trata os campos em camelCase, como no restante do JSON da API.
            validation.Errors = validation.Errors.ToDictionary(
                error => ToCamelCasePath(error.Key),
                error => error.Value,
                StringComparer.Ordinal);
        }

        problem.Detail ??= ProblemTexts.Detail(status);
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    }

    // "Items[0].Name" -> "items[0].name"
    private static string ToCamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
