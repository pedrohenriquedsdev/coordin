using System.Text.RegularExpressions;
using Coordin.Api.Features.Validation.Dataset;
using Coordin.Engine.Forecasting;
using Coordin.Engine.Planning;

namespace Coordin.Api.Tests.Validation;

/// <summary>
/// O conjunto fictício da validação (PRD 7.3, M2; DT-37; RNF-05): tamanho, prioridades, validade para o motor,
/// previsibilidade de todas as demandas e ausência de qualquer dado real.
/// </summary>
public sealed class ValidationDatasetTests
{
    /// <summary>Os setores do conjunto. São áreas genéricas, não nomes de empresa nem de pessoa.</summary>
    private static readonly string[] AllowedTeamNames =
        ["Web", "Dados", "Mobile", "Plataforma", "Pagamentos", "Relatorios", "Infraestrutura"];

    private static readonly Regex PersonName = new(@"^Pessoa \d{2}$", RegexOptions.CultureInvariant);
    private static readonly Regex DemandTitle = new(@"^Demanda \d{2}$", RegexOptions.CultureInvariant);

    [Fact]
    public void Dataset_has_seven_teams_thirty_to_forty_people_and_thirty_demands()
    {
        var dataset = ValidationDataset.Read();

        Assert.Equal(7, dataset.Teams.Count);
        Assert.InRange(dataset.People.Count, 30, 40);
        Assert.Equal(30, dataset.Demands.Count);
        Assert.NotEmpty(dataset.Absences);
        Assert.NotEmpty(dataset.Holidays);
    }

    [Fact]
    public void Priorities_are_one_to_thirty_each_used_once()
    {
        var dataset = ValidationDataset.Read();

        Assert.Equal(Enumerable.Range(1, 30), dataset.Demands.Select(demand => demand.PriorityRank).OrderBy(rank => rank));
    }

    [Fact]
    public void Dataset_holds_no_real_data_only_generic_names_and_no_addresses()
    {
        // Lê o próprio recurso embutido, o mesmo que a aplicação usa, sem depender de caminhos do disco.
        using var stream = typeof(ValidationDataset).Assembly.GetManifestResourceStream(ValidationDataset.ResourceName);
        using var reader = new StreamReader(stream!);
        var text = reader.ReadToEnd();
        var dataset = ValidationDataset.Read();

        Assert.DoesNotContain("@", text, StringComparison.Ordinal);
        Assert.All(dataset.Teams, team => Assert.Contains(team.Name, AllowedTeamNames));
        Assert.All(dataset.People, person => Assert.Matches(PersonName, person.Name));
        Assert.All(dataset.Demands, demand => Assert.Matches(DemandTitle, demand.Title));
    }

    [Fact]
    public void The_planning_is_accepted_by_the_engine_validator()
    {
        var snapshot = ValidationDataset.Read().ToSnapshot();

        PlanningSnapshotValidator.Validate(snapshot);
    }

    [Fact]
    public void Every_demand_is_predictable_and_none_is_completed()
    {
        var dataset = ValidationDataset.Read();
        var snapshot = dataset.ToSnapshot();

        var forecast = ForecastEngine.Calculate(snapshot);

        Assert.All(forecast.Demands, demand => Assert.Equal(DemandStatus.Predictable, demand.Status));
        Assert.All(snapshot.Demands, demand => Assert.False(demand.IsCompleted));
    }

    [Fact]
    public void Every_part_has_an_estimate_and_every_demand_has_a_person_in_each_team()
    {
        var dataset = ValidationDataset.Read();

        foreach (var demand in dataset.Demands)
        {
            Assert.All(demand.Parts, part => Assert.True(part.EstimatedEffortHours > 0m));
            foreach (var part in demand.Parts)
            {
                var people = dataset.People.Where(person => demand.AssignedPersonIds.Contains(person.Id)).Select(person => person.TeamId);
                Assert.Contains(part.TeamId, people);
            }
        }
    }

    [Fact]
    public void Each_prerequisite_has_a_higher_priority_than_its_dependent_so_there_is_no_cycle()
    {
        var dataset = ValidationDataset.Read();
        var rankById = dataset.Demands.ToDictionary(demand => demand.Id, demand => demand.PriorityRank);

        foreach (var demand in dataset.Demands)
        {
            foreach (var prerequisite in demand.PrerequisiteIds)
            {
                Assert.True(rankById[prerequisite] < demand.PriorityRank);
            }
        }
    }

    [Fact]
    public void Reading_twice_gives_the_same_planning()
    {
        var first = ValidationDataset.Read().ToSnapshot();
        var second = ValidationDataset.Read().ToSnapshot();

        // Listas em records comparam por referência: compara-se o conteúdo do cálculo.
        Assert.Equal(ForecastEngine.Calculate(first).Demands, ForecastEngine.Calculate(second).Demands);
    }
}
