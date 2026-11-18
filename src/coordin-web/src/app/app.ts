import { Component, effect, inject, untracked } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from './core/auth/session.service';
import { ValidationAvailability } from './validation/validation-availability';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);

  protected readonly session = inject(SessionService);

  /** Se a área de validação existe neste ambiente (RF-24). Consultada uma vez por sessão. */
  protected readonly availability = inject(ValidationAvailability);

  constructor() {
    effect(() => {
      if (this.session.isAuthenticated()) {
        untracked(() => this.availability.check());
      }
    });
  }

  /** Encerra a sessão (no servidor e no navegador) e volta ao login. */
  protected signOut(): void {
    this.session.logout().subscribe(() => {
      this.availability.reset();
      void this.router.navigate(['/login']);
    });
  }
}
