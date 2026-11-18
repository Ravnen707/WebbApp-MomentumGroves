# MomentumGroves

MomentumGroves is a personal health, nutrition, and activity companion designed to make everyday progress easier to track and easier to understand. It combines food logging, recipes, body metrics, and activity tools in one place. The long-term roadmap is described below.

The project currently consists of an **ASP.NET Core MVC (.NET 10)** web application using **SQLite**. The **.NET MAUI Android** companion is maintained in a separate GitHub repository. It is currently a personal project, not a finished public health platform and unsure if it will ever be anything more then for myself and any other who wants to use this.

> This guide describes the project and the setup supported by the supplied documentation and source snapshot. It does not guarantee that every feature has been tested in a clean installation.

## Table of contents

- [Project vision and roadmap](#project-vision-and-roadmap)
- [Overview and features](#overview-and-features)
- [Technology and project structure](#technology-and-project-structure)
- [Fork and set up the web application](#fork-and-set-up-the-web-application)
- [SQLite databases and initial data](#sqlite-databases-and-initial-data)
- [Configuration and external services](#configuration-and-external-services)
- [Android companion and synchronization](#android-companion-and-synchronization)
- [Testing](#testing)
- [Publishing safely](#publishing-safely)
- [Troubleshooting](#troubleshooting)

---

<a id="project-vision-and-roadmap"></a>
<details>
<summary><strong>Project vision and roadmap</strong></summary>

MomentumGroves is intended to become more than a personal logbook. Its design direction is a recognizable, consistent theme across the website and mobile app, with the mobile interface gradually adopting the web application's visual language in a compact, touch-friendly layout. The longer-term goal is an experience that can also serve other users, but account management, multi-user support, and public deployment are **future possibilities**, not current features.

### Current development priorities — in order

1. **Android step counting:** collect daily steps from supported Android sensors, show daily/weekly history and goals, synchronize with the web app, and handle permissions, device restarts, and duplicate readings. Background collection depends on Android restrictions and hardware.
2. **GPS activity tracking:** start, pause, resume, and stop a walk; record elapsed time, distance, and a route trace; store and synchronize completed sessions. Background location requires explicit permissions and appropriate foreground-service behavior.
3. **Achievements:** add a shared server-side achievement engine for food goals, steps, walking distance, training, weight milestones, and streaks. Support progress, unlock dates, XP/levels, hidden achievements, and eligible historical data without awarding duplicates.
4. **Achievement-powered game:** introduce a lightweight web/mobile game using ordinary UI elements, icons, cards, and progress bars rather than an elaborate sprite-based game. Achievements and healthy routines may award in-game resources or unlock upgrades; the game must remain playable without exercise.

These are **planned milestones**, not claims that step tracking, GPS tracking, achievements, or the game already work.

### Longer-term direction

- **Meaningful achievement rewards:** unlock game items, upgrades, quests, or optional features rather than collecting badges solely for display.
- **Multiple paths to progress:** nutrition habits alone should support progression; walking, training, and other activities offer additional routes, not mandatory gates.
- **Optional health-to-game integration:** a future account setting could enable or disable game rewards tied to personal health goals and weekly activity.
- **Personalization and accounts:** individual goals, preferences, achievements, and game progress may eventually be tied to user accounts.
- **A coherent identity:** use the MomentumGroves theme throughout the web and mobile experience, without requiring a complex game engine.

Achievement design should encourage sustainable habits. Avoid rewarding extreme calorie restriction, excessive exercise, or rapid weight loss. Rest days, illness, and the existing Sick Day/Cheat Day behaviors need deliberate handling; in-game rewards should never determine whether a person is 'allowed' to eat.

</details>

---

<a id="overview-and-features"></a>
<details>
<summary><strong>Overview and features</strong></summary>

<details>
<summary><strong>Web application features</strong></summary>

The supplied project and documentation include these areas:

- **Food diary:** register meals, view calories and macronutrients, copy previous meals into the entry form, and edit existing entries. Recipe logging supports proportional portion scaling and a separate per-ingredient customization workflow; customized entries should not alter the saved recipe.
- **Custom food library:** manage custom foods with nutrition values per 100 g or 100 ml. The newer library workflow is designed for newest-first, server-side pagination (10 items at a time), with search across the full database.
- **Recipes:** combine custom foods with ingredient quantities and a configurable meal weight; select recipes for diary entry.
- **Drinks:** search products through Open Food Facts, enter quantities in ml, review or edit nutritional values, and explicitly save a drink. Product selection alone does not save it.
- **Nutrition targets and history:** daily nutrition tracking, seven-day views, and sick-day/cheat-day controls. Sick days are intended to be excluded from target evaluation; cheat days remain in the seven-day intake balance.
- **Body metrics and weight tracking:** dedicated views for body measurements and weight history.
- **Training:** dedicated training tracking view.
- **Administration:** an administration view for the web app.

The latest JavaScript refactoring discussed for this project has not been fully browser-tested; feature presence should not be interpreted as a verification that every control works.

</details>

<details>
<summary><strong>Android companion features</strong></summary>

- Create and edit custom foods offline with calories, protein, carbohydrates, fat, and optional fiber.
- Scan a barcode with the camera and look up product nutrition through Open Food Facts, then review/edit the values before saving. Camera availability, permissions, and product coverage vary.
- Use g or ml as the food's measurement unit.
- Keep pending entries locally until a transfer is acknowledged.
- Transfer entries to the desktop over a trusted private Wi-Fi network.
- **Planned:** align mobile navigation, styling, and feature layouts more closely with the web UI, adapted for a smaller touchscreen.
- Remove successfully acknowledged entries from the phone; retain unacknowledged entries for retry.

The Android app is a temporary staging area, not a long-term backup of successfully transferred foods.

</details>

</details>

---

<a id="technology-and-project-structure"></a>
<details>
<summary><strong>Technology and project structure</strong></summary>

| Area | Technology |
|---|---|
| Web runtime | **.NET 10**, ASP.NET Core MVC (`Microsoft.NET.Sdk.Web`) |
| Frontend | Razor `.cshtml`, JavaScript, HTML, CSS |
| Storage | SQLite via `Microsoft.Data.Sqlite` (package 8.0.22 in the inspected project) |
| Spreadsheet import | `ExcelDataReader` 3.7.0 |
| Automated tests | **NUnit** 3.14.0, NUnit3TestAdapter 4.5.0, Microsoft.NET.Test.Sdk 17.8.0 |
| Coverage | `coverlet.collector` 6.0.0 |
| Development environment | Any .NET 10-compatible IDE or code editor (such as Rider, Visual Studio, or VS Code) |

```text
MomentumGroves/
├── MomentumGroves.sln
├── MomentumGroves.csproj
├── Program.cs
├── .env.example
├── src/
│   ├── Controllers/       # MVC/API HTTP endpoints
│   ├── DTO/               # API data transfer types
│   ├── Models/            # Application models
│   ├── Service/           # Business logic
│   ├── Repository/        # Data access
│   ├── Helper/            # Utilities and converters
│   └── Data/              # Local SQLite files (not committed)
├── Views/                 # Razor views and partials
├── wwwroot/               # Static CSS, JS, and assets
└── tests/
    └── MomentumGroves.Tests/
```

The mobile application has its own GitHub repository and its own DTO definitions. Both apps communicate using JSON over an API; their C# namespaces do not have to match, but their JSON contracts must remain compatible.

---

</details>

---

<a id="fork-and-set-up-the-web-application"></a>
<details>
<summary><strong>Fork and set up the web application</strong></summary>

These instructions are for a **new fork/clone**, not for overwriting an existing installation. The repository intentionally does **not** distribute personal databases or real API credentials.

### Step 1 — Install prerequisites

1. Install the **.NET 10 SDK** (not just the runtime).
2. Install your preferred .NET 10-compatible IDE or code editor.
3. Install **Git** if you intend to fork and clone.
4. Fork the web repository on GitHub, then clone your fork to a local folder.
5. Open `MomentumGroves.sln` in your IDE, or open the project root in your code editor.

The installed SDK must support `net10.0`. This web repository does **not** require the MAUI/Android workload.

### Step 2 — Create local configuration

1. In the repository root (beside `Program.cs`), copy `.env.example` to a new file named `.env`.
2. Keep `.env` local: it is ignored by Git.
3. Set `MobileSync__Key` if you use mobile food synchronization.
4. Do not put real keys into `.env.example`, README files, source code, or Git commits.

`Program.cs` calls `DotEnvLoader.Load(...)` for the root `.env` file, so **this application does load `.env`** when started from the repository root. If you run it with a different working directory, check that the file is being found.

### Step 3 — Database setup (automatic)

The repository does not include personal database files. MomentumGroves checks for the required database files when the application starts.

- If `momentum.db` or `food_and_recipes.db` is missing, a **Database initialization** modal appears. Click **Yes, initialize** to create the missing local database files and initialize their required tables.
- Once local initialization is complete, if `frida.db` is missing, the modal offers **Go to Admin to make FridaDb**. Click it to open the existing Frida installation page.
- In Admin, click **Check for updates**, then **Yes, install**. This downloads the DTU Frida dataset and creates and populates `frida.db` automatically.
- If only Frida is missing, you are directed to Admin without being asked to initialize the other databases again.
- When all three databases already exist, the startup check runs without showing a modal or additional status messages.

The databases are stored under `src/Data/`. Existing database files are preserved; the setup flow must not overwrite or reset them. No SQLite server or database creation tool is required.

### Step 4 — Run the web app

Start **MomentumGroves** using your development environment’s run or debug command. Open the local address shown in its output. Restore the project’s NuGet dependencies if your environment does not do so automatically.

Optional command-line alternative from the repository root:

```powershell
dotnet run --project .\MomentumGroves.csproj
```

### Step 5 — Check your installation

1. Confirm the home page opens and complete the automatic database initialization modal if prompted.
2. Open the custom food library and add a temporary test food; this confirms `food_and_recipes.db` and its tables work.
3. Save an application setting or other app-state entry to confirm `momentum.db` works.
4. If Frida is missing, use **Go to Admin to make FridaDb** and install the dataset; then test Frida food search.
6. Run the automated tests under [Testing](#testing).
7. For Android transfers, follow [Android companion and synchronization](#android-companion-and-synchronization).

---

</details>

---

<a id="sqlite-databases-and-initial-data"></a>
<details>
<summary><strong>SQLite databases and initial data</strong></summary>

The repository deliberately contains **no personal `.db` files**. All three databases are handled through the application; there is no manual database creation step.

| Filename | Purpose | How to initialize |
|---|---|---|
| `food_and_recipes.db` | Custom foods, recipes, ingredients, and mobile import mapping | Created and initialized by the **Database initialization** modal when missing |
| `momentum.db` | Application state and saved tracking data | Created and initialized by the **Database initialization** modal when missing |
| `frida.db` | DTU Frida food composition reference data | Created and populated separately through **Admin → Check for updates → Yes, install** |

### First-run setup

1. Start MomentumGroves.
2. If either local database is missing, click **Yes, initialize** in the setup modal.
3. When local initialization finishes, click **Go to Admin to make FridaDb** if Frida has not yet been installed.
4. Install Frida from Admin using the existing update workflow.

Only missing local databases are initialized. The Frida dataset is **not** created by the local initialization action. If all databases already exist, the startup check remains silent.

### Frida data initialization

1. Start the web application.
2. Open the **Admin** page (`/Admin`) on your local web app.
3. Click **Check for updates** to find the latest available DTU Frida dataset.
4. Select **Yes, install** when prompted. The application downloads the dataset and automatically creates and populates `src/Data/frida.db` if it does not exist.
5. Confirm that Admin displays the installed version and food count, then test a Frida food search.

The same Admin workflow supports both first-time installation and later updates. No database needs to be created manually. Installation requires internet access. In the verified first-install test, Admin installed DTU Frida version 6.1 and imported 1,390 foods.

### Database filenames and versions

Use the filenames above for a standard installation. The filenames are referenced in services/repositories. `frida.db` is **not** a required build-time content file in `MomentumGroves.csproj`; its absence must not prevent the project from building. If you rename one, update **all** matching paths in the source and project configuration. SQLite is embedded; no separate database server is required. The .NET SQLite package version is specified in the `.csproj`.

### Protecting local data

`.gitignore` excludes `.db` files, SQLite journal/sidecar files, and backups. Keep local databases private, and back up your own data before making schema changes. Never commit your personal database files.

</details>

---

<a id="configuration-and-external-services"></a>
<details>
<summary><strong>Configuration and external services</strong></summary>

The repository includes `.env.example`. Create a local `.env` at the **project root** and set the following values as needed:

| Setting | Where to put it | Purpose |
|---|---|---|
| `MobileSync__Key` | Local environment or `.env` | Private shared key used by the web sync endpoint; must match the mobile app |

The mobile sync setting is optional. The web sync service reads the configuration key `MobileSync:Key` (double underscores map to colons in environment-variable configuration).

Open Food Facts is used for product lookup and depends on internet access and available product data.

Do not expose a development sync endpoint or private keys on the public internet.

---

</details>

---

<a id="android-companion-and-synchronization"></a>
<details>
<summary><strong>Android companion and synchronization</strong></summary>

**The Android app is a separate project/repository.** Fork/clone and build it separately if you need mobile food entry and barcode scanning. Its own README should document the Android SDK, MAUI workload, signing, and installation; this web README does not claim to build the mobile app.

To connect the two apps on a **trusted private Wi-Fi network**:

1. Make sure the PC and phone are on the same reachable local network.
2. Choose a long, private synchronization key; configure it on the web app as `MobileSync__Key` and enter the **same key** in the mobile app.
3. Start the web app with a network-accessible listening address, for example from PowerShell at the web project root:

   ```powershell
   $env:ASPNETCORE_URLS='http://0.0.0.0:5000'
   $env:MobileSync__Key='REPLACE_WITH_A_PRIVATE_RANDOM_KEY'
   dotnet run --no-launch-profile --project .\MomentumGroves.csproj
   ```

4. Run `ipconfig` on the PC to find the current LAN IPv4 address.
5. In the Android app, open the food synchronization screen and set the PC address to `http://YOUR-PC-IP:5000`.
6. Enter the matching sync key and use the app’s transfer-to-PC action.
7. Verify the imported foods appear in the web food library before deleting anything manually.

The web endpoint is `POST /api/mobile/foods/import`, with the key sent in the `X-Momentum-Sync-Key` header. `localhost` on the phone refers to the phone, **not** the PC. The current example uses **unencrypted HTTP**, so do not use public Wi-Fi, router port forwarding, or public firewall rules. If Windows Firewall blocks port 5000, permit it on **private networks only**.

The import logic is designed to skip existing food names and acknowledge processed items rather than overwrite desktop foods. Keep mobile and web DTO JSON fields compatible when changing either repository.

---

</details>

---

<a id="testing"></a>
<details>
<summary><strong>Testing</strong></summary>

The repository contains a separate **NUnit** test project:

```text
tests/MomentumGroves.Tests/MomentumGroves.Tests.csproj
```

It targets **.NET 10** and references the web project. Tests cover service areas including food handling, recipes/library, Frida updates, mobile food sync, app state, training images, and a body-metrics controller.

Run the tests from the repository root:

```powershell
dotnet test .\tests\MomentumGroves.Tests\MomentumGroves.Tests.csproj
```

Or open the **Unit Tests** window in your application and run the test project. The test project includes `coverlet.collector` for coverage collection. Tests are not a guarantee that all browser interactions or external-service integrations work; test those separately.

---

</details>

---

<a id="publishing-safely"></a>
<details>
<summary><strong>Publishing safely</strong></summary>

This section replaces the separate `PUBLISHING.md` document.

- **Use Git to publish source**, not GitHub's manual upload of a ZIP or your entire working directory. `.gitignore` applies to Git tracking, not arbitrary uploads.
- Do **not** commit `.env`, API keys, private sync keys, signing material, personal SQLite databases, backups, exports, or downloaded private datasets.
- `.env.example` may be committed **only with placeholder values**. Review any `appsettings.json`, source code, tests, and reference CSV/JSON before making the repository public.
- Keep personal exports in `exports/` or `private/`, regardless of file extension.
- Before committing, review what Git is about to publish:

  ```powershell
  git status --short
  git add .
  git diff --cached --name-only
  git diff --cached
  ```

- If a private file was already tracked, `.gitignore` will **not** untrack it. Use `git rm --cached -- path/to/file` to stop tracking it locally, then commit that change.
- If a secret or private dataset was **ever committed**, removing it from the latest commit does not erase its history. Clean the Git history before publishing, rotate exposed credentials, and remember existing clones/forks may retain copies.
- Test sync keys found in automated tests are **fixtures**, not real deployment credentials; never reuse them.
- A fresh clone contains **no SQLite `.db` files**. On first launch, use the initialization modal to create missing local databases, then follow its Admin link to install DTU Frida. No manual SQLite database creation is required.

---

</details>

---

<a id="security-and-privacy"></a>
<details>
<summary><strong>Security and privacy</strong></summary>

<details>
<summary><strong>Network and repository safety</strong></summary>

- Sync uses **HTTP without transport encryption**. The connection key is sent over the local network. Use only trusted private networks.
- If necessary, allow TCP port 5000 through Windows Firewall for **private** networks only.
- Do not use public firewall rules or router port forwarding.
- Never commit credentials, `.env`, personal databases, Android signing keys, or machine-specific secrets.
- A `.gitignore` helps prevent accidental additions but cannot remove secrets already tracked in Git history.
- Review security, authentication, and privacy controls before exposing this project to the public internet.

</details>

</details>

---

<a id="troubleshooting"></a>
<details>
<summary><strong>Troubleshooting</strong></summary>

<details>
<summary><strong>Web app or database fails to start</strong></summary>

- Verify the .NET 10 SDK is installed.
- Start the web project from the correct root directory.
- Check that `src/Data/` exists and is writable.
- If a local database is missing, use the **Database initialization** modal and click **Yes, initialize**.
- If `frida.db` is missing or Frida search is empty, use **Go to Admin to make FridaDb**, or open Admin directly, click **Check for updates**, and install the dataset. Do not create `frida.db` manually.
- If the database status check fails, use the modal’s **Retry check** action and inspect the application error if it persists.
- Read the application error before deleting or replacing any database.

</details>

<details>
<summary><strong>Android cannot transfer foods</strong></summary>

- Confirm the web app is running and listening on port 5000.
- Confirm the phone and PC are on the same reachable private network.
- Verify the PC's current IPv4 address and matching secret key.
- Check the private-network Windows Firewall rule.
- Do not use `localhost` on the phone.
- Do not delete pending mobile entries to troubleshoot a failed transfer.

</details>

---

*Repository-owner notes, if present, are separate from this public setup guide.*

</details>
