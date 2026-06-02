param(
    [Parameter(Mandatory = $true)]
    [string]$FtpServer,

    [string]$SiteUrl = "",

    [int]$MaxAttempts = 8,

    [int]$InitialDelaySeconds = 30,

    [int]$RetryDelaySeconds = 15
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-HostName {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $hostName = $Value.Trim().TrimEnd('/')
    $hostName = $hostName -replace '^https?://', ''
    return $hostName
}

function Add-UniqueBaseUrl {
    param(
        [System.Collections.Generic.List[string]]$List,
        [string]$HostName
    )

    if ([string]::IsNullOrWhiteSpace($HostName)) {
        return
    }

    foreach ($scheme in @("https", "http")) {
        $base = '{0}://{1}' -f $scheme, $HostName
        if (-not $List.Contains($base)) {
            [void]$List.Add($base)
        }
    }
}

$baseUrls = [System.Collections.Generic.List[string]]::new()

$siteHost = Normalize-HostName $SiteUrl
if ($siteHost) {
    Add-UniqueBaseUrl -List $baseUrls -HostName $siteHost
}

$ftpHost = Normalize-HostName $FtpServer
if ($ftpHost) {
    Add-UniqueBaseUrl -List $baseUrls -HostName $ftpHost

    if ($ftpHost -match '\.siteasp\.net$') {
        $publicHost = $ftpHost -replace '\.siteasp\.net$', '.monsterasp.net'
        Add-UniqueBaseUrl -List $baseUrls -HostName $publicHost
    }
}

if ($baseUrls.Count -eq 0) {
    throw "No smoke-test base URLs. Set FTP_SERVER and/or SITE_URL."
}

$healthPaths = @(
    "/api/v1/health",
    "/health",
    "/",
    "/index.html"
)

Write-Host "Smoke-test base URLs: $($baseUrls -join ', ')"
Write-Host "Waiting $InitialDelaySeconds s for IIS/app startup (migrations may run on first boot) ..."
Start-Sleep -Seconds $InitialDelaySeconds

$success = $false
$lastErrors = [System.Collections.Generic.List[string]]::new()

:attemptLoop for ($attempt = 1; $attempt -le $MaxAttempts -and -not $success; $attempt++) {
    foreach ($base in $baseUrls) {
        foreach ($path in $healthPaths) {
            $url = "$base$path"
            Write-Host "Attempt $attempt – GET $url"

            try {
                $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30

                if ($path -eq "/api/v1/health") {
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

                if ($path -in @("/", "/index.html") -and $response.StatusCode -eq 200) {
                    Write-Host "Site is up at $url (HTTP 200). API route not verified yet."
                }
            }
            catch {
                [void]$lastErrors.Add("$url -> $($_.Exception.Message)")
            }
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
    $lastErrors | Select-Object -Last 12 | ForEach-Object { Write-Host "  - $_" }
    Write-Host ""
    Write-Host "Tips:"
    Write-Host "  - Set SITE_URL to your public URL (e.g. https://site1234.monsterasp.net), not the FTP host."
    Write-Host "  - FTP host is often siteXXXX.siteasp.net; the website is usually siteXXXX.monsterasp.net."
    Write-Host "  - In MonsterASP control panel: Websites -> .NET version = .NET 10, restart site."
    Write-Host "  - Check wwwroot/logs/stdout*.log on FTP if the app fails to start."

    throw "Health check failed after $MaxAttempts attempts."
}
