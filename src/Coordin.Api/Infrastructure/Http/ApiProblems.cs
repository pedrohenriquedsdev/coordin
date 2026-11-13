namespace Coordin.Api.Infrastructure.Http;

/// <summary>Fábricas de respostas de erro de regra de negócio (DT-18).</summary>
public static class ApiProblems
{
    /// <summary>
    /// Conflito de regra de negócio: <c>409</c> com a extensão <c>code</c>, um identificador
    /// estável em kebab-case que o front usa para reagir ao erro (por exemplo, <c>team-has-people</c>).
    /// </summary>
    public static IResult Unauthorized(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, detail: detail);

    public static IResult Conflict(string code, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
