import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes)],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('shows the product name in the header', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const header = (fixture.nativeElement as HTMLElement).querySelector('header h1');
    expect(header?.textContent).toContain('Coordin');
  });

  it('has a main content area for the routed page', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/');
    await fixture.whenStable();

    const main = (fixture.nativeElement as HTMLElement).querySelector('main');
    expect(main).not.toBeNull();
    expect(main?.textContent).toContain('Planejamento de entregas');
  });
});
