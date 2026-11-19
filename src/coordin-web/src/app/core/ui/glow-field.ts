import { Component, ViewEncapsulation } from '@angular/core';

/**
 * Moldura de campo com borda brilhante: ao passar o mouse a luz azul/ciano gira em volta, e no foco
 * ela dá uma volta lenta e acende um halo. Só envolve o campo (projeção de conteúdo); não toca no
 * `<input>`, então formulários, validação e rótulos seguem iguais. Os estilos vivem em `styles.css`.
 */
@Component({
  selector: 'app-glow-field',
  encapsulation: ViewEncapsulation.None,
  host: { class: 'glow-field' },
  template: `
    <span class="gf-glow" aria-hidden="true"></span>
    <span class="gf-border" aria-hidden="true"></span>
    <div class="gf-body"><ng-content /></div>
  `,
})
export class GlowField {}
