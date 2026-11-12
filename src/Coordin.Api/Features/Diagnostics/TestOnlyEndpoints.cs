using System.ComponentModel.DataAnnotations;
using Coordin.Api.Infrastructure.Http;

namespace Coordin.Api.Features.Diagnostics;

/// <summary>
/// Endpoints de diagnóstico que existem <b>somente</b> no ambiente <c>Testing</c>. Eles provam o
/// comportamento comum da API (erros, validação, JSON) antes de haver endpoints de negócio.
/// Nunca são mapeados em <c>Development</c> ou <c>Production</c>.
/// </summary>
public static class TestOnlyEndpoints
{
    public static IEndpointRouteBuilder MapTestOnlyEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsEnvironment("Testing"))
        {
            return app;
        }

        var group = app.MapGroup("/api/_test");

        group.MapGet("/error", IResult () =>
            throw new InvalidOperationException("Segredo interno: Host=db;Password=senha-secreta"));

        group.MapGet("/conflict", () =>
            ApiProblems.Conflict("example-conflict", "Conflito de exemplo."));

        group.MapPost("/validation", (ValidationProbe input) => Results.Ok(input));

        group.MapGet("/json", () => new JsonProbe("ok", new DateOnly(2026, 11, 28)));

        return app;
    }
}

public sealed record ValidationProbe(
    [property: Required(ErrorMessage = "O campo é obrigatório.")] string? Name);

public sealed record JsonProbe(string DatabaseStatus, DateOnly PlanningDate);
