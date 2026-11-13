using System.Reflection;

namespace Coordin.Engine.Tests;

/// <summary>
/// Garante a pureza do motor (DT-02): o assembly não pode depender de nada além do runtime
/// do .NET (nenhum pacote, nenhum framework web, nenhum acesso a dados).
/// </summary>
public class EngineArchitectureTests
{
    [Fact]
    public void EngineAssembly_References_OnlyRuntimeAssemblies()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var engine = typeof(EngineAssemblyMarker).Assembly;

        var outsideRuntime = engine
            .GetReferencedAssemblies()
            .Select(name => Assembly.Load(name))
            .Where(assembly => !IsInDirectory(assembly.Location, runtimeDirectory))
            .Select(assembly => assembly.GetName().Name)
            .ToList();

        Assert.Empty(outsideRuntime);
    }

    /// <summary>
    /// Determinismo por construção (RNF-02, DT-02): o código do motor não pode ler o relógio, o fuso
    /// da máquina, gerar números aleatórios nem criar identificadores novos.
    /// </summary>
    [Theory]
    [InlineData("DateTime")]
    [InlineData("TimeZoneInfo")]
    [InlineData("TimeProvider")]
    [InlineData("Random")]
    [InlineData("Guid.NewGuid")]
    public void EngineSource_DoesNotUseClockTimeZoneOrRandomness(string forbidden)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Coordin.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var offenders = Directory
            .EnumerateFiles(Path.Combine(directory.FullName, "src", "Coordin.Engine"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains(forbidden, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    private static bool IsInDirectory(string assemblyPath, string directory) =>
        string.Equals(
            Path.GetDirectoryName(assemblyPath),
            directory,
            StringComparison.OrdinalIgnoreCase);
}
