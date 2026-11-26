# Browser specs

Add your `*.spec.ts` files here. Use `await clickMenu(page, text)` for an exact text match after importing `clickMenu` from `../Helper/playwrightMethodsHelper`.

TypeScript Playwright uses string roles, for example `page.getByRole('button', { name: 'Save' })`. Put repeated interactions in `Helper` and keep the assertions that describe each scenario in its spec.
