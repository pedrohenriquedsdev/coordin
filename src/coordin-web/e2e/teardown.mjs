// Remove os containers do smoke ponta a ponta ao fim da execução do Playwright (DT-32). Existe porque, no Windows, o
// Playwright derruba a pilha à força: o script `stack.mjs` não recebe o sinal e não limpa sozinho. Roda com a pilha
// ainda no ar, antes de o `webServer` ser encerrado.
import { spawnSync } from 'node:child_process';

export default async function globalTeardown() {
  const list = spawnSync('docker', ['ps', '-aq', '--filter', 'name=coordin-e2e-'], {
    encoding: 'utf8',
  });
  const ids = list.stdout
    .split('\n')
    .map((id) => id.trim())
    .filter(Boolean);
  if (ids.length > 0) {
    spawnSync('docker', ['rm', '-f', ...ids], { stdio: 'ignore' });
  }
}
