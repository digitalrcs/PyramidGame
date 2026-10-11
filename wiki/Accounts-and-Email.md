# Accounts, recovery and administration

The server stores accounts, password hashes, sessions, solo/shared progress, verified/pending email addresses, permissions, login history, audit events, recovery tokens and an email outbox in a private SQLite database. Startup applies schema migrations in place. Back up the database and key directory together before upgrades and validate on a copy first. No SQL Server connection is required; use one application instance on local storage.

Existing players can continue signing in with their explorer name. Account
lets them add email after confirming their current password. New registration
asks for email plus an explorer name. Email stays private and is not added
to the public explorers list. Email verification or explicit administrator approval is required before gameplay and email recovery. Signing in by explorer name or a uniquely matching pending email opens a limited account session until approval; it does not bypass verification.

Forgot password sends a generic response regardless of account existence.
For a matching verified account, it queues a link valid for 30 minutes.
The link allows setting a new password, never retrieving the old one.
Successful reset revokes all existing sessions and leaves companions in a
trance. Tokens are single-use and stored as hashes. Verification links last
24 hours. Opening an email link does not perform the action: the user must
submit the verification/reset form. Link tokens use URL fragments and are
removed from the browser address by the client.

Account notification preferences control expedition invitations and admin
information messages. Requested verification and recovery messages still
work when these optional messages are disabled. In-game invitations require an existing explorer account. Email delivery additionally requires an approved address, enabled notifications and a nonsuspended account. An invited unverified explorer must complete approval before accessing expeditions. Opening a link never joins a world automatically; the user signs in, selects it, then accepts.

## Verification before play

New accounts must confirm their email link or receive explicit administrator approval before entering the game. Registration creates a limited session so the explorer can read the verification instructions, resend the link, correct the email address, manage their account or sign out. Gameplay, room commands, the explorer roster and expedition endpoints reject that session until approval. Signing out and back in does not remove the requirement.

After successful verification, the existing session can enter the game; the server checks current approval rather than trusting a stale browser flag. Invalid, expired or already-used links do not grant access. If a previously verified explorer requests a different email address, their existing approved address and gameplay access remain active while the replacement is pending.

When email delivery is disabled, the account still requires approval. Contact an administrator rather than expecting an unavailable verification message. A pending email may be used for limited sign-in only when it identifies one account unambiguously; use the explorer name if it is ambiguous. Recovery still requires an approved address.

## Administrator controls

Admin is shown only for an administrator; all routes also check the role
on the server. It lists users, effective current level/score, login counts,
recent successful/failed sign-ins, moderation audit and mail status counts.

Kick ends all sessions and preserves progress; the user can sign in again.
Suspend ends sessions and prevents sign-in until an administrator restores
access. Delete requires typing the exact username. It removes the account,
solo save, party membership, tokens, login records and queued mail. Shared
expeditions with remaining members survive: ownership transfers, non-lamp
carried items return to the room, and empty expeditions are removed. Audit
records retain internal actor/target IDs. Shared notes/messages already
received by other users, private backups and Development capture files are
not retroactively erased. Do not describe this as comprehensive erasure of
all historical copies. The current administrator cannot kick, suspend or delete their own account. Manual email approval and a personal-adventure reset are available for their own account.

The Admin email form sends plain-text information to a selected verified
explorer who has notifications enabled. Delivery is queued, not proof of
inbox receipt. Status counts distinguish pending, sent, failed and captured.
No password hashes, recovery tokens, SMTP passwords or email bodies appear
in the dashboard user-list response. The separate private backup download does include password hashes.

See [Administration](Administration) for the dedicated dashboard, manual email approval, personal/shared adventure resets and portable JSON backup/import. The dashboard is available at /pyramid/admin for an installation beneath /pyramid/.

## First administrator

Register the intended account first. Run this once from the published application's directory using its normal environment and private data directory:

```text
dotnet Pyramid.Web.dll --Pyramid:DataDirectory "<private-data-directory>" --Pyramid:BootstrapAdmin "<existing-explorer-name>" --Pyramid:BootstrapOnly true
```

An optional `--Pyramid:BootstrapEmail "<email-address>"` queues verification; it does not verify the address automatically. Remove bootstrap settings after the operation. The first public registrant never becomes an administrator automatically.

## SMTP configuration

Default mail mode is `Disabled`. Configure `Smtp` through protected host configuration outside the repository and published assets:

```text
Pyramid__PublicBaseUrl=https://example.com/pyramid/
Pyramid__Mail__Mode=Smtp
Pyramid__Mail__Host=<smtp-host>
Pyramid__Mail__Port=465
Pyramid__Mail__Username=<smtp-account>
Pyramid__Mail__FromAddress=<authorized-sender-address>
Pyramid__Mail__SenderName=Pyramid
Pyramid__Mail__Password=<secret-from-protected-storage>
```

Port 465 uses implicit TLS; port 587 requires STARTTLS. Certificate validation stays enabled. Use an authorized sender address and check outbound access on the chosen port. `PublicBaseUrl` must match the trusted HTTPS deployment URL, including the application folder. It is not derived from request headers.

The SQLite outbox encrypts message payloads with ASP.NET Data Protection, retries delivery up to six times, and clears payloads after completion or failure. Preserve the key directory with the database. Delivery rechecks the account, verified address and notification preference. A crash after provider acceptance can cause duplicate delivery; a sent status does not prove inbox receipt. Old queue metadata is cleaned after 30 days. Approximately 10,000 recent login events are retained; the admin page shows the most recent 100.

## Development mail

Use `Capture` mode and a separate test database to exercise links without sending real messages. Set a loopback `PublicBaseUrl`. Captured `.eml` files appear in the private data directory's `MailCapture` folder and contain usable test links. Capture mode is rejected in Production. Never commit those files.

The Windows `RUN-LOCAL-MAIL.ps1` helper can load an external JSON configuration and a DPAPI-encrypted password file. Run it as the Windows identity that encrypted the password. Do not copy Windows DPAPI secrets to a Linux host. Production has its own protected configuration.

See [deployment](Deployment) for filesystem permissions, backup and restore, and the [API reference](API) for account routes.

## Editing settings from the dashboard

Administrators can edit SMTP and site preferences in **Settings**. The dashboard returns only a password-configured indicator, never the password. Blank password input retains the current secret; replacement is explicit, and clearing requires disabled delivery. Runtime settings are protected privately on the server and excluded from portable gameplay exports. See [Administration](Administration) for revision conflicts, test delivery, registration and log maintenance. Environment configuration remains suitable for initial deployment; include protected runtime settings and their key material in complete host backups.

## Installation owner

An operator can bind one existing active account as the installation owner using `--Pyramid:BootstrapGod "<existing-explorer-name>" --Pyramid:BootstrapOnly true` with the installation's normal data/configuration. Use the exact explorer name. This promotes that account to administrator and binds the GOD role; it does not approve email. A different account cannot replace an existing owner through this command. Owner protections and restore requirements are described in [Administration](Administration).

Saved dashboard settings override initial environment mail settings. For a restored test database, do not assume `Pyramid__Mail__Mode=Disabled` overrides an existing saved SMTP configuration; isolate outbound mail before starting the worker.
