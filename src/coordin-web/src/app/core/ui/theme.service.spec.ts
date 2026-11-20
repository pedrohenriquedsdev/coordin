import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
    TestBed.resetTestingModule();
  });

  it('aplica o tema salvo no html', () => {
    localStorage.setItem('coordin-theme', 'dark');
    const service = TestBed.inject(ThemeService);

    expect(service.isDark()).toBe(true);
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('alterna e guarda a escolha', () => {
    localStorage.setItem('coordin-theme', 'light');
    const service = TestBed.inject(ThemeService);

    service.toggle();

    expect(service.theme()).toBe('dark');
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
    expect(localStorage.getItem('coordin-theme')).toBe('dark');

    service.toggle();
    expect(localStorage.getItem('coordin-theme')).toBe('light');
  });

  it('ignora um valor salvo inválido', () => {
    localStorage.setItem('coordin-theme', 'roxo');
    const service = TestBed.inject(ThemeService);

    expect(['light', 'dark']).toContain(service.theme());
  });
});
