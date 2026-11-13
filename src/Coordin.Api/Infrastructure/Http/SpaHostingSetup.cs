using Microsoft.AspNetCore.StaticFiles;

namespace Coordin.Api.Infrastructure.Http;

/// <summary>
/// A API serve o build do Angular na mesma origem (DT-01): arquivos estáticos e, para qualquer
/// caminho que não seja da API nem um arquivo, o <c>index.html</c>, para recarregar a página ou abrir
/// um link direto funcionar.
/// </summary>
public static class SpaHostingSetup
{
    private const string IndexFile = "index.html";

    public static WebApplication UseCoordinSpaFiles(this WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = NoCacheForIndex });
        return app;
    }

    /// <summary>
    /// Deve vir <b>depois</b> do fallback de <c>/api</c>, que é mais específico: assim uma rota de API
    /// inexistente nunca devolve o <c>index.html</c>. O padrão do fallback ignora caminhos com extensão,
    /// então um arquivo inexistente (como <c>/x.js</c>) responde 404 em vez da SPA.
    /// </summary>
    public static IEndpointRouteBuilder MapSpaFallback(this IEndpointRouteBuilder app)
    {
        // O HTML da SPA é público; os dados é que exigem login (RNF-04). Sem AllowAnonymous a
        // política de fallback da autenticação bloquearia até a página de login.
        app.MapFallbackToFile(IndexFile, new StaticFileOptions { OnPrepareResponse = NoCacheForIndex })
            .AllowAnonymous();

        return app;
    }

    // O index.html aponta para arquivos com hash no nome; sem cache, um novo deploy vale na hora.
    private static void NoCacheForIndex(StaticFileResponseContext context)
    {
        if (string.Equals(context.File.Name, IndexFile, StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-cache";
        }
    }
}
