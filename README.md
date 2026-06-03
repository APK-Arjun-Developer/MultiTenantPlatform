# Multi-Tenant Platform

.NET **10** multi-tenant SaaS API with JWT authentication, permission-based RBAC, tenant isolation, and automated production deploys.

## Documentation

| Document | Description |
|----------|-------------|
| [docs/PROJECT.md](docs/PROJECT.md) | Architecture, auth, data model, migrations & seeds |
| [docs/API.md](docs/API.md) | v1 endpoints, envelope, login, profiles, addresses |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | GitHub Actions → MonsterASP.NET (FTP) production deploy |

## How it works (workflow)

```text
┌─────────────┐     push master     ┌──────────────────┐     FTPS      ┌─────────────┐
│  Developer  │ ──────────────────► │ GitHub Actions   │ ────────────► │ MonsterASP  │
│  local dev  │                     │ build + deploy   │               │  wwwroot    │
└─────────────┘                     └──────────────────┘               └─────────────┘
       │                                      │                                │
       │ dotnet run                           │ smoke: /api/v1/health          │ app start
       ▼                                      ▼                                ▼
 Apply pending migrations              secrets → appsettings.Production    Apply pending
 Apply pending seeds (SeedHistory)                                          migrations + seeds
```

On every app start (local or production):

1. **Migrations** — pending EF migrations from `__EFMigrationsHistory` are applied (`ApplyMigrationsOnStartup`).
2. **Seeds** — pending versioned seeds from `SeedHistory` are applied (`ApplySeedsOnStartup`). Already-applied seeds are skipped.

Schema baseline: single migration **`InitialCreate`**. Seed versions are tracked like migrations.

## Quick start (local)

### 1. Configure secrets

Secrets are **not** committed. Use user secrets or environment variables:

```powershell
dotnet user-secrets set "Jwt:Key" "your-32-char-dev-secret-here-minimum" --project src/Api
dotnet user-secrets set "Seeding:AdminPassword" "Admin123!" --project src/Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost\SQLEXPRESS;Database=MultiTenantPlatformDb;Trusted_Connection=True;TrustServerCertificate=True;" --project src/Api
```

Or rely on [appsettings.Development.json](src/Api/appsettings.Development.json) for local SQL/JWT (dev only).

### 2. Run the API

```powershell
dotnet run --project src/Api
```

Migrations and pending seeds run automatically on startup. To recreate the database from scratch:

```powershell
dotnet ef database drop --force `
  --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Api/Api.csproj
dotnet run --project src/Api
```

Open:

- Swagger: `https://localhost:<port>/swagger` (Development, no login)
- Health: `GET /api/v1/health`
- API base: `/api/v1`

**Default SuperAdmin** (after seeds): `admin@system.com` / value of `Seeding:AdminPassword` (e.g. `Admin123!` in Development).

### 3. Add a migration (developers)

```powershell
dotnet ef migrations add YourMigrationName `
  --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Api/Api.csproj `
  --output-dir Persistence/Migrations
```

### 4. Add a seed (developers)

1. Create `Infrastructure/Persistence/Seed/Seeds/YourSeed.cs` implementing `IDataSeed` with a unique `SeedId` (e.g. `20260604000001_MyFeature`).
2. Register in `Infrastructure/Persistence/DependencyInjection.cs`.
3. Restart the app — only the new seed runs.

See [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) for production deploy.

## Production

- Deploy: push to **`master`** or run **Deploy to MonsterASP (FTPS)** workflow.
- Required GitHub secrets: `FTP_*`, `PRODUCTION_CONNECTION_STRING`, `JWT_KEY`, `ADMIN_PASSWORD`, recommended `SITE_URL`.
- Swagger: `https://<your-site>/swagger` — sign in with `admin@system.com` + `ADMIN_PASSWORD`.
- Fresh database: delete and recreate the database in the MonsterASP control panel (or drop all tables in SSMS), then redeploy.

## Highlights

- **Permissions**: PascalCase (`Users.View`); checked per request, **not** in JWT
- **JWT claims**: `user_id`, `tenant_id`, `role_id`, role names
- **Versioned seeds**: `SeedHistory` table; pending seeds only (like migrations)
- **Profiles**: user & tenant profile images via `Files` FK; `profileUrl` in responses
- **Addresses**: optional user/tenant address with `fullAddress` combined string
- **Onboarding**: `POST /api/v1/tenants` with `user`, `tenant`, `roles[]` (permission GUIDs)
- **No public registration** — SuperAdmin onboards tenants

## Project structure

```text
src/
  Api/              Controllers, middleware, Swagger gate, Program.cs
  Application/      DTOs, validators, interfaces, PermissionNames
  Domain/           Entities (Tenant, Product, Permission, Address, …)
  Infrastructure/   EF Core, Identity, services, seeds, migrations
  Shared/           Shared utilities (minimal)
deploy/             FTP deploy helpers (app_offline, smoke test, web.config)
.github/workflows/  deploy-monsterasp-ftp.yml
docs/               PROJECT.md, API.md, DEPLOYMENT.md
```
