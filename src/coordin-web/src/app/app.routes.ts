import { Routes } from '@angular/router';
import { authGuard, guestGuard, unknownPageGuard } from './core/auth/auth.guard';
import { Login } from './login/login';

/*
 * Cada tela é carregada sob demanda (`loadComponent`): o navegador baixa só o código da tela aberta, e a
 * primeira carga fica leve. O Chart.js, por exemplo, só vem junto com as telas que desenham gráficos.
 * A tela de login fica de fora de propósito: é a primeira que quase todo acesso vê.
 */
export const routes: Routes = [
  // Pública: a única rota que não exige sessão. Quem já tem sessão é levado à página inicial.
  { path: 'login', component: Login, canActivate: [guestGuard] },

  // Tudo o mais exige o gestor autenticado (RNF-04). As telas das próximas changes entram aqui como
  // filhas, e já nascem protegidas.
  {
    path: '',
    canActivate: [authGuard],
    children: [
      {
        path: 'inicio',
        loadComponent: () => import('./home/home-page').then((m) => m.HomePage),
      },
      {
        path: '',
        loadComponent: () => import('./schedule/schedule-page').then((m) => m.SchedulePage),
      },
      {
        path: 'demands',
        loadComponent: () => import('./demands/demands-page').then((m) => m.DemandsPage),
      },
      {
        path: 'demands/new',
        loadComponent: () => import('./demands/demand-form-page').then((m) => m.DemandFormPage),
      },
      {
        path: 'demands/:id/edit',
        loadComponent: () => import('./demands/demand-form-page').then((m) => m.DemandFormPage),
      },
      {
        path: 'demands/:id',
        loadComponent: () =>
          import('./demands/demand-history-page').then((m) => m.DemandHistoryPage),
      },
      {
        path: 'scenarios',
        loadComponent: () => import('./scenarios/scenarios-page').then((m) => m.ScenariosPage),
      },
      {
        path: 'reports/teams',
        loadComponent: () => import('./reports/team-report-page').then((m) => m.TeamReportPage),
      },
      {
        path: 'reports/accuracy',
        loadComponent: () => import('./reports/accuracy-page').then((m) => m.AccuracyPage),
      },
      {
        path: 'settings',
        loadComponent: () => import('./settings/settings-page').then((m) => m.SettingsPage),
      },
      {
        path: 'validation',
        loadComponent: () => import('./validation/validation-page').then((m) => m.ValidationPage),
      },
      {
        path: 'teams',
        loadComponent: () => import('./teams/teams-page').then((m) => m.TeamsPage),
      },
      {
        path: 'calendar',
        loadComponent: () => import('./calendar/calendar-page').then((m) => m.CalendarPage),
      },
    ],
  },

  // Um caminho que não existe: login (guardando o destino) para visitantes, página inicial para o gestor.
  { path: '**', canActivate: [unknownPageGuard], children: [] },
];
