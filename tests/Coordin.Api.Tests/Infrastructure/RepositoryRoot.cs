namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Localiza a raiz do repositório (onde está o <c>Coordin.slnx</c>) a partir da pasta dos testes.</summary>
public static class RepositoryRoot
{
    public static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Coordin.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Raiz do repositório (Coordin.slnx) não encontrada.");
    }
}
