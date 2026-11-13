using System.ComponentModel.DataAnnotations;
using Coordin.Api.Infrastructure.Http;

namespace Coordin.Api.Features.Diagnostics;

/// <summary>
/// Endpoints de diagnóstico que existem <b>somente</b> no ambiente <c>Testing</c>. Eles provam o
/// comportamento comum da API (erros, validação, JSON, proteção por padrão) antes de haver
/// endpoints de negócio. Nunca são mapeados em <c>Development</c> ou <c>Production</c>.
/// </summary>
public static class TestOnlyEndpoints
{
    public static IEndpointRouteBuilder MapTestOnlyEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsEnvironment("Testing"))
        {
            return app;
        }

        // Anônimos de propósito: provam erros e validação sem depender do login (change 1).
        var group = app.MapGroup("/api/_test").AllowAnonymous();

        group.MapGet("/error", IResult () =>
            throw new InvalidOperationException("Segredo interno: Host=db;Password=senha-secreta"));

        group.MapGet("/conflict", () =>
            ApiProblems.Conflict("example-conflict", "Conflito de exemplo."));

        group.MapPost("/validation", (ValidationProbe input) => Results.Ok(input));

        group.MapGet("/json", () => new JsonProbe("ok", new DateOnly(2026, 11, 28)));

        // De propósito FORA do grupo e sem nenhum metadado de autorização: prova que um endpoint
        // "comum", como os de negócio das próximas changes, já nasce exigindo login (DT-28).
        app.MapGet("/api/_test/protected", () => Results.Ok(new ProtectedProbe("ok")));

        return app;
    }
}

public sealed record ValidationProbe(
    [property: Required(ErrorMessage = "O campo é obrigatório.")] string? Name);

public sealed record JsonProbe(string DatabaseStatus, DateOnly PlanningDate);

public sealed record ProtectedProbe(string Status);
