# Deployment — MonsterASP.NET (FTP) via GitHub Actions

Automated production deploys use [`.github/workflows/deploy-monsterasp-ftp.yml`](../.github/workflows/deploy-monsterasp-ftp.yml).

**Flow:** push to `main` (or manual **Run workflow**) → build & publish (`win-x86`) → inject secrets into `appsettings.Production.json` → upload to MonsterASP **`/wwwroot/`** over FTP.

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

### First-time database

Run migrations **once** against the production database (from your PC or a one-off job):

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Use the production connection string via environment variable:

```powershell
$env:ConnectionStrings__DefaultConnection = "<your-monsterasp-sql-connection-string>"
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet ef database update --project src/Infrastructure --startup-project src/Api
```

Production has **`SeedOnStartup: false`** so the default `admin@system.com` account is **not** created on the server. Create your SuperAdmin manually or run a one-time seed in a controlled environment.

---

## 2. GitHub repository secrets

Open **Settings → Secrets and variables → Actions → New repository secret**.

| Secret | Required | Example / notes |
|--------|----------|-----------------|
| `FTP_SERVER` | Yes | `site1234.siteasp.net` |
| `FTP_USERNAME` | Yes | `site1234` |
| `FTP_PASSWORD` | Yes | FTP password from control panel |
| `FTP_SERVER_DIR` | No | Default `/wwwroot/` |
| `FTP_PORT` | No | Default `21` |
| `PRODUCTION_CONNECTION_STRING` | Yes | MonsterASP MSSQL connection string |
| `JWT_KEY` | Yes | Random string, **≥ 32 characters** |
| `JWT_ISSUER` | No | Defaults to `MultiTenantPlatform` |
| `JWT_AUDIENCE` | No | Defaults to `MultiTenantPlatformUsers` |

Never commit real connection strings or JWT keys. The workflow **overwrites** `appsettings.Production.json` in the publish output at deploy time.

---

## 3. Production configuration

Committed templates: [`appsettings.Development.json`](../src/Api/appsettings.Development.json) and [`appsettings.Production.json`](../src/Api/appsettings.Production.json) use the **same keys**; only values differ (local SQL/JWT vs empty secrets, `SeedOnStartup`, log levels).

| Setting | Production behavior |
|---------|---------------------|
| `SeedOnStartup` | `false` — no automatic demo/admin seed |
| `ConnectionStrings:DefaultConnection` | Injected from `PRODUCTION_CONNECTION_STRING` |
| `Jwt:Key` | Injected from `JWT_KEY` |
| `Serilog` | `Warning` default |
| Swagger | Disabled (only registered in Development) |

Local production testing (without FTP):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "<connection-string>"
$env:Jwt__Key = "<at-least-32-char-secret>"
dotnet run --project src/Api
```

---

## 4. Trigger a deploy

- **Automatic:** push to the `main` branch
- **Manual:** GitHub → **Actions** → **Deploy to MonsterASP (FTP)** → **Run workflow**

Monitor the workflow log. On success, browse `https://<your-site>.monsterasp.net/` (or your assigned subdomain).

---

## 5. FTP file locks (troubleshooting)

If deploy fails with **“file in use”** / **550 Cannot delete file**:

1. **Control panel** → Websites → your site → **Restart**, then re-run the workflow, or  
2. Upload [`deploy/app_offline.htm`](../deploy/app_offline.htm) to `wwwroot` before deploy (stops IIS), then remove it after a successful deploy.

See [MonsterASP FTP deploy docs](https://help.monsterasp.net/books/deploy/page/how-to-deploy-website-content-via-ftpsftp).

---

## 6. Alternative: WebDeploy

MonsterASP also documents [GitHub Actions with WebDeploy](https://help.monsterasp.net/books/github/page/how-to-deploy-website-via-github-actions). This project uses **FTP** as requested; WebDeploy secrets differ (`WEBSITE_NAME`, `SERVER_COMPUTER_NAME`, etc.).

---

## Related

- [PROJECT.md](./PROJECT.md) — architecture summary  
- [API.md](./API.md) — API reference
