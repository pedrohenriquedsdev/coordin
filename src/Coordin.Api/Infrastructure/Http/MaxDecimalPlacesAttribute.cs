using System.ComponentModel.DataAnnotations;

namespace Coordin.Api.Infrastructure.Http;

/// <summary>
/// Limita as casas decimais de um número (por exemplo, horas com no máximo duas, DT-15). O
/// <c>Range</c> do .NET confere a faixa, mas não a precisão: 8,123 passaria. Valores nulos são
/// ignorados, para o <c>[Required]</c> decidir sozinho se o campo é obrigatório.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class MaxDecimalPlacesAttribute(int places) : ValidationAttribute
{
    public int Places { get; } = places;

    public override bool IsValid(object? value) =>
        value is not decimal number || decimal.Round(number, Places) == number;

    public override string FormatErrorMessage(string name) =>
        ErrorMessage ?? $"Use no máximo {Places} casas decimais.";
}
