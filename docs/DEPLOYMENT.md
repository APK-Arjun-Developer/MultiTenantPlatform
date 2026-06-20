# Deployment — MonsterASP.NET (FTP) via GitHub Actions

Production deploy workflow: [`.github/workflows/deploy-monsterasp-ftp.yml`](../.github/workflows/deploy-monsterasp-ftp.yml).

## End-to-end flow

```text
push master / manual run
    → dotnet publish (win-x86)
    → inject secrets → appsettings.Production.json
    → upload app_offline.htm (stop IIS)
    → FTPS deploy to /wwwroot/
    → remove app_offline.htm
    → smoke test GET /api/v1/health on SITE_URL
    → app starts: pending migrations + pending seeds
```

---

## 1. MonsterASP free hosting setup

1. Sign up at [MonsterASP.NET](https://www.monsterasp.net/).
2. In the control panel, create a **website** and an **MSSQL database** (free plan includes one DB).
3. Note the **FTP** details: **Deploy → FTP/WebDeploy/Git**
   - Host: `siteXXXX.siteasp.net`
   - Username: `siteXXXX`
   - Password: (from panel)
   - Port: `21`
   - Upload target: **`wwwroot`** (website root)
4. In the website settings, set the **.NET version** to **.NET 10** (or latest available).
5. Copy the **SQL Server connection string** from the database panel (use it for `PRODUCTION_CONNECTION_STRING`).

### Database on startup (production)

| Setting | Default on deploy | What it does |
|---------|-------------------|--------------|
| `ApplyMigrationsOnStartup` | `true` | Applies **pending** EF migrations (`__EFMigrationsHistory`) |
| `ApplySeedsOnStartup` | `true` | Applies **pending** versioned seeds (`SeedHistory` table) |

Both work like migrations: only missing versions run. Already-applied migrations/seeds are skipped.

**Current seeds** (in order):

| SeedId | Description |
|--------|-------------|
| `20260603000002_Permissions` | RBAC permission catalog |
| `20260603000003_SuperAdmin` | SystemAdmin role + `admin@system.com` user |

**SystemAdmin login:** `admin@system.com` / `ADMIN_PASSWORD` GitHub secret.

---

## 2. Fresh database (when needed)

The schema baseline is a single migration: **`InitialCreate`**. If you need an empty database:

### Local

```powershell
dotnet ef database drop --force `
  --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Api/Api.csproj
dotnet run --project src/Api
```

Pending migrations and seeds apply on startup.

### Production (MonsterASP)

1. **Control panel** — delete and recreate the MSSQL database, **or**
2. **SSMS** — connect to your database and drop all user tables
3. Redeploy the app — on startup, `InitialCreate` and pending seeds run

Ensure `ADMIN_PASSWORD` is set in GitHub secrets before the SystemAdmin seed runs.

---

## 3. GitHub repository secrets

Open **Settings → Secrets and variables → Actions → New repository secret**.

| Secret | Required | Example / notes |
|--------|----------|-----------------|
| `FTP_SERVER` | Yes | FTP host only: `site1234.siteasp.net` (no `https://`) |
| `FTP_USERNAME` | Yes | `site1234` |
| `FTP_PASSWORD` | Yes | FTP password from control panel |
| `FTP_SERVER_DIR` | No | Default `/wwwroot/` |
| `SITE_URL` | No | **Recommended.** Public site URL for smoke test, e.g. `https://site1234.monsterasp.net` |
| `PRODUCTION_CONNECTION_STRING` | Yes | MonsterASP MSSQL connection string |
| `JWT_KEY` | Yes | Random string, **≥ 32 characters** |
| `ADMIN_PASSWORD` | Yes | SystemAdmin (`admin@system.com`) password |
| `JWT_ISSUER` | No | Defaults to `MultiTenantPlatform` |
| `JWT_AUDIENCE` | No | Defaults to `MultiTenantPlatformUsers` |
| `ALLOWED_ORIGINS` | No | Comma-separated CORS origins. If empty, deploy uses `SITE_URL` as the allowed origin. |
| `APP_BASE_URL` | Yes (prod) | Public HTTPS URL used in emailed links, e.g. `https://site1234.monsterasp.net` |

Never commit real connection strings or JWT keys. The workflow **overwrites** `appsettings.Production.json` at deploy time.

---

## 4. Production configuration

| Setting | Production behavior |
|---------|---------------------|
| `ApplyMigrationsOnStartup` | `true` |
| `ApplySeedsOnStartup` | `true` |
| `ConnectionStrings:DefaultConnection` | From `PRODUCTION_CONNECTION_STRING` |
| `Seeding:AdminPassword` | From `ADMIN_PASSWORD` |
| `Features:RequireEmailVerification` | `true` — new users must verify their email via OTP before logging in |
| `AppBaseUrl` | From `APP_BASE_URL` — must be an `https://` URL; used in emailed links |
| Swagger | `/swagger` open (toggle via `Swagger:EnabledInProduction`) |

### Email verification in production

`Features:RequireEmailVerification` is `true` by default in production. When a new user is created (via tenant onboarding or direct user creation), their account has `EmailConfirmed = false`. A 6-digit OTP is emailed automatically. The user must call `POST /api/v1/auth/verify-email` before they can log in.

To disable email verification in production (not recommended), add `"Features": { "RequireEmailVerification": false }` to the deployed `appsettings.Production.json` or inject it as an environment variable `Features__RequireEmailVerification=false`.

---

## 5. Adding a new seed (developers)

1. Create `Infrastructure/Persistence/Seed/Seeds/YourSeed.cs` implementing `IDataSeed` with a **new unique** `SeedId` (timestamp prefix, e.g. `20260604000001_MyFeature`).
2. Register in `Infrastructure/Persistence/DependencyInjection.cs`: `services.AddScoped<IDataSeed, YourSeed>();`
3. Deploy — only the new seed runs on next startup.

---

## 6. EF migrations (developers)

```powershell
dotnet ef migrations add YourMigrationName `
  --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Api/Api.csproj `
  --output-dir Persistence/Migrations
```

---

## 7. Trigger a deploy

- **Automatic:** push to `master`
- **Manual:** GitHub → **Actions** → **Deploy to MonsterASP (FTPS)** → **Run workflow**

Smoke test: `GET /api/v1/health` on `SITE_URL`.

---

## 8. Local vs production settings

| Setting | Development | Production (deployed) |
|---------|-------------|------------------------|
| `ApplyMigrationsOnStartup` | `true` | `true` |
| `ApplySeedsOnStartup` | `true` | `true` |
| `Seeding:AdminPassword` | `appsettings.Development.json` or user secrets | `ADMIN_PASSWORD` GitHub secret |
| `Features:RequireEmailVerification` | `false` — skip email OTP; all users log in immediately | `true` — users must verify email before first login |
| `AppBaseUrl` | `http://localhost:5173` | `APP_BASE_URL` GitHub secret (must be `https://`) |
| Swagger | Open at `/swagger` | Open at `/swagger` (`EnabledInProduction`) |
| CORS | `AllowedOrigins` in Development config | `ALLOWED_ORIGINS` secret (optional) |
| Email | Localhost SMTP (e.g. Mailhog on port 1025) | Configured SMTP credentials |

Both environments only apply **pending** migrations and seeds — safe on every restart.

---

## 9. FTP file locks (troubleshooting)

See [MonsterASP FTP deploy docs](https://help.monsterasp.net/books/deploy/page/how-to-deploy-website-content-via-ftpsftp). The workflow uploads `app_offline.htm` before deploy.

---

## Related

- [PROJECT.md](./PROJECT.md) — architecture summary
- [API.md](./API.md) — API reference
