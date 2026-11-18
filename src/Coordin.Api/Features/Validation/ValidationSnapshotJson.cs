using System.Text.Json;
using System.Text.Json.Serialization;
using Coordin.Engine.Planning;

namespace Coordin.Api.Features.Validation;

/// <summary>
/// A serialização do planejamento guardado (DT-37). As opções são fixas: enum como texto, datas no formato ISO e nomes
/// das propriedades como estão no motor. O mesmo texto volta a produzir o mesmo planejamento, e o mesmo cálculo.
/// </summary>
public static class ValidationSnapshotJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(PlanningSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);

    public static PlanningSnapshot Deserialize(string json) =>
        JsonSerializer.Deserialize<PlanningSnapshot>(json, Options)
        ?? throw new InvalidOperationException("O plano guardado está vazio.");
}
