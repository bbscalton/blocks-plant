## Blocks Plant Management System

Concrete blocks plant POS and operations system — Phase 1 (API) + Phase 2 (Windows desktop POS) + Phase 3 (web owner dashboard) + Phase 4 (Android floor app).

### Demo on GitHub Pages

A **static product showcase** lives in `site/` and is published with GitHub Actions (`.github/workflows/pages.yml`). It explains what Blocks Plant does for plant owners and staff.

**Try the live app (hosted web dashboard):**

- https://blocks.neuereatec.org/

That URL is a demo/live environment for people interested in the product. Sign in with the seeded accounts below (for example `owner` / `owner123`). The GitHub Pages site links to it as the primary “try it” CTA; Windows POS and Android still run from a local clone.

**Showcase site:**

- https://bbscalton.github.io/blocks-plant/

**Enable Pages (once per repo):**

1. Push this repository to GitHub (default branch `main`).
2. Repo **Settings → Pages → Build and deployment → Source**: choose **GitHub Actions**.
3. After the first successful `Deploy GitHub Pages` workflow run, the site is at:

   `https://<org-or-user>.github.io/blocks-plant/`

   (Replace `<org-or-user>` and `blocks-plant` if your GitHub owner or repo name differs.)

**Full system can also run locally** — start the API, then desktop / web / Android as below. Use the live URL to try the web app quickly; use local runs for POS, Android, or a private stack.

### Solution layout

```
blocks-plant/
  BlocksPlant.sln
  backend/          ASP.NET Core 8 Web API + EF Core SQLite
  desktop/          WPF POS (.NET 8 Windows)
  web/              Blazor Server owner dashboard (.NET 8)
  android/          Kotlin / Jetpack Compose plant-floor app
  site/             Static GitHub Pages showcase (HTML/CSS/JS)
  .github/workflows/pages.yml
  README.md
```

### Default users

| Username  | Password     | Role     |
|-----------|--------------|----------|
| owner     | owner123     | Owner    |
| cashier   | cashier123   | Cashier  |
| operator  | operator123  | Operator |

### How to run the API

```powershell
cd backend
dotnet run --launch-profile http
```

- HTTP: `http://localhost:5118`
- HTTPS profile: `https://localhost:7082` (also `dotnet run --launch-profile https`)
- Swagger (Development): `http://localhost:5118/swagger`

SQLite database file `blocksplant.db` is created next to the API on first run (seeded products, users, stock).

### How to run the desktop POS

1. Start the API first.
2. Then:

```powershell
cd desktop
dotnet run
```

Default API URL in the login screen: `http://localhost:5118`  
(Change it if you use HTTPS or another host; the value is saved under `%AppData%\BlocksPlant\config.json`.)

### How to run the web dashboard

1. Start the API first (`backend`, profile `http` → `http://localhost:5118`).
2. Then:

```powershell
cd web
dotnet run --launch-profile http
```

- Web UI: `http://localhost:5137` (use this HTTP URL with the `http` profile)
- API base URL is configured in `web/appsettings.json` / `web/appsettings.Production.json` (`Api:BaseUrl`, default `http://localhost:5118`)
- Sign in: `owner` / `owner123` (Owner dashboard). Cashier/Operator accounts work with role-limited pages.

**Production hosting (e.g. `blocks.neuereatec.org`):** the Blazor Server app calls the API with server-side `HttpClient`, so `Api:BaseUrl` must be reachable **from the web host**, not from the browser. Same-machine API on `http://localhost:5118` is fine; otherwise set env var `Api__BaseUrl` (or edit `appsettings.Production.json`) before publish. GitHub Pages only hosts the static `site/` showcase — it does **not** deploy the Blazor Server dashboard; redeploy `web/` (and `backend/` if needed) on the neuereatec host after pulling fixes.

If port 5137 or 5118 is already in use, stop the old `BlocksPlant.Web` / `BlocksPlant.Api` process (or change the profile ports) before starting again.

CORS on the API already allows any origin (useful if you later switch the web client to Blazor WASM).

### How to run the Android app

1. Start the API on the host machine (`backend`, profile `http` → port **5118**).
2. Open the `android/` folder in Android Studio **or** build from the command line:

```powershell
cd android
.\gradlew.bat assembleDebug
```

APK: `android/app/build/outputs/apk/debug/app-debug.apk`

3. Run on an **emulator**:
   - Default API base URL is `http://10.0.2.2:5118` (emulator alias for the host’s `localhost`).
   - Cleartext HTTP is allowed for local/dev networking.
4. Run on a **physical device**:
   - Phone and PC must be on the same LAN.
   - In the app **Settings**, set API base URL to e.g. `http://192.168.x.x:5118` (your PC’s LAN IP).
   - Windows Firewall must allow inbound TCP 5118.

**Roles in the app**

| Role     | Screens |
|----------|---------|
| Operator | Production entry (offline queue), finished stock + raw materials (quantities only) |
| Cashier  | POS backup, sales, customers/payments, deliveries, stock, materials (read) |
| Owner    | Dashboard (incl. low materials) + Materials list + all of the above |

JWT is stored in EncryptedSharedPreferences. Production entries queue in Room when the API is unreachable and sync when online (`clientId` idempotency).

**Note:** Sales/POS on Android require network (Windows remains primary cashier POS). Production is offline-first. Material deduction happens on the API when production syncs.

### Roles

- **Operator** — production entry; view finished + raw material quantities; cannot see prices, sales, or balances; cannot edit recipes or receive materials.
- **Cashier** — POS, customers, payments, sales history, deliveries; can view materials; cannot adjust finished or raw stock / recipes.
- **Owner** — everything: dashboard, product prices, stock adjust, materials receive/adjust, recipes (BOM), production history, POS, etc.

### Business rules enforced by the API

- Products priced per block only (4 / 6 / 8 inch).
- Credit or partial payment with balance due requires a customer.
- Sales rejected if finished stock would go negative.
- Production increases finished stock and **deducts raw materials** = recipe × good qty produced (rejects do not consume extra).
- Production rejected if any recipe material is insufficient (clear error listing shortages).
- Line total = qty × pricePerBlock.
- Sale `ClientId` (GUID) provides create idempotency.
- Production optional `ClientId` (GUID) provides offline sync idempotency.

### Switching SQLite → SQL Server later

In `backend/appsettings.json`:

1. Change connection string, e.g.  
   `"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=BlocksPlant;Trusted_Connection=True;TrustServerCertificate=True"`
2. In `Program.cs`, replace `UseSqlite(...)` with `UseSqlServer(...)`.
3. Add package: `dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 8.0.11`
4. Prefer EF migrations for production instead of `EnsureCreated` seeding.

### API overview

| Method | Path | Roles |
|--------|------|-------|
| POST | /api/auth/login | Anonymous |
| GET | /api/products | Authenticated (prices hidden for Operator) |
| PUT | /api/products/{id} | Owner |
| GET | /api/stock | Authenticated |
| POST | /api/stock/adjust | Owner |
| GET/POST/PUT | /api/materials | GET: Authenticated; POST/PUT: Owner |
| POST | /api/materials/{id}/receive | Owner |
| POST | /api/materials/{id}/adjust | Owner |
| GET | /api/recipes | Authenticated |
| GET/PUT | /api/recipes/by-product/{productId} | GET: Authenticated; PUT: Owner |
| POST | /api/production | Operator, Owner (checks materials; optional `clientId`) |
| GET | /api/production | Owner |
| GET/POST | /api/customers | Owner, Cashier |
| GET | /api/customers/debtors | Owner, Cashier |
| POST/GET | /api/sales | Owner, Cashier |
| POST | /api/payments | Owner, Cashier |
| GET/PATCH | /api/deliveries | Owner, Cashier |
| GET | /api/dashboard | Owner (includes finished + raw material low-stock) |

CORS is enabled for web clients.

### Desktop features

- Login with role-based navigation
- POS: lines, collect/deliver, cash/partial/credit, customer picker, receipt print
- Production entry (operators) — API deducts materials; insufficient materials shows API error
- Customers + record payment
- Sales history, deliveries, owner dashboard / products / stock adjust
- **Materials** inventory (Owner: receive/adjust/add; Operator/Cashier: read-only)
- **Recipes** BOM per product (Owner)

### Web dashboard features

- JWT login against the API
- Owner dashboard: finished stock + raw materials low-stock alerts, today’s production/sales, unpaid/debtors
- Stock view + Owner adjust
- **Materials** list + Owner receive/adjust/add
- **Recipes** editor (Owner)
- Product price / min-stock edit (Owner)
- Sales history search
- Customers / debtors + record payment
- Deliveries pending list + mark delivered
- Production history (Owner, read-only)

### Android features

- JWT login; secure token storage
- Operator: production entry + offline Room queue + pending sync count
- Finished stock + **raw materials** list with low-stock indication
- Cashier/Owner: simple POS backup, sales list, customers + payments, deliveries
- Owner: dashboard summary (incl. materials)
- Configurable API base URL (Settings / BuildConfig default)

### Try materials + production

1. Start the API (`cd backend; dotnet run --launch-profile http`).
2. Seed creates Cement / Sand / Aggregate / Water and recipes for 4″ / 6″ / 8″ blocks.
3. As Owner, open **Materials** — note Cement on hand (e.g. 80 bags).
4. As Operator, post production of e.g. 100 × 4″ blocks.
5. Refresh Materials — Cement / Sand / Aggregate / Water drop by recipe × 100.
6. Try producing a huge qty — API returns `Insufficient raw materials…` and finished stock is unchanged.

### Build

```powershell
dotnet build BlocksPlant.sln
cd android
.\gradlew.bat assembleDebug
```

### Caveats

- Production offline sync is supported on Android; **sales/POS require network** (Windows is primary POS).
- JWT secret in `appsettings.json` is for local/dev — change for any shared deployment.
- Dev HTTPS may require trusting the ASP.NET Core dev certificate (`dotnet dev-certs https --trust`).
- Receipt printing uses the standard Windows print dialog (desktop).
- Web dashboard is Blazor Server (interactive); the API must be reachable from the machine running the web app. Prefer the `http` launch profiles for local dev so HTTPS redirection does not interfere.
- Android cleartext HTTP is enabled for plant LAN / emulator use only.
