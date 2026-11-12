using Coordin.Api.Tests.Infrastructure;

namespace Coordin.Api.Tests.Http;

/// <summary>Spec http-security: nenhum segredo versionado (DT-30, RNF-05).</summary>
public class NoCommittedSecretsTests
{
    [Fact]
    public void AppSettings_ContainNoPasswordsKeysOrConnectionStrings()
    {
        var apiDirectory = Path.Combine(RepositoryRoot.Find(), "src", "Coordin.Api");
        var files = Directory.GetFiles(apiDirectory, "appsettings*.json", SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            Assert.DoesNotContain("Password", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SigningKey", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ConnectionStrings", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoEnvTemplateFile_IsVersioned()
    {
        var root = RepositoryRoot.Find();

        Assert.False(File.Exists(Path.Combine(root, ".env.example")));
    }
}
