import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
    testDir: './Specs',
    testMatch: '**/*.spec.ts',
    use: {
        ...devices['Desktop Chrome'],
        baseURL: process.env.MOMENTUMGROVES_BASE_URL || 'http://127.0.0.1:5070',
    },
});
