# Multi-Tenant Platform — Project Summary

Canonical architecture and workflow summary. For endpoints see [API.md](./API.md); for deploy see [DEPLOYMENT.md](./DEPLOYMENT.md).

---

## Overview

A **multi-tenant SaaS backend** on **.NET 10** with:

- Tenant isolation via `TenantId`, EF global query filters, and middleware
- **JWT** access tokens + **refresh tokens** (login / refresh / logout)
- **Permission-based RBAC** checked per request (with `IMemoryCache`)
- **ASP.NET Core Identity** for users and roles (`Guid` keys)
- **Versioned database seeds** tracked in `SeedHistory` (like EF migrations)
- No public self-registration — tenants onboarded by platform **SuperAdmin**

---

## Technology stack

| Area | Choice |
|------|--------|
| Runtime | **.NET 10** (`net10.0`) |
| API | ASP.NET Core Web API, `/api/v1` |
| Data | EF Core 10, SQL Server |
| Auth | JWT Bearer + ASP.NET Core Identity |
| Validation | FluentValidation |
| Logging | Serilog + request middleware |
| API docs | Swagger (Development open; Production login-gated) |
| Deploy | GitHub Actions → MonsterASP.NET FTPS (`win-x86`, InProcess) |

---

## Solution layout

```text
src/
  Api/              HTTP pipeline, controllers, middleware, envelope, Swagger gate
  Application/      DTOs, validators, interfaces, PermissionNames / RoleNames
  Domain/           Entities, ITenantEntity, IAuditableEntity
  Infrastructure/   DbContext, migrations, seeds, Identity, feature services
  Shared/           Minimal shared utilities
deploy/             FTP deploy helpers (app_offline, smoke test, web.config)
```

Services live in **Infrastructure** and register via DI extensions. Data access uses `ApplicationDbContext` and feature services directly (no generic repository).

---

## Database workflow

### Migrations

- Stored in `Infrastructure/Persistence/Migrations/`
- Baseline: **`InitialCreate`** (consolidated schema)
- History table: `__EFMigrationsHistory`
- On startup when `ApplyMigrationsOnStartup: true`: apply **pending** migrations only

```powershell
dotnet ef migrations add Name `
  --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Api/Api.csproj `
  --output-dir Persistence/Migrations
```

### Versioned seeds

- Interface: `IDataSeed` with stable `SeedId` (e.g. `20260603000002_Permissions`)
- Runner: `SeedRunner` compares registered seeds vs `SeedHistory` table
- On startup when `ApplySeedsOnStartup: true`: apply **pending** seeds only, in `SeedId` order

| SeedId | Purpose |
|--------|---------|
| `20260603000002_Permissions` | RBAC permission catalog |
| `20260603000003_SuperAdmin` | SuperAdmin role + `admin@system.com` (requires `Seeding:AdminPassword`) |

Add a seed: new class in `Persistence/Seed/Seeds/`, register in `Persistence/DependencyInjection.cs`.

### Fresh database

| Environment | Approach |
|-------------|----------|
| Local | `dotnet ef database drop --force` then `dotnet run --project src/Api` |
| Production | Delete/recreate database in MonsterASP control panel, or drop all tables in SSMS, then redeploy |

---

## Multi-tenancy

- Tenant-scoped rows carry **`TenantId`** (`ITenantEntity`).
- EF global filters: soft delete (`DeletedAt == null`) + tenant scope where applicable.
- **`TenantMiddleware`** validates JWT `tenant_id` after authentication.
- **SuperAdmin**: `tenant_id = Guid.Empty`. Login omits `tenantSlug`; tenant users require matching `tenantSlug`.

---

## Authentication

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/auth/login` | Access + refresh tokens |
| POST | `/api/v1/auth/refresh` | Rotate tokens |
| POST | `/api/v1/auth/logout` | Revoke refresh token |

### JWT claims

| Claim | Description |
|-------|-------------|
| `user_id` | User GUID |
| `tenant_id` | Tenant GUID (`Guid.Empty` for SuperAdmin) |
| `role_id` | Primary role GUID |
| `full_name` | Display name |
| `role` | Role names |

**Permissions are not in the JWT.** Loaded from DB per request (cached).

---

## Authorization

- **`[HasPermission("Module.Action")]`** on controllers/actions (PascalCase).
- `PermissionPolicyProvider` + `PermissionAuthorizationHandler`.
- SuperAdmin: all permissions from catalog.
- Tenant users: union of role permissions in current tenant.

Catalog: `GET /api/v1/permissions` (`?grouped=true` optional). Tenant callers do not see `Tenants.*`.

---

## Identity model

| Concept | Implementation |
|---------|----------------|
| Users | `ApplicationUser` + `TenantId`, `FullName`, `ProfileFileId`, soft delete |
| Roles | `ApplicationRole` + `TenantId`, `Description`, soft delete |
| User ↔ role | Identity `AspNetUserRoles` |
| Role ↔ permission | `RolePermissions` |
| Permissions | `Permissions` table (seeded) |
| Addresses | `Addresses` table; optional FK to user or tenant |
| Seed tracking | `SeedHistory` table |

**SuperAdmin** cannot be created/modified/deleted/assigned inside tenant onboarding.

---

## Profiles & addresses

- **User / tenant profile image**: `ProfileFileId` → `Files`; `profileUrl` = `/api/v1/files/{id}/download`
- **Address**: separate `Addresses` row linked to user or tenant; responses include `line1`, `city`, … and `fullAddress`
- Update via `PUT /users`, `PUT /users/current`, `PUT /tenants` with `address` / `clearAddress`

---

## Tenant onboarding (SuperAdmin)

`POST /api/v1/tenants` requires `Tenants.Create`:

```json
{
  "tenant": { "name": "...", "slug": "..." },
  "user": { "fullName": "...", "email": "...", "password": "..." },
  "roles": [{ "name": "Admin", "description": "...", "permissions": ["<guid>", "..."] }]
}
```

---

## API conventions

### Response envelope

```json
{
  "data": { },
  "message": "Success message",
  "errors": null,
  "traceId": "..."
}
```

Errors mapped by `ExceptionHandlingMiddleware` to the same envelope.

### Pagination

`GET /users`, `GET /tenants`: `page` (default 1), `pageSize` (default 20, max 100).

### List scoping

| Resource | SuperAdmin | Tenant user |
|----------|------------|-------------|
| Users | All tenants (excl. self) + nested tenant | Current tenant only |
| Tenants | Paginated all | Own tenant only |
| Roles / products / files / reports | Platform rules | Current tenant |

---

## HTTP surface (v1)

| Area | Route |
|------|-------|
| Auth | `/api/v1/auth` |
| Health | `/api/v1/health` (+ `/health` EF probe) |
| Users | `/api/v1/users`, `/current` |
| Tenants | `/api/v1/tenants`, `/current` |
| Roles | `/api/v1/roles`, `/current` |
| Products | `/api/v1/products` |
| Permissions | `/api/v1/permissions` |
| Reports | `/api/v1/reports` |
| Files | `/api/v1/files` |

---

## Cross-cutting behavior

- **Soft delete** — `DeletedAt` / `DeletedBy`; global query filters
- **Audit fields** — stamped on `SaveChangesAsync` from JWT `user_id`
- **Activity logging** — auth and CRUD events to `ActivityLogs`
- **Request logging** — path, status, duration, tenant/user correlation
- **Caching** — permission catalog, roles, tenants, products, reports (`Caching` section in appsettings)
- **File storage** — local disk (`FileStorage:BasePath`)
- **Rate limiting** — auth endpoints (`10`/minute)
- **CORS** — `AllowedOrigins` in configuration

---

## Swagger

| Environment | URL | Access |
|-------------|-----|--------|
| Development | `/swagger` | Open |
| Production | `/swagger` | Login page (`admin@system.com` + `Seeding:AdminPassword` / `ADMIN_PASSWORD` secret) |

Use **Authorize** in Swagger UI with `Bearer {accessToken}` from `POST /auth/login`.

---

## Deployment

Production: **GitHub Actions** → **MonsterASP.NET** via FTPS. See [DEPLOYMENT.md](./DEPLOYMENT.md).

Startup on production:

- `ApplyMigrationsOnStartup: true`
- `ApplySeedsOnStartup: true`
- Secrets injected: connection string, JWT key, admin password, CORS origins

Post-deploy smoke test: `GET /api/v1/health` on `SITE_URL`.

---

## Local development

```powershell
dotnet ef database drop --force `
  --project src/Infrastructure/Infrastructure.csproj `
  --startup-project src/Api/Api.csproj   # optional — fresh start
dotnet run --project src/Api
```

- Swagger: `/swagger`
- SuperAdmin: `admin@system.com` / `Seeding:AdminPassword` from Development config or user secrets

---

## Intentionally not implemented

- Public user registration
- Permissions inside JWT
- Custom `UserRole` table
- `/api/v2`
- Redis, Hangfire, cloud blob storage, centralized log stacks

---

## Related documentation

- [API.md](./API.md) — endpoints, profiles, addresses, login
- [DEPLOYMENT.md](./DEPLOYMENT.md) — FTP deploy, secrets, migrations & seeds
- `src/Application/Common/PermissionNames.cs` — permission constants
