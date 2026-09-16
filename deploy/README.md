# Deploying Pos (jhani)

One command puts the whole system on the server:

```powershell
.\deploy\deploy.ps1
```

That publishes the MVC app self-contained, takes the site offline, uploads only
what changed, brings it back, and waits until the site answers 200. If it prints
a green "Done", the shop can sell.

---

## The server

| | |
|---|---|
| Host | Libyan Spider, Windows shared hosting (Plesk / IIS) |
| IP | `102.213.180.7` |
| Site root | document root of this site's FTP account, `httpdocs` |
| SQL Server | same host, database `jhani` |

The app is published **self-contained for win-x64**, so whatever .NET the server
has installed does not affect us.

---

## One-time setup

**1. Install WinSCP** — the deploy script drives it for the FTP sync.

```powershell
winget install -e --id WinSCP.WinSCP
```

**2. Create `deploy/deploy.settings.ps1`** from the example beside it and fill in
the FTP host, user, password, remote path and health URL.

```powershell
copy deploy\deploy.settings.example.ps1 deploy\deploy.settings.ps1
```

This file is gitignored. **The FTP password must never be committed.**

**3. Create `deploy/appsettings.Production.json`** from the example beside it:
the production connection string (from Plesk: Databases → the Pos database →
Connection Info) and the mail settings.

```powershell
copy deploy\appsettings.Production.example.json deploy\appsettings.Production.json
```

Also gitignored, for the same reason.

**4. Check the connection before uploading anything.**

```powershell
.\deploy\deploy.ps1 -CheckOnly
```

This connects, lists the target folder, runs every safety check and stops. It
uploads nothing. Run it now, and any time the FTP account or the remote path
changes. The first run reports the server's TLS certificate SHA-256 — copy it
into `$FtpCertFingerprint` in `deploy.settings.ps1`.

**5. Send the production config, once.**

```powershell
.\deploy\deploy.ps1 -UploadConfig
```

Every normal deploy deliberately skips this file, so the server keeps its own
copy and nobody's laptop can overwrite the shop's connection string by accident.

**6. Deploy.**

```powershell
.\deploy\deploy.ps1
```

---

## The database

Migrations are applied automatically when the app starts
(`db.Database.MigrateAsync()` in `Program.cs`), so a schema change ships with
the code and nothing has to be remembered.

**Back the database up before deploying a release that contains a migration.**
The migration runs by itself and there is no undo.

---

## Everyday use

```powershell
.\deploy\deploy.ps1                 # full deploy
.\deploy\deploy.ps1 -CheckOnly      # connect and inspect, upload nothing
.\deploy\deploy.ps1 -SkipBuild      # re-upload the last publish, no rebuild
.\deploy\deploy.ps1 -UploadConfig   # send appsettings.Production.json deliberately
```

**Deploy outside shop hours.** A cashier watching "جارٍ التحديث" does not know
when it will be back.

The first deploy uploads the whole self-contained runtime and takes a few
minutes. A normal code change after that is a few hundred KB, because the sync
compares timestamps and sizes and sends only what differs.

---

## What the deploy refuses to do

The script stops rather than guess, because FTP has no idea that two applications
are different products:

- **the target folder holds another application** — it looks for other apps on
  this host (`Alrand.Api.exe`, `ConstructionProgress.Api.exe`, `AttendaceApp.exe`)
  and refuses outright.
- **the target is not empty and holds no `NewsApp2.exe`** — probably the wrong
  folder. Run `-CheckOnly` to see what is in there.
- **`deploy.settings.ps1` is missing, or still says FILL-ME**
- **`appsettings.Production.json` still says FILL-ME** (on `-UploadConfig`)

It also never deletes on the server, so a wrong exclude mask can never wipe the
logs or a backup folder. Four server paths are excluded from every sync:
`appsettings.Production.json`, `App_Data/`, `logs/`, `backups/`.

---

## When something goes wrong

**"Uploaded, but the site never answered 200."**
The files are in place but the app did not start. Read `logs\stdout*.log` on the
server — a bad connection string or a failed migration shows up there. Fix the
config and restart the app in Plesk; the files do not need uploading again.

**The shop is stuck on "جارٍ التحديث".**
The deploy failed after the sync and `app_offline.htm` is still on the server.
Re-run the deploy — removing it is the last step.

**A file-lock error during the upload.**
IIS had not finished releasing `NewsApp2.exe`. Re-run; `app_offline.htm` is
already up, so the second run is quick.

**"Certificate verification failed" on a run that used to work.**
Plesk regenerated the server's certificate. Take the new SHA-256 out of
`deploy/deploy.log` — search it for `fingerprint` — and put it in
`$FtpCertFingerprint`. Do not set it back to `"*"`.

**Details of the last run** are in `deploy/deploy.log` (gitignored, overwritten
each run).