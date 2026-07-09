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

```json
{ "email": "admin@system.com", "password": "Admin123!" }
```

Returns `accessToken` + `refreshToken` in the response body. Both are also set as HttpOnly cookies (`access_token`, `refresh_token`).

Login fails with HTTP 400 if:
- The user's email is not verified (`EmailConfirmed = false`)
- The user account is inactive (`IsActive = false`)
- The user record is soft-deleted (`DeletedAt != null`) — returns "Your account has been deactivated. Please contact your administrator." (same message as inactive, to avoid revealing deletion state to the caller)

### Refresh — `POST /auth/refresh`

Uses the `refresh_token` HttpOnly cookie automatically. No request body needed.

### Logout — `POST /auth/logout`

Uses the `refresh_token` cookie. Revokes the token and clears both cookies.

### Me — `GET /auth/me`

Requires: `Authorization: Bearer {token}`

Returns user ID, full name, roles, and the caller's effective **permissions** list from the current JWT. Permissions are computed server-side (not stored in the token) and included here so the client has them available immediately after auth without a separate round-trip.

```json
{
  "id": "...",
  "email": "user@acme.com",
  "fullName": "Jane Smith",
  "roles": ["SalesRep"],
  "systemRole": "TenantUser",
  "permissions": ["Products.View", "Products.Create", "Reports.View"]
}
```

### Me — `GET /auth/me` (updated)

When the calling user is being impersonated by a SystemAdmin, the `impersonatedBy` field is populated:

```json
{
  "id": "...", "email": "jane@acme.com", "fullName": "Jane Smith",
  "roles": ["SalesRep"], "systemRole": "TenantUser",
  "permissions": ["Products.View"],
  "impersonatedBy": {
    "id": "...", "email": "admin@system.com", "fullName": "System Admin"
  }
}
```

`impersonatedBy` is `null` for normal (non-impersonated) sessions.

### JWT claims

| Claim | Description |
|-------|-------------|
| `user_id` | User GUID |
| `tenant_id` | Tenant GUID (`Guid.Empty` for SystemAdmin) |
| `system_role` | `1` = SystemAdmin, `2` = TenantAdmin, `3` = TenantUser |
| `full_name` | Display name |
| `role_ids` | GUIDs of custom roles assigned to the user |
| `email` | User email address |
| `impersonated_by_id` | (impersonation only) GUID of the admin who started impersonation |
| `impersonated_by_email` | (impersonation only) Admin's email |
| `impersonated_by_name` | (impersonation only) Admin's full name |

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
{ "email": "user@acme.com" }
```

### Verify OTP — `POST /auth/verify-email`

Rate-limited.

```json
{ "email": "user@acme.com", "otp": "123456" }
```

On success, `EmailConfirmed` is set to `true` and the user can log in.

OTPs are 6 digits, valid for 15 minutes, single-use.

---

## Password reset

### Send reset link — `POST /auth/forgot-password`

Rate-limited. Always returns success (never reveals whether the account exists).

```json
{ "email": "user@acme.com" }
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
  "tenant": { "name": "Acme Corp" },
  "user": { "fullName": "Acme Admin", "email": "admin@acme.com", "password": "SecurePass123!" },
  "roles": [
    { "name": "SalesRep", "description": "Sales representative", "permissions": ["<permGuid>", "..."] }
  ]
}
```

`roles` is optional. Use `GET /permissions` for permission GUIDs.

In production (`RequireEmailVerification: true`), the TenantAdmin created here will have `EmailConfirmed = false` and must verify their email before logging in. In Development, they can log in immediately.

---

## Dashboard

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/dashboard/stats` | `TenantAdminOrAbove` policy | Caller-scoped stats. SystemAdmin: `totalTenants`, `totalTenantAdmins`, `totalTenantUsers` (platform-wide). TenantAdmin: `totalTenantUsers` for own tenant; the other two fields are `null`. |

```json
// SystemAdmin
{ "totalTenants": 12, "totalTenantAdmins": 15, "totalTenantUsers": 240 }

// TenantAdmin
{ "totalTenants": null, "totalTenantAdmins": null, "totalTenantUsers": 18 }
```

---

## Tenants

All routes require `Tenants.*` permissions (SystemAdmin only).

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/tenants` | `Tenants.List` | SystemAdmin: all tenants (paginated). TenantAdmin/TenantUser: own tenant only. Responses include `createdVia: "Direct" \| "Invitation"` and `adminEmail` (primary TenantAdmin's email). |
| GET | `/tenants/{id}` | `Tenants.View` | SystemAdmin: any tenant. Others: own tenant only. |
| GET | `/tenants/current` | `Tenants.View` | TenantAdmin/TenantUser only. SystemAdmin gets HTTP 400. |
| POST | `/tenants` | `Tenants.Create` | Direct-create tenant + admin user; sends account-setup email. Accepts `user.address` and `tenant.address`. |
| PUT | `/tenants` | `Tenants.Edit` | Update name, active flag, profile image, address. Body: `{ id, name, isActive, ... }`. |
| PUT | `/tenants/current/address` | Authenticated (TenantAdmin only) | Update own tenant's address. No permission attribute — controller checks `system_role == 2`. |
| DELETE | `/tenants` | `Tenants.Delete` | Soft-delete tenant. Body: `{ id }`. |
| GET | `/tenants/invitations` | `Tenants.List` | List new-tenant creation invitations (filter by `status`). |
| POST | `/tenants/invite` | `Tenants.Create` | Send new-tenant invitation by email. Admin enters email only; invited user sets up tenant name, addresses, and password via the invitation link. |
| POST | `/tenants/invitations/{id}/revoke` | `Tenants.Create` | Revoke a pending new-tenant invitation. |
| POST | `/tenants/invitations/{id}/resend` | `Tenants.Create` | Resend a pending new-tenant invitation email (regenerates token, extends expiry). Only works when invitation is not yet accepted, revoked, or expired. |
| POST | `/tenants/{id}/logo` | `Tenants.Edit` | SystemAdmin: upload company logo for any tenant (JPEG/PNG/GIF/WebP, max 10 MB). Multipart form-data with `file` field. |
| DELETE | `/tenants/{id}/logo` | `Tenants.Edit` | SystemAdmin: remove company logo for any tenant. |

---

## Tenant Admins (SystemAdmin)

All routes require SystemAdmin. `X-Tenant-Id` is not required for these routes — tenant is inferred from the TenantAdmin user record.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/tenant-admins` | `Tenants.List` | List all TenantAdmins; filter by `tenantId` query param |
| GET | `/tenant-admins/{id}` | `Tenants.View` | Get TenantAdmin details by ID |
| POST | `/tenant-admins` | `Onboarding.Create` | Direct-create TenantAdmin; body requires `tenantId` (Guid); sends account-setup email; optional `address` saved immediately |
| PUT | `/tenant-admins/{id}` | `Tenants.Edit` | Update TenantAdmin |
| DELETE | `/tenant-admins/{id}` | `Tenants.Delete` | Delete TenantAdmin. Returns HTTP 409 if the admin is the last one for their tenant (a tenant must always have at least one admin). |
| POST | `/tenant-admins/invite` | `Onboarding.Invite` | Invite prospective TenantAdmin by email; body requires `tenantId` (Guid) and `email` |
| POST | `/tenant-admins/{userId}/resend` | `Onboarding.Resend` | Resend account-setup email |
| GET | `/tenant-admins/invitations` | `Tenants.List` | List TenantAdmin invitations (filter by `status`) |
| POST | `/tenant-admins/invitations/{id}/revoke` | `Onboarding.Revoke` | Revoke pending invitation |
| POST | `/tenant-admins/invitations/{id}/resend` | `Onboarding.Resend` | Resend pending invitation email (regenerates token, extends expiry). Only works when invitation is not yet accepted, revoked, or expired. |
| POST | `/tenant-admins/{userId}/activate` | `Onboarding.Activate` | Activate TenantAdmin account |
| POST | `/tenant-admins/{userId}/deactivate` | `Onboarding.Deactivate` | Deactivate TenantAdmin account |

---

## Users (TenantAdmin)

Requires `X-Tenant-Id` header for SystemAdmin. TenantAdmin/TenantUser are scoped automatically.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/users` | `Users.List` | List **TenantUser**-role users in current tenant (excl. self, excl. SystemAdmin and TenantAdmin) |
| GET | `/users/{id}` | `Users.View` | Get user details by ID |
| GET | `/users/current` | Authenticated | Own profile (self-service; no permission required) |
| POST | `/users` | `Users.Create` | Create TenantUser with immediate password; optional `address` saved immediately |
| POST | `/users/direct-create` | `Onboarding.Create` | Create TenantUser; sends account-setup email; optional `address` saved immediately |
| POST | `/users/invite` | `Onboarding.Invite` | Invite prospective TenantUser by email |
| PUT | `/users` | `Users.Edit` | Update user by email in body |
| PUT | `/users/current` | Authenticated | Update own profile (self-service; no permission required) |
| POST | `/users/current/avatar` | Authenticated | Upload own profile picture (JPEG/PNG/GIF/WebP, max 5 MB, **must be square**). Returns updated `UserDto`. |
| DELETE | `/users/current/avatar` | Authenticated | Remove own profile picture. |
| GET | `/users/{id}/avatar` | Authenticated | Stream any user's profile picture image. No permission required — bypasses tenant scope for cross-tenant visibility (e.g., SystemAdmin viewing TenantAdmin avatars). |
| POST | `/users/{id}/avatar` | `Users.Edit` | Admin: upload profile picture for any user in the current tenant. Multipart form-data with `avatar` field. |
| DELETE | `/users/{id}/avatar` | `Users.Edit` | Admin: remove profile picture for any user in the current tenant. |
| POST | `/users/current/change-password` | Authenticated | Change own password (self-service; no permission required) |
| DELETE | `/users` | `Users.Delete` | Soft-delete user by email in body |
| POST | `/users/{userId}/resend` | `Onboarding.Resend` | Resend account-setup email. Only applicable when `hasPendingSetup` is `true` (directly-created user who has not yet completed setup). User responses include `createdVia: "Direct" \| "Invitation"`. |
| GET | `/users/invitations` | `Onboarding.Invite` | List TenantUser invitations (filter by `status`) |
| POST | `/users/invitations/{id}/revoke` | `Onboarding.Revoke` | Revoke pending TenantUser invitation |
| POST | `/users/invitations/{id}/resend` | `Onboarding.Resend` | Resend pending TenantUser invitation email (regenerates token, extends expiry). Only works when invitation is not yet accepted, revoked, or expired. |
| POST | `/users/{userId}/activate` | `Onboarding.Activate` | Activate user account |
| POST | `/users/{userId}/deactivate` | `Onboarding.Deactivate` | Deactivate user account |

`POST /users` (direct creation) respects `RequireEmailVerification`. In production, the created user must verify their email before logging in.

---

## Roles (TenantAdmin)

Requires `X-Tenant-Id` header for SystemAdmin. Custom roles only — `SystemAdmin`, `TenantAdmin`, `TenantUser` are not in this table.

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| GET | `/roles` | `Roles.List` | List custom roles. Query: `page`, `pageSize`, `search` (name), `permissionId` (Guid — only roles containing this permission) |
| GET | `/roles/{name}` | `Roles.View` | Get role details by name |
| GET | `/roles/current` | `Roles.View` | Caller's own role |
| POST | `/roles` | `Roles.Create` | Create custom role with permissions |
| PUT | `/roles` | `Roles.Edit` | Update role (rename, description, permissions) by name in body |
| DELETE | `/roles/{name}` | `Roles.Delete` | Delete role by name |

`POST /roles` body:

```json
{
  "name": "SalesRep",
  "description": "Sales representative",
  "permissions": ["<permGuid>", "..."]
}
```

`PUT /roles` uses the same body plus an optional `newName` — when set and different from `name`, the role is renamed (must not collide with an existing role or a built-in role name).

At least one permission is required. Permissions must be within the `TenantUser` scope — roles cannot escalate beyond a TenantUser's ceiling. This ceiling applies to **every caller, including SystemAdmin** (enforced centrally in `IdentityRoleService`, so it also covers custom roles supplied to `POST /tenants`).

---

## Invitations (public, token-gated)

No authentication required. Access is controlled by the short-lived invitation token sent via email. Rate-limited.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/invitations/validate?token=...` | Validate before showing registration form. Returns email, type, tenant name, tenant slug. |
| POST | `/invitations/accept/tenant-admin` | Complete TenantAdmin invitation (provide name, password, optional address) |
| POST | `/invitations/accept/user` | Complete TenantUser invitation (provide name, password, optional address) |
| POST | `/invitations/accept/new-tenant` | Complete new-tenant creation invitation. Provide name, password, tenant name, tenant slug, optional tenant address, optional user address. Creates tenant + admin user atomically. |

Accept request body (both endpoints):

```json
{
  "token": "...",
  "fullName": "Jane Smith",
  "phone": "+1 555 0100",
  "password": "SecurePass123!",
  "confirmPassword": "SecurePass123!",
  "address": {
    "line1": "123 Main St",
    "city": "Austin",
    "state": "TX",
    "postalCode": "78701",
    "country": "US"
  }
}
```

`address` is optional. Omit it to leave the user's address unset.

---

## Account setup (public, token-gated)

No authentication required. Used for the direct-create flow (TenantAdmin creates user and sends setup email). Rate-limited.

| Method | Path | Notes |
|--------|------|-------|
| GET | `/account-setup/validate?token=...` | Validate setup token. Returns email, name, and `hasAddress` (bool) for the client to decide whether to show the address step. |
| POST | `/account-setup/set-password` | Set password, optionally update full name, and optionally set address. Token is consumed (single-use). |

`set-password` request body:

```json
{
  "token": "...",
  "password": "SecurePass123!",
  "confirmPassword": "SecurePass123!",
  "fullName": "Jane Smith",
  "address": {
    "line1": "123 Main St",
    "city": "Austin",
    "state": "TX",
    "postalCode": "78701",
    "country": "US"
  }
}
```

`fullName` and `address` are both optional. When the user was created via the direct-create flow the admin already provided their address — in that case `validate` returns `hasAddress: true` and the client omits the address step and leaves `address` out of `set-password`.

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
- `PUT /tenants` — update name, active flag, profile image, address (slug is immutable)

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

## Pagination & Filtering

`GET /users`, `GET /tenants`, `GET /roles`:

| Query | Default | Max |
|-------|---------|-----|
| `page` | `1` | — |
| `pageSize` | `20` | `100` |

`GET /users`, `GET /tenants`, `GET /tenant-admins` also accept filter params:

| Query | Type | Notes |
|-------|------|-------|
| `isActive` | `bool?` | `true` → active only, `false` → inactive only, omit → all |
| `createdVia` | `Direct` \| `Invitation` | omit → all |

`GET /tenant-admins` additionally accepts `tenantId` (GUID) to scope by tenant.

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
| `Files` | `View`, `Upload` | TenantUser |
| `Files` | `Delete` | TenantAdmin |
| `Users` | `View`, `Create`, `Edit`, `Delete` | TenantAdmin |
| `Roles` | `View`, `Create`, `Edit`, `Delete` | TenantAdmin |
| `Onboarding` | `Create`, `Invite`, `Resend`, `Revoke`, `Activate`, `Deactivate` | TenantAdmin |
| `Tenants` | `View`, `Create`, `Edit`, `Delete` | SystemAdmin only |
| `Subscriptions` | `View`, `Edit` | SystemAdmin only |

Activity logs (`GET /activity-logs`) require `SystemAdminOnly` policy — no permission name, no TenantAdmin access.

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

## Subscriptions

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/subscriptions/plans` | `Subscriptions.View` (SystemAdmin) | Returns all available plans with features |
| PUT | `/subscriptions/tenant-plan` | `Subscriptions.Edit` (SystemAdmin) | Change a tenant's plan |

Plan types: `Free` (MaxUsers=10, MaxStorageMb=500) · `Pro` (MaxUsers=unlimited, MaxStorageMb=10240).

PUT body:
```json
{ "tenantId": "<guid>", "planType": "Pro" }
```

`TenantDto` now includes `planType`, `planName`, `planFeatures` on all tenant responses.

---

## Tenant Settings

Self-service tenant settings for TenantAdmin (cannot change `isActive` or `planType`).

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/tenant-settings` | TenantAdmin only | Returns current tenant (same shape as `TenantDto`) |
| PUT | `/tenant-settings` | TenantAdmin only | Update name, address, profile image |
| POST | `/tenant-settings/logo` | TenantAdmin only | Upload tenant logo (JPEG/PNG/GIF/WebP, max 10 MB). Multipart form-data with `file` field. |
| DELETE | `/tenant-settings/logo` | TenantAdmin only | Remove tenant logo. |

---

## Impersonation (SystemAdmin)

Allows a SystemAdmin to temporarily act as a TenantUser within a specific tenant. Impersonation replaces the `access_token` cookie with a short-lived JWT containing `impersonated_by_*` claims. The admin's session is preserved in the `impersonation_restore_token` cookie (HttpOnly) for restoration.

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| POST | `/impersonation/start` | `SystemAdminOnly` | Start impersonating a tenant user. Requires `X-Tenant-Id` header and active `refresh_token` cookie. |
| POST | `/impersonation/stop` | Authenticated | End impersonation. Requires `impersonation_restore_token` cookie. Restores admin session. |

### Start impersonation request

```json
{ "targetUserId": "<guid>" }
```

### Start impersonation response

```json
{
  "userId": "...",
  "email": "jane@acme.com",
  "fullName": "Jane Smith",
  "systemRole": "TenantUser",
  "roles": ["SalesRep"],
  "expiresAt": "2026-07-06T15:30:00Z"
}
```

Cookies set: `access_token` = target user JWT, `impersonation_restore_token` = admin refresh token. The `refresh_token` cookie is cleared (no token refresh during impersonation).

### Stop impersonation response

```json
{
  "userId": "...",
  "email": "admin@system.com",
  "fullName": "System Admin",
  "systemRole": "SystemAdmin",
  "expiresAt": "2026-07-06T15:45:00Z"
}
```

Cookies set: `access_token` = admin JWT, `refresh_token` = new admin refresh token. `impersonation_restore_token` cookie cleared.

---

## Activity Logs

**SystemAdmin only** — TenantAdmin and TenantUser cannot access audit logs.

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/activity-logs` | `SystemAdminOnly` | Paginated log of tenant actions |

Query params: `page`, `pageSize`, `userId` (GUID), `module`, `action`, `dateFrom` (ISO), `dateTo` (ISO).

Supply `X-Tenant-Id` header to scope to a specific tenant. Omit the header to see all tenants' logs.

Response item:
```json
{
  "id": "...",
  "tenantId": "...",
  "tenantName": "Acme Corp",
  "userId": "...",
  "userDisplayName": "Jane Doe",
  "userEmail": "jane@example.com",
  "module": "Users",
  "action": "Created",
  "description": "Created user bob@example.com",
  "ipAddress": "1.2.3.4",
  "createdAt": "2026-07-05T10:00:00Z"
}
```

---

## Dashboard (updated)

`GET /dashboard/stats` response now includes additional chart data fields:

| Field | Visible to | Description |
|-------|-----------|-------------|
| `freePlanTenants` | SystemAdmin | Count of Free-plan tenants |
| `proPlanTenants` | SystemAdmin | Count of Pro-plan tenants |
| `acceptedInvitations` | TenantAdmin | Accepted user invitations |
| `expiredInvitations` | TenantAdmin | Expired user invitations |
| `revokedInvitations` | TenantAdmin | Revoked user invitations |

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
| Impersonation | `POST start`, `POST stop` |
| Users | `GET`, `GET {id}`, `GET current`, `POST`, `POST direct-create`, `POST invite`, `PUT`, `PUT current`, `POST current/change-password`, `DELETE`, `POST {id}/resend`, `POST {id}/activate`, `POST {id}/deactivate`, `GET invitations`, `POST invitations/{id}/revoke`, `POST invitations/{id}/resend`, `POST {id}/avatar`, `DELETE {id}/avatar` | Responses include `lastLoginAt` (ISO datetime or null) |
| Tenant Admins | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`, `POST invite`, `POST {id}/resend`, `POST {id}/activate`, `POST {id}/deactivate`, `GET invitations`, `POST invitations/{id}/revoke`, `POST invitations/{id}/resend` |
| Tenants | `GET`, `GET {id}`, `GET current`, `POST`, `PUT`, `DELETE`, `POST {id}/logo`, `DELETE {id}/logo` |
| Roles | `GET`, `GET {name}`, `GET current`, `POST`, `PUT`, `DELETE {name}` |
| Products | `GET`, `GET {id}`, `POST`, `PUT`, `DELETE` |
| Permissions | `GET` |
| Files | `GET`, `GET {id}`, `GET {id}/download`, `POST`, `DELETE {id}` |
| Reports | `GET summary`, `GET export`, `GET platform-summary`, `GET platform-export` |
| Invitations | `GET validate`, `POST accept/tenant-admin`, `POST accept/user` |
| Account setup | `GET validate`, `POST set-password` |
| Health | `GET /api/v1/health`, `GET /health` |
