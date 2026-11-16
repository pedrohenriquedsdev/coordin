import { ReportTeam, defaultPeriod, mapTeamReport, summarizeTeam, weekLabel } from './team-report';

const team = (weeks: Partial<ReportTeam['weeks'][number]>[]): ReportTeam => ({
  teamId: 't1',
  teamName: 'Web',
  unpredictableDemandCount: 1,
  weeks: weeks.map((week, index) => ({
    weekStart: `2027-01-${String(4 + index * 7).padStart(2, '0')}`,
    weekEnd: `2027-01-${String(10 + index * 7).padStart(2, '0')}`,
    capacityHours: 0,
    allocatedHours: 0,
    occupancy: null,
    idleHours: 0,
    isOverloaded: false,
    overloadedDemands: [],
    ...week,
  })),
});

describe('mapTeamReport', () => {
  it('turns the numeric strings into numbers and keeps a null occupancy', () => {
    const report = mapTeamReport({
      periodStart: '2027-01-04',
      periodEnd: '2027-01-10',
      teams: [
        {
          teamId: 't1',
          teamName: 'Web',
          unpredictableDemandCount: '2',
          weeks: [
            {
              weekStart: '2027-01-04',
              weekEnd: '2027-01-10',
              capacityHours: '80',
              allocatedHours: '60',
              occupancy: '0.75',
              idleHours: '20',
              isOverloaded: false,
              overloadedDemands: [],
            },
            {
              weekStart: '2027-01-11',
              weekEnd: '2027-01-17',
              capacityHours: '0',
              allocatedHours: '0',
              occupancy: null,
              idleHours: '0',
              isOverloaded: false,
              overloadedDemands: [],
            },
          ],
        },
      ],
    });

    expect(report.teams[0].unpredictableDemandCount).toBe(2);
    expect(report.teams[0].weeks[0].capacityHours).toBe(80);
    expect(report.teams[0].weeks[0].occupancy).toBe(0.75);
    expect(report.teams[0].weeks[1].occupancy).toBeNull();
  });
});

describe('summarizeTeam', () => {
  it('gives the occupancy of the period: 120 h of 160 h is 75%', () => {
    const summary = summarizeTeam(
      team([
        { capacityHours: 80, allocatedHours: 60, idleHours: 20 },
        { capacityHours: 80, allocatedHours: 60, idleHours: 20 },
      ]),
    );

    expect(summary.capacityHours).toBe(160);
    expect(summary.allocatedHours).toBe(120);
    expect(summary.occupancy).toBe(0.75);
    expect(summary.idleHours).toBe(40);
  });

  it('has no occupancy when the team has no capacity in the period', () => {
    expect(summarizeTeam(team([{ capacityHours: 0 }])).occupancy).toBeNull();
  });

  it('counts the overloaded weeks and the unpredictable demands', () => {
    const summary = summarizeTeam(
      team([{ isOverloaded: true }, { isOverloaded: false }, { isOverloaded: true }]),
    );

    expect(summary.overloadedWeeks).toBe(2);
    expect(summary.unpredictableDemandCount).toBe(1);
  });
});

describe('defaultPeriod', () => {
  it('starts on the Monday of the current week and ends on the Sunday of the eighth', () => {
    // 2026-11-04 é uma quarta-feira.
    expect(defaultPeriod('2026-11-04')).toEqual({ from: '2026-11-02', to: '2026-12-27' });
  });
});

describe('weekLabel', () => {
  it('shows the Monday and the Sunday in day/month', () => {
    expect(weekLabel(team([{}]).weeks[0])).toBe('04/01 a 10/01');
  });
});
