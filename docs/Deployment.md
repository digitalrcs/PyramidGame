# Deployment and recovery

Pyramid is a server application. A static hosting folder cannot run it. Use one application instance, a local SQLite database and persistent private keys. The application can sit beneath an existing site at `/pyramid/` on IIS or behind nginx on Linux.

All paths and hostnames below are examples. Keep deployment secrets and player data outside source control and web roots.

## Build

Build the frontend before publishing the server. `BUILD-WEB.ps1` performs the frontend build, .NET checks and framework-dependent publish for IIS. For a self-contained Linux x64 release, after building the frontend:

```text
dotnet publish Pyramid.Web -c Release -r linux-x64 --self-contained true -o artifacts/linux-release
```

Self-contained publishing includes the .NET runtime; rebuild and redeploy when servicing that runtime. The host still needs the operating system libraries required by .NET. Keep the previous release available for rollback.

## Shared production settings

| Setting | Purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT=Production` | Enforces production behavior, including HTTPS |
| `AllowedHosts=example.com` | Restricts accepted host names; separate additional hosts with semicolons |
| `Pyramid__DataDirectory=<private-directory>` | SQLite database and persistent `keys/` |
| `Pyramid__ContentDirectory=<content-directory>` | Reviewed JSON packs outside the public web root |
| `Pyramid__AllowRegistration=true` | Set false to close registration |
| `Pyramid__PublicBaseUrl=https://example.com/pyramid/` | Trusted base URL for email links |

Configure mail and explicitly promote an existing administrator as described in [accounts and email](Accounts-and-Email.md). Do not leave bootstrap settings enabled. Do not run Capture-mode email in Production.

## IIS

1. Install the serviced .NET 10 Hosting Bundle and configure HTTPS on the parent site.
2. Create a dedicated 64-bit application pool with No Managed Code and one worker process.
3. Add a child **Application** named `pyramid` with its own physical directory and pool. A virtual directory alone is insufficient.
4. Copy the published output into the child application's directory. Preserve the parent site's files and configuration.
5. Give the pool read/execute access to the release and Modify access only to the private data directory.
6. Configure production environment variables in the child application's `aspNetCore` element. Preserve these host-specific settings on upgrades.

IIS supplies the application path. Do not set `Pyramid__PathBase` for ordinary IIS child-application hosting. Exclude the child application from parent rewrite rules if necessary. Do not overwrite the parent `web.config` with the publish-generated child configuration.

Windows production keys use machine DPAPI as well as filesystem permissions. They cannot simply be copied to a different machine and decrypted there. Plan key rotation and renewed sign-ins when moving between hosts; retain the original backup for recovery.

## Linux and nginx

Run the application as a dedicated unprivileged service identity. Keep releases read-only to that identity, with write access to its private data directory. Bind Kestrel to loopback, for example `http://127.0.0.1:5188`, and configure:

```text
Pyramid__PathBase=/pyramid
Pyramid__TrustedProxy=127.0.0.1
```

Trust only the actual reverse proxy address. Preserve the prefix in the nginx proxy route:

```nginx
location = /pyramid { return 308 /pyramid/; }
location ^~ /pyramid/ {
    proxy_pass http://127.0.0.1:5188;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header Connection "";
    client_max_body_size 8k;
}
```

The `^~` prefix keeps a parent site's asset regex from intercepting game assets. Configure this inside the existing HTTPS server block, validate with `nginx -t`, and reload only after validation succeeds. Keep the main site's root and routes intact.

Store the service environment file outside the release with restrictive permissions. A systemd service should restart on failure, use a restrictive umask, and allow writes only where required. Database and keys must not be downloadable through nginx. Linux keys rely on private filesystem permissions unless the operator adds another key-protection mechanism.

## Backups and restore

Back up the SQLite database, Data Protection keys, exact content packs, custom mod images and protected configuration together. Keep backups outside every web root with restricted access. Set retention and disk monitoring appropriate to the host.

For a simple consistent backup, stop only the game service or application pool, copy the complete private data directory and matching content/configuration, then restart. For an online backup, use SQLite's backup API. Copying an active `.db` alone can omit committed WAL data.

Test restoration into a separate private directory and instance, isolated from outbound mail and public traffic. Saved dashboard settings override environment defaults, so environment variables alone may not disable restored SMTP or registration. Keep the test host isolated until its runtime settings have been reviewed. Confirm database integrity, expected account/progress records and application startup. Do not send old queued messages during a restore test. Confirm key portability before moving between machines or operating systems.

During an upgrade, retain external data and keys, back up first, replace the release, restart, then verify. A code rollback may also need its matching database/content backup if a migration changed their format. Do not delete the previous release or backup until the new version is verified.

## Post-deployment checks

- `/pyramid/` loads over HTTPS and `/pyramid/health` returns `{"status":"ok"}`.
- The parent website still loads unchanged.
- Assets and API requests remain beneath the application prefix.
- Session cookies are Secure, HttpOnly, SameSite=Strict and path-scoped.
- Anonymous private API requests fail; POST requests without valid CSRF fail.
- Two disposable accounts can maintain separate solo saves and cooperate in a shared expedition.
- Progress survives sign-out and a service restart.
- Private database, keys, content definitions and configuration cannot be downloaded.
- Verification/recovery links use the public HTTPS URL; verify SMTP acceptance separately from inbox delivery.
- Remove disposable verification accounts after testing and confirm backup restoration.

A subfolder shares its browser origin with the parent website. Keep that site trusted and maintained. This architecture is not a web farm: horizontal scaling needs coordinated rate limits and a suitable shared persistence design.

## Portable gameplay backup and import

The [administration dashboard](Administration.md) can export accounts and gameplay as private JSON and restore them on a compatible installation. Keep installed content packs alongside these exports. The export does not include SMTP/settings, Data Protection keys, content files, sessions, recovery tokens or queued email; continue taking complete host backups as described above.

The import form accepts a JSON file up to 20 MiB and wraps it in a request object. Reverse proxies and IIS request filtering must allow that body size specifically on `/pyramid/api/admin/backup/import`, including a little allowance for the wrapper. Keep the smaller limit on normal gameplay routes. Follow the supplied deployment templates rather than increasing every endpoint's allowance. Application-level administrator, CSRF, size and snapshot validation still apply.

Before import, the application writes a recovery JSON export beneath the private data directory in `ImportRecovery`. That directory must remain outside the web root, writable by the service and restricted to the application/operator. Include it in retention and disk-capacity planning. Import replaces all accounts and game state and revokes all sessions. If an imported snapshot removes the previous administrator, recovery requires an administrator login from that snapshot or an operator retrieving the pre-import export.

## Upgrade preflight and content preservation

Against a private copy of the database and matching keys, use the new release with its normal protected configuration and `--Pyramid:ValidateOnly true`. This applies schema migrations and validates content/settings without opening a listener or starting the mail worker. It does modify the selected database: point it at the rehearsal copy first. Compare account, adventure and settings records before activating the release. Never substitute an empty development database for production data.

Keep the persistent data path and Data Protection application/key configuration stable across releases. Preserve administrator-installed packs and images, but review and install updated bundled packs from the release as well; blindly copying an entire old ContentPacks directory over the new release can discard new features and fixes. Keep stable pack, realm and room IDs so saved adventures continue to resolve. The browser designer's local drafts are not included in server backups; authors must export them separately.

Complete host backups include the `installation_owner` binding and `site_settings` database records. Portable gameplay exports intentionally exclude both, and cannot transfer or replace GOD ownership. On an owner-bound installation, portable import requires that owner and must preserve its account ID, exact name and active administrator role.
