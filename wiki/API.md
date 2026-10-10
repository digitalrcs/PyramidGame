# HTTP API

The API uses JSON and same-origin session cookies. All paths below are relative to the application prefix, for example `/pyramid/`. It is the bundled client API, not an API-key service.

## Game and expedition routes

```text
GET  api/session          -> CSRF token, signed-in user, registration flag
POST api/register         {name,email,password}; creates local explorer account
POST api/login            {name,password}; name is verified email or explorer name
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
403 closed registration, and 429 rate limiting. Poll no faster than the
bundled client's five seconds; the per-account limit is 120 requests/minute.
Legacy api/game and api/command remain original-solo-only endpoints.

## Account and administrator routes

All POST routes require X-Pyramid-CSRF and the same cookie jar. Paths are
relative to /pyramid/ (or the configured application prefix).

```text
POST api/register {name,email,password}
POST api/login {name,password}  name accepts verified email or explorer name
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
retain authorization and optimistic revision checks. session.user adds
isAdmin. Recovery tokens are never returned by public API responses.

## Command flow

1. Fetch `GET api/session` and retain its CSRF token and cookies.
2. Submit `POST api/login`, then fetch the session again for the current token.
3. Fetch `GET api/play` and retain `game.revision` and `party.id`.
4. Submit the command with the same cookies and `X-Pyramid-CSRF` header:

```json
{"text":"look","revision":0,"expeditionId":null}
```

Replace the sample revision with the latest server value. Use the current party ID for shared play; `null` selects solo. On a conflict, inspect the returned state and let the player decide whether to try again.

`play.game` includes `mapsUsed`, `bonusPoints`, `roomActivity` and `mapSnapshot`. A map snapshot is null before purchase and then contains `serial`, `realm`, `currentRoom`, `nodes` (`id`, `name`) and `edges` (`from`, `to`, `direction`). These reveal only personally visited rooms in the current realm.

Administrator actions are `kick`, `suspend`, `restore` and `delete`; deletion requires the exact explorer name in `confirmation`. The server checks administrator privileges for every action. See [accounts and email](Accounts-and-Email) for retention and notification behavior.
