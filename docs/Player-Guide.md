# Player guide

Read the room, try a short command, and keep notes. The six-level campaign has sixty rooms, eighteen relics and a final puzzle whose names change between expeditions. There is no real-time deadline; the lamp does not run out.

## First steps

Create an account with an explorer name and email, then start your solo expedition. Confirm your email to enable email sign-in and recovery. Explorer-name sign-in remains available. Begin with:

```text
GET LAMP
LIGHT LAMP
GET BOTTLE
NORTH
LOOK
```

Each command is saved. Return using the same account to continue. The explorer sidebar shares room, level, score and achievements; your personal inventory, notes and transcript stay private.

## Commands

| Command | Use |
| --- | --- |
| `N`, `S`, `E`, `W` or `GO EAST` | Move through an exit |
| `LOOK` / `L` | Repeat the room description |
| `GET <item>`, `DROP <item>` | Pick up or set down an object |
| `EXAMINE <thing>` | Inspect an object or feature |
| `INVENTORY` / `I` | Check your eight-slot satchel |
| `LIGHT LAMP`, `OFF LAMP` | Control your lamp |
| `READ TABLET` | Record a clue |
| `JOURNAL`, `NOTE <text>` | Review clues or record an observation |
| `DEPOSIT` | Bank relics at the current landing |
| `SAY <answer>`, `ASCEND` | Answer a gate riddle and move to the next level |
| `MAP` | Buy a map of rooms you have personally visited; costs 2 points |
| `HINT` | Request progressively clearer local help; costs 5 points |
| `CLEAR` | Clear your saved transcript without clearing progress or notes |
| `RETREAT` | Follow your safety cord to the current landing |
| `SCORE`, `HELP`, `SAVE` | Check progress, commands or automatic saving |
| `CHIMES ON`, `CHIMES OFF` | Set this browser's room-activity sound preference |

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

Maps contain only your visited rooms in the current realm, with connections between them. Each opening buys a snapshot; it does not reveal another explorer's discoveries. Room-entry chimes flag visible items, unfinished puzzles or authored features. They invite investigation rather than promising an immediately usable object.

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
