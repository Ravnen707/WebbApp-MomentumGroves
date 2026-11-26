import type { Page } from '@playwright/test';

export async function clickMenu(page: Page, text: string): Promise<void> {
    await page.getByText(text, { exact: true }).click();
}
