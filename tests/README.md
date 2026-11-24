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
