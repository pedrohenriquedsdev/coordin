namespace Coordin.Api.Infrastructure.Http;

/// <summary>Fábricas de respostas de erro de regra de negócio (DT-18).</summary>
public static class ApiProblems
{
    public static IResult Unauthorized(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, detail: detail);

    /// <summary>
    /// Conflito de regra de negócio: <c>409</c> com a extensão <c>code</c>, um identificador
    /// estável em kebab-case que o front usa para reagir ao erro (por exemplo, <c>team-has-people</c>).
    /// </summary>
    public static IResult Conflict(string code, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });

    /// <summary>
    /// <c>400</c> com o erro em um campo do corpo, no mesmo formato da validação nativa
    /// (<c>errors.campo</c> em <c>camelCase</c>), para regras que dependem do banco, como
    /// "o time informado existe". O <paramref name="field"/> já deve estar em <c>camelCase</c>.
    /// </summary>
    public static IResult InvalidField(string field, string message) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = new Dictionary<string, string[]> { [field] = [message] },
            });
}
