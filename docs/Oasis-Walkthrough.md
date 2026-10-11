# The Oasis of Stars — developer walkthrough

This is a complete second adventure installed through the JSON content-pack system. It contains **22 rooms, three connected realms, and 13 puzzles**. It uses the existing command parser, inventory, cooperative expeditions, explored-room map, and saved realm state. No script execution or custom server code is required by this adventure.

The base file is `Pyramid.Web/ContentPacks/oasis-of-stars.json`. Its Moon and Dawn extension is `Pyramid.Web/ContentPacks/oasis-moon-addon.json`. Install both files. The extension is merged into the base adventure and is not a separate game in the selector.

## Intended experience

The caravan arrives at an oasis where dawn has stopped. Restore its spring, recover the moon's voice, and carry both gifts to a realm that has forgotten how to begin a day. The final crown combines clues from two earlier locations; a final choice asks whether a ruler should preserve life or seek glory.

There is no timer or irreversible wrong answer. Every realm has a return portal. Objects remain where they are dropped and can be collected again. Puzzle tools are not consumed. A player who passes the oasis too quickly can return for its water seal. In a shared expedition, explorers may investigate different branches and exchange objects or findings when together.

The old demonstration path still works: camp → well → camp → arch → TURN KEY → east → temple → RING BELL. That only begins the expanded adventure. Ringing the bell is no longer the end of the game.

## Deterministic route

Start a new **The Oasis of Stars** expedition. Enter each line below as a separate command. These routes include deliberate tool drops to keep the satchel below its eight-object limit, even with the standard lamp and bottle. Map and hint requests are optional and cost points; none is required here.

### 1. The Forgotten Oasis

Visit the workshop, restore the spring, take its seal, and unlock the original portal.

```text
north
get brass crank
examine plan
south
west
get bronze key
examine reeds
down
fit crank
east
turn wheel east
north
examine bowl
bless water
east
get water seal
examine seal
west
south
west
up
east
east
turn key
drop bronze key
drop brass crank
east
```

Expected position: **Moonlit landing**, realm 2. The first realm has `portal-open`, `crank-fitted`, `water-flowing`, and `water-blessed`. Keep the water seal; the bronze key and brass crank can stay at the arch.

### 2. The Moon Realm

Collect the mirror and archive clues, align the stars, and repair the bridge. The archive's guardian clue and distant-light riddle are needed in the final realm.

```text
north
get hand mirror
examine stele
south
east
ring bell
north
get star chart
get cedar plank
examine chart
examine scroll
examine memory
south
east
align mirrors
drop hand mirror
east
set stars three one two
drop star chart
west
west
south
lay plank
drop cedar plank
east
get moon shard
examine carving
west
north
east
east
north
```

Expected position: **Threshold of the unwritten dawn**, realm 3. The second realm has `moon-awake`, `mirrors-aligned`, `stars-aligned`, and `bridge-mended`. Carry the water seal and moon shard into the final realm. The observatory portal stays open after dropping the chart.

### 3. The Unwritten Dawn

Restore breath and memory, solve the three-guardian order, then make the final offering.

```text
north
still wind
tune organ
east
get dawn feather
examine inscription
south
examine crown
west
speak starlight
east
set crown ibis jackal falcon
north
examine bowls
offer water to stranger
```

Expected result: **THE OASIS OF STARS IS COMPLETE**. The final realm has `wind-stilled`, `voice-restored`, `memory-restored`, `crown-aligned`, and `dawn-returned`.

## Puzzle and clue reference

| Realm | Puzzle command | Required objects or preparation | Clue |
| --- | --- | --- | --- |
| Oasis | `TURN KEY` | Bronze key from dry well | Portal inscription |
| Oasis | `FIT CRANK` | Brass crank from workshop | Workshop plan and cistern spindle |
| Oasis | `TURN WHEEL EAST` | Crank fitted | Well reeds point toward sunrise |
| Oasis | `BLESS WATER` | Water flowing | Shrine bowl |
| Moon | `RING BELL` | None | Temple inscription |
| Moon | `ALIGN MIRRORS` | Hand mirror from dunes | Mirror bracket |
| Moon | `SET STARS THREE ONE TWO` | Star chart, mirrors aligned | Archive chart |
| Moon | `LAY PLANK` | Cedar plank, bell ringing | Bridge anchors |
| Dawn | `STILL WIND` | Water seal | Moon-vault carving and basin |
| Dawn | `TUNE ORGAN` | Moon shard, wind stilled | Same carving and basin |
| Dawn | `SPEAK STARLIGHT` | None | Memory question and archive memory |
| Dawn | `SET CROWN IBIS JACKAL FALCON` | Dawn feather, voice and memory restored | Crown's river/grave/sky order plus archive guardians |
| Dawn | `OFFER WATER TO STRANGER` | Water seal, moon shard, dawn feather; crown aligned | Garden inscription and sanctum bowls |

Each puzzle has three escalating hints, with the final hint providing the literal command. No single-player action requires a second player.

## Compatibility and regression expectations

- Preserve pack IDs `oasis-of-stars` and `oasis-moon-addon` and the `extends` relationship.
- Preserve original room IDs `camp`, `well`, `arch`, `landing`, and `temple`, their existing exits, `portal-open`, and `moon-awake`.
- Old saved expeditions remain at their recorded room. Newly introduced rooms receive their initial objects when that realm is restored. Existing objects and flags remain intact.
- A legacy `The Moon Realm complete` achievement may remain as history. Actual campaign completion must evaluate current required flags, not that earlier achievement alone.
- Realm 1 completion requires `portal-open` and `water-blessed`; realm 2 requires `moon-awake`, `stars-aligned`, and `bridge-mended`; realm 3 requires `dawn-returned`.
- Verify sealed exits before their puzzle, failure without required objects, no repeated puzzle reward, portal round trips, and persistence after restoring a serialized expedition.
- Verify the complete route above wins; visiting the Moon Realm and ringing the bell alone must not win.
- Original **Chamber of the Gods** progress remains independent of this adventure.

The artwork references existing bundled gold room illustrations. New artwork can be supplied later through the pack's permitted `mod-art/oasis-of-stars/` image directory without changing room IDs or saves.

## Version 1.4.1 presentation checks

The literal route above is unchanged. Solved mechanisms now have updated LOOK descriptions and relevant EXAMINE text. After taking the water seal, LOOK on the terrace should no longer claim it is on the shelf; dropping it there restores the present-item description. Revisit the opened grate, aligned mirrors and completed sanctum to confirm their prose reflects saved flags.

In the archive, `READ STAR CHART` is an optional alternative to viewing the fixed chart clue with EXAMINE. READ requires the chart on the floor here or in your satchel, and copies its writing into your personal journal once. It does not solve the observatory puzzle automatically. The guardian and distant-memory clues remain fixed archive features.

MAP at the well and cistern shows visited floors connected by stairs. The Moon bridge and vault use bridge route labels. No map opens new rooms or solves a mechanism. Positive point changes, puzzle completion and victory have distinct browser sounds; mute them with CHIMES OFF. These optional checks change action counts or map costs, so use a separate test save when comparing the deterministic route.
