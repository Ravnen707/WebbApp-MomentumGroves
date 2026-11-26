# MomentumGroves

---

MomentumGroves is a personal project for tracking meals, workouts, and weight over time. Open the English README for the story behind it, its features, and the programming languages, frameworks, and tools I use. You’ll also find what I’m learning, how to get started, and what’s planned next.
<details>
<summary><strong>English README</strong></summary>

MomentumGroves is a personal health tracker for logging meals, workouts, and weight over time.

This repository contains the **ASP.NET Core MVC (.NET 10)** web app, which stores data locally in **SQLite**. The **.NET MAUI Android** companion lives in a separate repository.

> **Development and AI assistance.** I build and maintain MomentumGroves as a personal learning project. I use OpenAI Codex to draft code scaffolding. I review all code, revise it where needed, and implement functionality myself when the scaffold falls short. The project gives me hands-on practice building features with ASP.NET Core MVC and writing browser automation with Playwright and NUnit.

## Table of contents

- [Motivation and why I built this](#motivation-and-why-i-built-this)
- [Project vision and roadmap](#project-vision-and-roadmap)
- [Overview and features](#overview-and-features)
- [Technology and project structure](#technology-and-project-structure)
- [Fork and set up the web application](#fork-and-set-up-the-web-application)
- [SQLite databases and initial data](#sqlite-databases-and-initial-data)
- [Configuration and external services](#configuration-and-external-services)
- [Android companion and synchronization](#android-companion-and-synchronization)
- [Testing](#testing)
- [Publishing safely](#publishing-safely)
- [Security and privacy](#security-and-privacy)
- [Troubleshooting](#troubleshooting)

---

## Motivation and why I built this

I started MomentumGroves because I wanted to lose weight and feel better. I weighed 117 kg and was already dealing with some physical issues that the extra weight wasn't helping. I wanted a way to track my progress and build habits I could stick with.

I know how easily I fall out of the habit of using tracking apps. I'll get into a routine, then find that some feature I want to use is behind a paywall. Once I can't keep track the way I wanted to, opening the app starts to feel like a chore. So I started building something around the way I actually live. I wanted to log what I eat and see how it adds up against what I need each day. I wanted my own recipes and my own food database, a graph that shows how my weight has changed over time, and a way to see whether I've kept up with training and how many weeks in a row I've managed it. Now when I catch myself thinking, *I wish the app did this one little thing,* I get to sit down and make it.

Keeping my own log helps me stay honest. If I leave out a meal or tick off a workout I skipped, the numbers I look back at won't match what I actually did. I built the app to see how I'm doing, so there's little point in entering anything other than what happened.

I also have a lot of fun building it. When I'm between jobs or going through a stretch where I don't get to code much, it gives me a reason to keep my skills current. I can learn something like Playwright by working on an app I know inside out, and I have something personal to show when I apply for jobs. Some days I sit down because I want to see how my training or weight is coming along. Other days I sit down because I can't wait to add something I've been thinking about. I think that's why I have such a hard time leaving it alone. I get to make something I enjoy, learn as I go, and use it to take better care of myself.

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

- **Shared date picker** — Use **Selected date** at the top right to open a past day. The date follows you between Dashboard, Food Diary, Training, and Weight Progress in the same browser tab. Select **Today** to return to the current day.
- **Dashboard** — View meals and nutrition for the selected date. The seven-day nutrition chart and balance end on that date.
- **Food Diary** — Add, edit, or delete meals and drinks for the selected date, copy an earlier meal, and mark the day as a sick day or cheat day. Scale a recipe portion or customize its ingredients for a diary entry while keeping the saved recipe intact.
- **Custom food library** — Manage foods with nutrition values per 100 g or 100 ml. The library lists the newest entries first, loads 10 items at a time, and searches across the full library.
- **Recipes** — Combine custom foods with ingredient quantities and a configurable meal weight, then select saved recipes in the Food Diary.
- **Drinks** — Search Open Food Facts, enter a quantity in ml, and review or edit the nutritional values before saving.
- **Nutrition targets** — The Health & Nutrition Calculator sets daily targets. Sick days are excluded from target assessment and the seven-day balance. Cheat days still count toward the balance.
- **Weight Progress** — Log or correct a weigh-in or mark the selected date as not weighed. Review weight history, trends, and goals.
- **Training** — Manage programme groups, select a group for the Dashboard, and review training history. A checkbox shows whether the programme was completed during the selected Monday-to-Sunday week. Checking it saves the selected date to training history. For example, selecting Sunday on a Monday logs the workout in the previous week. Completed programmes stay checked for their week after restarting the app.
- **Admin** — Install and update the DTU Frida food composition database.

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
| Testing tools | NUnit for .NET tests and Playwright for browser automation |
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
    ├── MomentumGroves.UnitTests/
    └── MomentumGroves.PlaywrightTests/
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

### Step 3 — First-run database setup

A fresh clone or fork runs on SQLite with the default configuration. Leave `ConnectionStrings:Supabase` unset or blank. The app needs no Supabase account, cloud credentials, or separate database server for this setup.

- On your first visit from `localhost`, the **Database initialization** dialog appears if `momentum.db` or `food_and_recipes.db` was missing at startup. Click **Yes, initialize** to create the missing databases.
- Once local initialization is complete, if `frida.db` is missing, the modal offers **Go to Admin to make FridaDb**. Click it to open the existing Frida installation page.
- In Admin, click **Check for updates**, then **Yes, install**. This downloads the DTU Frida dataset and creates and populates `frida.db` automatically.
- If only Frida is missing, you are directed to Admin without being asked to initialize the other databases again.
- When all three databases already exist, the startup check runs without showing a modal or additional status messages.

The files live under `src/Data/`. MomentumGroves preserves existing data and reuses these files on later runs. Frida stays local with either personal database provider.

### Step 4 — Run the web app

Start **MomentumGroves** using your development environment’s run or debug command. With the default port, the app opens `http://localhost:5000` in your browser. If you use a different port, open the local address shown in the run output. Restore the project’s NuGet dependencies if your environment does not do so automatically.

Optional command-line alternative from the repository root:

```powershell
dotnet run --project .\MomentumGroves.csproj
```

### Step 5 — Check your installation

1. Confirm the home page opens and complete the automatic database initialization modal if prompted.
2. Open the custom food library and add a temporary test food; this confirms `food_and_recipes.db` and its tables work.
3. Save an application setting or other app-state entry to confirm `momentum.db` works.
4. If Frida is missing, use **Go to Admin to make FridaDb** and install the dataset; then test Frida food search.
5. For Android transfers, follow [Android companion and synchronization](#android-companion-and-synchronization).

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

The same Admin workflow supports first-time installation and later updates. Installation requires internet access.

### Database filenames and versions

Use the filenames above for a standard SQLite installation. SQLite is embedded and works without a database server or cloud credentials. The Admin installer creates the local Frida database when needed.

### Exercise pictures in standalone mode

Open **Admin → Technical status** to see the active application mode, database, exercise image storage, Frida storage, and Mobile Data API setting.

With `ConnectionStrings:Supabase` unset or blank, MomentumGroves uses local SQLite databases and reads exercise pictures from `wwwroot/TrainingImages`. The project includes sample pictures in that folder. Add a JPG, PNG, or WebP file there, then open an exercise's picture picker and choose **Pick from program**. You can also choose **Upload picture** in the editor to save a picture to the same folder.

### Optional Supabase PostgreSQL

Set `ConnectionStrings:Supabase` in .NET User Secrets for Development, or supply `ConnectionStrings__Supabase` through the deployment's secret store. Use the project's PostgreSQL connection string for the direct connection or session pooler. For certificate and hostname verification, download the CA certificate from Supabase **Database Settings → SSL Configuration**, then set `SSL Mode=VerifyFull` and point `Root Certificate` to the downloaded file. The app defaults to `SSL Mode=Require` when the connection string omits an SSL mode.

A nonblank connection string sends application state, training, custom foods, recipes, and mobile food import tracking to PostgreSQL. MomentumGroves initializes the `momentum_groves` schema on startup using a server-side database role that can create and own its schema and tables. Startup fails if the selected database is unavailable or initialization fails. With the setting absent or blank, the app uses the existing `momentum.db` and `food_and_recipes.db` files. Keep the connection credential server-side. Frida search and Admin updates always use local `frida.db`.

By default, keep `momentum_groves` out of the Data API's exposed schemas. PostgreSQL initialization enables Row Level Security and revokes schema and table access from standard API roles. The web app connects directly as the schema owner.

To enable the optional mobile Data API, set `MobileDataApi__Enabled=true` on the **web host** and restart it before applying the reviewed SQL from the mobile repository's `docs/supabase-setup.md` guide. The setting allows web startup to coexist with the mobile grants. The SQL installation and dashboard exposure are manual. Keep the existing verified TLS settings. The mobile app uses its project URL, publishable key and user sign-in.

#### Exercise pictures in Supabase mode

Create the private bucket and owner read policy described in the mobile repository's `docs/training-image-storage.md`. Set `TrainingImages__SupabaseUrl` and `TrainingImages__ServerKey` on the web host and restart it. The training editor on localhost uploads images to the private bucket in PostgreSQL mode. Keep the secret key on the web host. If Supabase Storage is unavailable, the picture picker reports that it could not load pictures and lets you retry.

To inspect existing local pictures before copying them, run from the web repository:

```powershell
$env:DOTNET_ENVIRONMENT = 'Development' # Use this when settings are in .NET User Secrets.
dotnet run --project .\MomentumGroves.csproj -- migrate-training-images --dry-run
```

Once you have reviewed the dry run, rerun with `--apply` to copy objects. Existing local files and training references stay in place.

### Copy existing SQLite data to Supabase

Stop processes writing to the source databases and keep a private backup. Using SQLite's backup facility, prepare consistent snapshots named `momentum.db` and `food_and_recipes.db` in a separate directory. The tool opens those snapshots read-only. If an installation has only one of the two files, the tool reports the missing file and copies the available database. Frida is handled by its local Admin installer.

With the target connection configured in the secret store, run the dry run from the repository root. When using .NET User Secrets, use the Development environment.

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project .\MomentumGroves.csproj -- migrate-sqlite --source "C:\private\momentum-snapshots" --dry-run
```

The dry run verifies the copy and rolls back schema and data changes. Review the table counts before repeating the command with `--apply` to commit. Keep the app stopped against the target during the copy. The destination personal tables must be empty. The command checks counts and foreign keys and preserves IDs, timestamps, JSON documents, and mobile import mappings. Standard application startup initializes the schema only.

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
3. Start the web app with an HTTPS endpoint reachable from the phone. Use an address that matches the certificate and a certificate Android trusts. Keep the web app on your private network.
4. From the Android home screen, open **Sync to PC** and enter the HTTPS address and matching key.
5. Tap **Sync now** to save the settings, send pending foods and training weights, and download the latest programmes. Offline edits stay on the phone until a sync succeeds.

The mobile food and training endpoints use the `X-Momentum-Sync-Key` header. Enter the PC's network address in the Android app because `localhost` points to the phone.

The import logic is designed to skip existing food names and acknowledge processed items rather than overwrite desktop foods. Keep mobile and web DTO JSON fields compatible when changing either repository.

---

</details>

---

<a id="testing"></a>
<details>
<summary><strong>Testing</strong></summary>

The solution includes two test projects.

- **MomentumGroves.UnitTests** uses NUnit to exercise application services and controllers.
- **MomentumGroves.PlaywrightTests** is used to learn browser automation by interacting with the web app through its user interface.

To run the unit test project, use the following command from the repository root.

```powershell
dotnet test .\tests\MomentumGroves.UnitTests\MomentumGroves.UnitTests.csproj
```

You can also select the unit test project in your IDE's test runner.

---

</details>

---

<a id="publishing-safely"></a>
<details>
<summary><strong>Publishing safely</strong></summary>

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

- Confirm the web app is running on the HTTPS address saved in the Android app.
- Confirm the phone and PC are on the same reachable private network.
- Verify the PC address matches the HTTPS certificate and that both apps use the same sync key.
- Check the private-network Windows Firewall rule.
- Do not use `localhost` on the phone.
- Do not delete pending mobile entries to troubleshoot a failed transfer.

</details>

---

*Repository-owner notes, if present, are separate from this public setup guide.*

</details>

</details>

---

MomentumGroves er mit personlige projekt til at registrere måltider, træning og vægt over tid. Åbn den danske README for at læse om historien bag projektet, appens funktioner og de programmeringssprog, frameworks og værktøjer, jeg bruger. Du kan også læse om, hvad jeg lærer undervejs, hvordan du kommer i gang, og hvad jeg har planer om at bygge videre på.
<details>
<summary><strong>Dansk README</strong></summary>

MomentumGroves er et personligt værktøj til at registrere måltider, træning og vægt over tid.

Dette repository indeholder webappen, som er bygget med **ASP.NET Core MVC (.NET 10)** og gemmer data lokalt i **SQLite**. Den tilhørende **.NET MAUI-app til Android** ligger i sit eget repository.

> **Udvikling og brug af AI.** Jeg bygger og vedligeholder MomentumGroves som mit eget læringsprojekt. Jeg bruger OpenAI Codex til at lave udkast til kodens grundstruktur. Jeg gennemgår al kode, retter den efter behov og implementerer selv funktionalitet, når grundstrukturen ikke rækker. Projektet giver mig praktisk erfaring med at bygge funktioner i ASP.NET Core MVC og automatisere browseren med Playwright og NUnit.

## Indholdsfortegnelse

- [Min motivation, og hvorfor jeg byggede appen](#da-motivation)
- [Projektets vision og udviklingsplan](#da-vision)
- [Overblik og funktioner](#da-funktioner)
- [Teknologi og projektstruktur](#da-teknologi)
- [Fork og opsætning af webappen](#da-opsaetning)
- [SQLite-databaser og startdata](#da-databaser)
- [Konfiguration og eksterne tjenester](#da-konfiguration)
- [Android-app og synkronisering](#da-android)
- [Test](#da-test)
- [Sikker publicering](#da-publicering)
- [Sikkerhed og privatliv](#da-sikkerhed)
- [Fejlfinding](#da-fejlfinding)

---

<a id="da-motivation"></a>

## Min motivation, og hvorfor jeg byggede appen

Jeg startede MomentumGroves, fordi jeg ville tabe mig og få det bedre. Jeg vejede 117 kg og døjede allerede med nogle fysiske problemer, som de ekstra kilo ikke gjorde lettere. Jeg ville have en måde at følge mine fremskridt på og få nogle vaner, jeg kunne holde fast i.

Jeg ved, hvor let jeg falder ud af vanen med at bruge apps til at registrere min mad og træning. Jeg kommer ind i en rytme, og så opdager jeg, at en funktion, jeg gerne vil bruge, er bag en betalingsmur. Når jeg ikke længere kan følge med på den måde, jeg havde tænkt mig, begynder det at føles som en pligt at åbne appen. Derfor begyndte jeg at bygge noget, der passede til min egen hverdag. Jeg ville kunne registrere, hvad jeg spiser, og se, hvordan det passer med det, jeg har brug for hver dag. Jeg ville have mine egne opskrifter, min egen database med madvarer, en graf over min vægt og en måde at se, om jeg havde holdt fast i træningen, og hvor mange uger i træk det var lykkedes. Når jeg nu tænker, *Gid appen lige kunne den her ene lille ting,* kan jeg sætte mig ned og lave den.

At føre min egen log hjælper mig også med at være ærlig over for mig selv. Hvis jeg udelader et måltid eller sætter kryds ved en træning, jeg sprang over, passer de tal, jeg senere kigger på, ikke med det, jeg faktisk gjorde. Jeg byggede appen for at se, hvordan det går, så der er ikke meget ved at skrive andet ind end det, der skete.

Jeg synes også, det er sjovt at bygge appen. Når jeg er mellem jobs eller har en periode, hvor jeg ikke får kodet så meget, giver den mig en grund til at holde mine færdigheder ved lige. Jeg kan lære noget som Playwright ved at arbejde på en app, jeg kender ud og ind, og jeg har noget personligt at vise frem, når jeg søger job. Nogle dage sætter jeg mig, fordi jeg vil se, hvordan det går med træningen eller vægten. Andre dage er det, fordi jeg næsten ikke kan vente med at tilføje noget, jeg har gået og tænkt på. Det er nok derfor, jeg har så svært ved at lade projektet ligge. Jeg får lov til at bygge noget, jeg synes er sjovt, lære undervejs og bruge det til at passe bedre på mig selv.

---

<a id="da-vision"></a>
<details>
<summary><strong>Projektets vision og udviklingsplan</strong></summary>

På sigt skal MomentumGroves kunne mere end at fungere som min personlige logbog. Jeg vil gerne give webappen og mobilappen et fælles, genkendeligt udtryk. Mobilappen skal gradvist overtage webappens visuelle stil i et kompakt layout, der fungerer godt på en touchskærm. På længere sigt kunne andre også få glæde af appen. Konti, understøttelse af flere brugere og offentlig drift er mulige fremtidige funktioner.

### Nuværende udviklingsprioriteter i rækkefølge

1. **Skridttælling på Android** — Registrér daglige skridt fra understøttede sensorer, vis historik og mål for dage og uger, og synkronisér med webappen. Løsningen skal håndtere tilladelser, genstart af enheden og dubletter. Registrering i baggrunden afhænger af Androids begrænsninger og telefonens hardware.
2. **Registrering af aktivitet med GPS** — Start, sæt på pause, genoptag og afslut en gåtur. Registrér tid, afstand og rute, og gem og synkronisér afsluttede ture. Lokationsregistrering i baggrunden kræver udtrykkelige tilladelser og korrekt brug af en Android-foreground service.
3. **Præstationer** — Tilføj fælles serverlogik for præstationer knyttet til kostmål, skridt, gangdistance, træning, vægtmål og sammenhængende perioder. Understøt fremskridt, datoer for oplåsning, XP, niveauer, skjulte præstationer og relevante historiske data. Hver præstation skal kun tildeles én gang.
4. **Spil med belønninger for præstationer** — Lav et enkelt spil til web og mobil med almindelige grænsefladeelementer, ikoner, kort og statusbjælker. Præstationer og sunde rutiner kan give ressourcer eller låse op for opgraderinger. Spillet skal kunne spilles uden motion.

Skridttælling, GPS, præstationer og spillet er **planlagte milepæle**.

### Retning på længere sigt

- **Belønninger med en funktion** — Præstationer kan låse op for spilgenstande, opgraderinger, opgaver eller valgfrie funktioner.
- **Flere måder at gøre fremskridt på** — Kostvaner alene skal kunne give fremskridt. Gåture, træning og andre aktiviteter giver flere muligheder.
- **Valgfri kobling mellem sundhed og spil** — En fremtidig kontoindstilling kan slå spilbelønninger knyttet til personlige sundhedsmål og ugentlig aktivitet til eller fra.
- **Personlige indstillinger og konti** — Individuelle mål, præferencer, præstationer og fremskridt i spillet kan med tiden knyttes til brugerkonti.
- **Et fælles udtryk** — Brug MomentumGroves' visuelle tema på web og mobil med en enkel teknisk løsning.

Præstationerne skal understøtte vaner, man kan holde fast i. Ekstrem kalorierestriktion, overdreven træning og hurtigt vægttab skal ikke belønnes. Hviledage, sygdom og de nuværende funktioner for **Sick Day** og **Cheat Day** skal håndteres med omtanke. Belønninger i spillet skal aldrig afgøre, om nogen har lov til at spise.

</details>

---

<a id="da-funktioner"></a>
<details>
<summary><strong>Overblik og funktioner</strong></summary>

<details>
<summary><strong>Funktioner i webappen</strong></summary>

- **Fælles datovælger** — Brug **Selected date** øverst til højre til at åbne en tidligere dag. Datoen følger med, når du skifter mellem **Dashboard**, **Food Diary**, **Training** og **Weight Progress** i samme browserfane. Vælg **Today** for at vende tilbage til dags dato.
- **Dashboard** — Se måltider og næringsindtag for den valgte dato. Grafen og balancen for de seneste syv dage slutter på den dato.
- **Maddagbog** — Tilføj, ret eller slet måltider og drikkevarer for den valgte dato, kopiér et tidligere måltid, og markér dagen som en sygedag eller cheat day. Tilpas portionsstørrelsen eller de enkelte ingredienser i dagbogen, mens den gemte opskrift bevares.
- **Eget madbibliotek** — Administrér madvarer med næringsværdier pr. 100 g eller 100 ml. Biblioteket viser de nyeste først, henter 10 ad gangen og søger i hele biblioteket.
- **Opskrifter** — Sammensæt egne madvarer med ingrediensmængder og en justerbar vægt for det samlede måltid. Vælg derefter de gemte opskrifter i maddagbogen.
- **Drikkevarer** — Søg i Open Food Facts, indtast en mængde i ml, og gennemgå eller ret næringsværdierne, inden du gemmer.
- **Ernæringsmål** — **Health & Nutrition Calculator** fastsætter de daglige mål. Sygedage udelades fra vurderingen af målene og fra syvdagesbalancen. Cheat days tæller med i balancen.
- **Vægtudvikling** — Registrér eller ret en vejning, eller markér den valgte dato som en dag uden vejning. Se vægthistorik, udvikling og mål.
- **Træning** — Administrér programgrupper, vælg en gruppe til dashboardet, og se din træningshistorik. Et afkrydsningsfelt viser, om programmet er gennemført i den valgte uge fra mandag til søndag. Når du sætter kryds, gemmes den valgte dato i træningshistorikken. Vælger du eksempelvis søndag på en mandag, tæller træningen med i den foregående uge. Gennemførte programmer forbliver afkrydset i deres uge, også efter en genstart.
- **Admin** — Installér og opdatér DTU Fridas fødevaredatabase.

</details>

<details>
<summary><strong>Funktioner i Android-appen</strong></summary>

- Opret og ret egne madvarer offline med kalorier, protein, kulhydrat, fedt og valgfri kostfibre.
- Scan en stregkode med kameraet, slå næringsværdier op i Open Food Facts, og gennemgå eller redigér dem, inden du gemmer. Kameraunderstøttelse, tilladelser og produktdækning varierer.
- Brug g eller ml som måleenhed.
- Behold ventende poster på telefonen, indtil modtagelsen er bekræftet.
- Overfør poster til computeren via et privat Wi-Fi-netværk, du har tillid til.
- **Planlagt** — Tilpas mobilappens navigation, udseende og funktionslayout til webappens stil og den mindre skærm.
- Fjern poster fra telefonen, når modtagelsen er bekræftet. Behold de øvrige til næste forsøg.

Android-appen opbevarer madvarer midlertidigt, indtil de er overført.

</details>

</details>

---

<a id="da-teknologi"></a>
<details>
<summary><strong>Teknologi og projektstruktur</strong></summary>

| Område | Teknologi |
|---|---|
| Webplatform | **.NET 10**, ASP.NET Core MVC (`Microsoft.NET.Sdk.Web`) |
| Frontend | Razor `.cshtml`, JavaScript, HTML og CSS |
| Databaselagring | SQLite via `Microsoft.Data.Sqlite` (pakkeversion 8.0.22 i projektet) |
| Import af regneark | `ExcelDataReader` 3.7.0 |
| Testværktøjer | NUnit til .NET-test og Playwright til browserautomatisering |
| Udviklingsmiljø | En IDE eller kodeeditor med understøttelse af .NET 10, for eksempel Rider, Visual Studio eller VS Code |

```text
MomentumGroves/
├── MomentumGroves.sln
├── MomentumGroves.csproj
├── Program.cs
├── .env.example
├── src/
│   ├── Controllers/       # HTTP-endpoints for MVC og API
│   ├── DTO/               # Datatyper til overførsel via API'et
│   ├── Models/            # Appens modeller
│   ├── Service/           # Forretningslogik
│   ├── Repository/        # Dataadgang
│   ├── Helper/            # Hjælpefunktioner og konvertere
│   └── Data/              # Lokale SQLite-filer uden for Git
├── Views/                 # Razor-visninger og partial views
├── wwwroot/               # Statiske CSS- og JS-filer samt øvrige ressourcer
└── tests/
    ├── MomentumGroves.UnitTests/
    └── MomentumGroves.PlaywrightTests/
```

Mobilappen har sit eget GitHub-repository og egne DTO-definitioner. De to apps kommunikerer med JSON via et API. C#-namespaces kan være forskellige, så længe JSON-kontrakterne forbliver kompatible.

---

</details>

---

<a id="da-opsaetning"></a>
<details>
<summary><strong>Fork og opsætning af webappen</strong></summary>

Vejledningen gælder en **ny fork eller klon**. Personlige databaser og virkelige API-nøgler følger ikke med repositoryet.

### Trin 1 — Installér forudsætningerne

1. Installér **.NET 10 SDK**. Du skal bruge SDK'et ud over runtime.
2. Installér en IDE eller kodeeditor med understøttelse af .NET 10.
3. Installér **Git**, hvis du vil oprette en fork og klone den.
4. Opret en fork af webprojektet på GitHub, og klon den til en lokal mappe.
5. Åbn `MomentumGroves.sln` i din IDE, eller åbn projektets rodmappe i din kodeeditor.

Det installerede SDK skal understøtte `net10.0`. Webprojektet kræver ingen MAUI/Android-workload.

### Trin 2 — Opret lokal konfiguration

1. Kopiér `.env.example` i projektets rodmappe ved siden af `Program.cs` til en ny fil ved navn `.env`.
2. Behold `.env` lokalt. Git ignorerer filen.
3. Angiv `MobileSync__Key`, hvis du vil synkronisere madvarer fra mobilen.
4. Hold virkelige nøgler ude af `.env.example`, README-filer, kildekode og Git-commits.

`Program.cs` kalder `DotEnvLoader.Load(...)` og indlæser `.env`, når appen startes fra projektets rodmappe. Hvis du starter appen fra en anden arbejdsmappe, så kontrollér, at filen bliver fundet.

### Trin 3 — Opsætning af databaser ved første start

En ny klon eller fork bruger SQLite med standardindstillingerne. Lad `ConnectionStrings:Supabase` være tom eller udeladt. Opsætningen kræver hverken en Supabase-konto, loginoplysninger til en cloudtjeneste eller en separat databaseserver.

- Når du åbner appen på `localhost` første gang, vises dialogen **Database initialization**, hvis `momentum.db` eller `food_and_recipes.db` manglede ved opstart. Klik på **Yes, initialize** for at oprette de manglende databaser.
- Hvis `frida.db` mangler efter den lokale opsætning, viser dialogen knappen **Go to Admin to make FridaDb**. Klik på den for at åbne Frida-installationen.
- Klik på **Check for updates** i Admin, og vælg derefter **Yes, install**. Appen henter DTU Fridas datasæt og opretter og udfylder automatisk `frida.db`.
- Hvis kun Frida mangler, bliver du sendt til Admin. De øvrige databaser kræver ingen ny opsætning.
- Hvis alle tre databaser allerede findes, afsluttes opstartstjekket uden at vise en dialog eller statusmeddelelse.

Filerne ligger i `src/Data/`. MomentumGroves bevarer eksisterende data og genbruger filerne ved senere opstarter. Frida forbliver lokal, uanset hvor appen gemmer personlige data.

### Trin 4 — Start webappen

Start **MomentumGroves** med kør- eller debugkommandoen i dit udviklingsmiljø. Med standardporten åbner appen `http://localhost:5000` i browseren. Hvis du bruger en anden port, skal du åbne den lokale adresse, der vises i outputtet. Gendan projektets NuGet-afhængigheder, hvis udviklingsmiljøet ikke gør det automatisk.

Du kan også bruge følgende kommando i projektets rodmappe.

```powershell
dotnet run --project .\MomentumGroves.csproj
```

### Trin 5 — Kontrollér installationen

1. Kontrollér, at forsiden åbner, og gennemfør databaseopsætningen, hvis dialogen vises.
2. Åbn madbiblioteket, og tilføj en midlertidig madvare for at kontrollere, at `food_and_recipes.db` og dens tabeller fungerer.
3. Gem en indstilling eller en anden post i appen for at kontrollere, at `momentum.db` fungerer.
4. Hvis Frida mangler, skal du bruge **Go to Admin to make FridaDb** og installere datasættet. Prøv derefter at søge efter en madvare i Frida.
5. Følg [Android-app og synkronisering](#da-android), hvis du vil overføre data fra Android.

---

</details>

---

<a id="da-databaser"></a>
<details>
<summary><strong>SQLite-databaser og startdata</strong></summary>

Repositoryet indeholder ingen personlige `.db`-filer. Appen håndterer opsætningen af alle tre databaser.

| Filnavn | Formål | Sådan oprettes databasen |
|---|---|---|
| `food_and_recipes.db` | Egne madvarer, opskrifter, ingredienser og kobling til mobilimport | Dialogen **Database initialization** opretter databasen og dens tabeller, hvis den mangler |
| `momentum.db` | Appens tilstand og gemte registreringer | Dialogen **Database initialization** opretter databasen og dens tabeller, hvis den mangler |
| `frida.db` | DTU Fridas referencedata om fødevarers sammensætning | Oprettes og udfyldes separat via **Admin → Check for updates → Yes, install** |

### Opsætning ved første start

1. Start MomentumGroves.
2. Hvis en af de lokale databaser mangler, skal du klikke på **Yes, initialize** i opsætningsdialogen.
3. Når den lokale opsætning er færdig, skal du klikke på **Go to Admin to make FridaDb**, hvis Frida endnu ikke er installeret.
4. Installér Frida via opdateringsfunktionen i Admin.

Den lokale opsætning opretter kun manglende lokale databaser. Frida installeres separat gennem Admin. Hvis alle databaser allerede findes, vises der ingen besked ved opstartstjekket.

### Installation af Frida-data

1. Start webappen.
2. Åbn siden **Admin** (`/Admin`) i din lokale webapp.
3. Klik på **Check for updates** for at finde det nyeste tilgængelige datasæt fra DTU Frida.
4. Vælg **Yes, install**, når du bliver bedt om det. Appen downloader datasættet og opretter og udfylder automatisk `src/Data/frida.db`, hvis filen mangler.
5. Kontrollér, at Admin viser den installerede version og antallet af fødevarer, og prøv at søge efter en madvare i Frida.

Du bruger samme funktion i Admin ved første installation og ved senere opdateringer. Installationen kræver internetadgang.

### Databasefilnavne og versioner

Brug filnavnene ovenfor til en standardinstallation med SQLite. SQLite er indbygget og virker uden en databaseserver eller cloud-legitimationsoplysninger. Frida-søgning og opdateringer via Admin bruger altid den lokale `frida.db`.

Hvis `ConnectionStrings:Supabase` er angivet, bruger webappen PostgreSQL til træning, appdata, fødevarer, opskrifter og registrering af mobilimport. Forbindelsesstrengen opbevares i .NET User Secrets under udvikling eller i serverens hemmelighedslager. Hvis PostgreSQL ikke kan åbnes, stopper opstarten. SQLite bruges, når indstillingen mangler eller er tom. Se [Supabase-opsætningen](#optional-supabase-postgresql) og [vejledningen til at kopiere SQLite-data](#copy-existing-sqlite-data-to-supabase).

### Beskyttelse af lokale data

`.gitignore` udelukker `.db`-filer, SQLite-journaler, tilhørende filer og sikkerhedskopier. Hold dine lokale databaser private, tag en sikkerhedskopi, før du ændrer databaseskemaet, og hold personlige databasefiler ude af Git.

</details>

---

<a id="da-konfiguration"></a>
<details>
<summary><strong>Konfiguration og eksterne tjenester</strong></summary>

Repositoryet indeholder `.env.example`. Opret en lokal `.env` i **projektets rodmappe**, og angiv følgende værdi efter behov.

| Indstilling | Hvor den angives | Formål |
|---|---|---|
| `MobileSync__Key` | Lokalt miljø eller `.env` | Delt privat nøgle til webappens synkroniseringsendpoint. Nøglen skal matche mobilappens |

Mobilsynkronisering er valgfri. Webappens synkroniseringstjeneste læser konfigurationsnøglen `MobileSync:Key`. Dobbelt underscore i miljøvariabler oversættes til kolon ved indlæsning af konfigurationen.

Appen bruger Open Food Facts til produktopslag, som kræver internetadgang og tilgængelige produktdata.

Hold udviklingens synkroniseringsendpoint og private nøgler væk fra det offentlige internet.

---

</details>

---

<a id="da-android"></a>
<details>
<summary><strong>Android-app og synkronisering</strong></summary>

**Android-appen ligger i et separat projekt og repository.** Opret en fork eller klon, og byg mobilappen separat, hvis du vil registrere madvarer fra mobilen eller scanne stregkoder. Se mobilappens egen README for oplysninger om Android SDK, MAUI-workload, signering og installation.

Sådan forbinder du de to apps på et **privat Wi-Fi-netværk, du har tillid til**.

1. Sørg for, at computeren og telefonen er på samme lokale netværk og kan nå hinanden.
2. Vælg en lang, privat synkroniseringsnøgle. Sæt den som `MobileSync__Key` i webappen, og angiv **samme nøgle** i mobilappen.
3. Start webappen med en HTTPS-adresse, som telefonen kan nå. Adressen skal matche certifikatet, og Android skal have tillid til certifikatet. Hold webappen på dit private netværk.
4. Åbn **Sync to PC** fra Android-appens startside, og indtast HTTPS-adressen og den samme synkroniseringsnøgle.
5. Tryk på **Sync now** for at gemme indstillingerne, sende ventende madvarer og træningsvægte og hente de seneste programmer. Ændringer, du foretager offline, bliver på telefonen, indtil synkroniseringen lykkes.

Mobilens endpoints til madvarer og træning bruger headeren `X-Momentum-Sync-Key`. Angiv computerens netværksadresse i Android-appen, da `localhost` henviser til telefonen.

Importen springer eksisterende madvarenavne over og kvitterer for behandlede poster, så madvarerne på computeren bevares. Sørg for, at DTO'ernes JSON-felter fortsat er kompatible, når du ændrer et af de to repositories.

---

</details>

---

<a id="da-test"></a>
<details>
<summary><strong>Test</strong></summary>

Løsningen indeholder to testprojekter.

- **MomentumGroves.UnitTests** bruger NUnit til at teste appens services og controllers.
- **MomentumGroves.PlaywrightTests** bruges til at lære browserautomatisering ved at arbejde med webappen gennem dens brugergrænseflade.

Kør følgende kommando fra repositoryets rodmappe for at køre unit test-projektet.

```powershell
dotnet test .\tests\MomentumGroves.UnitTests\MomentumGroves.UnitTests.csproj
```

Du kan også vælge unit test-projektet i din IDE's testværktøj.

---

</details>

---

<a id="da-publicering"></a>
<details>
<summary><strong>Sikker publicering</strong></summary>

- **Brug Git til at udgive kildekoden**. `.gitignore` gælder for Git og beskytter dig ikke, hvis du manuelt uploader en ZIP-fil eller hele din arbejdsmappe til GitHub.
- Hold `.env`, API-nøgler, private synkroniseringsnøgler, signeringsmateriale, personlige SQLite-databaser, sikkerhedskopier, eksporter og downloadede private datasæt ude af dine commits.
- Commit kun `.env.example` med pladsholdere. Gennemgå eventuelle `appsettings.json`-filer, kildekode, tests og CSV- eller JSON-referencefiler, før du gør repositoryet offentligt.
- Opbevar personlige eksporter i `exports/` eller `private/`, uanset filtype.
- Gennemgå, hvad Git er ved at publicere, inden du laver et commit.

  ```powershell
  git status --short
  git add .
  git diff --cached --name-only
  git diff --cached
  ```

- Hvis Git allerede sporer en privat fil, skal du fjerne den fra Git-indekset med `git rm --cached -- path/to/file` og derefter committe ændringen. Filen bevares lokalt.
- Hvis en hemmelighed eller et privat datasæt tidligere er blevet committet, ligger det fortsat i Git-historikken, selvom du fjerner det fra det nyeste commit. Ryd op i historikken før publicering, udskift eksponerede nøgler, og husk, at eksisterende kloner og forks kan have kopier.
- Synkroniseringsnøglerne i automatiserede tests er testdata og må ikke bruges ved rigtig drift.
- En ny klon indeholder ingen SQLite-`.db`-filer. Brug opsætningsdialogen ved første start, og følg derefter linket til Admin for at installere DTU Frida. Appen opretter databaserne for dig.

---

</details>

---

<a id="da-sikkerhed"></a>
<details>
<summary><strong>Sikkerhed og privatliv</strong></summary>

<details>
<summary><strong>Sikkerhed for netværk og repository</strong></summary>

- Synkroniseringen bruger **HTTP uden transportkryptering**. Nøglen sendes over lokalnetværket, så brug kun private netværk, du har tillid til.
- Åbn om nødvendigt TCP-port 5000 i Windows Firewall, kun for **private** netværk.
- Hold offentlige firewallregler og port forwarding slået fra.
- Hold legitimationsoplysninger, `.env`, personlige databaser, Android-signaturnøgler og maskinspecifikke hemmeligheder ude af Git.
- En `.gitignore` kan forhindre utilsigtede tilføjelser. Hemmeligheder, der allerede ligger i Git-historikken, skal fjernes særskilt.
- Gennemgå sikkerhed, login og beskyttelse af private data, inden du gør projektet tilgængeligt på det offentlige internet.

</details>

</details>

---

<a id="da-fejlfinding"></a>
<details>
<summary><strong>Fejlfinding</strong></summary>

<details>
<summary><strong>Webappen eller databasen starter ikke</strong></summary>

- Kontrollér, at .NET 10 SDK er installeret.
- Start webprojektet fra den rigtige rodmappe.
- Kontrollér, at `src/Data/` findes, og at appen kan skrive til mappen.
- Hvis en lokal database mangler, skal du bruge dialogen **Database initialization** og klikke på **Yes, initialize**.
- Hvis `frida.db` mangler, eller søgning i Frida ikke giver resultater, skal du bruge **Go to Admin to make FridaDb** eller åbne Admin direkte. Klik på **Check for updates**, og installér datasættet gennem appen.
- Hvis databasens statustjek fejler, skal du klikke på **Retry check** i dialogen og undersøge appens fejlmeddelelse, hvis fejlen fortsætter.
- Læs appens fejlmeddelelse, inden du sletter eller erstatter en database.

</details>

<details>
<summary><strong>Android kan ikke overføre madvarer</strong></summary>

- Kontrollér, at webappen kører på den HTTPS-adresse, der er gemt i Android-appen.
- Kontrollér, at telefonen og computeren kan nå hinanden på det samme private netværk.
- Kontrollér, at computerens adresse matcher HTTPS-certifikatet, og at begge apps bruger samme synkroniseringsnøgle.
- Kontrollér Windows Firewall-reglen for private netværk.
- Brug computerens netværksadresse på telefonen.
- Behold ventende poster på mobilen, mens du undersøger en mislykket overførsel.

</details>

---

*Eventuelle noter til repositoryets ejer ligger separat fra denne offentlige opsætningsvejledning.*

</details>

</details>

---

<details>
<summary><strong>Showcase</strong></summary>

A look at the MomentumGroves web application and its main features.

### Dashboard

Daily overview, training programmes, seven-day nutrition balance and meals for the selected date.

<p>
<img width="1890" alt="Dashboard daily overview and training programmes" src="https://github.com/user-attachments/assets/d3aada51-f3fa-4d8c-bed6-48d3e77d1357" /><br>
<img width="1885" alt="Dashboard seven day nutrition balance and overview" src="https://github.com/user-attachments/assets/d77de3d3-468d-4037-bd92-e9024d4cc79e" /><br>
<img width="1885" alt="Dashboard meals for the selected date" src="https://github.com/user-attachments/assets/86114ee8-ed51-4d53-95d6-e00320ba3c07" />
</p>

### Dashboard controls and details

Date selection, nutrition ranges, nutrient filters and recent meals.

<p>
<img width="339" alt="Dashboard date picker" src="https://github.com/user-attachments/assets/94271d7b-8ab2-4dc0-9ae5-6c69d80c8908" /><br>
<img width="1002" alt="Dashboard daily nutrition ranges" src="https://github.com/user-attachments/assets/7d0ef784-8dfb-4c1d-b3a3-5bd99982f81a" /><br>
<img width="249" alt="Dashboard nutrition balance nutrient selection" src="https://github.com/user-attachments/assets/69725147-bc11-4bf9-9fe9-db3ad98bf273" /><br>
<img width="285" alt="Dashboard seven day overview nutrient selection" src="https://github.com/user-attachments/assets/d4828aea-dc3f-4b2c-a7cd-0b97fbf2a902" /><br>
<img width="1003" alt="Dashboard recent meals dialog" src="https://github.com/user-attachments/assets/d86c57ec-a9f4-48db-ba49-0886d8944380" />
</p>

### Food Diary

Daily nutrition, drinks, meals and previous entries.

<p>
<img width="1887" alt="Food diary daily nutrition drinks and meals" src="https://github.com/user-attachments/assets/8d271d85-a376-42f9-b1b2-485d93bed110" /><br>
<img width="1890" alt="Food diary entries and previous meals" src="https://github.com/user-attachments/assets/2b75261c-ab24-485a-98c8-2a0b4cc23ad9" />
</p>

### Sick Day and Cheat Day

Alternative day modes and their visual indicators.

<p>
<img width="1552" alt="Food diary sick day mode" src="https://github.com/user-attachments/assets/2df471b1-8c35-4e88-9ad1-8c528a614fff" /><br>
<img width="1555" alt="Food diary cheat day mode" src="https://github.com/user-attachments/assets/9a9209de-0a40-40cd-bd38-41a7e06c4401" />
</p>

### Adding drinks

Drink search and manual nutrition entry.

<p>
<img width="718" alt="Food diary add drink" src="https://github.com/user-attachments/assets/21ca6063-4ff6-4be2-82b6-18a8aedbf7f5" /><br>
<img width="685" alt="Food diary manual drink entry" src="https://github.com/user-attachments/assets/0d0ff0d7-e9f2-41ee-a9cf-ad0d4673e2fe" />
</p>

### Creating a meal

Create a meal, search Frida or saved recipes, adjust quantities and review before saving.

<p>
<img width="759" alt="Food diary create meal" src="https://github.com/user-attachments/assets/9658eba5-3791-4146-9304-ddadad2c409b" /><br>
<img width="762" alt="Food diary search dtu frida" src="https://github.com/user-attachments/assets/0bd9ce10-40c1-48db-a493-b7673045bc9a" /><br>
<img width="746" alt="Food diary search saved recipes" src="https://github.com/user-attachments/assets/34816edd-0561-48f8-9da4-3d5668ca6820" /><br>
<img width="737" alt="Food diary adjust selected food or recipe amount" src="https://github.com/user-attachments/assets/b2d3785f-d138-4ab9-b11c-decf3a93e6fa" /><br>
<img width="738" alt="Food diary review meal before saving" src="https://github.com/user-attachments/assets/f8d3b3ed-6347-41ec-85d0-d8c56d7d6478" />
</p>

### Editing and copying meals

Edit an existing entry or copy a previous meal.

<p>
<img width="720" alt="Food diary edit entry" src="https://github.com/user-attachments/assets/d7cdf9ff-012b-44b2-9b29-97d1cdbffaf4" /><br>
<img width="712" alt="Food diary copy a previous meal" src="https://github.com/user-attachments/assets/01e29df1-022e-4f4b-8a9e-b6de742aa09e" />
</p>

### Training

Training programmes, exercise picture selection and training progress.

<p>
<img width="1886" alt="Training training programmes" src="https://github.com/user-attachments/assets/c6f44b03-231f-4346-b9dd-5ccf87a23678" /><br>
<img width="719" alt="Training exercise picture dialog" src="https://github.com/user-attachments/assets/d8fea129-3e52-46de-b4c3-c79e396d1307" /><br>
<img width="1889" alt="Training training progress" src="https://github.com/user-attachments/assets/d9f6b00c-4702-4eef-868f-eac973d3daba" />
</p>

### Health & Nutrition Calculator

Calculate daily nutrition targets.

<p>
<img width="1660" alt="Health nutrition calculator calculate daily targets" src="https://github.com/user-attachments/assets/0dc5b2ad-1328-482a-a9c4-26a5837f0a6d" />
</p>

### Weight Progress

Weight logging, goals, trend chart and weigh-in history.

<p>
<img width="1865" alt="Weight progress weight logging and goals" src="https://github.com/user-attachments/assets/68aceaeb-9203-401a-98ef-8e6c0a312dbb" /><br>
<img width="1877" alt="Weight progress weight chart and estimated trend" src="https://github.com/user-attachments/assets/e3b4dc1b-5cd6-4eaa-9a2a-57fa5537a1b7" /><br>
<img width="1865" alt="Weight progress weight history" src="https://github.com/user-attachments/assets/d46fcfa0-6cee-4699-b45d-34932009342e" />
</p>

### Food Library — My Foods

Manage custom foods and their nutritional values.

<p>
<img width="1297" alt="Food library my foods save foods that cannot be scanned with the mobile app" src="https://github.com/user-attachments/assets/7296a0b3-5e57-45db-b5e2-108a39aaa717" />
</p>

### Food Library — My Recipes

Create reusable recipes for the Food Diary.

<p>
<img width="1656" alt="Food library my recipes create reusable recipes for the food diary" src="https://github.com/user-attachments/assets/5dd727b4-3875-43ef-84d8-91bbd5f5f77e" />
</p>

### Administration

Technical status, database management, Frida connection testing and update checks.

<p>
<img width="1235" alt="Administration technical status and database management" src="https://github.com/user-attachments/assets/8d9d3193-71fc-49f4-b9f0-f8dbb759fb85" /><br>
<img width="1227" alt="Administration dtu frida connection test" src="https://github.com/user-attachments/assets/465cb458-30c3-48f9-8822-02e3a2efcb4a" /><br>
<img width="924" alt="Administration dtu frida update check" src="https://github.com/user-attachments/assets/02d36324-f977-4bdb-a172-c24f863b5690" />
</p>

</details>
