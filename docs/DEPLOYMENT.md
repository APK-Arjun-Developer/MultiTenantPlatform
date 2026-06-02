# Deployment — MonsterASP.NET (FTP) via GitHub Actions

Automated production deploys use [`.github/workflows/deploy-monsterasp-ftp.yml`](../.github/workflows/deploy-monsterasp-ftp.yml).

**Flow:** push to `master` (or manual **Run workflow**) → build & publish (`win-x86`) → inject secrets into `appsettings.Production.json` → upload to MonsterASP **`/wwwroot/`** over FTP.

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

With **`ApplyMigrationsOnStartup: true`** and **`SeedOnStartup: true`** (set in `appsettings.Production.json` and the deploy workflow), each app start will:

1. Apply any **pending EF Core migrations**
2. Run **idempotent seeders** (permissions, default tenant, SuperAdmin if missing)

First SuperAdmin (only if no user exists): `admin@system.com` / `Admin123!` — change the password after first login.

Optional manual migration from your PC:

```powershell
$env:ConnectionStrings__DefaultConnection = "<your-monsterasp-sql-connection-string>"
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

---

## 2. GitHub repository secrets

Open **Settings → Secrets and variables → Actions → New repository secret**.

| Secret | Required | Example / notes |
|--------|----------|-----------------|
| `FTP_SERVER` | Yes | FTP host only: `site1234.siteasp.net` (no `https://`) |
| `FTP_USERNAME` | Yes | `site1234` |
| `FTP_PASSWORD` | Yes | FTP password from control panel |
| `FTP_SERVER_DIR` | No | Default `/wwwroot/` |
| `SITE_URL` | No | **Recommended.** Public site URL for smoke test, e.g. `https://site1234.monsterasp.net`. FTP host (`*.siteasp.net`) is not the browser URL and often returns 404. |
| `FTP_PORT` | No | Default `21` |
| `PRODUCTION_CONNECTION_STRING` | Yes | MonsterASP MSSQL connection string |
| `JWT_KEY` | Yes | Random string, **≥ 32 characters** |
| `JWT_ISSUER` | No | Defaults to `MultiTenantPlatform` |
| `JWT_AUDIENCE` | No | Defaults to `MultiTenantPlatformUsers` |
| `ALLOWED_ORIGINS` | **Yes** | Comma-separated list of allowed CORS origins, e.g. `https://myapp.com,https://www.myapp.com`. The API will refuse to start in Production if this is empty. |

Never commit real connection strings or JWT keys. The workflow **overwrites** `appsettings.Production.json` in the publish output at deploy time.

---

## 3. Production configuration

Committed templates: [`appsettings.Development.json`](../src/Api/appsettings.Development.json) and [`appsettings.Production.json`](../src/Api/appsettings.Production.json) use the **same keys**; only values differ (local SQL/JWT vs empty secrets, `SeedOnStartup`, log levels).

| Setting | Production behavior |
|---------|---------------------|
| `ApplyMigrationsOnStartup` | `true` — apply pending migrations on startup |
| `SeedOnStartup` | `false` — seeders do not run on each startup; re-run manually when needed |
| `ConnectionStrings:DefaultConnection` | Injected from `PRODUCTION_CONNECTION_STRING` |
| `Jwt:Key` | Injected from `JWT_KEY` |
| `Serilog` | `Warning` default |
| Swagger | **Production:** enabled at `/swagger` behind login (`admin@system.com` + `ADMIN_PASSWORD` from secrets). **Development:** open without login. |

Local production testing (without FTP):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "<connection-string>"
$env:Jwt__Key = "<at-least-32-char-secret>"
dotnet run --project src/Api
```

---

## 4. Trigger a deploy

- **Automatic:** push to the `master` branch
- **Manual:** GitHub → **Actions** → **Deploy to MonsterASP (FTP)** → **Run workflow**

Monitor the workflow log. On success, browse `https://<your-site>.monsterasp.net/` (or your assigned subdomain).

The deploy smoke test calls **`GET /api/v1/health`** on your public site URL. It auto-tries `https://<ftp-host-with-siteasp-replaced-by-monsterasp.net>` when `SITE_URL` is not set. Set **`SITE_URL`** if you use a custom domain.

If the smoke test fails but FTP deploy succeeded, open `https://<your-site>/api/v1/health` in a browser and check `wwwroot/logs/stdout*.log` on FTP for startup errors (migrations, connection string, .NET version in control panel).

---

## 5. FTP file locks (troubleshooting)

Error **`550 Could not access file: driver error: calling GetHandle`** usually means **IIS has locked `Api.dll`** while the site is running.

The workflow handles this automatically:

1. Upload `app_offline.htm` to `wwwroot` (stops the site)
2. Wait 20 seconds
3. Deploy all files
4. Delete `app_offline.htm` (site comes back online)

If deploy still fails:

1. **Control panel** → Websites → your site → **Restart**, then re-run the workflow, or  
2. Manually upload [`deploy/app_offline.htm`](../deploy/app_offline.htm) via FileZilla, deploy, then delete it.

See [MonsterASP FTP deploy docs](https://help.monsterasp.net/books/deploy/page/how-to-deploy-website-content-via-ftpsftp).

---

## 6. Alternative: WebDeploy

MonsterASP also documents [GitHub Actions with WebDeploy](https://help.monsterasp.net/books/github/page/how-to-deploy-website-via-github-actions). This project uses **FTP** as requested; WebDeploy secrets differ (`WEBSITE_NAME`, `SERVER_COMPUTER_NAME`, etc.).

---

## Related

- [PROJECT.md](./PROJECT.md) — architecture summary  
- [API.md](./API.md) — API reference
