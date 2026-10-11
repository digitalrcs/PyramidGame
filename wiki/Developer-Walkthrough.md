# Developer walkthrough - full spoilers

This is the complete built-in campaign route and a multiplayer acceptance checklist. Use a fresh disposable account on a local test server. Do not serve this file as in-game help. See [development](Development) for setup.

## Following the route

Enter one command from a code block at a time in the command box and press
Enter. Section labels and explanations are not commands. Do not paste an
entire block as one command. Directions are full words for clarity; N/S/E/W
also work. The route assumes a fresh SOLO game with no other player moving
objects. Multiplayer variations are described separately in section 5.

Each level has the same logical layout, with different names and puzzles:

```text
                       gate
                        |
        study ------- gallery ------- north-shrine
                        |
  west-shrine           |             east-shrine
       |                |                  |
      west ----------- hub -------------- east
                        |
                       camp
```

North is up; east is right. The west and east shrines are reached by going
NORTH from their branches. The north-shrine is reached by going EAST from
the gallery. Its internal ID is not a direction instruction. Those three
passages remain sealed until their respective mechanisms are solved.

The route collects tools, records the tablet, opens all three branches,
retrieves all relics, returns to camp to DEPOSIT, then visits the gate.
Satchels hold eight objects. The route fits without dropping anything.
Keep your lamp: darkness blocks normal actions. LIGHT LAMP restores light
if it is with you; RETREAT returns to the current landing if you get lost.

READ TABLET in levels 1 through 5 saves a clue required for that level's
ascent. Level 6 has instructions rather than a sixth journal clue. ASCEND
resets level tools and carries a lit lamp onward; journal and banked relics
persist. Built-in levels do not provide a route back down after ascending.

Extra LOOK, INVENTORY and JOURNAL commands are safe but change action counts.
HINT costs 5; MAP costs 2 per request. Neither is needed for this route.
Do the optional UI and reward checks in separate test expeditions if you
want the clean score checkpoints shown below.

## 3  Complete six-level campaign

## Level 1  The Buried Gate

Start: level 1 landing. Relics: golden scarab, ivory ibis, sun disk.
Water lowers the basin counterweight; grain distracts the ibis; rope bridges the shaft.

### 1.1  Collect the tools and read the study tablet; finish at the central hall

```text
GET LAMP
LIGHT LAMP
GET BOTTLE
NORTH
WEST
GET GRAIN
EAST
EAST
GET ROPE
FILL BOTTLE
WEST
NORTH
WEST
READ TABLET
EAST
SOUTH
```

### 1.2  Open the western shrine and collect its relic

```text
WEST
POUR WATER
NORTH
GET GOLDEN SCARAB
SOUTH
EAST
```

### 1.3  Open the eastern shrine and collect its relic

```text
EAST
FEED IBIS
NORTH
GET IVORY IBIS
SOUTH
WEST
```

### 1.4  Open the gallery crossing and collect its relic

```text
NORTH
TIE ROPE
EAST
GET SUN DISK
WEST
```

### 1.5  Return to the landing, bank the relics, then answer the gate riddle

```text
SOUTH
SOUTH
DEPOSIT
NORTH
NORTH
NORTH
SAY DAWN
```

Checkpoint: 3/18 relics banked; 1 tablet clues; all 10 rooms
visited on this level; score 204 before ASCEND.

```text
ASCEND
```

Expected: next level landing, lamp lit, old tools removed, +100 points.

## Level 2  The Drowned Archive

Start: level 2 landing. Relics: lapis tablet, bronze heron, crystal vessel.
Oil frees the wheel before it can turn. The weight balances the scales; the plank repairs the bridge.

### 2.1  Collect the tools and read the study tablet; finish at the central hall

```text
NORTH
WEST
GET IRON WEIGHT
EAST
EAST
GET OIL FLASK
WEST
NORTH
WEST
GET CEDAR PLANK
READ TABLET
EAST
SOUTH
```

### 2.2  Open the western shrine and collect its relic

```text
WEST
OIL WHEEL
TURN WHEEL
NORTH
GET LAPIS TABLET
SOUTH
EAST
```

### 2.3  Open the eastern shrine and collect its relic

```text
EAST
PLACE WEIGHT
NORTH
GET BRONZE HERON
SOUTH
WEST
```

### 2.4  Open the gallery crossing and collect its relic

```text
NORTH
LAY PLANK
EAST
GET CRYSTAL VESSEL
WEST
```

### 2.5  Return to the landing, bank the relics, then answer the gate riddle

```text
SOUTH
SOUTH
DEPOSIT
NORTH
NORTH
NORTH
SAY MEMORY
```

Checkpoint: 6/18 relics banked; 2 tablet clues; all 10 rooms
visited on this level; score 508 before ASCEND.

```text
ASCEND
```

Expected: next level landing, lamp lit, old tools removed, +100 points.

## Level 3  The Observatory

Start: level 3 landing. Relics: silver crescent, star sapphire, obsidian eye.
The cloth restores the mirror. The cog repairs the engine, and the star chart names ORION. Sunrise means EAST.

### 3.1  Collect the tools and read the study tablet; finish at the central hall

```text
NORTH
WEST
GET STAR CHART
EAST
EAST
GET POLISHING CLOTH
WEST
NORTH
WEST
GET BRASS COG
READ TABLET
EAST
SOUTH
```

### 3.2  Open the western shrine and collect its relic

```text
WEST
POLISH MIRROR
NORTH
GET SILVER CRESCENT
SOUTH
EAST
```

### 3.3  Open the eastern shrine and collect its relic

```text
EAST
FIT COG
SET ORION
NORTH
GET STAR SAPPHIRE
SOUTH
WEST
```

### 3.4  Open the gallery crossing and collect its relic

```text
NORTH
TURN EAST
EAST
GET OBSIDIAN EYE
WEST
```

### 3.5  Return to the landing, bank the relics, then answer the gate riddle

```text
SOUTH
SOUTH
DEPOSIT
NORTH
NORTH
NORTH
SAY SHADOW
```

Checkpoint: 9/18 relics banked; 3 tablet clues; all 10 rooms
visited on this level; score 812 before ASCEND.

```text
ASCEND
```

Expected: next level landing, lamp lit, old tools removed, +100 points.

## Level 4  The Halls of Echoes

Start: level 4 landing. Relics: amber mask, golden reed, echo pearl.
The bell inscription gives SUN, RIVER, MOON. The flute answers the reed door; the fork reveals the bridge.

### 4.1  Collect the tools and read the study tablet; finish at the central hall

```text
NORTH
WEST
GET REED FLUTE
EAST
EAST
GET TUNING FORK
WEST
NORTH
WEST
GET WOODEN MALLET
READ TABLET
EAST
SOUTH
```

### 4.2  Open the western shrine and collect its relic

```text
WEST
STRIKE SUN RIVER MOON
NORTH
GET AMBER MASK
SOUTH
EAST
```

### 4.3  Open the eastern shrine and collect its relic

```text
EAST
PLAY FLUTE
NORTH
GET GOLDEN REED
SOUTH
WEST
```

### 4.4  Open the gallery crossing and collect its relic

```text
NORTH
SOUND FORK
EAST
GET ECHO PEARL
WEST
```

### 4.5  Return to the landing, bank the relics, then answer the gate riddle

```text
SOUTH
SOUTH
DEPOSIT
NORTH
NORTH
NORTH
SAY SILENCE
```

Checkpoint: 12/18 relics banked; 4 tablet clues; all 10 rooms
visited on this level; score 1091 before ASCEND.

```text
ASCEND
```

Expected: next level landing, lamp lit, old tools removed, +100 points.

## Level 5  The Hall of Judgment

Start: level 5 landing. Relics: jade heart, royal seal, serpent crown.
The feather balances the heart. Reflecting NEMA reveals AMEN; speaking it opens the veil. The mirror distracts the serpent.

### 5.1  Collect the tools and read the study tablet; finish at the central hall

```text
NORTH
WEST
GET BRONZE MIRROR
EAST
EAST
GET FEATHER
WEST
NORTH
WEST
READ TABLET
EAST
SOUTH
```

### 5.2  Open the western shrine and collect its relic

```text
WEST
PLACE FEATHER
NORTH
GET JADE HEART
SOUTH
EAST
```

### 5.3  Open the eastern shrine and collect its relic

```text
EAST
REFLECT NAME
SAY AMEN
NORTH
GET ROYAL SEAL
SOUTH
WEST
```

### 5.4  Open the gallery crossing and collect its relic

```text
NORTH
SHOW MIRROR
EAST
GET SERPENT CROWN
WEST
```

### 5.5  Return to the landing, bank the relics, then answer the gate riddle

```text
SOUTH
SOUTH
DEPOSIT
NORTH
NORTH
NORTH
SAY TRUTH
```

Checkpoint: 15/18 relics banked; 5 tablet clues; all 10 rooms
visited on this level; score 1395 before ASCEND.

```text
ASCEND
```

Expected: next level landing, lamp lit, old tools removed, +100 points.

## Level 6  Chamber of the Gods

Start: level 6 landing. Relics: feather of maat, ankh of ages, solar heart.
The cube balances stone with stone; the rod conducts power; the solar iris must face EAST.

### 6.1  Collect the tools and read the study tablet; finish at the central hall

```text
NORTH
WEST
GET COPPER ROD
EAST
EAST
GET STONE CUBE
WEST
NORTH
WEST
READ TABLET
EAST
SOUTH
```

### 6.2  Open the western shrine and collect its relic

```text
WEST
PLACE CUBE
NORTH
GET FEATHER OF MAAT
SOUTH
EAST
```

### 6.3  Open the eastern shrine and collect its relic

```text
EAST
FIT ROD
NORTH
GET ANKH OF AGES
SOUTH
WEST
```

### 6.4  Open the gallery crossing and collect its relic

```text
NORTH
TURN EAST
EAST
GET SOLAR HEART
WEST
```

### 6.5  Return to the landing, bank the relics, then answer the gate riddle

```text
SOUTH
SOUTH
DEPOSIT
NORTH
NORTH
NORTH
SAY ETERNITY
```

Checkpoint: 18/18 relics banked; 5 tablet clues; all 10 rooms
visited on this level; score 1674 before the final invocation.

Stay at the Throne of the gods. Continue with section 4; do not ASCEND.

## 4  Final god arrangement and victory

At the level 6 throne, all 18 relics should be banked and SAY ETERNITY
accepted. Read your saved clues:

```text
JOURNAL
```

God names are shuffled per expedition. A fixed RA/ISIS/etc. sequence copied
from another save will usually fail. Assign temporary labels from YOUR
tablets as follows; these letters are reasoning aids, not game commands:

```text
C = the god named in Tablet I as standing at neither edge.
D = the god in Tablet II immediately to the right of C.
E = the god in Tablet III exactly two places to the right of C.
A = the god in Tablet IV somewhere to the left of E.
B = the god in Tablet V somewhere between A and C.
```

C cannot be 1 or 5. E=C+2 means C can only be 2 or 3. If C=2, D=3 and
E=4; A must be 1, leaving no niche for B between A and C. Therefore C=3,
D=4 and E=5. The two remaining niches are A=1 and B=2.

Replace each placeholder below with the actual name from your journal:

```text
SET <A name> 1
SET <B name> 2
SET <C name> 3
SET <D name> 4
SET <E name> 5
INVOKE
```

For example, ONLY if your clues identify A=RA, B=ISIS, C=THOTH,
D=ANUBIS, E=OSIRIS, the five commands would be SET RA 1, SET ISIS 2,
SET THOTH 3, SET ANUBIS 4, SET OSIRIS 5. Do not assume this example matches.

Expected: THE FIVE NAMES BECOME ONE victory text, 100 percent progress,
18 relics, Chamber of the Gods and Level 6 complete achievements. With no
hints, Unaided ascension is awarded. The clean route earns 2174 points:
360 relic points + 700 puzzle/riddle points + 500 ascent points + 500 win
points + 114 exploration bonus points. Extra paid maps or hints reduce it.

If invocation fails, recheck the clues and overwrite incorrect placements.
Putting a god in an occupied slot removes that slot's previous occupant;
verify all five slots are populated before invoking. Failed arrangements
do not erase offerings. Reading the journal and rearranging are free.

## 5  Multiplayer acceptance walkthrough

Use ExplorerOne and ExplorerTwo in independent browser sessions. Both accounts
must already exist; inviting a name does not create an account. Optional email
notifications require a verified address and enabled notifications.

## Invitation and late arrival

1. Sign ExplorerTwo out. As ExplorerOne, open EXPEDITIONS and create a named

```text
 Chamber of the Gods expedition. Use the manual-name option in the invite
 picker to enter ExplorerTwo. Leave public join requests off for this test.
```

2. Start immediately. Check that the host enters without waiting for a

```text
 companion. Shared entrants receive a personal lit lamp, so skip GET LAMP
 and LIGHT LAMP from the solo instructions. Take the bottle if needed.
```

3. Move the host NORTH then WEST. Sign ExplorerTwo in separately, open

```text
 EXPEDITIONS, select the invitation from the dropdown and accept/join.
```

4. Verify the guest starts at the landing while the host stays at Star

```text
 basin. When both are online, the invite selector can list active users.
 Typed names remain useful for existing accounts that are offline.
```

## Independent movement and meetings

5. Host at Star basin: GET GRAIN. Guest from landing: NORTH, EAST, GET ROPE.

```text
 They should be in different rooms with separate inventories.
```

6. Host: EAST to Hall of dust. Guest: WEST to the same hall. LOOK should

```text
 show the companion, and arrivals should appear in the room transcript.
 Allow the visible-tab polling interval of about five seconds.
```

7. Host enters the following; replace ExplorerTwo if you chose another name:

```text
   SHARE ITEM grain WITH ExplorerTwo
```

```text
 Guest checks OFFERS, then accepts:
```

```text
   GET grain FROM ExplorerOne
```

```text
 Grain must leave the host's satchel and appear in the guest's. The giver
 receives +5 on this first qualifying transfer. Offering alone earns zero.
 Trying GET without an offer, or accepting from another room, must fail.
```

8. Host sends a room message:

```text
   SHARE INFORMATION The ibis needs grain in the eastern chamber.
```

```text
 Only awake companions in this room receive it. Chat itself earns no
 points. Guest can return EAST and FEED IBIS while host explores west.
```

## Journal sharing and personal progress

9. Host reads the study tablet (from Hall of dust: NORTH, WEST, READ TABLET),

```text
 then returns EAST, SOUTH to the hall. Guest returns WEST from the cistern.
 Host uses SHARE JOURNAL WITH ExplorerTwo. The guest should gain the clue;
 host earns +3 once for a share that actually adds new information.
 Repeating unchanged notes earns zero. Private notes are copied too.
```

10. Visited rooms, maps, transcripts, inventories and bonus counters remain

```text
  personal. Puzzle flags, floor items and banked relics are shared.
  Have only one player take each loose object: objects are not duplicated.
```

## Trance, resumption and ascent

11. Sign the guest out in a known room while the host remains. The companion

```text
  should be in a trance, retaining location and inventory. Closing a tab
  instead takes about 45 seconds to expire presence. Resume the expedition
  to wake at the saved location. Offers are canceled on leaving/switching.
```

12. Finish a built-in level using the solo solutions divided between players.

```text
  Gather all awake members at the gate. Make sure the ascending player has
  that level's tablet in their journal; SHARE JOURNAL can supply it. Deposit
  all relics and speak the riddle, then ASCEND. Awake members away from the
  gate must prevent ascent. Sleeping members move to the new landing with
  the party; this is the documented exception to exact-location resumption.
```

## Open requests and permission checks

13. In a separate test world enable join requests. A nonmember selects it

```text
  from the dropdown and requests admission. The host approves; the guest
  then accepts. An open world does not grant automatic entry. Declining
  or denying should leave the requester's current game unchanged.
```

14. Verify a non-host cannot invite/manage admission or change world access.

```text
  Switch to the original solo save and back: both saves retain their own
  progress. Host creation does not require any invitation to succeed.
```

## 6  Map, sound, CLEAR and reward checks

Run these in disposable saves, apart from the clean victory route.

MAP: Explore two or three rooms, note score, then enter MAP or click its
button. The gold diagram must include only personally visited rooms in the
current realm and connections between them. No hidden rooms or another
player's discoveries should appear. Each request costs 2 points. Closing
the dialog is free. It is a paid snapshot; later exploration does not update
the already-purchased snapshot automatically. Verify narrow-screen scrolling.
At score zero, costs accrue rather than disappearing; future earnings first
offset them. A clamped zero display need not visibly drop by two.

CLEAR: First add NOTE test note and carry an item. Enter CLEAR. The command
transcript should empty while room, satchel, journal and progression remain.
Reload/sign back in and verify the cleared history stays cleared. The other
player's transcript must not be cleared.

CHIMES: Enable CHIMES ON and enter a lit room with a loose object, unfinished
puzzle or authored feature. Expect a short chime and the investigation
notice. Polling while standing still should not keep chiming. Toggle OFF,
move and reload to confirm the browser preference persists. Browsers may
need a click/keypress to unlock audio. Listen on the target device; a clean
console or visible notice alone does not verify audible output. The cue is
an activity signal, not a promise that an item can be picked up immediately.

## Reward matrix

Activity                         Award   Limit and trigger
New non-starting room             +1      Once per room per level/realm
Every room in a level/realm       +10     Once per level/realm
Accepted non-lamp item gift       +5      Giver; 3 distinct items per realm
Journal share adding new notes    +3      Giver; once per expedition
Room chat / offering without take  0      No reward

Check revisits, repeated puzzles, repeated DEPOSIT and repeated INVOKE do
not award their points again. Passing the same item back cannot repeatedly
reward the same giver. Lamp gifts never earn sharing bonuses. A recipient
can qualify as a giver on their own first eligible transfer; the cap is
per giver, not a universal lock on that item. Rewards and map costs must
survive a restart. Bonus points earned is distinct from net total score.

## 7  Included mod and portal walkthrough

Both files below must be installed in the server's ContentPacks directory:

```text
oasis-of-stars.json
oasis-moon-addon.json
```

They are already included in the source and publish output. Restart the
server after changing content. Create an expedition selecting The Oasis of
Stars. From Caravan camp, enter these commands individually:

```text
WEST
GET BRONZE KEY
EAST
EAST
TURN KEY
EAST
EAST
RING BELL
WEST
WEST
```

Expected sequence: Dry well -> Caravan camp -> Portal arch -> Moonlit
landing -> Temple of the Moon -> Moonlit landing -> Portal arch. TURN KEY
opens the east portal; RING BELL restores the moon voice but does not complete the expanded realm or adventure. Returning keeps
the key removed from the well and the portal unlocked. Personal visited
history survives going between realms. In co-op, another player can stay
in the oasis while you enter the moon realm; it is not a group ascent.

For the complete 22-room, three-realm adventure and its 13 puzzles, follow [The Oasis of Stars walkthrough](Oasis-Walkthrough). The short route above tests portal compatibility only.

## To author an extension

1. Copy the examples to a separate test content directory. Choose stable

```text
 lowercase IDs. A base pack defines schemaVersion 1, id, name, startLevel,
 levels and portals. An addon uses extends with the base pack ID.
```

2. Define each realm's rooms and exits, puzzle needs/requires/flag, optional

```text
 features and winFlags. Add portals with fromLevel/fromRoom/direction and
 toLevel/toRoom. Define a reverse portal explicitly if return is intended.
```

3. Point a separate test server at this directory with

```text
 --Pyramid:ContentDirectory "<absolute test content directory>".
 Restart and fix any loader diagnostic before testing fresh and old saves.
```

4. Test all prerequisites, a missing-tool path, both portal directions and

```text
 an offline player holding a critical tool. Additive rooms should appear
 without respawning previously collected objects.
```

5. Keep pack, level, room, item and flag identifiers stable after release.

```text
 Back up the matching database, keys and exact content versions together.
```

JSON packs are data only. Players cannot upload/install packs in the web UI.
Admins review and install them outside the public web root. Schema 1 extends
JSON adventures; it cannot override the built-in C# campaign or travel
between different packs. New built-in campaign rules require source edits.
Reserve engine commands including MAP and CLEAR. Puzzle commands do not
consume tools automatically. See the modding guide for full fields,
limits and image restrictions. This walkthrough does not replace the schema.

## Version 1.4.1 acceptance checks

Use disposable local expeditions for these checks; preserve production users and saves.

- Confirm FEED BIRD, CLEAN MIRROR and LAY BOARD perform their built-in canonical actions only when normal object and prerequisite requirements are met. Repeating a solved action must not award points again.
- In the Drowned Archive, READ LAPIS TABLET while the relic is visible or carried. Confirm the writing is shown and copied once. In that level's study, READ TABLET must still give the fixed study clue; READ LAPIS TABLET must give the collectible's separate writing. A remote or deposited relic is not readable.
- Read an accessible star chart twice. Confirm one journal entry and no reading points. Test ambiguous item names and a full 100-entry journal; both must explain what happened without losing existing notes.
- Pick up and drop an object, then LOOK. Check the live object list and matching item-dependent descriptions. Solve a mechanism and verify its current LOOK and EXAMINE descriptions; old transcript entries should remain unchanged.
- In an authored test pack, use up/down exits with stairs or ladders and a one-way hole. Visit both ends and open MAP. Check floor selection, arrow direction and route labels. Unvisited rooms must stay hidden; switching floors/views must not charge another map fee.
- Check room activity, an arriving companion, positive points, a solved puzzle and final victory sounds. A combined event should play one prioritized cue. CHIMES OFF must mute all cues, and switching accounts/adventures must not sound like a newly earned victory.
- Export and reimport a designer pack containing descriptionStates, feature overrides, exitKinds and readableItems. Compare the resulting fields, restart a development server with the pack, and test both new and existing saved expeditions.
