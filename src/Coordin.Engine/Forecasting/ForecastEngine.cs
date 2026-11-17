using Coordin.Engine.Planning;
using Coordin.Engine.Scenarios;

namespace Coordin.Engine.Forecasting;

/// <summary>
/// Ponto de entrada do motor de previsão (DT-02): função pura, sem banco, relógio ou aleatoriedade.
/// Valida o snapshot, classifica as demandas (RF-10), simula a execução (DT-21), aplica a margem
/// (DT-22) e devolve as previsões e o consumo diário por pessoa, sempre na mesma ordem (RNF-02).
/// Uma demanda só começa depois do fim previsto, com margem, dos seus pré-requisitos (RF-18; DT-23).
/// O cálculo em si mora em <see cref="PreparedPlanning"/>, o caminho único também usado para avaliar
/// cenários de sequenciamento.
/// </summary>
public static class ForecastEngine
{
    public static ForecastResult Calculate(PlanningSnapshot snapshot) =>
        new PreparedPlanning(snapshot).Run(ExecutionOrders.Current(snapshot), collectConsumption: true);
}
