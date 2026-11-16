using System.ComponentModel.DataAnnotations;
using Coordin.Api.Infrastructure.Http;
using Coordin.Engine.Planning;

namespace Coordin.Api.Features.Planning;

/// <summary>
/// Corpo de <c>PUT /api/settings</c> (RF-09, DT-22). Só a margem é alterável por esta rota: o modo de
/// ordem muda apenas pela aprovação de um cenário (regras invioláveis 2 e 3), então é ignorado se vier.
/// </summary>
public sealed record SettingsRequest(
    [property: Required(ErrorMessage = "A margem de segurança é obrigatória.")]
    [property: Range(typeof(decimal), "0", "1000", ErrorMessage = "A margem deve estar entre 0% e 1000%.")]
    [property: MaxDecimalPlaces(2, ErrorMessage = "A margem deve ter no máximo 2 casas decimais.")]
    decimal? SafetyMarginPercent);

/// <summary>Configurações vigentes do planejamento.</summary>
public sealed record SettingsResponse(decimal SafetyMarginPercent, ExecutionOrderMode ExecutionOrderMode);
