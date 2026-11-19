import {
  afterNextRender,
  Component,
  DestroyRef,
  ElementRef,
  inject,
  input,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';

// Mola amortecida que leva a luz até o cursor (sobre-amortecida o bastante para não balançar demais).
const FREQUENCY = 3.4;
const DAMPING = 0.78;
const GLOW_RISE = 0.5;

/**
 * Botão com uma luz que persegue o cursor, com física de mola, e um brilho na borda do lado em que
 * o cursor está. Com "reduzir movimento" ligado no sistema, a luz fica parada. É um `<button>`
 * de verdade (tipo, desativado e foco funcionam como em qualquer botão).
 */
@Component({
  selector: 'app-glow-button',
  encapsulation: ViewEncapsulation.None,
  host: { class: 'block' },
  template: `
    <div #container class="glow-btn" [attr.data-block]="block()">
      <span #right class="gb-edge" aria-hidden="true">
        <span class="gb-e1"></span>
        <span class="gb-e2"></span>
        <span class="gb-e3"></span>
      </span>
      <span #left class="gb-edge gb-edge-left" aria-hidden="true">
        <span class="gb-e1"></span>
        <span class="gb-e2"></span>
        <span class="gb-e3"></span>
      </span>
      <button #button class="gb-button" [type]="type()" [disabled]="disabled()">
        <span #track class="gb-track" aria-hidden="true">
          <span class="gb-core"></span>
          <span class="gb-halo"></span>
        </span>
        <span><ng-content /></span>
      </button>
    </div>
  `,
})
export class GlowButton {
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input(false);
  /** Ocupa toda a largura do pai (formulários). */
  readonly block = input(false);

  private readonly container = viewChild.required<ElementRef<HTMLElement>>('container');
  private readonly button = viewChild.required<ElementRef<HTMLButtonElement>>('button');
  private readonly track = viewChild.required<ElementRef<HTMLElement>>('track');
  private readonly right = viewChild.required<ElementRef<HTMLElement>>('right');
  private readonly left = viewChild.required<ElementRef<HTMLElement>>('left');

  constructor() {
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {
      if (
        typeof matchMedia === 'function' &&
        matchMedia('(prefers-reduced-motion: reduce)').matches
      ) {
        return;
      }

      const container = this.container().nativeElement;
      const button = this.button().nativeElement;
      const track = this.track().nativeElement;
      const rightGlow = this.right().nativeElement;
      const leftGlow = this.left().nativeElement;

      let bound = container.getBoundingClientRect().width / 2 + 12;
      let x = bound;
      let velocity = 0;
      let target = bound;
      let inside = false;
      let last = 0;
      let frameId: number | null = null;

      const measure = () => {
        bound = container.getBoundingClientRect().width / 2 + 12;
      };

      const paint = () => {
        track.style.setProperty('--light-x', `${x.toFixed(2)}px`);
        const normalized = Math.max(-1, Math.min(1, x / bound));
        const magnitude = Math.abs(normalized);
        const intensity = Math.pow(magnitude, GLOW_RISE);
        const tuning = `saturate(${(1 + 0.4 * magnitude).toFixed(3)}) brightness(${(1 + 0.1 * magnitude).toFixed(3)})`;
        rightGlow.style.opacity = (normalized > 0 ? intensity : 0).toFixed(3);
        leftGlow.style.opacity = (normalized < 0 ? intensity : 0).toFixed(3);
        rightGlow.style.filter = tuning;
        leftGlow.style.filter = tuning;
      };

      const frame = (now: number) => {
        const delta = Math.min((now - last) / 1000, 0.032);
        last = now;
        const omega = 2 * Math.PI * FREQUENCY;
        velocity += (omega * omega * (target - x) - 2 * DAMPING * omega * velocity) * delta;
        x += velocity * delta;
        paint();

        if (inside || Math.abs(target - x) > 0.15 || Math.abs(velocity) > 0.6) {
          frameId = requestAnimationFrame(frame);
        } else {
          frameId = null;
          x = target;
          velocity = 0;
          paint();
        }
      };

      const kick = () => {
        if (frameId === null) {
          last = performance.now();
          frameId = requestAnimationFrame(frame);
        }
      };

      const onMove = (event: PointerEvent) => {
        const box = container.getBoundingClientRect();
        inside = true;
        target = Math.max(-bound, Math.min(bound, event.clientX - (box.left + box.width / 2)));
        kick();
      };
      const onLeave = () => {
        inside = false;
        target = x;
        kick();
      };
      const onFocus = () => {
        inside = false;
        target = 0;
        kick();
      };

      measure();
      paint();
      container.addEventListener('pointermove', onMove);
      container.addEventListener('pointerleave', onLeave);
      button.addEventListener('focus', onFocus);
      button.addEventListener('blur', onLeave);
      const observer = typeof ResizeObserver === 'function' ? new ResizeObserver(measure) : null;
      observer?.observe(container);

      destroyRef.onDestroy(() => {
        observer?.disconnect();
        container.removeEventListener('pointermove', onMove);
        container.removeEventListener('pointerleave', onLeave);
        button.removeEventListener('focus', onFocus);
        button.removeEventListener('blur', onLeave);
        if (frameId !== null) {
          cancelAnimationFrame(frameId);
        }
      });
    });
  }
}
