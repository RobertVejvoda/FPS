import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

const sharedClientsDir = fileURLToPath(new URL('../../clients', import.meta.url));
const webAppDir = fileURLToPath(new URL('.', import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    dedupe: ['react', 'react-dom'],
    alias: {
      '@robertvejvoda/fairspot-api-client': fileURLToPath(new URL('../../clients/typescript/src', import.meta.url)),
      '@robertvejvoda/fairspot-ui': fileURLToPath(new URL('../../clients/ui/src/index.ts', import.meta.url)),
    },
  },
  server: {
    port: 5200,
    strictPort: true,
    fs: {
      allow: [webAppDir, sharedClientsDir],
    },
  },
});
