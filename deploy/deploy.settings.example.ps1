# Template for deploy.settings.ps1 - copy this file to that name and fill it in.
#
#   Copy-Item deploy\deploy.settings.example.ps1 deploy\deploy.settings.ps1
#
# deploy.settings.ps1 is gitignored, so the password stays off the repository.
# This template is committed and must never hold a real password.

# The Libyan Spider server. A bare IP is fine - the same machine that hosts alrand.
$FtpHost = "102.213.180.7"

# "ftp" = FTP over explicit TLS (what this script uses), "ftps" = implicit.
$FtpProtocol = "ftp"

# ---------------------------------------------------------------------------
# An FTP account whose home directory is this site's document root.
#
# It must be the account for THIS site. Shared hosting puts several applications
# on one server, and an account pointed at the wrong document root would sync
# this system on top of a different, working application. The pre-flight check
# below refuses to upload into a folder that holds someone else's app.
# ---------------------------------------------------------------------------
$FtpUser = "FILL-ME"
$FtpPass = "FILL-ME"

# Where the site lives, relative to wherever the FTP account lands after login.
# Run  .\deploy\deploy.ps1 -CheckOnly  to confirm before the first real deploy.
$RemotePath = "/httpdocs"

# Shared hosting serves a self-signed panel certificate, so TLS is pinned to one
# exact fingerprint - otherwise WinSCP refuses to connect. The transfer stays
# encrypted either way.
#
# Leave this as "*" for the very first -CheckOnly run: WinSCP will print the real
# SHA-256 in its error, and you paste it here. Do not leave it as "*" afterwards.
$FtpCertFingerprint = "*"

# Polled after the upload until it answers 200, so a deploy that leaves the shop
# without its POS is reported as a failure instead of a success.
$HealthUrl = "http://102.213.180.7/"

# Leave empty to auto-detect WinSCP.com; set it only for a non-standard install.
$WinScpCom = ""