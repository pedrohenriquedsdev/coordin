using Coordin.Api.Infrastructure.Http;

namespace Coordin.Api.Tests.Http;

/// <summary>Porta de escuta da API (DT-09): lida da variável PORT, com 8080 por padrão.</summary>
public class ListenUrlsTests
{
    [Fact]
    public void Resolve_UsesThePortFromTheEnvironment()
    {
        Assert.Equal("http://+:10000", ListenUrls.Resolve("10000"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_UsesPort8080WhenMissing(string? port)
    {
        Assert.Equal("http://+:8080", ListenUrls.Resolve(port));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("-1")]
    [InlineData("80.5")]
    public void Resolve_RejectsAnInvalidPort(string port)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ListenUrls.Resolve(port));

        Assert.Equal("A variável PORT deve ser um número entre 1 e 65535.", error.Message);
    }
}
