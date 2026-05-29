# Multi-Tenant Platform API (v1)

Base URL: `/api/v1`

All JSON responses use the same envelope:

```json
{
  "data": { },
  "message": "Success message",
  "errors": null,
  "traceId": "..."
}
```

Errors return `data: null`, a `message`, and optional `errors` (validation details, codes).

---

## Authentication

### Login — `POST /auth/login`

| Field | Required | Notes |
|-------|----------|--------|
| `email` | Yes | User email |
| `password` | Yes | |
| `tenantSlug` | No | **Omit** for platform **SuperAdmin** (`tenant_id` = `00000000-0000-0000-0000-000000000000`). **Required** for tenant users. Slug must match an active, non-deleted tenant (e.g. `acme-corp`). |

**Examples**

- SuperAdmin: `{ "email": "admin@system.com", "password": "..." }` — no `tenantSlug`
- Tenant user: `{ "email": "user@acme.com", "password": "...", "tenantSlug": "acme-corp" }`

### Refresh — `POST /auth/refresh`

Body: `{ "refreshToken": "..." }`

### Logout — `POST /auth/logout`

Body: `{ "refreshToken": "..." }`

### JWT claims

| Claim | Description |
|-------|-------------|
| `user_id` | Current user GUID |
| `tenant_id` | Tenant GUID (empty GUID for platform SuperAdmin) |
| `role_id` | Primary role GUID (optional) |
| Role names | Standard `role` claims (e.g. `SuperAdmin`) |

Permissions are **not** embedded in the JWT. Each request is checked against the database (with caching).

---

## Tenant onboarding (SuperAdmin)

### `POST /tenants`

Requires `Tenants.Create`. Creates a tenant, initial roles (with permission IDs), and the first admin user in one transaction.

```json
{
  "tenant": {
    "name": "Acme Corp",
    "slug": "acme-corp"
  },
  "user": {
    "fullName": "Acme Admin",
    "email": "admin@acme.com",
    "password": "SecurePass123!"
  },
  "roles": [
    {
      "name": "Admin",
      "description": "Tenant administrator",
      "permissions": ["<permission-guid>", "..."]
    }
  ]
}
```

Use `GET /permissions` (optionally `?grouped=true`) to obtain permission GUIDs for role setup.

---

## List scoping (who sees what)

| Endpoint | SuperAdmin (`tenant_id` empty) | Tenant user |
|----------|-------------------------------|-------------|
| `GET /users` | All users in all tenants (except self), includes `tenant` on each user | Users in **current tenant** only |
| `GET /tenants` | Paginated list of all tenants | Single current tenant (`totalCount: 1`) |
| `GET /roles` | N/A (platform scope) | Roles in **current tenant** |
| `GET /products` | N/A | Products in **current tenant** (EF filter) |
| `GET /permissions` | Full catalog including `Tenants.*` | Tenant-safe permissions only (no `Tenants.*`) |

---

## Pagination

Supported on:

- `GET /users?page=1&pageSize=20`
- `GET /tenants?page=1&pageSize=20`

| Query | Default | Max |
|-------|---------|-----|
| `page` | `1` | — |
| `pageSize` | `20` | `100` |

`data` shape:

```json
{
  "items": [ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## Current user profile

- `GET /users/current` — JWT user profile
- `PUT /users/current` — Update **own** `fullName` and optional `password` (requires `Users.Edit`). Cannot change email or role here.

Admin updates to other users: `PUT /users` with `email` in body.

---

## Permission names

PascalCase with module prefix, e.g. `Users.View`, `Products.Create`, `Tenants.Onboard` is not used — use `Tenants.Create` for onboarding.

---

## Swagger

Run the API in Development and open `/swagger` for interactive docs. Use **Authorize** with `Bearer {accessToken}`.
