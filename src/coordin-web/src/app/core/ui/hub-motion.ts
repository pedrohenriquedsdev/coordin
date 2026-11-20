import {
  afterNextRender,
  DestroyRef,
  Directive,
  effect,
  ElementRef,
  inject,
  input,
} from '@angular/core';
import { animate } from 'motion';

/** Há animação possível? (não em testes sem a Web Animations API, nem com "reduzir movimento"). */
export function canAnimate(element: HTMLElement): boolean {
  if (typeof element.animate !== 'function') {
    return false;
  }
  return !(
    typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches
  );
}

/**
 * Cartão de hub interativo: o brilho (spotlight) segue o ponteiro, e o cartão se inclina de leve em
 * direção a ele, com uma mola do Motion. Ao sair, volta ao repouso.
 */
@Directive({ selector: '[appHubTilt]' })
export class HubTilt {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    const element = this.host;
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {
      if (!canAnimate(element)) {
        return;
      }

      const onMove = (event: PointerEvent) => {
        const box = element.getBoundingClientRect();
        const px = (event.clientX - box.left) / box.width;
        const py = (event.clientY - box.top) / box.height;
        element.style.setProperty('--mx', `${(px * 100).toFixed(1)}%`);
        element.style.setProperty('--my', `${(py * 100).toFixed(1)}%`);
        animate(
          element,
          { rotateX: (0.5 - py) * 7, rotateY: (px - 0.5) * 9, y: -4 },
          { type: 'spring', stiffness: 220, damping: 22 },
        );
      };
      const onLeave = () => {
        animate(
          element,
          { rotateX: 0, rotateY: 0, y: 0 },
          { type: 'spring', stiffness: 180, damping: 20 },
        );
      };

      element.addEventListener('pointermove', onMove);
      element.addEventListener('pointerleave', onLeave);
      destroyRef.onDestroy(() => {
        element.removeEventListener('pointermove', onMove);
        element.removeEventListener('pointerleave', onLeave);
      });
    });
  }
}

/** Entrada escalonada: o elemento sobe e aparece, com atraso proporcional à posição (`appReveal="3"`). */
@Directive({ selector: '[appReveal]' })
export class Reveal {
  readonly index = input(0, { alias: 'appReveal' });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    afterNextRender(() => {
      if (!canAnimate(this.host)) {
        return;
      }
      animate(
        this.host,
        { opacity: [0, 1], y: [20, 0] },
        { duration: 0.55, delay: Math.min(this.index(), 12) * 0.06, ease: [0.16, 1, 0.3, 1] },
      );
    });
  }
}

/**
 * Número que "conta" de 0 até o valor. Escreve só no próprio elemento (que deve estar vazio no
 * template), então não briga com a interpolação do Angular. Sem animação possível, mostra o valor direto.
 */
@Directive({ selector: '[appCountUp]' })
export class CountUp {
  readonly value = input.required<number>({ alias: 'appCountUp' });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private shown = 0;
  private controls: { stop: () => void } | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.controls?.stop());

    effect(() => {
      const target = this.value();
      this.controls?.stop();
      if (!canAnimate(this.host)) {
        this.host.textContent = String(target);
        this.shown = target;
        return;
      }
      this.controls = animate(this.shown, target, {
        duration: 0.9,
        ease: [0.16, 1, 0.3, 1],
        onUpdate: (latest) => {
          this.shown = latest;
          this.host.textContent = String(Math.round(latest));
        },
      });
    });
  }
}

/**
 * Barra que cresce da esquerda até a largura final (`appGrow="atraso em segundos"`). A largura final
 * vem do estilo normal do elemento; a animação só escala de 0 a 1, então o valor lido é sempre o certo.
 */
@Directive({ selector: '[appGrow]' })
export class GrowBar {
  readonly delay = input(0, { alias: 'appGrow' });
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    afterNextRender(() => {
      if (!canAnimate(this.host)) {
        return;
      }
      this.host.style.transformOrigin = 'left center';
      animate(
        this.host,
        { transform: ['scaleX(0)', 'scaleX(1)'], opacity: [0.4, 1] },
        { duration: 0.8, delay: this.delay(), ease: [0.16, 1, 0.3, 1] },
      );
    });
  }
}
