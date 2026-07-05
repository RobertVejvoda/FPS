import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@robertvejvoda/fairspot-api-client': '../../clients/typescript/src',
      '@robertvejvoda/fairspot-ui': '../../clients/ui/src/index.ts',
    },
  },
  server: {
    port: 5200,
    strictPort: true,
  },
});
