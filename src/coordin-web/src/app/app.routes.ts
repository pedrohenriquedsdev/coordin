import { Routes } from '@angular/router';
import { authGuard, guestGuard, unknownPageGuard } from './core/auth/auth.guard';
import { CalendarPage } from './calendar/calendar-page';
import { DemandFormPage } from './demands/demand-form-page';
import { DemandHistoryPage } from './demands/demand-history-page';
import { AccuracyPage } from './reports/accuracy-page';
import { DemandsPage } from './demands/demands-page';
import { TeamReportPage } from './reports/team-report-page';
import { SchedulePage } from './schedule/schedule-page';
import { ScenariosPage } from './scenarios/scenarios-page';
import { SettingsPage } from './settings/settings-page';
import { Login } from './login/login';
import { TeamsPage } from './teams/teams-page';
import { ValidationPage } from './validation/validation-page';

export const routes: Routes = [
  // Pública: a única rota que não exige sessão. Quem já tem sessão é levado à página inicial.
  { path: 'login', component: Login, canActivate: [guestGuard] },

  // Tudo o mais exige o gestor autenticado (RNF-04). As telas das próximas changes entram aqui como
  // filhas, e já nascem protegidas.
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', component: SchedulePage },
      { path: 'demands', component: DemandsPage },
      { path: 'demands/new', component: DemandFormPage },
      { path: 'demands/:id/edit', component: DemandFormPage },
      { path: 'demands/:id', component: DemandHistoryPage },
      { path: 'scenarios', component: ScenariosPage },
      { path: 'reports/teams', component: TeamReportPage },
      { path: 'reports/accuracy', component: AccuracyPage },
      { path: 'settings', component: SettingsPage },
      { path: 'validation', component: ValidationPage },
      { path: 'teams', component: TeamsPage },
      { path: 'calendar', component: CalendarPage },
    ],
  },

  // Um caminho que não existe: login (guardando o destino) para visitantes, página inicial para o gestor.
  { path: '**', canActivate: [unknownPageGuard], children: [] },
];
