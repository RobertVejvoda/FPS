import { copyFile, mkdir, rm, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const src = join(root, 'src');
const dist = join(root, 'dist');
const modules = ['identity', 'booking', 'profile', 'notification', 'customer'];

await rm(dist, { recursive: true, force: true });
await mkdir(dist, { recursive: true });

for (const moduleName of modules) {
  await copyFile(join(src, `${moduleName}.d.ts`), join(dist, `${moduleName}.d.ts`));
}

await writeFile(
  join(dist, 'index.d.ts'),
  `${modules.map((moduleName) => `export type * from './${moduleName}';`).join('\n')}\n`,
);
