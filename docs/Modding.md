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
ContentPacks/oasis-moon-addon.json   - adds the Moon and Dawn realms and return portals
```

Root fields:

```text
schemaVersion: 1
id: unique lowercase identifier, letters/digits/dashes (starts a letter)
name: display name, up to 100 characters
startLevel: stable positive level number (default 1)
extends: optional base pack ID; used only by addon files
levels: array of realm definitions
readableItems: optional item-name-to-text dictionary
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
features ({"inscription":"Descriptive text"}), image (optional),
descriptionStates (optional ordered rules), exitKinds (optional route labels).
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

The included base adventure is `Pyramid.Web/ContentPacks/oasis-of-stars.json`; `oasis-moon-addon.json` demonstrates a separate extension with a two-way portal. See the [developer walkthrough](Developer-Walkthrough.md) for its solution and [deployment guide](Deployment.md) for private content storage.

An adventure is won when every realm with nonempty winFlags has all its flags set. Realms with empty winFlags are exploration areas. Completion records a finish time and the standard victory bonus once; players may continue exploring. The expanded Oasis of Stars is a complete three-realm example. See [its developer walkthrough](Oasis-Walkthrough.md) for the full solution.

## Descriptions that follow game state

Room `description` remains the default. Optional `descriptionStates` is an ordered array of up to 40 rules. All conditions on a rule must match. The **last matching rule** supplies the replacement description. Rules read current realm puzzle flags and current-room loose items; an item held by any player is absent from the room. Returning or dropping an item updates the next LOOK automatically. Rules do not change saves, award points, consume items, or execute scripts.

```json
"descriptionStates": [
  {
    "presentItems": ["bronze key"],
    "absentFlags": ["portal-open"],
    "description": "A bronze key rests beside the closed arch."
  },
  {
    "absentItems": ["bronze key"],
    "absentFlags": ["portal-open"],
    "description": "The arch is closed. The key is no longer here."
  },
  {
    "requires": ["portal-open"],
    "description": "Starlight shines through the open arch.",
    "features": {"inscription": "The lock is settled; the eastern portal stays open."}
  }
]
```

Each rule requires nonempty `description` text (up to 2,000 characters). The four condition arrays default to empty: `requires` means all listed puzzle flags must be set; `absentFlags` means none may be set; `presentItems` means every named object must be loose in this room; `absentItems` means none may be loose here. An empty set of conditions always matches. Put more specific rules later. Flag references must exist in this realm. Item names use the normal lowercase item syntax and may include objects carried in from other rooms. A condition cannot require and exclude the same flag or object.

Optional rule `features` overrides matching EXAMINE/READ text by feature name. Matching overrides merge over the room's permanent `features` in rule order; the last matching override of each key wins. Other feature text remains unchanged. Up to 40 features are allowed per rule, with the same names and text limits as permanent features. EXAMINE of an unknown feature falls back to the current conditional room description.

Prefer neutral base descriptions for shelves, cushions and pedestals. The live `You see:` line already lists available objects. Avoid writing that an object is present in permanent prose unless a `presentItems` condition controls it. Likewise, give solved mechanisms a flag-based description and feature override so an open grate is not still described as locked.

Older packs can omit `descriptionStates` entirely. The rules are recalculated from saved flags and room contents, including shared expedition state; no save migration is needed.

## Passage kinds for maps

A room may add an `exitKinds` object. Keys must match its existing exits; values are `passage`, `stairs`, `ladder`, `hole`, or `bridge`. For example, `"exitKinds": {"down":"stairs"}` marks an existing downward exit as a stairway. Describe both ends consistently, such as stairs down from the well and stairs up from the cistern. This metadata labels the map connection without changing direction, access requirements, or movement. Older rooms may omit it.

## Readable items

A complete pack or add-on can define `readableItems`, a dictionary from exact item names to plain text (up to 100 entries, 2000 characters each). For example: `"readableItems": {"papyrus scroll": "The river remembers the moon."}`. Place the corresponding item in a room's `items` list. READ requires the object to be carried or visible in the current room; unique short names work. The text is copied once into that player's journal. Repeated reading does not add duplicate notes; a full 100-entry journal still displays the writing. EXAMINE suggests READ. Add-ons cannot redefine a readable item already defined by the parent. The designer's Readables tab authors this dictionary.

A readable object's text belongs to its adventure, not to a global item dictionary. The bundled Moon extension assigns its existing star-chart clue to `readableItems["star chart"]`; Chamber of the Gods supplies its separate built-in chart and lapis-tablet writing. READ first resolves an accessible item. If no item matches, a mod can answer from a current-room feature with that name. Use distinct names when portable writing and fixed scenery should remain easy to tell apart. Named READ/EXAMINE feature overrides follow the matching room-state rules.

Built-in action synonyms such as FEED BIRD are not injected into custom puzzles. Author the command stated in your clue. Players can use a unique shortened name for READ, but ordinary mod GET/DROP commands still use the exact item names.
