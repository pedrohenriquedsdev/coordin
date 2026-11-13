import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from './core/auth/session.service';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);

  protected readonly session = inject(SessionService);

  /** Encerra a sessão (no servidor e no navegador) e volta ao login. */
  protected signOut(): void {
    this.session.logout().subscribe(() => void this.router.navigate(['/login']));
  }
}
