# Changelog

## 1.4.2 — 2026-10-11

- Made Create an account a prominent full-width button and gave password recovery a separate button with an underlined action.
- Added clearer keyboard focus and larger tap targets for login-page account options.
- Registration now identifies which field needs attention rather than blaming email, name and password together. Surrounding spaces in names and email addresses are trimmed; passwords are unchanged.
- Mixed-case email addresses and .tv domains remain supported.

## 1.4.1 — 2026-10-11

### Tablet reading fix

- `READ LAPIS TABLET` displays an inscription and records it once in the journal.
- `EXAMINE LAPIS TABLET` explains how to read it.
- Carrying the lapis relic no longer intercepts `READ TABLET` in the keeper's study. The study clue and collectible inscription remain separate.

### Changes since the initial public release

- An illustrated adventure library offers saved journeys and new expeditions after sign-in.
- The Oasis of Stars adds a second adventure: 22 rooms, three realms and 13 puzzles, implemented as JSON content packs.
- The browser adventure designer supports rooms, puzzles, clues, items, locked exits, portals, illustrations and JSON import/export. Drafts are scoped to the signed-in explorer and saved on the current device.
- Conditional room descriptions and EXAMINE overrides respond to puzzle flags and loose-item presence. Authors can preview states before exporting.
- Current room descriptions no longer claim collected objects are still present. Solved mechanisms describe their changed state.
- Readable items copy authored text to the player's journal once. Item availability, lighting and journal capacity are checked.
- Command aliases include `FEED BIRD`, `CLEAN MIRROR`, `LAY BOARD`, and common alternative item names.
- Visited-only maps separate relative floors, label passage types, distinguish portals and show one-way travel. Mobile players can switch between route cards and the drawing.
- Sound cues distinguish room activity, arrivals, points, puzzle success and victory. One update plays one prioritized cue, and the sound toggle mutes all cues.
- Mobile navigation provides access to the terminal, satchel, explorer information, maps and adventure selection.
- Administration includes account activity, email approval, role management, adventure resets, settings, bounded log views and clearing, plus validated gameplay export/import.
- The verification gate explains the pending email and offers resend/check controls. Normal explorers return to gameplay when leaving an administrative URL.
- Updated player, author, API, administrator, development and deployment documentation, including both adventure walkthroughs.

### Compatibility and scope

Existing saves remain supported. New content fields are optional; older packs retain their authored behavior. Backups and validation precede production upgrades. Custom packs and artwork are preserved.

The designer exports data for administrator review; it does not publish directly, execute scripts or synchronize drafts between devices. Arrival audio is available for companions; roaming mummy gameplay has not been added.

### Validation

The local .NET suite passed 860 checks. Map-layout and sound-event checks, frontend builds, desktop/mobile UI checks, and server validation of an actual designer export also passed. Production activation verified database row preservation and public health checks. Browser sound checks verified emitted motifs; perceived volume depends on the player's device and browser settings.
