namespace Coordin.Engine.Validation;

/// <summary>Parâmetros da validação: a faixa de variação do esforço e a semente (PRD 7.3, passo 6).</summary>
/// <param name="MinEffortFactor">Fator mínimo; maior que zero.</param>
/// <param name="MaxEffortFactor">Fator máximo; maior ou igual ao mínimo.</param>
/// <param name="Seed">Semente do realizado simulado; a mesma semente reproduz o mesmo realizado.</param>
public sealed record ValidationParameters(decimal MinEffortFactor, decimal MaxEffortFactor, long Seed);

/// <summary>
/// O fator de esforço de cada demanda no realizado simulado (PRD 7.3, passo 6; DT-37). Depende só da semente e do
/// id da demanda: não depende da ordem das demandas nem do relógio.
/// </summary>
public static class EffortFactors
{
    /// <summary>
    /// <c>min + (max − min) × u</c>, com <c>u</c> em [0, 1) gerado pela semente combinada com o id da demanda.
    /// Não é arredondado: o limite superior continua exclusivo.
    /// </summary>
    public static decimal FactorFor(Guid demandId, ValidationParameters parameters)
    {
        ValidateRange(parameters);

        var generator = new SplitMix64(unchecked((ulong)parameters.Seed) ^ StableHash.Of(demandId));
        var unit = (decimal)generator.NextDouble();
        return parameters.MinEffortFactor + ((parameters.MaxEffortFactor - parameters.MinEffortFactor) * unit);
    }

    private static void ValidateRange(ValidationParameters parameters)
    {
        if (parameters.MinEffortFactor <= 0m)
        {
            throw new ArgumentException("O fator mínimo de esforço precisa ser maior que zero.");
        }

        if (parameters.MinEffortFactor > parameters.MaxEffortFactor)
        {
            throw new ArgumentException("O fator mínimo de esforço não pode ser maior que o máximo.");
        }
    }
}
