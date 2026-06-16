# Multi-Tenant Platform API (v1)

Architecture summary: [PROJECT.md](./PROJECT.md). Deploy: [DEPLOYMENT.md](./DEPLOYMENT.md).

Base URL: `/api/v1`

All JSON responses use the envelope:

```json
{
  "data": { },
  "message": "Success message",
  "errors": null,
  "traceId": "..."
}
```

---

## Health

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/api/v1/health` | No | API envelope; `data.status` = `"healthy"` |
| GET | `/health` | No | EF health check JSON (database probe) |

Deploy smoke test uses `/api/v1/health` on your public `SITE_URL`.

---

## Authentication

### Login — `POST /auth/login`

| Field | Required | Notes |
|-------|----------|--------|
| `email` | Yes | |
| `password` | Yes | |
| `tenantSlug` | No | **Omit** for SuperAdmin. **Required** for tenant users. |

Examples:

- SuperAdmin: `{ "email": "admin@system.com", "password": "..." }`
- Tenant user: `{ "email": "user@acme.com", "password": "...", "tenantSlug": "acme-corp" }`

### Refresh — `POST /auth/refresh`

Body: `{ "refreshToken": "..." }`

### Logout — `POST /auth/logout`

Body: `{ "refreshToken": "..." }`

### JWT claims

| Claim | Description |
|-------|-------------|
| `user_id` | User GUID |
| `tenant_id` | Tenant GUID (`Guid.Empty` for SuperAdmin) |
| `role_id` | Primary role GUID |
| `role` | Role name claims |

Permissions are checked per request from the database (cached), not from the token.

---

## Tenant onboarding (SuperAdmin)

### `POST /tenants`

Requires `Tenants.Create`. Creates tenant, roles (with permission GUIDs), and first admin in one transaction.

```json
{
  "tenant": { "name": "Acme Corp", "slug": "acme-corp" },
  "user": { "fullName": "Acme Admin", "email": "admin@acme.com", "password": "SecurePass123!" },
  "roles": [
    { "name": "Admin", "description": "Tenant administrator", "permissions": ["<guid>", "..."] }
  ]
}
```

Use `GET /permissions` for permission GUIDs.

---

## List scoping

| Endpoint | SuperAdmin | Tenant user |
|----------|------------|-------------|
| `GET /users` | All tenants (except self); includes nested `tenant` | Current tenant only |
| `GET /tenants` | Paginated all tenants | Single current tenant |
| `GET /roles` | — | Current tenant roles |
| `GET /products` | — | Current tenant (EF filter) |
| `GET /permissions` | Full catalog incl. `Tenants.*` | Tenant-safe (no `Tenants.*`) |

Mutating operations identify resources by **body** fields (email, slug, role name, product name) — not route IDs.

---

## Pagination

`GET /users`, `GET /tenants`:

| Query | Default | Max |
|-------|---------|-----|
| `page` | `1` | — |
| `pageSize` | `20` | `100` |

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## Profiles (user & tenant)

Responses include `profileFileId` and `profileUrl` (`/api/v1/files/{id}/download`) when set.

### User profile

- `GET /users/current` — own profile (+ optional nested `tenant` with profile/address)
- `PUT /users/current` — update `fullName`, optional `password`, profile image, address
- `PUT /users` — admin update by `email` in body

### Tenant profile

- `GET /tenants/current` — tenant profile + address
- `PUT /tenants` — update name, slug, active flag, profile image, address

### Profile image flow

1. `POST /files` — upload (`Files.Upload`)
2. `PUT /users/current` or `PUT /tenants` with `profileFileId: "<file-guid>"`
3. `clearProfileImage: true` — remove without replacing

---

## Addresses

Optional address on users and tenants. Returned on GET responses:

```json
"address": {
  "id": "...",
  "line1": "123 Main St",
  "line2": "Suite 4",
  "city": "Austin",
  "state": "TX",
  "postalCode": "78701",
  "country": "US",
  "fullAddress": "123 Main St, Suite 4, Austin, TX, 78701, US"
}
```

Update via `PUT /users`, `PUT /users/current`, or `PUT /tenants`:

```json
{
  "address": {
    "line1": "123 Main St",
    "line2": null,
    "city": "Austin",
    "state": "TX",
    "postalCode": "78701",
    "country": "US"
  },
  "clearAddress": false
}
```

Set `"clearAddress": true` to remove. Omit `address` to leave unchanged.

---

## Files

| Method | Path | Permission |
|--------|------|------------|
| GET | `/files` | `Files.View` |
| GET | `/files/{id}` | `Files.View` |
| GET | `/files/{id}/download` | `Files.View` |
| POST | `/files` | `Files.Upload` |
| DELETE | `/files/{id}` | `Files.Delete` |

Deleting a file clears any user/tenant `ProfileFileId` referencing it.

---

## Reports

| Method | Path | Permission |
|--------|------|------------|
| GET | `/reports/summary` | `Reports.View` |
| GET | `/reports/export` | `Reports.Export` |

---

## Permission names

PascalCase with module prefix: `Users.View`, `Products.Create`, `Tenants.Create`, etc.

Constants: `Application.Common.PermissionNames`.

---

## Swagger

| Environment | URL | Access |
|-------------|-----|--------|
| Development | `/swagger` | Open |
| Production | `/swagger` | Open |

Use **Authorize** with `Bearer {accessToken}` from login. Swagger auto-auth script persists tokens automatically.

---

## Endpoint index

| Area | Methods |
|------|---------|
| Auth | `POST login`, `refresh`, `logout` |
| Users | `GET`, `GET current`, `POST`, `PUT`, `PUT current`, `DELETE` |
| Tenants | `GET`, `GET current`, `POST`, `PUT`, `DELETE` |
| Roles | `GET`, `GET current`, `POST`, `PUT`, `DELETE` |
| Products | `GET`, `GET by-name`, `POST`, `PUT`, `DELETE` |
| Permissions | `GET` |
| Files | `GET`, `GET {id}`, `GET {id}/download`, `POST`, `DELETE` |
| Reports | `GET summary`, `GET export` |
| Health | `GET /api/v1/health` |
