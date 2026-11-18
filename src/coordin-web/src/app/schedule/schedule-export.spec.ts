import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideApiConfiguration } from '../api/api-configuration';
import { provideLocalePtBr } from '../core/locale';
import { csvBlob, downloadBlob, fileNameFrom } from './schedule-export';
import { ScheduleStore } from './schedule.store';

describe('fileNameFrom', () => {
  it('reads the file name the API sends, quoted or not', () => {
    expect(fileNameFrom('attachment; filename=coordin-cronograma-2026-11-24.csv')).toBe(
      'coordin-cronograma-2026-11-24.csv',
    );
    expect(fileNameFrom('attachment; filename="coordin-cronograma-2026-11-24.csv"')).toBe(
      'coordin-cronograma-2026-11-24.csv',
    );
  });

  it('falls back to the default name when there is no header or no file name', () => {
    expect(fileNameFrom(null)).toBe('coordin-cronograma.csv');
    expect(fileNameFrom('attachment')).toBe('coordin-cronograma.csv');
  });
});

describe('csvBlob', () => {
  it('puts the UTF-8 BOM back in front of the text, as CSV for Excel', () => {
    const text = 'Prioridade;Demanda\r\n';

    const blob = csvBlob(text);

    // BOM (3 bytes) + o texto em UTF-8.
    expect(blob.size).toBe(3 + new TextEncoder().encode(text).length);
    expect(blob.type).toBe('text/csv;charset=utf-8');
  });
});

describe('downloadBlob', () => {
  it('creates a temporary URL, clicks a download link with the file name and releases the URL later', () => {
    vi.useFakeTimers();
    const createObjectURL = vi.fn(() => 'blob:temp');
    const revokeObjectURL = vi.fn();
    URL.createObjectURL = createObjectURL;
    URL.revokeObjectURL = revokeObjectURL;
    const clicks: HTMLAnchorElement[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      clicks.push(this);
    });

    downloadBlob(new Blob(['x']), 'coordin-cronograma-2026-11-24.csv');

    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(clicks).toHaveLength(1);
    expect(clicks[0].download).toBe('coordin-cronograma-2026-11-24.csv');
    expect(clicks[0].href).toBe('blob:temp');
    expect(revokeObjectURL).not.toHaveBeenCalled();
    vi.runAllTimers();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:temp');
    vi.useRealTimers();
    vi.restoreAllMocks();
  });
});

describe('ScheduleStore.exportCsv', () => {
  let store: ScheduleStore;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideApiConfiguration(''),
        provideLocalePtBr(),
        ScheduleStore,
      ],
    });
    store = TestBed.inject(ScheduleStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    vi.restoreAllMocks();
  });

  it('asks the export route, downloads the file with the name from the header, and ends not busy', () => {
    URL.createObjectURL = vi.fn(() => 'blob:temp');
    URL.revokeObjectURL = vi.fn();
    const clicks: HTMLAnchorElement[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (
      this: HTMLAnchorElement,
    ) {
      clicks.push(this);
    });

    store.exportCsv();
    expect(store.exporting()).toBe(true);
    http
      .expectOne({ method: 'GET', url: '/api/schedule/export' })
      .flush('Prioridade;Demanda\r\n1;Checkout\r\n', {
        headers: {
          'Content-Disposition': 'attachment; filename=coordin-cronograma-2026-11-24.csv',
        },
      });

    expect(clicks[0].download).toBe('coordin-cronograma-2026-11-24.csv');
    expect(store.exporting()).toBe(false);
    expect(store.exportError()).toBeNull();
  });

  it('keeps the message and ends not busy when the export fails', () => {
    store.exportCsv();
    http.expectOne('/api/schedule/export').flush('erro', { status: 500, statusText: 'Erro' });

    expect(store.exporting()).toBe(false);
    expect(store.exportError()).toBe(
      'Não foi possível concluir agora. Tente novamente em instantes.',
    );
  });
});
