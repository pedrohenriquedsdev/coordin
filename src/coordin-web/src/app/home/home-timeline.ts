import { Component, input } from '@angular/core';
import { GrowBar } from '../core/ui/hub-motion';
import { Timeline } from './home-data';

/**
 * Linha do tempo das próximas demandas: cada barra vai do início à entrega prevista; a parte clara no
 * fim é a margem de segurança (que só amplia o prazo). Em vermelho, a demanda que passa do prazo informado
 * (também escrito em palavras). O texto das datas está sempre visível, então a barra nunca é o único sinal.
 */
@Component({
  selector: 'app-home-timeline',
  imports: [GrowBar],
  templateUrl: './home-timeline.html',
})
export class HomeTimeline {
  readonly timeline = input.required<Timeline>();
}
