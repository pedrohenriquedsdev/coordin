using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Scenarios;
using Coordin.Engine.Tests.Planning;

namespace Coordin.Engine.Tests.Forecasting;

/// <summary>
/// Referência de resultado do motor (change 9, aceleração): para cada uma das 400 sementes do gerador, o
/// SHA-256 do JSON do <see cref="ForecastResult"/> (previsões e consumo) do cálculo na ordem atual e na
/// ordem de uma permutação fixa da semente. Os hashes foram gravados <b>antes</b> de qualquer otimização;
/// se um dia um resultado mudar, mesmo por um dia ou por uma casa decimal, este teste acusa.
///
/// Para regravar a referência de propósito (só quando uma regra de cálculo muda de verdade e o autor
/// aprovou), rode com a variável de ambiente <c>COORDIN_WRITE_GOLDEN=1</c>; o teste reescreve o arquivo
/// <c>golden-hashes.txt</c> ao lado deste código.
/// </summary>
public class ResultGoldenTests
{
    private const int Snapshots = 400;

    private static string Hash(ForecastResult result) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result))));

    private static IEnumerable<string> Lines() =>
        Enumerable.Range(1, Snapshots).Select(seed =>
        {
            var snapshot = RandomPlanningGenerator.Generate(seed);
            var random = new Random(seed + 7000);
            var order = ExecutionOrders.Current(snapshot).OrderBy(_ => random.Next()).ToList();

            var current = Hash(ForecastEngine.Calculate(snapshot));
            var reordered = Hash(ForecastEngine.Calculate(ExecutionOrders.WithOrder(snapshot, order)));
            return $"{seed};{current};{reordered}";
        });

    [Fact]
    public void TheResults_ForTheGeneratedPlannings_AreBitForBitTheRecordedOnes()
    {
        var actual = Lines().ToList();

        if (Environment.GetEnvironmentVariable("COORDIN_WRITE_GOLDEN") == "1")
        {
            File.WriteAllLines(SourcePath(), actual);
            return;
        }

        var expected = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Forecasting", "golden-hashes.txt"));

        Assert.Equal(Snapshots, expected.Length);
        for (var index = 0; index < Snapshots; index++)
        {
            Assert.True(expected[index] == actual[index], $"O resultado mudou na semente {index + 1} (cálculo na ordem atual ou na ordem permutada).");
        }
    }

    private static string SourcePath([CallerFilePath] string file = "") =>
        Path.Combine(Path.GetDirectoryName(file)!, "golden-hashes.txt");
}
