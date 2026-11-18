import { randomBytes, randomInt } from 'node:crypto';
import { defineConfig, devices } from '@playwright/test';

/**
 * Smoke ponta a ponta (DT-32; RNF-03). A pilha (PostgreSQL e API) sobe pelo `webServer`, com `e2e/stack.mjs`, e cai
 * ao fim da execução. A porta da API e as credenciais do gestor são sorteadas uma vez aqui: o processo principal e os
 * workers herdam os mesmos valores pelo ambiente, e nada disso é gravado em arquivo.
 */

// Sorteio da porta da API, na faixa 40000-59999, e da senha do gestor (12 caracteres ou mais).
const apiPort = process.env.E2E_API_PORT ?? String(randomInt(40000, 60000));
const managerPassword =
  process.env.E2E_MANAGER_PASSWORD ?? `Aa1-${randomBytes(12).toString('hex')}`;

process.env.E2E_API_PORT = apiPort;
process.env.E2E_MANAGER_PASSWORD = managerPassword;
process.env.E2E_MANAGER_EMAIL ??= 'e2e-manager@example.test';
process.env.E2E_JWT_KEY ??= randomBytes(32).toString('hex');

const baseURL = `http://127.0.0.1:${apiPort}`;

export default defineConfig({
  testDir: './e2e',
  globalTeardown: './e2e/teardown.mjs',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 60_000,
  reporter: [['list']],
  use: {
    baseURL,
    channel: 'chrome',
    headless: true,
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'desktop',
      testIgnore: /mobile\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        channel: 'chrome',
        viewport: { width: 1280, height: 720 },
      },
    },
    {
      name: 'mobile',
      testMatch: /mobile\.spec\.ts/,
      use: {
        channel: 'chrome',
        viewport: { width: 360, height: 800 },
        isMobile: true,
        hasTouch: true,
      },
    },
  ],
  webServer: {
    command: 'node e2e/stack.mjs',
    url: `${baseURL}/api/health`,
    reuseExistingServer: false,
    // A primeira execução compila a API e o front: dezenas de segundos a alguns minutos.
    timeout: 15 * 60_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
