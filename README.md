# Multi-Tenant Platform

.NET **10** multi-tenant SaaS API with JWT authentication, permission-based RBAC, and tenant isolation.

## Documentation

| Document | Description |
|----------|-------------|
| [docs/PROJECT.md](docs/PROJECT.md) | Architecture, auth model, permissions, data model (canonical summary) |
| [docs/API.md](docs/API.md) | v1 endpoints, envelope, login, onboarding, pagination |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | GitHub Actions → MonsterASP.NET FTP production deploy |

## Quick start

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
dotnet run --project src/Api
```

Open `/swagger` in Development. API base path: `/api/v1`.

## Highlights

- **Permissions**: PascalCase names (`Users.View`, not `users.view`); checked per request, **not** stored in JWT
- **JWT claims**: `user_id`, `tenant_id`, `role_id`, role names
- **User roles**: ASP.NET Identity `AspNetUserRoles` (no custom user–role table)
- **Onboarding**: `POST /api/v1/tenants` with `user`, `tenant`, and `roles[]` (permission GUIDs)
