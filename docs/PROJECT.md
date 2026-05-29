# Multi-Tenant Platform — Project Summary

This document is the **canonical project summary** for the codebase. It reflects what is implemented today (Phases 1–4). For endpoint-level details, see [API.md](./API.md).

---

## Overview

A **multi-tenant SaaS backend** on **.NET 10** with:

- Tenant isolation via `TenantId`, EF Core global query filters, and middleware
- **JWT** access tokens + **refresh tokens** (login / refresh / logout)
- **Permission-based RBAC** checked per request against the database (with in-memory caching)
- **ASP.NET Core Identity** for users and roles (`Guid` keys)
- No public self-registration — tenants are onboarded by a platform **SuperAdmin**

---

## Technology stack

| Area | Choice |
|------|--------|
| Runtime | **.NET 10** (`net10.0`) |
| API | ASP.NET Core Web API, `/api/v1` |
| Data | EF Core 10, SQL Server |
| Auth | JWT Bearer + ASP.NET Core Identity |
| Validation | FluentValidation |
| Logging | Serilog (host + request middleware enrichers) |
| Docs | Swagger (Development), [API.md](./API.md) |

---

## Solution layout

```text
src/
  Api/              HTTP pipeline, controllers, middleware, API envelope
  Application/      DTOs, validators, interfaces, permission/role constants
  Domain/           Entities, contracts (ITenantEntity, IAuditableEntity)
  Infrastructure/   EF DbContext, Identity, services, caching, file storage
  Shared/           Shared utilities (minimal today)
```

Services live in **Infrastructure** and are registered from `Infrastructure` DI extensions. There is **no generic `IRepository<>`** — data access uses `ApplicationDbContext` and feature services directly.

---

## Multi-tenancy

- Every tenant-scoped row carries **`TenantId`** (`ITenantEntity`).
- **EF global filters** hide rows where `DeletedAt != null` (soft delete) and scope tenant data where applicable.
- **`TenantMiddleware`** resolves the current tenant from the JWT `tenant_id` claim after authentication.
- Platform **SuperAdmin** users have `tenant_id = 00000000-0000-0000-0000-000000000000` (`Guid.Empty`). Login omits `tenantSlug`; tenant users must send a matching `tenantSlug`.

---

## Authentication

### Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/v1/auth/login` | Issue access + refresh tokens |
| POST | `/api/v1/auth/refresh` | Rotate tokens |
| POST | `/api/v1/auth/logout` | Revoke refresh token |

### JWT claims (access token)

| Claim | Description |
|-------|-------------|
| `user_id` | Current user GUID |
| `tenant_id` | Tenant GUID (`Guid.Empty` for SuperAdmin) |
| `role_id` | Primary role GUID (when applicable) |
| `full_name` | Display name |
| Standard `role` claims | Role names (e.g. `SuperAdmin`) |

**Permissions are not embedded in the JWT.** Authorization loads effective permissions from the database (role → permission mappings) on each request, with request-scoped and `IMemoryCache` caching.

### Refresh tokens

Stored in the database; validated on refresh and revoked on logout.

---

## Authorization

- Controllers/actions use **`[HasPermission("Module.Action")]`** (PascalCase permission names).
- Policies are resolved by `PermissionPolicyProvider`; requirements are handled by `PermissionAuthorizationHandler`.
- **SuperAdmin** receives all permissions from the catalog (cached).
- **Tenant users** receive the union of permissions assigned to their roles in the current tenant.

### Permission naming

Permissions use **PascalCase** with a module prefix, for example:

- `Users.View`, `Users.Create`, `Users.Edit`, `Users.Delete`
- `Roles.*`, `Products.*`, `Reports.*`, `Files.*`
- `Tenants.*` (platform scope only — hidden from tenant permission catalog)

Constants: `Application.Common.PermissionNames`.  
Catalog API: `GET /api/v1/permissions` (optional `?grouped=true`).

---

## Identity model

| Concept | Implementation |
|---------|----------------|
| Users | `ApplicationUser` : `IdentityUser<Guid>` + `TenantId`, `FullName`, soft-delete `DeletedAt` |
| Roles | `ApplicationRole` : `IdentityRole<Guid>` + `TenantId`, `Description`, soft-delete `DeletedAt` |
| User ↔ role | **ASP.NET Identity** `AspNetUserRoles` (`IdentityUserRole<Guid>`) — **no custom UserRole table** |
| Role ↔ permission | `RolePermissions` join table (`Domain.Entities.RolePermission`) |
| Permissions | `Permissions` master table (seeded) |

The reserved platform role name **`SuperAdmin`** cannot be created, modified, deleted, or assigned inside tenant onboarding.

---

## Tenant onboarding (SuperAdmin)

`POST /api/v1/tenants` requires `Tenants.Create`. Body shape:

```json
{
  "tenant": { "name": "...", "slug": "..." },
  "user": { "fullName": "...", "email": "...", "password": "..." },
  "roles": [
    {
      "name": "Admin",
      "description": "...",
      "permissions": ["<permission-guid>", "..."]
    }
  ]
}
```

Use `GET /api/v1/permissions` to obtain permission GUIDs for role setup.

---

## API conventions

### Response envelope

All JSON responses use:

```json
{
  "data": { },
  "message": "Success message",
  "errors": null,
  "traceId": "..."
}
```

Errors set `data` to `null`, include a `message`, and optional `errors` (validation details). Unhandled exceptions are mapped by `ExceptionHandlingMiddleware` to the same envelope (not RFC 7807 ProblemDetails).

### Pagination

`GET /api/v1/users` and `GET /api/v1/tenants` support `page` (default 1) and `pageSize` (default 20, max 100). Response `data` is a `PagedResponse<T>`.

### List scoping

| Resource | SuperAdmin | Tenant user |
|----------|------------|-------------|
| Users | All tenants (except self in list) | Current tenant only |
| Tenants | Paginated all | Single current tenant |
| Roles / products / files / reports | Platform rules vary | Current tenant |
| Permissions | Full catalog incl. `Tenants.*` | Tenant-safe set (no `Tenants.*`) |

See [API.md](./API.md) for login rules and profile endpoints.

---

## HTTP surface (v1)

| Area | Base route |
|------|------------|
| Auth | `/api/v1/auth` |
| Users | `/api/v1/users` (+ `GET`/`PUT` `/current`) |
| Tenants | `/api/v1/tenants` |
| Roles | `/api/v1/roles` |
| Products | `/api/v1/products` |
| Permissions | `/api/v1/permissions` |
| Reports | `/api/v1/reports` (`summary`, `export`) |
| Files | `/api/v1/files` |

---

## Cross-cutting behavior

### Soft delete

Entities with `DeletedAt` (including `ApplicationUser`, `ApplicationRole`, and `BaseEntity` types) are filtered with `DeletedAt == null`. Delete operations set `DeletedAt` / `DeletedBy` instead of removing rows.

### Audit fields

On `SaveChangesAsync`, `CreatedAt` / `UpdatedAt` / `DeletedAt` and `CreatedBy` / `UpdatedBy` / `DeletedBy` are stamped from the JWT `user_id` where applicable.

### Activity logging

`IActivityLogService` records auth events and significant CRUD actions to `ActivityLogs`.

### Request logging

`RequestLoggingMiddleware` logs path, status, duration, and Serilog properties `tenant_id` / `user_id`.

### Caching (`IMemoryCache` via `IAppCache`)

Cached with TTL from `appsettings.json` → `Caching`:

- Permission catalog (system and tenant views)
- Role → permission snapshots
- Tenant catalog / detail
- Product lists
- Report summaries

Cache entries are invalidated on relevant writes. **User list endpoints are not cached.**

### File storage

Local disk implementation (`FileStorage:BasePath` in configuration). Upload, list, download, and soft-delete via `/api/v1/files`.

---

## Intentionally not implemented

- Public user registration
- Permissions inside JWT
- Custom `UserRole` table (Identity `AspNetUserRoles` is correct)
- `/api/v2` (deferred until breaking changes)
- Redis, Hangfire, CQRS, cloud blob storage, centralized log stacks (see backlog below)

---

## Backlog (Phase 6)

Deferred unless explicitly prioritized:

- Redis distributed cache
- Hangfire background jobs
- CQRS / domain events / microservices split
- S3 / Azure Blob file providers
- Seq / Elasticsearch / Grafana observability stack
- API v2

---

## Deployment

Production deploys to **MonsterASP.NET** (free plan) via **GitHub Actions** and **FTP**. Secrets are injected at deploy time; see [DEPLOYMENT.md](./DEPLOYMENT.md).

---

## Local development

```bash
# Apply migrations
dotnet ef database update --project src/Infrastructure --startup-project src/Api

# Run API
dotnet run --project src/Api
```

Swagger: `https://localhost:<port>/swagger` (Development).  
Default seeded SuperAdmin: `admin@system.com` / `Admin123!` (change in production).

---

## Related documentation

- [API.md](./API.md) — envelope, login, onboarding, scoping, pagination
- Permission constants: `src/Application/Common/PermissionNames.cs`
