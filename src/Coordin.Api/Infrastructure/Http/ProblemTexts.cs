namespace Coordin.Api.Infrastructure.Http;

/// <summary>Títulos e mensagens padrão em português para as respostas de erro (RNF-07).</summary>
internal static class ProblemTexts
{
    public static string? Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Requisição inválida",
        StatusCodes.Status401Unauthorized => "Não autenticado",
        StatusCodes.Status403Forbidden => "Acesso negado",
        StatusCodes.Status404NotFound => "Não encontrado",
        StatusCodes.Status409Conflict => "Conflito",
        StatusCodes.Status429TooManyRequests => "Muitas requisições",
        StatusCodes.Status500InternalServerError => "Erro interno",
        StatusCodes.Status503ServiceUnavailable => "Serviço indisponível",
        _ => null,
    };

    public static string? Detail(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Um ou mais campos são inválidos.",
        StatusCodes.Status401Unauthorized => "É necessário entrar para acessar este recurso.",
        StatusCodes.Status403Forbidden => "Você não tem permissão para acessar este recurso.",
        StatusCodes.Status404NotFound => "O recurso solicitado não foi encontrado.",
        StatusCodes.Status409Conflict => "A operação conflita com o estado atual dos dados.",
        StatusCodes.Status429TooManyRequests => "Há requisições demais. Aguarde um pouco e tente novamente.",
        StatusCodes.Status500InternalServerError => "Ocorreu um erro inesperado. Tente novamente.",
        StatusCodes.Status503ServiceUnavailable => "O serviço está temporariamente indisponível.",
        _ => null,
    };
}
