import { AbsenceType } from '../api/models/absence-type';

const LABELS: Record<AbsenceType, string> = { Vacation: 'Férias', Other: 'Outra' };

/** O nome de um tipo de ausência para o gestor: "Férias" ou "Outra". */
export function absenceTypeLabel(type: AbsenceType): string {
  return LABELS[type];
}
