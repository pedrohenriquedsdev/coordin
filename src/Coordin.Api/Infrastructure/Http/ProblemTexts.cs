namespace Coordin.Api.Infrastructure.Http;

/// <summary>Títulos e mensagens padrão em português para as respostas de erro (RNF-07).</summary>
internal static class ProblemTexts
{
    public static string? Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Requisição inválida",
        StatusCodes.Status404NotFound => "Não encontrado",
        StatusCodes.Status409Conflict => "Conflito",
        StatusCodes.Status500InternalServerError => "Erro interno",
        StatusCodes.Status503ServiceUnavailable => "Serviço indisponível",
        _ => null,
    };

    public static string? Detail(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Um ou mais campos são inválidos.",
        StatusCodes.Status404NotFound => "O recurso solicitado não foi encontrado.",
        StatusCodes.Status409Conflict => "A operação conflita com o estado atual dos dados.",
        StatusCodes.Status500InternalServerError => "Ocorreu um erro inesperado. Tente novamente.",
        StatusCodes.Status503ServiceUnavailable => "O serviço está temporariamente indisponível.",
        _ => null,
    };
}
