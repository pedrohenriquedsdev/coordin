import { EstimateHistoryResponse } from '../api/models/estimate-history-response';
import { ForecastHistoryResponse } from '../api/models/forecast-history-response';
import {
  calendarText,
  causeLabel,
  estimateHistoryRows,
  forecastHistoryRows,
  hoursText,
  instantText,
} from './demand-history';

describe('demand history presentation', () => {
  it('names each cause in Portuguese, and keeps an unknown cause as it came', () => {
    expect(causeLabel('Estimate')).toBe('Estimativa');
    expect(causeLabel('ApprovedScenario')).toBe('Cenário aprovado');
    expect(causeLabel('SafetyMargin')).toBe('Margem de segurança');
    expect(causeLabel('Novidade')).toBe('Novidade');
  });

  it('shows hours with a decimal comma and a dash when there was no estimate', () => {
    expect(hoursText(16)).toBe('16');
    expect(hoursText('32.5')).toBe('32,5');
    expect(hoursText(null)).toBe('—');
  });

  it('shows a calendar date as dd/MM/aaaa and a missing one as a dash', () => {
    expect(calendarText('2027-03-10')).toBe('10/03/2027');
    expect(calendarText(null)).toBe('—');
  });

  it('shows an instant in the São Paulo time zone, whatever the machine zone is', () => {
    // 02:30 em UTC do dia 1º é 23:30 do dia 28 de fevereiro em Brasília (UTC-3).
    const text = instantText('2027-03-01T02:30:00Z');
    expect(text).toContain('28');
    expect(text).toContain('23:30');
  });

  it('builds the estimate rows with the team name, the reason, and a fallback for a removed team', () => {
    const items: EstimateHistoryResponse[] = [
      {
        teamId: 'a',
        teamName: 'Alfa',
        previousHours: 40,
        newHours: '60',
        reason: 'escopo maior',
        changedAt: '2027-03-01T12:00:00Z',
      },
      {
        teamId: 'b',
        teamName: null,
        previousHours: 8,
        newHours: null,
        reason: null,
        changedAt: '2027-03-02T12:00:00Z',
      },
    ];

    const rows = estimateHistoryRows(items);

    expect(rows[0]).toMatchObject({
      team: 'Alfa',
      previous: '40',
      next: '60',
      reason: 'escopo maior',
    });
    expect(rows[1]).toMatchObject({ team: 'Time removido', previous: '8', next: '—', reason: '' });
  });

  it('builds the forecast rows with the cause in Portuguese and the first forecast with no previous date', () => {
    const items: ForecastHistoryResponse[] = [
      {
        previousEndDate: null,
        newEndDate: '2027-03-10',
        cause: 'NewDemand',
        changedAt: '2027-03-01T12:00:00Z',
      },
      {
        previousEndDate: '2027-03-10',
        newEndDate: '2027-03-17',
        cause: 'Dependency',
        changedAt: '2027-03-02T12:00:00Z',
      },
    ];

    const rows = forecastHistoryRows(items);

    expect(rows[0]).toMatchObject({ previous: '—', next: '10/03/2027', cause: 'Nova demanda' });
    expect(rows[1]).toMatchObject({
      previous: '10/03/2027',
      next: '17/03/2027',
      cause: 'Dependência',
    });
  });
});

describe('buildTimeline', () => {
  it('merges both histories, newest first, with the cause and the reason in words', async () => {
    const { buildTimeline } = await import('./demand-history');
    const events = buildTimeline(
      [
        {
          teamId: 'a',
          teamName: 'Alfa',
          previousHours: 40,
          newHours: 60,
          reason: 'escopo',
          changedAt: '2027-03-01T12:00:00Z',
        },
        {
          teamId: 'b',
          teamName: null,
          previousHours: null,
          newHours: 8,
          reason: null,
          changedAt: '2027-03-05T12:00:00Z',
        },
      ] as never,
      [
        {
          previousEndDate: '2027-03-10',
          newEndDate: '2027-03-17',
          cause: 'Dependency',
          changedAt: '2027-03-03T12:00:00Z',
        },
      ] as never,
    );

    expect(events.map((e) => e.kind)).toEqual(['estimate', 'forecast', 'estimate']);
    expect(events[0].text).toBe('Time removido: — → 8 h');
    expect(events[1].text).toBe('10/03/2027 → 17/03/2027');
    expect(events[1].detail).toBe('Causa: Dependência');
    expect(events[2].detail).toBe('escopo');
  });

  it('is empty without histories', async () => {
    const { buildTimeline } = await import('./demand-history');
    expect(buildTimeline([], [])).toEqual([]);
  });
});
