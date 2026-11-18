// Sobe a pilha descartável do smoke ponta a ponta (DT-32): PostgreSQL em container, build do front e a API servindo
// esse build. Roda como `webServer` do Playwright e fica no ar até receber SIGINT ou SIGTERM, quando derruba tudo.
// Não lê o .env, o banco de desenvolvimento nem arquivo nenhum: a configuração chega pelo ambiente.
import { spawn, spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { createServer } from 'node:net';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = resolve(here, '..');
const apiRoot = resolve(webRoot, '..', 'Coordin.Api');
const distRoot = resolve(webRoot, 'dist', 'coordin-web', 'browser');

const apiPort = process.env.E2E_API_PORT;
const managerEmail = process.env.E2E_MANAGER_EMAIL;
const managerPassword = process.env.E2E_MANAGER_PASSWORD;
const jwtKey = process.env.E2E_JWT_KEY;
const containerName = `coordin-e2e-${apiPort}`;

function fail(message) {
  console.error(`\n[e2e] ${message}\n`);
  cleanup();
  process.exit(1);
}

/** Confere cedo os pré-requisitos, para a falha dizer o que falta em vez de um erro obscuro do navegador. */
function checkPrerequisites() {
  if (spawnSync('docker', ['info'], { stdio: 'ignore' }).status !== 0) {
    fail('O Docker não está respondendo. Abra o Docker Desktop (ou inicie o serviço) e rode o smoke de novo.');
  }

  if (spawnSync('dotnet', ['--version'], { stdio: 'ignore' }).status !== 0) {
    fail('O .NET SDK não foi encontrado no PATH. Instale o SDK indicado no global.json e rode de novo.');
  }

  const chromePaths = [
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe',
    '/usr/bin/google-chrome',
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
  ];
  if (!chromePaths.some((path) => existsSync(path)) && !process.env.E2E_CHROME_OK) {
    fail('O Google Chrome não foi encontrado nos caminhos padrão. Instale o Chrome, que o smoke usa no lugar de baixar um navegador.');
  }

  if (!apiPort || !managerEmail || !managerPassword || !jwtKey) {
    fail('A configuração do smoke está incompleta. Rode com `npx playwright test`, que prepara as variáveis.');
  }
}

/** Uma porta TCP livre no computador, para o PostgreSQL. */
function freePort() {
  return new Promise((resolvePort, reject) => {
    const server = createServer();
    server.unref();
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address();
      server.close(() => resolvePort(port));
    });
  });
}

async function waitFor(check, label, timeoutMs) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      if (await check()) return;
    } catch {}
    await new Promise((r) => setTimeout(r, 1000));
  }
  fail(`Tempo esgotado esperando ${label}.`);
}

/** Restos de execuções anteriores (containers com este prefixo) saem antes de subir a pilha nova. */
function removeLeftovers() {
  const list = spawnSync('docker', ['ps', '-aq', '--filter', 'name=coordin-e2e-'], { encoding: 'utf8' });
  const ids = list.stdout.split('\n').map((id) => id.trim()).filter(Boolean);
  if (ids.length > 0) {
    spawnSync('docker', ['rm', '-f', ...ids], { stdio: 'ignore' });
  }
}

let api = null;
let dbPort = null;

function cleanup() {
  if (api && api.pid) {
    // No Windows, `taskkill /T` derruba a árvore do `dotnet run`; no Linux, o sinal ao grupo basta.
    if (process.platform === 'win32') {
      spawnSync('taskkill', ['/F', '/T', '/PID', String(api.pid)], { stdio: 'ignore' });
    } else {
      try { process.kill(-api.pid, 'SIGTERM'); } catch {}
    }
    api = null;
  }

  spawnSync('docker', ['rm', '-f', containerName], { stdio: 'ignore' });
}

async function main() {
  checkPrerequisites();
  removeLeftovers();

  dbPort = await freePort();
  const dbPassword = (await import('node:crypto')).randomBytes(12).toString('hex');

  // 1. PostgreSQL descartável, com porta livre e senha sorteada.
  const run = spawnSync('docker', [
    'run', '-d', '--name', containerName,
    '-e', 'POSTGRES_USER=coordin', '-e', `POSTGRES_PASSWORD=${dbPassword}`, '-e', 'POSTGRES_DB=coordin',
    '-p', `127.0.0.1:${dbPort}:5432`, 'postgres:18',
  ], { encoding: 'utf8' });
  if (run.status !== 0) {
    fail(`Não foi possível subir o PostgreSQL descartável: ${run.stderr.trim()}`);
  }
  await waitFor(
    () => spawnSync('docker', ['exec', containerName, 'pg_isready', '-U', 'coordin', '-d', 'coordin'], { stdio: 'ignore' }).status === 0,
    'o PostgreSQL',
    120_000,
  );

  // 2. Build do front, para a API servir uma versão atual.
  const build = spawnSync(process.platform === 'win32' ? 'npm.cmd' : 'npm', ['run', 'build'], {
    cwd: webRoot,
    stdio: 'inherit',
    env: { ...process.env, NODE_OPTIONS: '--max-old-space-size=2048' },
    shell: process.platform === 'win32',
  });
  if (build.status !== 0 || !existsSync(distRoot)) {
    fail('O build do front falhou; o smoke não pode rodar sem ele.');
  }

  // 3. API a partir da pasta do projeto (o appsettings.json é lido da pasta atual).
  api = spawn('dotnet', ['run', '--no-launch-profile'], {
    cwd: apiRoot,
    detached: process.platform !== 'win32',
    stdio: 'ignore',
    env: {
      ...process.env,
      PORT: apiPort,
      ASPNETCORE_ENVIRONMENT: 'Development',
      ASPNETCORE_WEBROOT: distRoot,
      ConnectionStrings__Default: `Host=127.0.0.1;Port=${dbPort};Database=coordin;Username=coordin;Password=${dbPassword}`,
      Jwt__SigningKey: jwtKey,
      Manager__Email: managerEmail,
      Manager__Password: managerPassword,
    },
  });
  api.on('exit', (code) => {
    if (code !== null && code !== 0) {
      console.error(`[e2e] A API terminou com código ${code}.`);
    }
  });

  // 4. Espera a API responder; o `webServer` do Playwright também confere /api/health.
  await waitFor(async () => (await fetch(`http://127.0.0.1:${apiPort}/api/health`)).status === 200, 'a API responder', 10 * 60_000);
  console.log(`[e2e] Pilha no ar: API em http://127.0.0.1:${apiPort}, PostgreSQL descartável na porta ${dbPort}.`);
}

process.on('SIGINT', () => { cleanup(); process.exit(130); });
process.on('SIGTERM', () => { cleanup(); process.exit(143); });
process.on('exit', cleanup);

main().catch((error) => fail(error.message));

// Mantém o processo vivo enquanto a pilha roda; o Playwright encerra com SIGTERM ao fim dos testes.
setInterval(() => {}, 1 << 30);
