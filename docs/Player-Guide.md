# Player guide

Read the room, try a short command, and keep notes. The six-level campaign has sixty rooms, eighteen relics and a final puzzle whose names change between expeditions. There is no real-time deadline; the lamp does not run out.

## First steps

Create an account with an explorer name and email. Follow the verification link and confirm it before starting your expedition; an administrator can also approve your address. Until then, signing in opens the verification screen. Use RESEND VERIFICATION or CHECK VERIFICATION; the screen also checks automatically while open. Contact an administrator if the registered address needs correcting. For the original Chamber of the Gods adventure, begin with:

```text
GET LAMP
LIGHT LAMP
GET BOTTLE
NORTH
LOOK
```

Each command is saved. Return using the same account to continue. The explorer sidebar shares room, level, score and achievements; your personal inventory, notes and transcript stay private.

## Commands

The original Pyramid adventure accepts common alternatives: `FEED BIRD` for `FEED IBIS`, `CLEAN MIRROR` for `POLISH MIRROR`, and `LAY BOARD` for `LAY PLANK`. Item aliases include lantern (lamp), birdseed (grain), board (cedar plank), and gear (brass cog). They work with GET, TAKE, DROP, and EXAMINE. Requirements and rewards stay the same; ambiguous names ask for clarification. Custom adventures retain their authored vocabulary.

| Command | Use |
| --- | --- |
| `N`, `S`, `E`, `W` or `GO EAST` | Move through an exit |
| `LOOK` / `L` | Repeat the room description |
| `GET <item>`, `DROP <item>` | Pick up or set down an object |
| `EXAMINE <thing>` | Inspect an object or feature |
| `INVENTORY` / `I` | Check your eight-slot satchel |
| `LIGHT LAMP`, `OFF LAMP` | Control your lamp |
| `READ <item>` / `READ TABLET` | Read writing and copy supported clues into your journal |
| `JOURNAL`, `NOTE <text>` | Review clues or record an observation |
| `DEPOSIT` | Bank relics at the current landing |
| `SAY <answer>`, `ASCEND` | Answer a gate riddle and move to the next level |
| `MAP` | Buy a map of rooms you have personally visited; costs 2 points |
| `HINT` | Request progressively clearer local help; costs 5 points |
| `CLEAR` | Clear your saved transcript without clearing progress or notes |
| `RETREAT` | Follow your safety cord to the current landing |
| `SCORE`, `HELP`, `SAVE` | Check progress, commands or automatic saving |
| `CHIMES ON`, `CHIMES OFF` | Enable or mute this browser's game sounds |

Room descriptions introduce other verbs, including FILL, POUR, FEED, TIE, TURN and REFLECT. Use full names when an abbreviated noun is ambiguous. The up/down arrows recall recent commands. On a phone, tabs switch between the terminal and side panels.

## The ascent

| Level | Theme |
| --- | --- |
| I — The Buried Gate | Water, a guardian bird and a broken crossing |
| II — The Drowned Archive | Machinery and balance |
| III — The Observatory | Stars and ancient light |
| IV — The Halls of Echoes | Instruments and resonance |
| V — The Hall of Judgment | Balance, reflection and a hidden name |
| VI — Chamber of the Gods | Final offerings and the five-clue logic puzzle |

Collect and deposit three relics on each level. Read the study tablet, answer the gate's riddle, then ascend. Your lamp and journal continue with you; level tools stay behind. At the final throne, use the journal clues to place the gods with `SET <GOD> <1-5>`, then `INVOKE` when ready.

## Maps, sounds and points

Maps contain only your visited rooms in the current realm, with known connections between them. Each opening buys a snapshot; it does not reveal another explorer's discoveries. Use **Show floor** for visited rooms above or below you and **MY FLOOR** to return to your current floor. Floors are relative within each mapped area. Route labels identify stairs, ladders, holes, bridges and portals where authored; arrows show allowed travel. Some passages bend, so follow route labels when the schematic cannot preserve every direction. Changing floors or switching map views costs no additional points.

Room-entry chimes flag visible items, unfinished puzzles or authored features. They invite investigation rather than promising an immediately usable object. Different sounds mark an arriving companion, a visible room change, points earned, a solved puzzle and victory. When several happen together, one sound plays: victory takes priority, then a puzzle, points, arrival and room activity. A short text notice also describes the event. Use CHIMES OFF to mute them all, or CHIMES ON to restore them; the preference belongs to this browser. Sound begins after interacting with the page if the browser requires it.

LOOK and EXAMINE reflect supported room changes. Collected objects disappear from the live object list, and a solved mechanism can describe its new state. Old transcript entries remain a record of what was seen earlier; LOOK gives the current scene.

| Activity | Points |
| --- | ---: |
| Deposit a relic | 20 |
| Solve a mechanism or gate riddle | 25 |
| Ascend | 100 |
| Complete the final invocation | 500 |
| Discover a non-starting room | 1 |
| Survey every room in a realm | 10 |
| Give an accepted non-lamp item | 5 |
| First journal share that adds information | 3 |

Item-giving rewards are limited to three distinct items per realm per giver. The journal reward is once per expedition. Repeated visits, unchanged notes and chat do not generate points. Map and hint costs still accrue when the displayed score is zero and offset later earnings.

Continue with [shared expeditions](Multiplayer.md), or see the [developer walkthrough](Developer-Walkthrough.md) if you want full solutions.

## Mobile play

On Android and other small screens, the game keeps the same commands and saved adventures while moving the sidebars into touch-friendly panels:

- **Play** returns to the room transcript and command field.
- **Map** buys the current map snapshot for two points.
- **Satchel & journal** shows inventory, objectives and notes.
- **Explorers** shows other players and their progress.
- **Parties** opens expedition selection, invitations and join requests.

Use **Menu** for your account, help, administration (if permitted) and sign-out. On a phone, maps open as room-and-route cards, with your current room first. Switch to **Drawing** for the gold schematic and swipe to inspect it. Switching between cards and drawing is free; closing and reopening the map costs another two points. Neither view reveals unvisited rooms.

## Choose an adventure

After signing in, the adventure library opens. **Your journeys** lists the original Pyramid save and your saved expeditions. Choose **RESUME**, or select **NEW EXPEDITION** on an illustrated adventure card, name your journey, and choose **BEGIN ADVENTURE**. Leave invitations empty to play alone; companions can join later.

The Oasis of Stars is a separate journey through a forgotten oasis, a silent moon realm and the gates of dawn. Restore ancient mechanisms, carry clues between realms and find the way home. It uses the same commands, maps, journal and sharing system.

Every expedition has its own save. Use the saved-world selector to resume it, or **RETURN TO ORIGINAL SOLO SAVE** to return to your original Pyramid journey. Starting another adventure does not reset an existing one.

## Reading scrolls and other writing

Use READ followed by a readable item's name, such as READ ANCIENT SCROLL. You must carry it or be able to see it in the current room, and you need light. EXAMINE suggests READ when an item has authored writing. Full names work best; a unique shortened name is accepted, while ambiguous names ask you to choose.

The writing is copied into your personal JOURNAL once. Re-reading shows it again without duplicate entries or extra points. Your journal holds 100 entries; when full, reading still shows the text and explains that it was not copied. Fixed scenery may also respond to READ without becoming a journal entry. In Chamber of the Gods, `READ STAR CHART` records the Orion clue and `READ LAPIS TABLET` records the writing on the collectible tablet from the Drowned Archive. You must carry the relic or see it in the current room. In a study room, `READ TABLET` deliberately reads that room's fixed study inscription, even if you carry the lapis tablet. Use the full `READ LAPIS TABLET` command to distinguish the collectible. Outside a study, a unique shortened item name such as `READ TABLET` may resolve to the collectible.

In The Oasis of Stars, `READ STAR CHART` records the Moon Realm chart's own clue. The same item name can have different writing in different adventures. Scenic inscriptions and item writing are separate: `EXAMINE MEMORY` in the Moon archive reads its fixed clue, while `READ STAR CHART` reads the portable object.
