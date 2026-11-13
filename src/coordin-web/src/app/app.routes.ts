import { Routes } from '@angular/router';
import { authGuard, guestGuard, unknownPageGuard } from './core/auth/auth.guard';
import { Home } from './home/home';
import { Login } from './login/login';

export const routes: Routes = [
  // Pública: a única rota que não exige sessão. Quem já tem sessão é levado à página inicial.
  { path: 'login', component: Login, canActivate: [guestGuard] },

  // Tudo o mais exige o gestor autenticado (RNF-04). As telas das próximas changes entram aqui como
  // filhas, e já nascem protegidas.
  {
    path: '',
    canActivate: [authGuard],
    children: [{ path: '', component: Home }],
  },

  // Um caminho que não existe: login (guardando o destino) para visitantes, página inicial para o gestor.
  { path: '**', canActivate: [unknownPageGuard], children: [] },
];
