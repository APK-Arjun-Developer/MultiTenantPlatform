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
| GET | `/health` | No | EF health check JSON (database + email probe) |

---

## Authentication

### Login — `POST /auth/login`

| Field | Required | Notes |
|-------|----------|-------|
| `email` | Yes | |
| `password` | Yes | |
| `tenantSlug` | Conditional | **Omit** for SystemAdmin. **Required** for TenantAdmin and TenantUser. |

Examples:

```json
// SystemAdmin
{ "email": "admin@system.com", "password": "Admin123!" }

// TenantAdmin / TenantUser
{ "email": "user@acme.com", "password": "...", "tenantSlug": "acme" }
```

Returns `accessToken` + `refreshToken` in the response body. Both are also set as HttpOnly cookies (`access_token`, `refresh_token`).

Login fails with HTTP 400 if:
- The user's email is not verified (`EmailConfirmed = false`)
- The user account is inactive (`IsActive = false`)

### Refresh — `POST /auth/refresh`

Uses the `refresh_token` HttpOnly cookie automatically. No request body needed.

### Logout — `POST /auth/logout`

Uses the `refresh_token` cookie. Revokes the token and clears both cookies.

### Me — `GET /auth/me`

Requires: `Authorization: Bearer {token}`

Returns user ID, full name, roles, tenant slug, and the caller's effective **permissions** list from the current JWT. Permissions are computed server-side (not stored in the token) and included here so the client has them available immediately after auth without a separate round-trip.

```json
{
  "id": "...",
  "email": "user@acme.com",
  "fullName": "Jane Smith",
  "roles": ["SalesRep"],
  "systemRole": "TenantUser",
  "tenantSlug": "acme",
  "permissions": ["Products.View", "Products.Create", "Reports.View"]
}
```

### JWT claims

| Claim | Description |
|-------|-------------|
| `user_id` | User GUID |
| `tenant_id` | Tenant GUID (`Guid.Empty` for SystemAdmin) |
| `system_role` | `1` = SystemAdmin, `2` = TenantAdmin, `3` = TenantUser |
| `full_name` | Display name |
| `role_ids` | GUIDs of custom roles assigned to the user |
| `email` | User email address |

Permissions are checked per request from the database (cached), not from the token.

---

## X-Tenant-Id header

SystemAdmin's JWT has `tenant_id = Guid.Empty`. To perform any tenant-scoped operation, SystemAdmin must send:

```
X-Tenant-Id: {tenantGuid}
```

Without this header, tenant-scoped endpoints return HTTP 400 ("Tenant context is required. Provide the X-Tenant-Id request header.").

TenantAdmin and TenantUser are always scoped to their JWT `tenant_id`. Sending `X-Tenant-Id` has no effect for these roles — it is silently ignored.

---

## Email verification

When `Features:RequireEmailVerification` is `true` (production default), new users must verify their email before logging in.

### Send OTP — `POST /auth/resend-verification`

Rate-limited. Always returns success (never reveals whether the account exists).

```json
{ "email": "user@acme.com", "tenantSlug": "acme" }
```

Omit `tenantSlug` for SystemAdmin accounts.

### Verify OTP — `POST /auth/verify-email`

Rate-limited.

```json
{ "email": "user@acme.com", "tenantSlug": "acme", "otp": "123456" }
```

On success, `EmailConfirmed` is set to `true` and the user can log in.

OTPs are 6 digits, valid for 15 minutes, single-use.

---

## Password reset

### Send reset link — `POST /auth/forgot-password`

Rate-limited. Always returns success (never reveals whether the account exists).

```json
{ "email": "user@acme.com", "tenantSlug": "acme" }
```

### Validate token — `GET /auth/reset-password/validate?token=...`

Rate-limited. Returns whether the token is valid.

### Reset password — `POST /auth/reset-password`

Rate-limited.

```json
{ "token": "...", "newPassword": "NewPass123!" }
```

---

## Tenant onboarding (SystemAdmin)

### `POST /tenants`

Requires `Tenants.Create`. Creates tenant, optional custom roles, and first TenantAdmin in one transaction.

```json
{
  "tenant": { "name": "Acme Corp", "slug": "acme" },
  "user": { "fullName": "Acme Admin", "email": "admin@acme.com", "password": "SecurePass123!" },
  "roles": [
    { "name": "SalesRep", "description": "Sales representative", "permissions": ["<permGuid>", "..."] }
  ]
}
```

`roles` is optional. Use `GET /permissions` for permission GUIDs.

In production (`RequireEmailVerification: true`), the TenantAdmin created here will have `EmailConfirmed = false` and must verify their email before logging in. In Development, they can log in immediately.

---

## Tenants

All routes require `Tenants.*` permissions (SystemAdmin only).

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/tenants` | `Tenants.View` | SystemAdmin: all tenants (paginated). TenantAdmin/TenantUser: own tenant only. |
| GET | `/tenants/{id}` | `Tenants.View` | SystemAdmin: any tenant. Others: own tenant only. |
| GET | `/tenants/current` | `Tenants.View` | TenantAdmin/TenantUser only. SystemAdmin gets HTTP 400. |
| POST | `/tenants` | `Tenants.Create` | Onboard new tenant (see above). SystemAdmin only. |
| PUT | `/tenants` | `Tenants.Edit` | Update name, slug, active flag, profile image, address. |
| DELETE | `/tenants` | `Tenants.Delete` | Soft-delete tenant. Fails if tenant still has users. |

---

## Tenant Admins (SystemAdmin)

All routes require SystemAdmin. `X-Tenant-Id` is not required for these routes — tenant is inferred from the TenantAdmin user record.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/tenant-admins` | `Tenants.View` | List all TenantAdmins; filter by `tenantId` query param |
| GET | `/tenant-admins/{id}` | `Tenants.View` | Get TenantAdmin by ID |
| POST | `/tenant-admins` | `Onboarding.Create` | Direct-create TenantAdmin; sends account-setup email |
| PUT | `/tenant-admins/{id}` | `Tenants.Edit` | Update TenantAdmin |
| DELETE | `/tenant-admins/{id}` | `Tenants.Delete` | Delete TenantAdmin |
| POST | `/tenant-admins/invite` | `Onboarding.Invite` | Invite prospective TenantAdmin by email |
| POST | `/tenant-admins/{userId}/resend` | `Onboarding.Resend` | Resend account-setup email |
| GET | `/tenant-admins/invitations` | `Tenants.View` | List TenantAdmin invitations (filter by `status`) |
| POST | `/tenant-admins/invitations/{id}/revoke` | `Onboarding.Revoke` | Revoke pending invitation |
| POST | `/tenant-admins/{userId}/activate` | `Onboarding.Activate` | Activate TenantAdmin account |
| POST | `/tenant-admins/{userId}/deactivate` | `Onboarding.Deactivate` | Deactivate TenantAdmin account |

---

## Users (TenantAdmin)

Requires `X-Tenant-Id` header for SystemAdmin. TenantAdmin/TenantUser are scoped automatically.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/users` | `Users.View` | List users in current tenant (excl. self) |
| GET | `/users/{id}` | `Users.View` | Get user by ID |
| GET | `/users/current` | Authenticated | Own profile (self-service; no permission required) |
| POST | `/users` | `Users.Create` | Create TenantUser with immediate password |
| POST | `/users/direct-create` | `Onboarding.Create` | Create TenantUser; sends account-setup email |
| POST | `/users/invite` | `Onboarding.Invite` | Invite prospective TenantUser by email |
| PUT | `/users` | `Users.Edit` | Update user by email in body |
| PUT | `/users/current` | Authenticated | Update own profile (self-service; no permission required) |
| POST | `/users/current/change-password` | Authenticated | Change own password (self-service; no permission required) |
| DELETE | `/users` | `Users.Delete` | Soft-delete user by email in body |
| POST | `/users/{userId}/resend` | `Onboarding.Resend` | Resend setup email for inactive user |
| GET | `/users/invitations` | `Onboarding.Invite` | List TenantUser invitations (filter by `status`) |
| POST | `/users/invitations/{id}/revoke` | `Onboarding.Revoke` | Revoke pending TenantUser invitation |
| POST | `/users/{userId}/activate` | `Onboarding.Activate` | Activate user account |
| POST | `/users/{userId}/deactivate` | `Onboarding.Deactivate` | Deactivate user account |

`POST /users` (direct creation) respects `RequireEmailVerification`. In production, the created user must verify their email before logging in.

---

## Roles (TenantAdmin)

Requires `X-Tenant-Id` header for SystemAdmin. Custom roles only — `SystemAdmin`, `TenantAdmin`, `TenantUser` are not in this table.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/roles` | `Roles.View` | List custom roles in current tenant |
| GET | `/roles/{name}` | `Roles.View` | Get role by name |
| GET | `/roles/current` | `Roles.View` | Caller's own role |
| POST | `/roles` | `Roles.Create` | Create custom role with permissions |
| PUT | `/roles` | `Roles.Edit` | Update role (name, description, permissions) by name in body |
| DELETE | `/roles/{name}` | `Roles.Delete` | Delete role by name |

`POST /roles` body:

```json
{
  "name": "SalesRep",
  "description": "Sales representative",
  "permissions": ["<permGuid>", "..."]
}
```

At least one permission is required. Permissions must be within the `TenantUser` scope — roles cannot escalate beyond a TenantUser's ceiling.

---

## Invitations (public, token-gated)

No authentication required. Access is controlled by the short-lived invitation token sent via email. Rate-limited.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/invitations/validate?token=...` | Validate before showing registration form. Returns email, type, tenant name, tenant slug. |
| POST | `/invitations/accept/tenant-admin` | Complete TenantAdmin invitation (provide name, password) |
| POST | `/invitations/accept/user` | Complete TenantUser invitation (provide name, password, role selection) |

---

## Account setup (public, token-gated)

No authentication required. Used for the direct-create flow (TenantAdmin creates user and sends setup email). Rate-limited.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/account-setup/validate?token=...` | Validate setup token. Returns email and name for pre-filling the form. |
| POST | `/account-setup/set-password` | Set password and activate the account. Token is consumed (single-use). |

---

## Products

Requires `X-Tenant-Id` for SystemAdmin. Scoped to current tenant.

| Method | Path | Permission |
|--------|------|------------|
| GET | `/products` | `Products.View` |
| GET | `/products/{id}` | `Products.View` |
| POST | `/products` | `Products.Create` |
| PUT | `/products` | `Products.Edit` |
| DELETE | `/products` | `Products.Delete` |

---

## Permissions

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/permissions` | `Roles.View` | SystemAdmin sees full catalog (incl. `Tenants.*`). TenantAdmin/TenantUser see tenant-safe subset only. |

Optional query param: `?grouped=true` returns permissions grouped by module.

---

## Files

Requires `X-Tenant-Id` for SystemAdmin. Files are scoped to the current tenant.

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

### Tenant-scoped (TenantAdmin / TenantUser)

Requires `X-Tenant-Id` for SystemAdmin. Counts are scoped to the current tenant.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/reports/summary` | `Reports.View` | User, role, product, and activity log counts for the current tenant |
| GET | `/reports/export` | `Reports.Export` | CSV download of the tenant summary |

### Platform-wide (SystemAdmin only)

No `X-Tenant-Id` required. Counts span all tenants.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/reports/platform-summary` | `Tenants.View` | Total tenants, users, products, and activity logs across the platform |
| GET | `/reports/platform-export` | `Tenants.View` | CSV download of the platform summary |

---

## Profiles (user & tenant)

Responses include `profileFileId` and `profileUrl` (`/api/v1/files/{id}/download`) when set.

### User profile

These endpoints are **self-service** — they require only `[Authorize]` (valid session), not any specific permission. Any authenticated user can access their own profile regardless of their assigned roles.

- `GET /users/current` — own profile (+ optional nested `tenant` with profile/address)
- `PUT /users/current` — update `fullName`, profile image, address
- `POST /users/current/change-password` — change own password
- `PUT /users` — admin update by user ID in body (requires `Users.Edit`)

### Tenant profile

- `GET /tenants/current` — tenant profile + address (TenantAdmin/TenantUser only)
- `PUT /tenants` — update name, slug, active flag, profile image, address

### Profile image flow

1. `POST /files` — upload (`Files.Upload`)
2. `PUT /users/current` or `PUT /tenants` with `"profileFileId": "<file-guid>"`
3. `"clearProfileImage": true` — remove without replacing

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

## Pagination

`GET /users`, `GET /tenants`, `GET /roles`, `GET /products`:

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

## Permission names

PascalCase with module prefix. Constants: `Application.Common.PermissionNames`.

| Module | Permissions | Minimum role |
|--------|-------------|--------------|
| `Profile` | `View`, `Edit` | TenantUser |
| `Products` | `View`, `Create`, `Edit`, `Delete` | TenantUser |
| `Reports` | `View`, `Export` | TenantUser |
| `Files` | `View`, `Upload` | TenantUser |
| `Files` | `Delete` | TenantAdmin |
| `Users` | `View`, `Create`, `Edit`, `Delete` | TenantAdmin |
| `Roles` | `View`, `Create`, `Edit`, `Delete` | TenantAdmin |
| `Onboarding` | `Create`, `Invite`, `Resend`, `Revoke`, `Activate`, `Deactivate` | TenantAdmin |
| `Tenants` | `View`, `Create`, `Edit`, `Delete` | SystemAdmin only |

---

## List scoping summary

| Endpoint | SystemAdmin (requires X-Tenant-Id) | TenantAdmin / TenantUser |
|----------|-------------------------------------|--------------------------|
| `GET /users` | Users of specified tenant | Current tenant only |
| `GET /tenants` | All tenants (paginated) | Own tenant (1 item) |
| `GET /roles` | Roles of specified tenant | Current tenant only |
| `GET /products` | Products of specified tenant | Current tenant only |
| `GET /files` | Files of specified tenant | Current tenant only |
| `GET /reports/summary` | Report for specified tenant | Current tenant only |
| `GET /reports/platform-summary` | Platform-wide totals (no tenant needed) | N/A — SystemAdmin only |
| `GET /permissions` | Full catalog (incl. `Tenants.*`) | Tenant-safe subset |

---

## Swagger

| Environment | URL | Access |
|-------------|-----|--------|
| Development | `/swagger` | Open |
| Production | `/swagger` | Open |

Use **Authorize** with `Bearer {accessToken}` from login.

---

## Endpoint index

| Area | Methods |
|------|---------|
| Auth | `POST login`, `refresh`, `logout`, `verify-email`, `resend-verification`, `forgot-password`, `reset-password`, `GET me` |
| Users | `GET`, `GET {id}`, `GET current`, `POST`, `POST direct-create`, `POST invite`, `PUT`, `PUT current`, `POST current/change-password`, `DELETE`, `POST {id}/resend`, `POST {id}/activate`, `POST {id}/deactivate`, `GET invitations`, `POST invitations/{id}/revoke` |
| Tenant Admins | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`, `POST invite`, `POST {id}/resend`, `POST {id}/activate`, `POST {id}/deactivate`, `GET invitations`, `POST invitations/{id}/revoke` |
| Tenants | `GET`, `GET {id}`, `GET current`, `POST`, `PUT`, `DELETE` |
| Roles | `GET`, `GET {name}`, `GET current`, `POST`, `PUT`, `DELETE {name}` |
| Products | `GET`, `GET {id}`, `POST`, `PUT`, `DELETE` |
| Permissions | `GET` |
| Files | `GET`, `GET {id}`, `GET {id}/download`, `POST`, `DELETE {id}` |
| Reports | `GET summary`, `GET export`, `GET platform-summary`, `GET platform-export` |
| Invitations | `GET validate`, `POST accept/tenant-admin`, `POST accept/user` |
| Account setup | `GET validate`, `POST set-password` |
| Health | `GET /api/v1/health`, `GET /health` |
