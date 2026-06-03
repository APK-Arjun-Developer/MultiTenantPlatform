param(
    [Parameter(Mandatory = $true)]
    [string]$FtpServer,

    [string]$SiteUrl = "",

    [int]$MaxAttempts = 10,

    [int]$InitialDelaySeconds = 45,

    [int]$RetryDelaySeconds = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-SiteUrl {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $trimmed = $Value.Trim().TrimEnd('/')
    if ($trimmed -notmatch '^https?://') {
        $trimmed = "https://$trimmed"
    }

    return $trimmed
}

function Add-UniqueBaseUrl {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$BaseUrl
    )

    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        return
    }

    if (-not $List.Contains($BaseUrl)) {
        [void]$List.Add($BaseUrl)
    }
}

function Get-AlternateSchemeUrl {
    param([string]$BaseUrl)

    if ($BaseUrl.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
        return "http://" + $BaseUrl.Substring(8)
    }

    if ($BaseUrl.StartsWith("http://", [StringComparison]::OrdinalIgnoreCase)) {
        return "https://" + $BaseUrl.Substring(7)
    }

    return $null
}

$baseUrls = [System.Collections.Generic.List[string]]::new()
$normalizedSiteUrl = Normalize-SiteUrl $SiteUrl

if ($normalizedSiteUrl) {
    Add-UniqueBaseUrl -List $baseUrls -BaseUrl $normalizedSiteUrl
    $alternate = Get-AlternateSchemeUrl $normalizedSiteUrl
    if ($alternate) {
        Add-UniqueBaseUrl -List $baseUrls -BaseUrl $alternate
    }
}
else {
    $ftpHost = $FtpServer.Trim().TrimEnd('/')
    $ftpHost = $ftpHost -replace '^https?://', ''

    Add-UniqueBaseUrl -List $baseUrls -BaseUrl ('https://{0}' -f $ftpHost)
    Add-UniqueBaseUrl -List $baseUrls -BaseUrl ('http://{0}' -f $ftpHost)

    if ($ftpHost -match '\.siteasp\.net$') {
        $publicHost = $ftpHost -replace '\.siteasp\.net$', '.monsterasp.net'
        Add-UniqueBaseUrl -List $baseUrls -BaseUrl ('https://{0}' -f $publicHost)
        Add-UniqueBaseUrl -List $baseUrls -BaseUrl ('http://{0}' -f $publicHost)
    }
}

if ($baseUrls.Count -eq 0) {
    throw "No smoke-test base URLs. Set SITE_URL (recommended) or FTP_SERVER."
}

$healthUrl = "{0}/api/v1/health" -f $baseUrls[0]
$rootUrl = $baseUrls[0]

Write-Host "Smoke-test targets: $($baseUrls -join ', ')"
Write-Host "Primary health check: $healthUrl"
Write-Host "Waiting $InitialDelaySeconds s for IIS/app startup (migrations + seeds on first boot) ..."
Start-Sleep -Seconds $InitialDelaySeconds

$success = $false
$lastErrors = [System.Collections.Generic.List[string]]::new()
$staticSiteUp = $false

:attemptLoop for ($attempt = 1; $attempt -le $MaxAttempts -and -not $success; $attempt++) {
    foreach ($base in $baseUrls) {
        $url = "$base/api/v1/health"
        Write-Host "Attempt $attempt – GET $url"

        try {
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 45

            if ($response.StatusCode -ne 200) {
                [void]$lastErrors.Add("$url -> HTTP $($response.StatusCode)")
                continue
            }

            $body = $response.Content | ConvertFrom-Json
            if ($body.data.status -ne "healthy") {
                [void]$lastErrors.Add("$url -> API status not healthy: $($response.Content)")
                continue
            }

            Write-Host "Health check passed: $url"
            Write-Host $response.Content
            $success = $true
            break attemptLoop
        }
        catch {
            [void]$lastErrors.Add("$url -> $($_.Exception.Message)")
        }
    }

    if (-not $staticSiteUp) {
        try {
            $rootResponse = Invoke-WebRequest -Uri $rootUrl -UseBasicParsing -TimeoutSec 30
            if ($rootResponse.StatusCode -eq 200) {
                $staticSiteUp = $true
                Write-Host "Static site responds at $rootUrl but API health is not ready yet."
            }
        }
        catch {
            # ignore
        }
    }

    if (-not $success -and $attempt -lt $MaxAttempts) {
        Write-Host "Retrying in $RetryDelaySeconds s ..."
        Start-Sleep -Seconds $RetryDelaySeconds
    }
}

if (-not $success) {
    Write-Host ""
    Write-Host "Recent failures:"
    $lastErrors | Select-Object -Last 8 | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""

    if ($staticSiteUp) {
        Write-Host "Diagnosis: static files work (index.html) but /api/v1/health returns 404."
        Write-Host "The ASP.NET app likely failed to start. Common causes:"
        Write-Host "  - Missing or invalid PRODUCTION_CONNECTION_STRING / JWT_KEY / ADMIN_PASSWORD"
        Write-Host "  - Startup exception (check wwwroot/logs/stdout*.log on FTP)"
        Write-Host "  - .NET 10 not selected in MonsterASP control panel"
        Write-Host "  - AllowedOrigins was empty (fixed in latest deploy workflow — redeploy)"
    }

    Write-Host ""
    Write-Host "Tips:"
    Write-Host "  - Set SITE_URL to your public URL only (e.g. https://multi-tenant-api.runasp.net)"
    Write-Host "  - SITE_URL must not be the FTP host (siteXXXX.siteasp.net)"
    Write-Host "  - Optional: set ALLOWED_ORIGINS for CORS (or it defaults from SITE_URL)"

    throw "Health check failed after $MaxAttempts attempts."
}
