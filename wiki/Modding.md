# Content packs and portals

The server administrator installs reviewed JSON files in ContentPacks,
outside wwwroot, and restarts the app. Players cannot upload server code or
install packs through the web UI. JSON can define rooms, directions, loose
items, descriptive features, command puzzles, gates and portals. It cannot
run scripts, access files, SQL, network services or change authentication.
Anyone with the source can create and distribute packs for admin review.

Included working examples (in the source and server publish package):

```text
ContentPacks/oasis-of-stars.json     - standalone adventure
ContentPacks/oasis-moon-addon.json   - adds a realm and two-way portal
```

Root fields:

```text
schemaVersion: 1
id: unique lowercase identifier, letters/digits/dashes (starts a letter)
name: display name, up to 100 characters
startLevel: stable positive level number (default 1)
extends: optional base pack ID; used only by addon files
levels: array of realm definitions
portals: array of directional realm connections
```

Level fields:

```text
number, name, objective, start (room ID), rooms (object keyed by room ID),
puzzles (array), winFlags (array of puzzle flags marking realm completion).
```

Room fields:

```text
name, description, lit (default true), exits ({"west":"room-id"}),
requires ({"west":"flag"}), items (["bronze key"]),
features ({"inscription":"Descriptive text"}), image (optional).
```

Image may reference room-art/room1.png through room-art/room60.png, or
mod-art/<base-pack-id>/<scene-name>.png. Custom scene names use lowercase
letters, digits and dashes, beginning with a letter. For source builds place
reviewed PNGs in Client/public/mod-art/<base-pack-id>/; admins deploying
content directly place them in wwwroot/mod-art/<base-pack-id>/. Keep custom
assets in deployment backups. External URLs, SVG, traversal paths and
arbitrary file reads are rejected. PNGs are optional; missing art does not
prevent play. Artwork should not reveal puzzle solutions or private notes.

Puzzle fields (names are case insensitive in JSON):

```text
room, command, needs (carried items), requires (earlier local flags),
flag (unique result), response, hints (one to five strings).
```

Commands and item names are lowercase; commands have single spaces and
must not shadow built-in commands. Example command: "turn key".
Successful puzzles set a flag once; they do not consume items. Requirement
flags are local to that realm. Room exits and portal gates use those flags.
Completion grants an achievement and permits continued exploration.

Portal fields:

```text
fromLevel, fromRoom, direction, toLevel, toRoom, requires (optional flag),
description. Directions: north, south, east, west, up, down.
```

Create a reverse portal explicitly if players should return. A portal
cannot replace an existing exit in the same direction. The example addon
does not modify the original oasis file; it supplies new levels and portal
definitions that are merged at startup. Packs are separate adventures;
cross-pack travel and JSON overrides of the built-in six-level campaign
are not supported in schema 1. Addons extend JSON adventures, not C# rules.

Keep pack IDs, level numbers, room IDs and item/flag names stable after
release. Additive new rooms are initialized without respawning previously
collected items. Deleted/renamed rooms or removed packs may make a save
unavailable; restore the matching content files. Saves are not discarded.
Test both a fresh game and an existing save before installing an update.
The loader rejects bad references, excessive sizes, unknown fields and
unsafe asset paths, but cannot prove that your story is solvable. Playtest
for circular dependencies, one-way traps and indispensable items held by
offline players. Back up database, keys and exact content versions together.
Limits: 1 MiB per file, 100 realms/pack, 100 rooms/realm, 100 puzzles/realm,
200 portals/pack. Startup rejects an invalid pack with a diagnostic.

## Authoring workflow

1. Copy the included oasis examples into a separate test content directory.
2. Choose stable identifiers and write the rooms, puzzles and exits.
3. Point a local Development server at that directory with `--Pyramid:ContentDirectory "<absolute-content-directory>"`.
4. Restart to validate the pack, then create a new expedition selecting its name.
5. Test puzzle prerequisites, return routes, a missing tool and an offline companion holding a critical item.
6. Test an existing save before distributing an update. Include the exact content version in server backups.

The included base adventure is `Pyramid.Web/ContentPacks/oasis-of-stars.json`; `oasis-moon-addon.json` demonstrates a separate extension with a two-way portal. See the [developer walkthrough](Developer-Walkthrough) for its solution and [deployment guide](Deployment) for private content storage.
