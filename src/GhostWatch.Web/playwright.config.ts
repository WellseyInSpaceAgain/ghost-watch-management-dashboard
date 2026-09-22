import { defineConfig } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { resolve } from 'node:path';

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  use: { baseURL: 'http://127.0.0.1:4201', trace: 'retain-on-failure' },
  webServer: [
    {
      command: 'dotnet run --project ../GhostWatch.Api --no-launch-profile --urls http://127.0.0.1:5081',
      url: 'http://127.0.0.1:5081/api/health',
      env: { Storage__Directory: resolve('../GhostWatch.Api/App_Data/e2e', randomUUID()), ASPNETCORE_ENVIRONMENT: 'Development' },
      reuseExistingServer: false,
      timeout: 60000,
    },
    {
      command: 'npx ng serve --host 127.0.0.1 --port 4201 --proxy-config proxy.e2e.json',
      url: 'http://127.0.0.1:4201',
      reuseExistingServer: false,
      timeout: 60000,
    },
  ],
});
