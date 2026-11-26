# Tests

Run the NUnit service and controller tests from the repository root.

```powershell
dotnet test tests/MomentumGroves.UnitTests
```

Run Playwright from the same directory after installing Chromium for the test project.

```powershell
dotnet build tests/MomentumGroves.PlaywrightTests
pwsh tests/MomentumGroves.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
dotnet test tests/MomentumGroves.PlaywrightTests
```

NUnit uses temporary SQLite fixtures. Playwright starts its own loopback web host on an available port with a temporary content root, SQLite databases, and a seeded ingredient. It runs in the Testing environment with the Supabase connection cleared. Both projects are included in the solution for discovery in Rider.

## k6 smoke test

Start an isolated MomentumGroves instance with disposable SQLite data and no Supabase connection. The k6 and TypeScript projects default to `http://127.0.0.1:5070`. Set the `MOMENTUMGROVES_BASE_URL` environment variable if your isolated instance uses another URL.

With the [k6 CLI installed](https://grafana.com/docs/k6/latest/set-up/install-k6/), run this from the repository root. The script makes one request and checks that MomentumGroves serves its logo as a PNG.

```powershell
k6 run tests/MomentumGroves.K6Tests/Smoke/smoke.js
```

Keep smoke scenarios in `Smoke` and shared code in `Helper` as it becomes useful.

## Playwright TypeScript

Use the same isolated instance for browser testing. From the repository root, install the locked npm dependencies and Chromium.

```powershell
Set-Location tests/MomentumGroves.PlaywrightTypescriptTests
npm ci
npx playwright install chromium
```

Add your own `*.spec.ts` files under `Specs` and run `npm test` from the project directory. Put reusable browser actions in `Helper`.

Rider's Solution view shows the files explicitly listed in the solution. Use the File System view to see newly created files automatically, or add them as solution items if you want them in the Solution view. Playwright discovers specs from disk.
