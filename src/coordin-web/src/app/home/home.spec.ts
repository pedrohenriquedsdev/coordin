import { TestBed } from '@angular/core/testing';
import { Home } from './home';

describe('Home', () => {
  it('renders the placeholder text in pt-BR', async () => {
    await TestBed.configureTestingModule({ imports: [Home] }).compileComponents();
    const fixture = TestBed.createComponent(Home);
    await fixture.whenStable();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Planejamento de entregas');
    expect(text).toContain('Apoio à decisão');
  });
});
