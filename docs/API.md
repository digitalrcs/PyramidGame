# HTTP API

The API uses JSON and same-origin session cookies. All paths below are relative to the application prefix, for example `/pyramid/`. It is the bundled client API, not an API-key service.

## Game and expedition routes

```text
GET  api/session          -> CSRF token, signed-in user, registration flag
POST api/register         {name,email,password}; creates local explorer account
POST api/login            {name,password}; name is explorer name, approved email or unique pending email
POST api/logout           {}; ends session and puts explorer in trance
GET  api/play             -> {game,party}; current personal projection
POST api/play/command     {text,revision,expeditionId}; null ID means solo
GET  api/explorers        -> public progress for recent signed-in explorers
GET  api/expeditions      -> {active,games,packs}; no private puzzle solutions
POST api/expeditions/create {name,pack,open} -> new expedition ID
POST api/expeditions/action {id,action,target}
```

Actions: resume, accept, decline, request, solo (empty id/target), open,
close, invite (target=explorer name), approve/deny/revoke (target=user ID).
Host-only actions are checked on the server. Approval creates an invitation
for acceptance; it never switches another player's active game remotely.

Keep the same session cookies. All POST requests, including login/register,
require X-Pyramid-CSRF with the token from api/session. Refresh that token
after signing in/out. Cookies are HttpOnly, SameSite=Strict, Secure in
Production, and expire after eight hours. No API keys or cross-origin API
access are provided. Never put passwords or session cookies in URLs.

Use game.revision and party.id from the most recent api/play. HTTP 409
command responses include {error,play} containing the current state. Never
blindly retry a mutation. HTTP 400 indicates validation, 401 sign-in needed,
403 denied access (including pending email verification), and 429 rate limiting. Poll no faster than the
bundled client's five seconds; the game/account policy allows 120 requests/minute per account; authentication routes allow 12 requests/minute per source IP.
Legacy api/game and api/command remain original-solo-only endpoints.

## Account and administrator routes

All POST routes require X-Pyramid-CSRF and the same cookie jar. Paths are
relative to /pyramid/ (or the configured application prefix).

```text
POST api/register {name,email,password}
POST api/login {name,password}  name accepts explorer name, approved email or unique pending email
POST api/forgot-password {email}
POST api/reset-password {token,password}
POST api/verify-email {token}
GET  api/account
POST api/account/email {email,password}  current password required
POST api/account/resend-verification {}
POST api/account/notifications {enabled}
GET  api/admin/users  users, audit, logins, safe mail status
POST api/admin/action {target,action,confirmation}  target is account ID
POST api/admin/message {target,subject,message}
```

Do not expose internal bootstrap through HTTP. Existing game/session routes
retain authorization and optimistic revision checks. session.user includes
`isAdmin`, `isGod`, `role`, verification status and backup-import eligibility. Recovery tokens are never returned by public API responses.

## Command flow

1. Fetch `GET api/session` and retain its CSRF token and cookies.
2. Submit `POST api/login`, then fetch the session again for the current token.
3. If `verificationRequired` is true, complete verification or obtain administrator approval, then refresh the session. Otherwise fetch `GET api/expeditions` to select or resume a journey. Fetch `GET api/play` only when entering gameplay and retain `game.revision` and `party.id`.
4. Submit the command with the same cookies and `X-Pyramid-CSRF` header:

```json
{"text":"look","revision":0,"expeditionId":null}
```

Replace the sample revision with the latest server value. Use the current party ID for shared play; `null` selects solo. On a conflict, inspect the returned state and let the player decide whether to try again.

`play.game` includes `mapsUsed`, `bonusPoints`, `roomActivity`, `roomChanges` and `mapSnapshot`. `roomChanges` contains `visible`, `openRoutes` (directions) and `solved` (completed puzzle flags in the current room). Hidden rooms return empty route/flag arrays. The normal `solved` field is the explorer's numeric puzzle total. These observations support client-side sound notices; they are not command inputs.

A purchased map snapshot contains `serial`, `realm`, `currentRoom`, `nodes` (`id`, `name`) and `edges` (`from`, `to`, `direction`, `kind`). Edge kinds are `passage`, `stairs`, `ladder`, `hole`, `bridge` or `portal`; clients should treat a missing kind as `passage`. Nodes and edges include only personally visited rooms within the current realm. A saved snapshot may be absent until purchase or after a world reset/switch; each `MAP` command costs two points.

`GET api/play` wakes the caller in an active shared expedition, so avoid polling it while the player is choosing an adventure or editing a draft. Readable content uses the ordinary command endpoint (`READ <item>`); there is no server editor-upload or publishing endpoint. The designer exports data files for operator review.

Basic administrator actions are `kick`, `suspend`, `restore` and `delete`; deletion requires the exact explorer name in `confirmation`. Additional actions are described below. The server checks administrator privileges for every action. See [accounts and email](Accounts-and-Email.md) for retention and notification behavior.

## Administration dashboard and backups

`GET api/admin/users` also returns `overview` and `expeditions`, plus each explorer's online status, current room, moves, relics, solved puzzles, achievements, inventory and discovered-room count. These are private administrator responses, not public roster fields. Current progress reflects the active adventure.

Additional `POST api/admin/action` actions:

| Action | Request fields | Effect |
| --- | --- | --- |
| `approve` | `target` account ID, `confirmation` exact explorer name, `expectedEmail` displayed pending/current address | Manually approve the address. A changed address is rejected rather than approving unseen data. |
| `reset-solo` | `target` account ID, `confirmation` exact explorer name | Reset the personal adventure. |
| `reset-expedition` | `target` expedition ID, `confirmation` exact expedition name | Reset the shared world and all members' progress. |

`GET api/admin/backup` downloads the private portable JSON export. `POST api/admin/backup/import` accepts `{confirmation:"REPLACE ALL DATA",backup:<parsed export object>}` with the usual session and CSRF protection. The selected export file is limited to 20 MiB. Successful import invalidates all sessions; obtain a fresh session and sign in with an account from the restored snapshot. Do not automatically retry import after a network error: first determine whether restoration completed.

Exports contain password hashes and private account data. They exclude sessions, email tokens, queued mail, server credentials and content-pack files. See [Administration](Administration.md) for restoration requirements and the private recovery export created before replacement.

## Runtime administration settings

`GET api/admin/settings` returns the current `revision`, `allowRegistration`, `publicBaseUrl`, `maintenanceMessage`, `donationUrl`, `supportUrl`, `chimesDefault`, and `mail` settings. Mail fields are `mode`, `host`, `port`, `username`, `fromAddress`, `senderName` and the boolean `passwordConfigured`; the password is never returned.

`POST api/admin/settings` submits those editable values and the revision from the read. A stale revision returns HTTP 409; reload and review before resubmitting. Within `mail`, an omitted/blank `password` preserves the current secret. `clearPassword:true` removes it, but an enabled `Smtp` configuration cannot be saved without a password. The dashboard requires Disabled mode before offering this control. SMTP supports port 465 with implicit TLS or 587 with mandatory STARTTLS.

`POST api/admin/settings/test-email {}` queues one test to the current administrator's approved email address using saved settings. It does not accept an arbitrary recipient. Test mail is an explicit action, separate from saving settings.

`POST api/admin/logs/clear {kind,confirmation:"CLEAR LOGS"}` accepts `audit`, `logins` or `mail`. It clears only the selected history category; mail clearing excludes pending messages, and audit clearing creates a new record. Login counts and last-login values derived from deleted history are also cleared. All routes require an administrator session; POST requests require CSRF protection. None of these routes delete accounts or adventures.

## Email-verification access gate

Session users include `emailVerified`, `email`, `pendingEmail` and `verificationRequired`. A new or unapproved account receives a limited authenticated session. Account management, verification and sign-out remain available, but gameplay, command, explorer-roster and expedition routes return HTTP 403 with `code:"verification_required"` until the email is approved. Do not treat an authenticated cookie as permission to enter gameplay.

Registration and login also report `verificationRequired`. Registration reports whether verification delivery was queued or disabled. The client should fetch the session again after verification or manual approval; the existing session can then access gameplay. Invalid verification links leave the gate in place. A verified account changing its email remains approved on its existing address until the replacement is confirmed. Signing in by explorer name or a unique pending email never bypasses the gate.

## Role management

`GET api/admin/roles` returns available roles and their assignability. `POST api/admin/role` accepts `{target,role,expectedRole,confirmation}`; `target` is the account ID and confirmation is the exact explorer name. Assignable roles are `explorer` and `admin`. The `god` owner role is not assignable through HTTP. Session/admin-user data include `role` and `isGod`; session data also report `canRestoreBackup`.

Every role mutation requires administrative authorization and CSRF protection. A changed current role returns HTTP 409 so the administrator can review it again. Successful changes revoke the target's sessions and preserve adventures. Self-role changes and unsupported roles are rejected. The installation owner receives additional protection from ordinary administrator account actions and owner-containing expedition resets. On an owner-bound installation, backup import requires the owner and validates that the imported owner record remains intact and active.

`canRestoreBackup` describes owner eligibility, not a grant of administrator access. Both the administrator check and the owner check are enforced by the import endpoint. The request body limit is 21 MiB including its wrapper; the bundled file picker accepts export files up to 20 MiB. Ordinary POST bodies are limited to 4 KiB, while settings updates allow 8 KiB.
