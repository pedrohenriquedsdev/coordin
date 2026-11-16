using System.Globalization;

namespace Coordin.Api.Infrastructure.Http;

/// <summary>
/// Endereço de escuta da API (DT-09): a hospedagem informa a porta pela variável <c>PORT</c>;
/// sem ela, usa a 8080 da imagem local.
/// </summary>
public static class ListenUrls
{
    public const int DefaultPort = 8080;

    public static string Resolve(string? port)
    {
        if (string.IsNullOrWhiteSpace(port))
        {
            return $"http://+:{DefaultPort}";
        }

        if (!int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value is < 1 or > 65535)
        {
            throw new InvalidOperationException("A variável PORT deve ser um número entre 1 e 65535.");
        }

        return $"http://+:{value}";
    }
}
