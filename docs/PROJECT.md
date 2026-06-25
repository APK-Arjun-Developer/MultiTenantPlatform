# Multi-Tenant Platform — Project Summary

Canonical architecture and workflow summary. For endpoints see [API.md](./API.md); for deploy see [DEPLOYMENT.md](./DEPLOYMENT.md).

---

## Overview

A **multi-tenant SaaS backend** on **.NET 10** with:

- Complete tenant isolation via `TenantId`, EF global query filters, and middleware
- **JWT** access tokens + **refresh tokens** (HttpOnly cookies)
- **Hybrid RBAC**: `SystemRole` ceiling authority + custom permission-based roles
- **ASP.NET Core Identity** for users and roles (`Guid` keys)
- **Email verification** via OTP (6-digit, 15-minute lifetime); dev mode bypass available
- **Versioned database seeds** tracked in `SeedHistory` (like EF migrations)
- No public self-registration — tenants onboarded by platform **SystemAdmin**

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
| API docs | Swagger (open in Development and Production) |
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

## Authorization model (hybrid two-layer RBAC)

### Layer 1 — SystemRole (ceiling authority)

Every `ApplicationUser` has a `SystemRole` enum stored on the user record and embedded in the JWT as the `system_role` claim:

| Value | Name | Scope |
|-------|------|-------|
| `1` | `SystemAdmin` | Platform-wide. Manages all tenants. Never scoped to a single tenant. |
| `2` | `TenantAdmin` | Scoped to one tenant. Manages users, roles, and onboarding within that tenant. |
| `3` | `TenantUser` | Scoped to one tenant. Operational access only (products, files, reports, profile). |

`SystemRole` is a **ceiling** — it limits which permissions can ever be granted to a user. A role cannot grant a permission whose `Scope` exceeds the user's `SystemRole`.

`SystemAdmin` and `TenantAdmin` have their authority from `SystemRole` alone and do not need custom roles. Custom roles only exist to assign **TenantUser-scoped** business permissions to TenantUsers.

### Layer 2 — Custom roles and permissions

Custom roles live in the `Roles` table and are always scoped to a single `TenantId`. Each role has a set of `Permissions` (seeded catalog, PascalCase names).

| Permission module | Minimum `SystemRole` | Who can hold it |
|-------------------|---------------------|-----------------|
| `Profile.*` | TenantUser | TenantUser, TenantAdmin, SystemAdmin |
| `Products.*` | TenantUser | TenantUser, TenantAdmin, SystemAdmin |
| `Reports.*` | TenantUser | TenantUser, TenantAdmin, SystemAdmin |
| `Files.View`, `Files.Upload` | TenantUser | TenantUser, TenantAdmin, SystemAdmin |
| `Users.*`, `Roles.*` | TenantAdmin | TenantAdmin, SystemAdmin |
| `Onboarding.*` | TenantAdmin | TenantAdmin, SystemAdmin |
| `Files.Delete` | TenantAdmin | TenantAdmin, SystemAdmin |
| `Tenants.*` | SystemAdmin | SystemAdmin only |

Permission names are defined in `Application.Common.PermissionNames`. The full catalog is available at `GET /api/v1/permissions`.

### Permission check flow

```text
Request arrives → JWT validated → system_role extracted
    → if SystemAdmin: granted all permissions
    → else: load role_ids from JWT → fetch permissions from DB (cached)
           → check permission ceiling against system_role
```

Permissions are **not stored in the JWT** — they are computed per request from the database (with `IMemoryCache`).

`GET /api/v1/auth/me` returns the caller's effective `permissions: string[]` alongside identity data. Clients use this response (which `[Authorize]` already blocks until resolved) to gate UI elements without a separate permissions round-trip.

---

## Tenant isolation

### X-Tenant-Id header

- **SystemAdmin**: `tenant_id` in JWT is `Guid.Empty`. To perform any tenant-scoped operation, SystemAdmin **must** supply the `X-Tenant-Id` request header. Without it, tenant-scoped endpoints return HTTP 400.
- **TenantAdmin / TenantUser**: `tenant_id` is fixed in the JWT. The `X-Tenant-Id` header is accepted but **ignored** — the JWT `tenant_id` is always authoritative for non-SystemAdmin callers. This prevents header-spoofing attacks.

### EF global query filters

All entities implementing `ITenantEntity` + `IAuditableEntity` (i.e., all `BaseEntity` subclasses: `Product`, `FileEntity`, `ActivityLog`, etc.) have a global EF query filter:

```csharp
(!_currentTenantService.TenantId.HasValue || e.TenantId == _currentTenantService.TenantId)
&& e.DeletedAt == null
```

This filter is applied automatically to every LINQ query. Service methods also add explicit `WHERE TenantId = @tenantId` conditions for clarity and defense-in-depth.

`Invitation` does not extend `BaseEntity` and has no global filter; it is protected by explicit service-level checks.

### TenantScopedService

All tenant-scoped services inherit `TenantScopedService` which exposes:

- `RequireTenantId()` — returns the current tenant ID or throws HTTP 400 if it is missing (SystemAdmin without header)
- `IsSystemAdmin()` — true when `system_role == 1`
- `RequireUserId()` — returns the current user ID

---

## Authentication

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/auth/login` | Access token + refresh token (HttpOnly cookies) |
| POST | `/api/v1/auth/refresh` | Rotate tokens using `refresh_token` cookie |
| POST | `/api/v1/auth/logout` | Revoke refresh token; clear cookies |
| POST | `/api/v1/auth/verify-email` | Verify email address with OTP |
| POST | `/api/v1/auth/resend-verification` | Re-send email verification OTP |
| POST | `/api/v1/auth/forgot-password` | Send password reset link |
| GET | `/api/v1/auth/reset-password/validate` | Validate password reset token |
| POST | `/api/v1/auth/reset-password` | Complete password reset |
| GET | `/api/v1/auth/me` | Current user's identity + effective `permissions[]` computed server-side |

Tokens are set as **HttpOnly cookies** (`access_token`, `refresh_token`). The access token is also returned in the response body for clients that cannot use cookies.

### JWT claims

| Claim | Description |
|-------|-------------|
| `user_id` | User GUID |
| `tenant_id` | Tenant GUID (`Guid.Empty` for SystemAdmin) |
| `system_role` | `1` = SystemAdmin, `2` = TenantAdmin, `3` = TenantUser |
| `full_name` | Display name |
| `role_ids` | GUIDs of custom roles assigned to the user |
| `email` | User email |

Permissions are **not in the JWT** — loaded from DB per request (cached).

### Login rules

- **SystemAdmin**: omit `tenantSlug`. Login finds the user by email where `TenantId == Guid.Empty`.
- **TenantAdmin / TenantUser**: `tenantSlug` is required. The slug must match an active, non-deleted tenant.
- Login blocks users with `EmailConfirmed = false` ("Your email address has not been verified").
- Login blocks users with `IsActive = false`.

---

## Email verification

New user accounts are created with `EmailConfirmed` based on the `Features:RequireEmailVerification` setting:

| Environment | `RequireEmailVerification` | `EmailConfirmed` on creation | Login allowed immediately? |
|-------------|---------------------------|------------------------------|---------------------------|
| Development | `false` | `true` | Yes |
| Production | `true` | `false` | No — OTP required |

When `RequireEmailVerification: true`:
1. User is created with `EmailConfirmed = false`.
2. A 6-digit OTP is generated, hashed, stored in `EmailVerificationOtps`, and emailed.
3. The user calls `POST /auth/verify-email` with `{ email, tenantSlug, otp }`.
4. On success, `EmailConfirmed` is set to `true`; the user can now log in.
5. `POST /auth/resend-verification` issues a fresh OTP (invalidates the previous one).

This applies to users created via:
- `POST /api/v1/tenants` (tenant onboarding — TenantAdmin)
- `POST /api/v1/users` (direct user creation)

Users created via the account-setup flow (`direct-create` / invitation) go through a separate token-gated flow and their `EmailConfirmed` is set to `true` on account activation.

---

## User creation flows

There are three ways to create users:

### 1. Tenant onboarding (SystemAdmin)

`POST /api/v1/tenants` creates a new tenant and its first TenantAdmin in one transaction.

### 2. Direct creation (TenantAdmin)

`POST /api/v1/users` — creates a TenantUser immediately with the provided password. Email verification applies if enabled.

`POST /api/v1/tenant-admins` (SystemAdmin only) — creates a new TenantAdmin. Sends an account-setup email; the account is inactive until the user sets their password via the setup link.

`POST /api/v1/users/direct-create` — TenantAdmin direct-creates a TenantUser. Sends an account-setup email; user activates via the link.

### 3. Invitation flow

`POST /api/v1/tenant-admins/invite` (SystemAdmin) — invites a prospective TenantAdmin by email.

`POST /api/v1/users/invite` (TenantAdmin) — invites a prospective TenantUser by email.

The invited user receives a tokenized link and registers via:
- `GET /api/v1/invitations/validate?token=...` — validate before showing the form
- `POST /api/v1/invitations/accept/tenant-admin` — accept TenantAdmin invitation
- `POST /api/v1/invitations/accept/user` — accept TenantUser invitation

Account-setup links (direct-create flow):
- `GET /api/v1/account-setup/validate?token=...` — validate setup token
- `POST /api/v1/account-setup/set-password` — set password and activate account

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
| `20260603000003_SuperAdmin` | SystemAdmin role + `admin@system.com` (requires `Seeding:AdminPassword`) |

Add a seed: new class in `Persistence/Seed/Seeds/`, register in `Persistence/DependencyInjection.cs`.

### Fresh database

| Environment | Approach |
|-------------|----------|
| Local | `dotnet ef database drop --force` then `dotnet run --project src/Api` |
| Production | Delete/recreate database in MonsterASP control panel, or drop all tables in SSMS, then redeploy |

---

## Identity model

| Concept | Implementation |
|---------|----------------|
| Users | `ApplicationUser` + `TenantId`, `SystemRole`, `FullName`, `ProfileFileId`, soft delete |
| Roles | `ApplicationRole` + `TenantId`, `Description` (custom roles only; no built-in role rows) |
| User ↔ role | Identity `AspNetUserRoles` |
| Role ↔ permission | `RolePermissions` |
| Permissions | `Permissions` table (seeded) |
| Email verification | `EmailVerificationOtps` (UserId, OtpHash, ExpiresAt, UsedAt) |
| Addresses | `Addresses` table; optional FK to user or tenant |
| Seed tracking | `SeedHistory` table |

`SystemAdmin`, `TenantAdmin`, and `TenantUser` exist **only** as the `SystemRole` enum on `ApplicationUser`. There are no corresponding rows in the `Roles` table. The `Roles` table contains only custom tenant-scoped roles.

---

## Profiles & addresses

- **User / tenant profile image**: `ProfileFileId` → `Files`; `profileUrl` = `/api/v1/files/{id}/download`
- **Address**: separate `Addresses` row linked to user or tenant; responses include `line1`, `city`, … and `fullAddress`
- Set at creation: optional `address` field accepted by `POST /tenant-admins`, `POST /users`, `POST /users/direct-create`, `POST /invitations/accept/tenant-admin`, `POST /invitations/accept/user`, `POST /account-setup/set-password`
- Update after creation via `PUT /users`, `PUT /users/current`, or `PUT /tenants` with `address` / `clearAddress`

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

Errors are mapped by `ExceptionHandlingMiddleware` to the same envelope.

### Pagination

`GET /users`, `GET /tenants`: `page` (default 1), `pageSize` (default 20, max 100).

### List scoping

| Resource | SystemAdmin (with X-Tenant-Id) | TenantAdmin / TenantUser |
|----------|-------------------------------|--------------------------|
| Users | Users of the specified tenant | Own tenant only |
| Tenants | All tenants (paginated) | Own tenant only |
| Roles | Roles of the specified tenant | Own tenant only |
| Products / files / reports | Filtered to specified tenant | Own tenant only |
| Permissions | Full catalog (incl. `Tenants.*`) | Tenant-safe (no `Tenants.*`) |

---

## HTTP surface (v1)

| Area | Route |
|------|-------|
| Auth | `/api/v1/auth` |
| Health | `/api/v1/health` (+ `/health` EF probe) |
| Users | `/api/v1/users`, `/current` |
| Tenant Admins | `/api/v1/tenant-admins` (SystemAdmin only) |
| Tenants | `/api/v1/tenants`, `/current` |
| Roles | `/api/v1/roles`, `/current` |
| Products | `/api/v1/products` |
| Permissions | `/api/v1/permissions` |
| Reports | `/api/v1/reports` (`summary`, `export`, `platform-summary`, `platform-export`) |
| Files | `/api/v1/files` |
| Invitations | `/api/v1/invitations` (public, token-gated) |
| Account setup | `/api/v1/account-setup` (public, token-gated) |

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

## Feature flags

Defined in `Application.Options.FeatureOptions` (`appsettings.json` section `Features`):

| Flag | Development default | Production default | Purpose |
|------|--------------------|--------------------|---------|
| `RequireEmailVerification` | `false` | `true` | Controls whether new users must verify their email via OTP before logging in |

---

## Swagger

| Environment | URL | Access |
|-------------|-----|--------|
| Development | `/swagger` | Open |
| Production | `/swagger` | Open (toggle via `Swagger:EnabledInProduction`) |

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
- SystemAdmin: `admin@system.com` / `Seeding:AdminPassword` from Development config or user secrets
- Email verification is disabled in Development (`Features:RequireEmailVerification: false`)

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
- `src/Application/Common/PermissionNames.cs` — permission constants and scope map
