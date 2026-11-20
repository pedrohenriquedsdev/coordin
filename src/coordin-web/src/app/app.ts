import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { SessionService } from './core/auth/session.service';
import { ThemeService } from './core/ui/theme.service';
import { Icon, IconName } from './core/ui/icon';
import { ValidationAvailability } from './validation/validation-availability';

interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: IconName;
  readonly exact: boolean;
}

interface NavGroup {
  readonly title: string;
  readonly items: readonly NavItem[];
}

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
  host: {
    '(document:keydown.escape)': 'closeAll()',
    '(document:click)': 'onDocumentClick($event)',
  },
})
export class App {
  private readonly router = inject(Router);
  // Só injetar já aplica o tema salvo (ou o do sistema) em todas as telas.
  protected readonly theme = inject(ThemeService);

  protected readonly session = inject(SessionService);

  /** Se a área de validação existe neste ambiente (RF-24). Consultada uma vez por sessão. */
  protected readonly availability = inject(ValidationAvailability);

  /** Menu recolhível no celular; no computador o menu lateral está sempre aberto. */
  protected readonly menuOpen = signal(false);

  /** Grupo do menu com a lista aberta no computador (só um por vez). */
  protected readonly openGroup = signal<string | null>(null);

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map((event) => event.urlAfterRedirects.split(/[?#]/)[0]),
    ),
    { initialValue: this.router.url.split(/[?#]/)[0] },
  );

  /** Itens do menu, agrupados por intenção: planejar, analisar e cadastrar. */
  protected readonly groups = computed<readonly NavGroup[]>(() => {
    const analyze: NavItem[] = [
      { path: '/reports/teams', label: 'Relatório por time', icon: 'report', exact: false },
      { path: '/reports/accuracy', label: 'Precisão', icon: 'accuracy', exact: false },
    ];

    if (this.availability.available()) {
      analyze.push({ path: '/validation', label: 'Validação', icon: 'validation', exact: false });
    }

    return [
      {
        title: 'Planejamento',
        items: [
          { path: '/', label: 'Cronograma', icon: 'schedule', exact: true },
          { path: '/demands', label: 'Demandas', icon: 'demands', exact: false },
          { path: '/scenarios', label: 'Cenários', icon: 'scenarios', exact: false },
        ],
      },
      { title: 'Análise', items: analyze },
      {
        title: 'Cadastros',
        items: [
          { path: '/teams', label: 'Times e pessoas', icon: 'teams', exact: false },
          { path: '/calendar', label: 'Calendário', icon: 'calendar', exact: false },
          { path: '/settings', label: 'Configurações', icon: 'settings', exact: false },
        ],
      },
    ];
  });

  /** O grupo está "ativo" quando a tela aberta é um dos seus itens. */
  protected isGroupActive(group: NavGroup): boolean {
    const url = this.url();
    return group.items.some((item) =>
      item.exact ? url === item.path : url === item.path || url.startsWith(`${item.path}/`),
    );
  }

  protected toggleGroup(title: string): void {
    this.openGroup.update((open) => (open === title ? null : title));
  }

  protected closeAll(): void {
    this.openGroup.set(null);
    this.menuOpen.set(false);
  }

  /** Clique fora do menu fecha a lista aberta. */
  protected onDocumentClick(event: Event): void {
    const target = event.target;
    if (!(target instanceof Element) || !target.closest('[data-nav-group]')) {
      this.openGroup.set(null);
    }
  }

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
    this.openGroup.set(null);
  }

  constructor() {
    effect(() => {
      if (this.session.isAuthenticated()) {
        untracked(() => this.availability.check());
      }
    });
  }

  /** Encerra a sessão (no servidor e no navegador) e volta ao login. */
  protected signOut(): void {
    this.menuOpen.set(false);
    this.session.logout().subscribe(() => {
      this.availability.reset();
      void this.router.navigate(['/login']);
    });
  }
}
