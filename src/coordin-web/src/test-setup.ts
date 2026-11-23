import { vi } from 'vitest';

/**
 * Margem de tempo da suíte inteira. O padrão do Vitest (5 s por teste) basta com a máquina livre, mas a
 * suíte roda em paralelo e, com o computador ocupado (build, Docker, navegador), testes que desenham telas
 * grandes estouram o prazo sem terem nenhum defeito. `hookTimeout` vale para o `beforeAll` que carrega as
 * telas sob demanda (`preloadPages`).
 */
vi.setConfig({ testTimeout: 15_000, hookTimeout: 60_000 });
