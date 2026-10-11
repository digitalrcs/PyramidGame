# Adventure designer

Open **Adventure designer** from the adventure library. It creates complete schema-version-1 content packs for the existing game engine. No programming is required for rooms, clues, puzzle commands, locks or portals.

The designer saves its **draft, including unfinished edits, in this browser on this device**. It is not an account backup and does not synchronize across browsers or devices. Validation issues remain visible when you return; resolve them before exporting a playable pack. Export JSON regularly, especially before clearing browser data. Private browsing may not preserve drafts.

## Build an adventure

1. Begin with the playable **Silent Sanctuary** starter. Give the adventure a title and unique lowercase ID such as `sunken-temple`.
2. Name the realm and its objective. Add rooms using stable IDs. Write descriptions that help explorers understand what they can try.
3. Connect rooms with directional exits. Add reverse exits separately if players should be able to return. The starting room must be lit.
4. Add loose items that can be collected. Feature clues respond to `EXAMINE <feature name>`.
5. In **Puzzles**, choose the room, exact command, success response and completion flag. Commands must not replace built-in commands such as `GET`, `GO`, `LOOK`, `HINT` or `MAP`.
6. Required items must exist in the adventure (or be the starting lamp or bottle). They must be in the player's satchel to solve the puzzle; they are not consumed. Prerequisite flags refer to other puzzles in the same realm.
7. Select completion flags and use flags to lock room exits or portals. All checked completion flags are required. Flags do not automatically create an exit.
8. Add other realms and connect them under **Portals**. A portal's unlock flag belongs to its source realm. Add a separate return portal where needed.
9. Export the validated JSON and playtest it on a development installation before publishing.

Give every puzzle fair clues and progressively clearer hints. Allow players to retrieve tools they may have dropped. Avoid locking the only route to an item behind a puzzle that requires that item. Validation checks schema and references; it cannot prove that your story is solvable or that every room is reachable.

## Rooms that change

Keep permanent scenery in the base room description. The game lists loose items from the saved state, so picking up or dropping an object changes the `You see` list automatically.

Under **Changing room descriptions**, add a state with completed or incomplete puzzle flags, items present, or items absent. Every condition must match. The last matching rule replaces the base room text. Item conditions check loose objects in this room, not any explorer's satchel; use **Move up/down** to set priority. For example, require `basin-filled` and describe flowing water instead of a dry basin. Optional EXAMINE overrides keep feature clues consistent with the new scene. Each matching rule overrides only its named features; other feature clues stay available. An unconditional rule always matches, so put it before more specific rules.

Open **Preview room changes**, select completed puzzles, and enter the items currently on the floor. The preview shows the resulting description, loose items, and EXAMINE text without changing a saved game. Export/import retains these rules. Undefined flags and contradictory conditions block export.

Use **up/down** exits for vertical travel. Choose passage, stairs, ladder, hole, or bridge for the route type. A hole may have only a down exit; add a return exit only if the story allows one. Maps show relative floors and directions for visited rooms. Portals are distinct connections rather than ordinary corridors.

The **Readables** tab assigns text to items already placed in rooms. `READ <item>` displays it and copies it to the journal once. The player must carry it or see it in their current room and have light. Keep portable item names distinct from fixed features to avoid unclear READ commands. Leave the field blank for items without readable text.

Sounds are automatic: visible room changes, arriving companions, points, solved puzzles, and victory have distinct cues. A single action plays one prioritized cue, and players can mute all sounds. These content rules do not create moving characters or a mummy AI.

## Import, advanced JSON and artwork

Import accepts a complete adventure JSON file up to 1 MB. Importing or applying JSON asks before replacing the current draft. The JSON tab also supports deliberate changes that do not have dedicated controls, such as removing a room or changing an ID. Update its references at the same time.

Choose one of the 60 bundled gold illustrations in a room. Custom artwork must use `mod-art/<adventure-id>/<image-name>.png`; an administrator must install the actual PNG separately. JSON files never embed images, executable scripts or HTML. The designer does not upload content to the server.

The guided designer works with complete adventures. Existing `extends` add-on files should be maintained with the [Mod Guide](Modding), rather than imported independently. To edit a split adventure in the designer, first combine its levels, portals and readableItems into one complete pack and validate it in a development installation.

## Installation and review

An administrator reviews the exported file, installs it in the server's `ContentPacks` directory, adds any custom PNGs to the corresponding `wwwroot/mod-art/<adventure-id>` directory, and restarts the application using the deployment procedure. Validate and playtest in development before touching production; an invalid pack can prevent startup. Keep adventure, realm and room identifiers stable after publication so saved expeditions remain usable. Back up the current content and player database before changing an installed adventure.

This release provides content authoring and JSON export. It does not provide public publishing, cloud draft storage, script execution or an in-editor playtest engine.


