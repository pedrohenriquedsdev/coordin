using Coordin.Api.Features.People;

namespace Coordin.Api.Features.Teams;

/// <summary>
/// Time do setor (RF-01). O nome é único sem diferenciar maiúsculas: <see cref="NormalizedName"/>
/// guarda a forma de comparação e tem índice único no banco, que é quem decide em caso de corrida.
/// </summary>
public sealed class Team
{
    public const int NameMaxLength = 100;

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Name { get; private set; } = "";

    /// <summary>Nome sem espaços nas pontas e em maiúsculas, só para a comparação de unicidade.</summary>
    public string NormalizedName { get; private set; } = "";

    public ICollection<Person> People { get; } = [];

    public static Team Create(string name)
    {
        var team = new Team();
        team.Rename(name);
        return team;
    }

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = Normalize(Name);
    }

    /// <summary>A forma de comparação do nome: sem espaços nas pontas e sem diferenciar maiúsculas.</summary>
    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
