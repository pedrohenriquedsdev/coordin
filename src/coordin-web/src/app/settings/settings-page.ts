import { Component, computed, inject, signal } from '@angular/core';
import { messageFor } from '../core/problems/messages';
import { readProblem } from '../core/problems/problem';
import { Icon } from '../core/ui/icon';
import { Loader } from '../core/ui/loader';
import { PageHeader } from '../core/ui/page-header';
import { ThemeService } from '../core/ui/theme.service';
import { SettingsStore } from './settings.store';

const MARGIN_INVALID = 'A margem deve estar entre 0% e 1000%, com no máximo 2 casas decimais.';

/**
 * Lê a margem digitada: de 0 a 1000, com até duas casas (vírgula ou ponto). Qualquer outra coisa é inválida,
 * inclusive vazio, sinal negativo ou três casas (RNF-07; regra 1: nunca reduz o prazo).
 */
export function parseMargin(text: string): number | null {
  const normalized = text.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return null;
  }

  const value = Number(normalized);
  return value <= 1000 ? value : null;
}

/**
 * Quantos dias passa a ter uma duração de `baseDays` dias com a margem de `percent`%, arredondando para cima
 * (a margem só amplia o prazo e nunca o reduz). Em inteiros, para não depender de erro de ponto flutuante.
 */
export function daysWithMargin(baseDays: number, percent: number): number {
  const hundredths = Math.round(percent * 100);
  return Math.floor((baseDays * (10000 + hundredths) + 9999) / 10000);
}

/** Tela "Configurações" (RF-09): a margem de segurança das previsões. */
@Component({
  selector: 'app-settings-page',
  imports: [PageHeader, Icon, Loader],
  providers: [SettingsStore],
  templateUrl: './settings-page.html',
})
export class SettingsPage {
  protected readonly store = inject(SettingsStore);
  protected readonly theme = inject(ThemeService);

  /** Atalhos de valor para a margem. */
  protected readonly presets = [0, 10, 20, 30] as const;

  /** O exemplo ao vivo: uma demanda de 20 dias, com a margem digitada (só quando ela é válida). */
  protected readonly example = computed(() => {
    const percent = parseMargin(this.text());
    return percent === null ? null : { base: 20, withMargin: daysWithMargin(20, percent) };
  });

  /** O valor do controle deslizante: a margem digitada, de 0 a 100 (acima disso, o campo de texto manda). */
  protected readonly sliderValue = computed(() => {
    const percent = parseMargin(this.text());
    return percent === null ? 0 : Math.min(percent, 100);
  });

  protected readonly text = signal('');
  protected readonly message = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly saving = signal(false);

  constructor() {
    this.reload();
  }

  protected reload(): void {
    this.store.load().subscribe({
      next: () => this.text.set(String(this.store.safetyMarginPercent() ?? '')),
      error: () => this.store.status.set('failed'),
    });
  }

  protected setPreset(percent: number): void {
    this.text.set(String(percent));
    this.message.set(null);
    this.error.set(null);
  }

  protected onSlider(event: Event): void {
    this.setText(event);
  }

  protected setText(event: Event): void {
    this.text.set((event.target as HTMLInputElement).value);
    this.message.set(null);
    this.error.set(null);
  }

  protected save(): void {
    this.message.set(null);
    const percent = parseMargin(this.text());
    if (percent === null) {
      this.error.set(MARGIN_INVALID);
      return;
    }

    this.error.set(null);
    this.saving.set(true);
    this.store.save(percent).subscribe({
      complete: () => {
        this.saving.set(false);
        this.message.set('Margem salva.');
        this.text.set(String(this.store.safetyMarginPercent()));
      },
      error: (failure: unknown) => {
        this.saving.set(false);
        this.error.set(messageFor(readProblem(failure), 'Não foi possível salvar a margem.'));
      },
    });
  }
}
