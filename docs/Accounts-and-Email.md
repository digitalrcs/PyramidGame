# Accounts, recovery and administration

The server already stored users, password hashes, sessions and solo/shared
progress in the private pyramid.db SQLite database. Startup now migrates
that database in place, preserving existing explorers and games, and adds
verified/pending email addresses, administrator roles, suspension status,
login history, audit events, one-use recovery tokens and an email outbox.
No SQL Server connection is required. Back up the database and key directory
together before upgrades. Use one application instance on local storage.

Existing players can continue signing in with their explorer name. Account
lets them add email after confirming their current password. New registration
asks for email plus an explorer name. Email stays private and is not added
to the public explorers list. A verification link must be confirmed before
email sign-in or recovery works. Username sign-in remains available.

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
work when these optional messages are disabled. Invitations require an
existing account with verified email, and opening the link never joins a
world automatically. The user signs in, selects it, then accepts.

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
all historical copies. The current administrator cannot target themselves.

The Admin email form sends plain-text information to a selected verified
explorer who has notifications enabled. Delivery is queued, not proof of
inbox receipt. Status counts distinguish pending, sent, failed and captured.
No password hashes, recovery tokens, SMTP passwords or email bodies appear
in the admin response.

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

The SQLite outbox encrypts message payloads with ASP.NET Data Protection, retries delivery up to six times, and clears payloads after completion or failure. Preserve the key directory with the database. Delivery rechecks the account, verified address and notification preference. A crash after provider acceptance can cause duplicate delivery; a sent status does not prove inbox receipt. Old queue metadata is cleaned after 30 days. Up to 10,000 login events are retained; the admin page shows the most recent 100.

## Development mail

Use `Capture` mode and a separate test database to exercise links without sending real messages. Set a loopback `PublicBaseUrl`. Captured `.eml` files appear in the private data directory's `MailCapture` folder and contain usable test links. Capture mode is rejected in Production. Never commit those files.

The Windows `RUN-LOCAL-MAIL.ps1` helper can load an external JSON configuration and a DPAPI-encrypted password file. Run it as the Windows identity that encrypted the password. Do not copy Windows DPAPI secrets to a Linux host. Production has its own protected configuration.

See [deployment](Deployment.md) for filesystem permissions, backup and restore, and the [API reference](API.md) for account routes.
