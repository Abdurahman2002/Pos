# =============================================================================
#  Pos (jhani) - one-command deploy to the Libyan Spider server (Plesk / IIS)
#
#  What it does, every run:
#    1. dotnet publish (self-contained win-x64) of the whole MVC app
#    2. drop app_offline.htm on the server -> IIS stops the app and releases the
#       lock on NewsApp2.exe so the files can be overwritten
#    3. FTP-sync ONLY the files that changed (first deploy is large; a normal
#       code change after that is a few hundred KB = seconds)
#    4. remove app_offline.htm -> IIS restarts the app, which applies any new EF
#       Core migrations on the first request. No SQL script to run by hand.
#    5. poll the site root until it answers 200, so the run does not report
#       success for a shop that cannot sell
#
#  BACK UP THE DATABASE FIRST when the release contains a migration. Step 4 is
#  automatic and there is no undo. Confirm with the host when their backups run.
#
#  One-time setup:
#    - winget install -e --id WinSCP.WinSCP
#    - copy deploy.settings.example.ps1 to deploy.settings.ps1 and fill it in
#      (that file is gitignored - the password never enters the repository)
#    - upload the production config once:  .\deploy\deploy.ps1 -UploadConfig
#
#  Usage:
#    pwsh ./deploy/deploy.ps1                 full deploy
#    pwsh ./deploy/deploy.ps1 -CheckOnly      connect and inspect, upload nothing
#    pwsh ./deploy/deploy.ps1 -SkipBuild      re-upload the last publish
#    pwsh ./deploy/deploy.ps1 -UploadConfig   send appsettings.Production.json once
# =============================================================================

[CmdletBinding()]
param(
    # Connects, lists the target folder and runs every safety check, then stops
    # before touching anything. Use this the first time, and any time the FTP
    # account or the remote path changes.
    [switch]$CheckOnly,

    # Skips dotnet publish and uploads whatever is already in the publish folder.
    # Only useful when a previous run built fine but the upload failed.
    [switch]$SkipBuild,

    # Sends appsettings.Production.json, which every other run deliberately leaves
    # alone. It holds the connection string, so a routine sync must never
    # overwrite what is on the server with whatever is on this laptop.
    [switch]$UploadConfig
)

$ErrorActionPreference = "Stop"

# ----------------------------- SETTINGS --------------------------------------
# Host, credentials and remote path live in deploy.settings.ps1, which is
# gitignored. This script is committed and holds no secrets.
$SettingsFile = Join-Path $PSScriptRoot "deploy.settings.ps1"
if (-not (Test-Path $SettingsFile)) {
    Write-Host "Missing $SettingsFile" -ForegroundColor Red
    Write-Host "Copy deploy.settings.example.ps1 to deploy.settings.ps1 and fill in the FTP details." -ForegroundColor Yellow
    exit 1
}
. $SettingsFile

foreach ($required in @("FtpHost", "FtpUser", "FtpPass", "RemotePath", "FtpCertFingerprint", "HealthUrl")) {
    $value = Get-Variable -Name $required -ValueOnly -ErrorAction SilentlyContinue
    if ([string]::IsNullOrWhiteSpace($value) -or $value -like "*FILL-ME*") {
        Write-Host "deploy.settings.ps1: `$$required is not set." -ForegroundColor Red
        exit 1
    }
}

# Paths (relative to this script's location)
$Root       = Split-Path $PSScriptRoot -Parent
$Project    = Join-Path $Root "NewsApp2.csproj"
$PublishDir = Join-Path $Root "bin\Release\net9.0\publish"
$ConfigFile = Join-Path $Root "deploy\appsettings.Production.json"

# Server-side paths the sync must NEVER touch.
#   appsettings.Production.json  the connection string. Syncing it would replace
#                                the server's real settings with a developer's copy.
#   App_Data/                    the data protection keys, written while running.
#   logs/                        IIS stdout files on this host.
#   backups/                     database backups, if any.
# NOTE: no spaces between entries. WinSCP.com splits the /command argument on
# spaces, so a space here turns "logs/;" into a bogus separate command and the
# whole deploy aborts *before* app_offline.htm is removed (site stuck offline).
$ExcludeMask = "|app_offline.htm;appsettings.Production.json;App_Data/;logs/;backups/"

# A folder is only a valid target if it is empty or already holds this app.
$OwnMarker      = "NewsApp2.exe"
# ...and never, under any circumstance, another application on the same hosting.
$ForeignMarkers = @("Alrand.Api.exe", "Alrand.Api.dll", "ConstructionProgress.Api.exe", "AttendaceApp.exe", "AttendaceApp.dll")
# -----------------------------------------------------------------------------

# Auto-detect WinSCP.com. winget installs it user-scoped (AppData), the .msi puts
# it in Program Files.
if ([string]::IsNullOrWhiteSpace($WinScpCom)) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\WinSCP\WinSCP.com",
        "C:\Program Files (x86)\WinSCP\WinSCP.com",
        "C:\Program Files\WinSCP\WinSCP.com",
        (Get-Command winscp.com -ErrorAction SilentlyContinue).Source
    )
    $WinScpCom = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $WinScpCom -or -not (Test-Path $WinScpCom)) {
    Write-Host "WinSCP.com not found." -ForegroundColor Red
    Write-Host "Install it once with:  winget install -e --id WinSCP.WinSCP" -ForegroundColor Yellow
    Write-Host "Then re-run. (Set `$WinScpCom in deploy.settings.ps1 if you installed it elsewhere.)" -ForegroundColor Yellow
    exit 1
}
Write-Host "Using WinSCP: $WinScpCom" -ForegroundColor DarkGray

$CertOption = if ($FtpCertFingerprint -eq "*") { "-certificate=""*""" } else { "-certificate=""$FtpCertFingerprint""" }
$OpenCommand = "open ${FtpProtocol}://$([uri]::EscapeDataString($FtpUser)):$([uri]::EscapeDataString($FtpPass))@$FtpHost/ -explicit $CertOption"

if ($FtpCertFingerprint -eq "*") {
    Write-Host "WARNING: the server certificate is not pinned." -ForegroundColor Yellow
    Write-Host "         Fine for the first -CheckOnly run. Copy the SHA-256 WinSCP reports into" -ForegroundColor Yellow
    Write-Host "         `$FtpCertFingerprint before deploying for real." -ForegroundColor Yellow
}

# --------------------------- SAFETY PRE-FLIGHT -------------------------------
# Shared hosting puts several applications on one server. Point this script at the
# wrong document root and the sync would bury a working, unrelated application
# under this one - silently, because FTP has no idea the two are different products.
Write-Host "==> Inspecting $FtpHost$RemotePath ..." -ForegroundColor Cyan
$listing = & $WinScpCom /ini=nul /command `
    "option batch abort" `
    "option confirm off" `
    $OpenCommand `
    "ls ""$RemotePath""" `
    "exit" 2>&1 | Out-String

if ($LASTEXITCODE -ne 0) {
    Write-Host $listing
    Write-Host "==> Could not list $RemotePath. Check the host, credentials and path in deploy.settings.ps1." -ForegroundColor Red
    exit 1
}

$foundForeign = $ForeignMarkers | Where-Object { $listing -match [regex]::Escape($_) }
if ($foundForeign) {
    Write-Host ""
    Write-Host "REFUSING TO DEPLOY." -ForegroundColor Red
    Write-Host "$RemotePath on $FtpHost contains $($foundForeign -join ', ') - that is a DIFFERENT application." -ForegroundColor Red
    Write-Host "Syncing here would overwrite it. Fix RemotePath (or the FTP account) in deploy.settings.ps1." -ForegroundColor Yellow
    exit 1
}

$isOwnSite = $listing -match [regex]::Escape($OwnMarker)

$names = @()
foreach ($line in ($listing -split "`r?`n")) {
    if ($line -notmatch '(?i)^[-dl][-rwxsStT]{9}\s') { continue }

    if ($line -match '(?i)\s\w{3}\s+\d{1,2}\s+(?:\d{1,2}:\d{2}\s+\d{4}|\d{4}|\d{1,2}:\d{2})\s+(.+?)\s*$') {
        $name = $Matches[1]
    } else {
        $name = ($line -split '\s+')[-1]
    }

    if ($name -and $name -ne "." -and $name -ne "..") { $names += $name }
}

$Recognised = @(
    "index.html", "index.htm", "default.html", "default.htm",
    "web.config", "favicon.ico", "assets", "App_Data", ".user.ini", "error_docs",
    "appsettings.Production.json", "app_offline.htm", "wwwroot", "logs", "backups"
)

$unexpected = $names | Where-Object { $Recognised -notcontains $_ }

if (-not $isOwnSite -and $unexpected) {
    Write-Host ""
    Write-Host "REFUSING TO DEPLOY." -ForegroundColor Red
    Write-Host "$RemotePath holds files this script does not recognise, and no ${OwnMarker}:" -ForegroundColor Red
    foreach ($name in $unexpected) { Write-Host "    $name" -ForegroundColor Red }
    Write-Host "That is probably the wrong folder, or another application. Run -CheckOnly to see" -ForegroundColor Yellow
    Write-Host "all of it. If it really is the right first-ever target, empty it in Plesk first." -ForegroundColor Yellow
    exit 1
}

if ($isOwnSite) {
    Write-Host "    Target confirmed: existing Pos deployment." -ForegroundColor DarkGray
} else {
    Write-Host "    Target confirmed: $(if ($names) { 'Plesk default page' } else { 'empty folder' }) (first deploy)." -ForegroundColor DarkGray
}

if ($CheckOnly) {
    Write-Host ""
    Write-Host $listing
    Write-Host "==> -CheckOnly: connection and target verified. Nothing was uploaded." -ForegroundColor Green
    exit 0
}

# ---------------------- ONE-TIME PRODUCTION CONFIG ---------------------------
if ($UploadConfig) {
    if (-not (Test-Path $ConfigFile)) {
        Write-Host "Missing $ConfigFile" -ForegroundColor Red
        Write-Host "Copy deploy\appsettings.Production.example.json to that name and fill in the" -ForegroundColor Yellow
        Write-Host "connection string. It is gitignored." -ForegroundColor Yellow
        exit 1
    }

    $raw = Get-Content $ConfigFile -Raw
    if ($raw -match "FILL-ME") {
        Write-Host "$ConfigFile still contains FILL-ME. Fill it in first." -ForegroundColor Red
        exit 1
    }

    Write-Host "==> Uploading appsettings.Production.json ..." -ForegroundColor Cyan

    & $WinScpCom /ini=nul /log="$PSScriptRoot\deploy.log" /command `
        "option batch abort" `
        "option confirm off" `
        $OpenCommand `
        "put -nopermissions ""$ConfigFile"" ""$($RemotePath.TrimEnd('/'))/appsettings.Production.json""" `
        "exit"

    if ($LASTEXITCODE -ne 0) { Write-Host "Upload failed. Check deploy/deploy.log" -ForegroundColor Red; exit $LASTEXITCODE }
    Write-Host "==> Done. Run a normal deploy next." -ForegroundColor Green
    exit 0
}

# ------------------------------- BUILD ---------------------------------------
if (-not $SkipBuild) {
    Write-Host "==> Publishing (self-contained win-x64)..." -ForegroundColor Cyan
    dotnet publish $Project -c Release -r win-x64 --self-contained true -o $PublishDir --nologo
    if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed." -ForegroundColor Red; exit 1 }
} else {
    if (-not (Test-Path (Join-Path $PublishDir $OwnMarker))) {
        Write-Host "-SkipBuild was passed but $PublishDir holds no publish output." -ForegroundColor Red
        exit 1
    }
    Write-Host "==> -SkipBuild: uploading the existing publish in $PublishDir" -ForegroundColor Yellow
}

# A developer copy of the production config must never ride along in the publish.
$StrayConfig = Join-Path $PublishDir "appsettings.Production.json"
if (Test-Path $StrayConfig) {
    Write-Host "==> Removing appsettings.Production.json from the publish output." -ForegroundColor DarkGray
    Remove-Item $StrayConfig -Force
}

# Nor the development one. It carries a local connection string and a signing key.
$StrayDev = Join-Path $PublishDir "appsettings.Development.json"
if (Test-Path $StrayDev) {
    Write-Host "==> Removing appsettings.Development.json from the publish output." -ForegroundColor DarkGray
    Remove-Item $StrayDev -Force
}

# The page IIS serves while the app is down. It lives in its own file, in Arabic,
# because a PowerShell script that contains non-ASCII text has to be saved with a
# BOM or Windows PowerShell 5.1 reads it as ANSI, mangles the string and fails to
# parse. Keeping this script pure ASCII removes that whole class of failure.
# web.config is XML that IIS parses before it runs anything, and a malformed one
# answers 500 to every request with no clue why. Validate it before uploading.
$WebConfig = Join-Path $PublishDir "web.config"
if (Test-Path $WebConfig) {
    try {
        [xml](Get-Content $WebConfig -Raw) | Out-Null
    } catch {
        Write-Host "web.config in the publish output is not valid XML:" -ForegroundColor Red
        Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "IIS would answer 500 to every request. Nothing was uploaded." -ForegroundColor Yellow
        exit 1
    }
}

# Config files are copied into the publish verbatim, so they keep the timestamp of
# the source file -- and "synchronize remote" refuses to overwrite a remote file
# that looks newer. Stamping them now makes them unambiguously newer.
foreach ($name in @("web.config", "appsettings.json")) {
    $file = Join-Path $PublishDir $name
    if (Test-Path $file) { (Get-Item $file).LastWriteTime = Get-Date }
}

$AppOffline = Join-Path $PSScriptRoot "app_offline.htm"
if (-not (Test-Path $AppOffline)) {
    Write-Host "Missing $AppOffline - it ships with this script." -ForegroundColor Red
    exit 1
}

# ------------------------------- DEPLOY --------------------------------------
Write-Host "==> Deploying to $FtpHost$RemotePath ..." -ForegroundColor Cyan

# put app_offline -> sync changed files -> remove app_offline.
# -criteria=either, not time. -delete is intentionally OFF, so the sync can never
# wipe backups or logs even if the exclude mask is wrong.
$Base       = $RemotePath.TrimEnd('/')      # "/" -> "" so paths don't become "//x"
$OfflineUrl = "$Base/app_offline.htm"

& $WinScpCom /ini=nul /log="$PSScriptRoot\deploy.log" /command `
    "option batch abort" `
    "option confirm off" `
    $OpenCommand `
    "put -nopermissions ""$AppOffline"" ""$OfflineUrl""" `
    "synchronize remote -criteria=either ""$PublishDir"" ""$RemotePath"" -filemask=""$ExcludeMask""" `
    "rm ""$OfflineUrl""" `
    "exit"

$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Host "==> WinSCP exited with code $code. Check deploy/deploy.log" -ForegroundColor Red
    Write-Host "    If it was a file-lock error, the server may still be shutting down -" -ForegroundColor Yellow
    Write-Host "    re-run; app_offline.htm is already up so it will be quick." -ForegroundColor Yellow
    exit $code
}

# ------------------------------ VERIFY ---------------------------------------
Write-Host "==> Waiting for $HealthUrl ..." -ForegroundColor Cyan
$deadline  = (Get-Date).AddMinutes(3)
$healthy   = $false
$lastError = ""
while ((Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 20
        if ($response.StatusCode -eq 200) { $healthy = $true; break }
        $lastError = "HTTP $($response.StatusCode)"
    } catch {
        $lastError = $_.Exception.Message
    }
    Start-Sleep -Seconds 5
}

if ($healthy) {
    Write-Host "==> Done. The shop is live and any new migrations have been applied." -ForegroundColor Green
    exit 0
}

Write-Host "==> Uploaded, but $HealthUrl never answered 200." -ForegroundColor Red
Write-Host "    Last error: $lastError" -ForegroundColor Yellow
Write-Host "    Look at logs\stdout*.log on the server - a bad connection string or a failed" -ForegroundColor Yellow
Write-Host "    migration shows up there. The files are already in place, so fixing the config" -ForegroundColor Yellow
Write-Host "    and restarting the app in Plesk is usually enough." -ForegroundColor Yellow
exit 1