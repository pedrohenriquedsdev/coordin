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

    private static bool IsInDirectory(string assemblyPath, string directory) =>
        string.Equals(
            Path.GetDirectoryName(assemblyPath),
            directory,
            StringComparison.OrdinalIgnoreCase);
}
