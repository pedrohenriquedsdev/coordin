import { CHART_COLORS, buildTeamChart } from './team-report-chart';
import { ReportTeam } from './team-report';

const team: ReportTeam = {
  teamId: 't1',
  teamName: 'Web',
  unpredictableDemandCount: 0,
  weeks: [
    {
      weekStart: '2027-01-04',
      weekEnd: '2027-01-10',
      capacityHours: 40,
      allocatedHours: 40,
      occupancy: 1,
      idleHours: 0,
      isOverloaded: true,
      overloadedDemands: [],
    },
    {
      weekStart: '2027-01-11',
      weekEnd: '2027-01-17',
      capacityHours: 40,
      allocatedHours: 10,
      occupancy: 0.25,
      idleHours: 30,
      isOverloaded: false,
      overloadedDemands: [],
    },
  ],
};

describe('buildTeamChart', () => {
  it('draws one stacked bar per week, labelled by the Monday and the Sunday', () => {
    const chart = buildTeamChart(team);

    expect(chart.type).toBe('bar');
    expect(chart.data.labels).toEqual(['04/01 a 10/01', '11/01 a 17/01']);
    expect(chart.options?.scales?.['x']).toMatchObject({ stacked: true });
  });

  it('puts the allocated and idle hours in stacked series that add up to the capacity', () => {
    const [allocated, idle] = buildTeamChart(team).data.datasets;

    expect(allocated.label).toBe('Horas alocadas');
    expect(allocated.data).toEqual([40, 10]);
    expect(idle.label).toBe('Horas ociosas');
    expect(idle.data).toEqual([0, 30]);
    expect(allocated.stack).toBe(idle.stack);
  });

  it('highlights the overloaded week in the highlight colour and keeps the others', () => {
    const [allocated] = buildTeamChart(team).data.datasets;

    expect(allocated.backgroundColor).toEqual([CHART_COLORS.overloaded, CHART_COLORS.allocated]);
  });
});
