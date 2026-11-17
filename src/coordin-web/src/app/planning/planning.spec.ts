import { DemandResponse } from '../api/models/demand-response';
import { ScheduleResponse } from '../api/models/schedule-response';
import { mapDemand, mapSchedule } from './planning';

describe('mapSchedule', () => {
  it('turns the numeric strings of the response into numbers and keeps nulls', () => {
    const response: ScheduleResponse = {
      executionOrderMode: 'FollowPriority' as ScheduleResponse['executionOrderMode'],
      items: [
        {
          demandId: 'd1',
          title: 'Checkout',
          priorityRank: '2',
          executionRank: 2,
          isOutOfPriorityOrder: false,
          teams: [
            { teamId: 't1', teamName: 'Web', estimatedEffortHours: '80' },
            { teamId: 't2', teamName: 'Dados', estimatedEffortHours: null },
          ],
          totalEstimatedEffortHours: '80',
          startDate: '2026-12-01',
          isPredictable: false,
          unpredictableReason: 'MissingEstimate',
          forecastStartDate: null,
          endDateWithoutMargin: null,
          forecastEndDate: null,
          informedEndDate: null,
          exceedsInformedEndDate: false,
          completedOn: null,
          baselineEndDate: null,
          baselineDeviationDays: null,
          forecastDeviationDays: '-2',
          prerequisites: [],
        },
      ],
    };

    const schedule = mapSchedule(response);

    expect(schedule.items[0].priorityRank).toBe(2);
    expect(schedule.items[0].teams[0].estimatedEffortHours).toBe(80);
    expect(schedule.items[0].teams[1].estimatedEffortHours).toBeNull();
    expect(schedule.items[0].totalEstimatedEffortHours).toBe(80);
    expect(schedule.items[0].forecastDeviationDays).toBe(-2);
    expect(schedule.items[0].baselineDeviationDays).toBeNull();
    expect(schedule.items[0].startDate).toBe('2026-12-01');
  });

  it('keeps the order of the items', () => {
    const response = {
      executionOrderMode: 'FollowPriority',
      items: [
        {
          demandId: 'a',
          title: 'A',
          priorityRank: 1,
          executionRank: 1,
          teams: [],
          totalEstimatedEffortHours: 0,
          prerequisites: [],
        },
        {
          demandId: 'b',
          title: 'B',
          priorityRank: 2,
          executionRank: 2,
          teams: [],
          totalEstimatedEffortHours: 0,
          prerequisites: [],
        },
      ],
    } as unknown as ScheduleResponse;

    expect(mapSchedule(response).items.map((item) => item.demandId)).toEqual(['a', 'b']);
  });
});

describe('mapDemand', () => {
  it('maps a demand with its teams and people, converting the ranks', () => {
    const response: DemandResponse = {
      id: 'd1',
      title: 'Novo checkout',
      priorityRank: '1',
      executionRank: 1,
      startDate: '2026-12-01',
      informedEndDate: null,
      completedOn: null,
      teams: [{ teamId: 't1', teamName: 'Web', estimatedEffortHours: 40 }],
      assignedPersonIds: ['p1'],
      prerequisiteIds: [],
    };

    const demand = mapDemand(response);

    expect(demand.priorityRank).toBe(1);
    expect(demand.teams).toEqual([{ teamId: 't1', teamName: 'Web', estimatedEffortHours: 40 }]);
    expect(demand.assignedPersonIds).toEqual(['p1']);
  });
});
