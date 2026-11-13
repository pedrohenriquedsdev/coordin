using System.ComponentModel.DataAnnotations;

namespace Coordin.Api.Features.Teams;

/// <summary>Corpo de <c>POST /api/teams</c> e <c>PUT /api/teams/{id}</c> (RF-01).</summary>
public sealed record TeamRequest(
    [property: Required(ErrorMessage = "O nome do time é obrigatório.")]
    [property: StringLength(Team.NameMaxLength, ErrorMessage = "O nome do time deve ter no máximo 100 caracteres.")]
    string? Name);

/// <summary>
/// Um time com a quantidade de pessoas e a capacidade diária, que é a soma da capacidade das pessoas
/// dele (RF-02, DT-10), calculada pelo motor.
/// </summary>
public sealed record TeamResponse(Guid Id, string Name, int PeopleCount, decimal DailyCapacityHours);
