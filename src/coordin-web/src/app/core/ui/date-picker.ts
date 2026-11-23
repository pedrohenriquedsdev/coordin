import {
  ApplicationRef,
  ComponentRef,
  createComponent,
  Directive,
  ElementRef,
  EnvironmentInjector,
  inject,
  OnDestroy,
} from '@angular/core';
import { Clock } from '../dates/calendar-date';
import { DatePopup } from './date-popup';

/** A faixa à direita do campo (em px) em que o clique abre o calendário: onde fica o ícone. */
const ICON_ZONE = 44;
/** Distância entre o campo e o calendário, e entre o calendário e a borda da tela. */
const GAP = 6;

/** O calendário aberto agora: só existe um por vez. */
let openPopup: { close: () => void } | null = null;

/**
 * Troca o calendário do navegador (uma janela do Chrome, que não aceita estilo) pelo calendário do Coordin,
 * no mesmo visual das telas e nos dois temas. Vale para o `<input type="date" appDatePicker>` em que for marcada (e que importar esta diretiva).
 *
 * O campo nativo continua dono do valor: continuam valendo digitar a data, os formulários reativos e os
 * `(input)`. Ao escolher um dia, a diretiva grava `aaaa-mm-dd` no campo e dispara `input` e `change`, como se a
 * pessoa tivesse digitado. O ícone é desenhado pelo CSS (o do navegador fica escondido); a diretiva abre o
 * calendário quando o clique cai na faixa do ícone ou com Alt + seta para baixo (ou F4).
 */
@Directive({
  selector: 'input[type="date"][appDatePicker]',
  host: {
    '(click)': 'onClick($event)',
    '(keydown)': 'onKeydown($event)',
    '(mousemove)': 'onMove($event)',
    '(mouseleave)': 'setHot(false)',
  },
})
export class DatePicker implements OnDestroy {
  private readonly input = inject<ElementRef<HTMLInputElement>>(ElementRef).nativeElement;
  private readonly environment = inject(EnvironmentInjector);
  private readonly app = inject(ApplicationRef);
  private readonly clock = inject(Clock);

  private popup: ComponentRef<DatePopup> | null = null;
  private readonly onOutside = (event: Event): void => {
    const target = event.target as Node;
    if (!this.popup?.location.nativeElement.contains(target) && !this.input.contains(target)) {
      this.close();
    }
  };
  private readonly onViewport = (): void => this.reposition();

  protected onClick(event: MouseEvent): void {
    if (!this.isOnIcon(event)) {
      return;
    }

    if (this.popup) {
      this.close();
    } else {
      this.open();
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'F4' || (event.altKey && event.key === 'ArrowDown')) {
      event.preventDefault();
      this.open();
    } else if (event.key === 'Escape' && this.popup) {
      this.close();
    }
  }

  /** O ponteiro vira "mãozinha" só sobre o ícone; sobre os números, continua o cursor de texto. */
  protected onMove(event: MouseEvent): void {
    this.setHot(this.isOnIcon(event));
  }

  protected setHot(hot: boolean): void {
    this.input.classList.toggle('date-hot', hot);
  }

  private isOnIcon(event: MouseEvent): boolean {
    return event.clientX >= this.input.getBoundingClientRect().right - ICON_ZONE;
  }

  private open(): void {
    if (this.input.disabled || this.input.readOnly || this.popup) {
      return;
    }

    openPopup?.close();
    const popup = createComponent(DatePopup, { environmentInjector: this.environment });
    popup.setInput('value', this.input.value);
    popup.setInput('today', this.clock.today());
    popup.instance.picked.subscribe((date) => this.choose(date));
    popup.instance.cleared.subscribe(() => this.choose(''));
    popup.instance.closed.subscribe(() => {
      this.close();
      this.input.focus();
    });

    // Dentro de `.app-shell`, onde valem as cores do tema escuro (as variáveis do tema estão ali).
    const host = this.input.closest('.app-shell') ?? document.body;
    this.app.attachView(popup.hostView);
    host.appendChild(popup.location.nativeElement);
    popup.location.nativeElement.style.position = 'fixed';
    popup.location.nativeElement.style.zIndex = '60';
    this.popup = popup;
    openPopup = { close: () => this.close() };
    popup.changeDetectorRef.detectChanges();
    this.reposition();
    popup.instance.focusDay();

    document.addEventListener('pointerdown', this.onOutside, true);
    window.addEventListener('resize', this.onViewport);
    window.addEventListener('scroll', this.onViewport, true);
  }

  private close(): void {
    if (!this.popup) {
      return;
    }

    document.removeEventListener('pointerdown', this.onOutside, true);
    window.removeEventListener('resize', this.onViewport);
    window.removeEventListener('scroll', this.onViewport, true);
    this.app.detachView(this.popup.hostView);
    this.popup.destroy();
    this.popup = null;
    openPopup = null;
  }

  /** Grava a data no campo como se tivesse sido digitada, para formulários e `(input)` reagirem. */
  private choose(date: string): void {
    this.input.value = date;
    this.input.dispatchEvent(new Event('input', { bubbles: true }));
    this.input.dispatchEvent(new Event('change', { bubbles: true }));
    this.close();
    this.input.focus();
  }

  /** Abaixo do campo; acima, se não couber embaixo; e sempre dentro da largura da tela. */
  private reposition(): void {
    const element = this.popup?.location.nativeElement as HTMLElement | undefined;
    if (!element) {
      return;
    }

    const field = this.input.getBoundingClientRect();
    const width = element.offsetWidth;
    const height = element.offsetHeight;
    const left = Math.max(GAP, Math.min(field.left, window.innerWidth - width - GAP));
    const below = field.bottom + GAP;
    const fitsBelow = below + height <= window.innerHeight - GAP;
    const top = fitsBelow ? below : Math.max(GAP, field.top - height - GAP);
    element.style.left = `${left}px`;
    element.style.top = `${top}px`;
  }

  ngOnDestroy(): void {
    this.close();
  }
}
