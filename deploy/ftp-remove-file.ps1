param(
    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [string]$Password,

    [Parameter(Mandatory = $true)]
    [string]$RemotePath
)

$normalized = $RemotePath.Trim().Replace('\', '/')
if (-not $normalized.StartsWith('/')) {
    $normalized = "/$normalized"
}

$uri = "ftp://$Server$normalized"
$request = [System.Net.FtpWebRequest]::Create($uri)
$request.Method = [System.Net.WebRequestMethods+Ftp]::DeleteFile
$request.Credentials = New-Object System.Net.NetworkCredential($Username, $Password)
$request.UsePassive = $true
$request.UseBinary = $true
$request.KeepAlive = $false

try {
    $response = $request.GetResponse()
    $response.Close()
    Write-Host "Deleted: $normalized"
}
catch [System.Net.WebException] {
    if ($_.Exception.Message -match '550') {
        Write-Host "Already absent (550): $normalized"
        exit 0
    }

    throw
}
