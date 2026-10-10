#!/usr/bin/env python3
"""
Pyramid 2000 - Python recreation
A classic text adventure inspired by the 1979 Radio Shack TRS-80 game by Robert Arnstein.
An expanded tribute, not a verbatim port of the original adventure.
Goal: Recover the treasures and return them to the entrance.
"""

import random
import sys
import copy
import json
from pathlib import Path
import builtins
import textwrap


def print(*values, sep=" ", end="\n"):
    """A restrained 64-column, uppercase terminal presentation."""
    message = sep.join(str(value) for value in values).upper()
    lines = [textwrap.fill(line, width=64, replace_whitespace=False)
             for line in message.split("\n")]
    builtins.print("\n".join(lines), end=end)

# ---------------------------------------------------------------------------
# Game data
# ---------------------------------------------------------------------------

rooms = {
    "entrance": {
        "desc": "You stand at the eroded tip of the Great Lost Pyramid. The seal is broken. Desert sand stretches in all directions. The entrance gapes northward into darkness.",
        "exits": {"north": "hall"},
        "items": ["lamp", "food", "bottle"],
        "treasure_spot": True,
    },
    "hall": {
        "desc": "A grand Hall of the Gods. Hieroglyphs cover the walls. Statues of Horus and Anubis flank the passage. Passages lead south (entrance), east, and west.",
        "exits": {"south": "entrance", "east": "chamber1", "west": "maze1"},
        "items": [],
    },
    "chamber1": {
        "desc": "A small burial chamber. A sarcophagus rests against the wall. The air is still and dry.",
        "exits": {"west": "hall", "north": "pit"},
        "items": ["gold nugget"],
    },
    "pit": {
        "desc": "You are in a deep pit. The walls are smooth. You can climb up to the north or south.",
        "exits": {"south": "chamber1", "north": "treasure_room"},
        "items": [],
    },
    "treasure_room": {
        "desc": "A secret treasure chamber glittering with gold. The walls are lined with riches.",
        "exits": {"south": "pit"},
        "items": ["silver bar", "diamond", "golden eggs", "jewel key"],
    },
    "maze1": {
        "desc": "You are in a twisting maze of passages, all alike.",
        "exits": {"east": "hall", "south": "maze2", "west": "maze3"},
        "items": [],
    },
    "maze2": {
        "desc": "You are in a twisting maze of passages, all alike.",
        "exits": {"north": "maze1", "east": "maze4", "down": "maze5"},
        "items": [],
    },
    "maze3": {
        "desc": "You are in a twisting maze of passages, all alike.",
        "exits": {"east": "maze1", "south": "maze4"},
        "items": ["emerald"],
    },
    "maze4": {
        "desc": "You are in a twisting maze of passages, all alike.",
        "exits": {"west": "maze2", "north": "maze3", "east": "chest_room"},
        "items": [],
    },
    "maze5": {
        "desc": "You are in a twisting maze of passages, all alike. A deep pit yawns below.",
        "exits": {"up": "maze2"},
        "items": [],
    },
    "chest_room": {
        "desc": "A dead-end chamber containing a heavy wooden chest.",
        "exits": {"west": "maze4"},
        "items": ["chest"],
    },
}

# Treasures (must be deposited at entrance to score fully)
TREASURES = [
    "gold nugget", "silver bar", "diamond", "golden eggs",
    "jewel key", "emerald", "chest", "ruby", "pearl",
    "sapphire", "golden idol"
]

# Extra treasures placed in other rooms for a fuller game
rooms["hall"]["items"].append("ruby")
rooms["chamber1"]["items"].append("pearl")
rooms["treasure_room"]["items"].append("sapphire")
rooms["chest_room"]["items"].append("golden idol")
rooms["hall"]["desc"] += " A narrow doorway opens north into a gallery."
rooms["hall"]["exits"]["north"] = "gallery"
rooms.update({
    "gallery": {
        "desc": "A gallery of faded stars. A stone basin stands beneath a northern door. An inscription circles its rim. West, dripping water echoes; east, feathers rustle. The hall lies south.",
        "exits": {"south": "hall", "west": "cistern", "east": "ibis_room", "north": "shaft"},
        "items": [],
    },
    "cistern": {
        "desc": "A cool cistern holds clear water. A channel once carried it toward the gallery, but the channel has crumbled. The gallery is east.",
        "exits": {"east": "gallery"}, "items": [],
    },
    "ibis_room": {
        "desc": "An ibis occupies a chamber with a low eastern doorway and a feeding bowl. A painted scribe offers grain to a bird. The gallery is west.",
        "exits": {"west": "gallery", "east": "archive"}, "items": [],
    },
    "archive": {
        "desc": "You are in the scribe's room, beside a stone writing desk. A wall painting shows a rope looped around a bronze ring above a shaft. The bird's chamber is west.",
        "exits": {"west": "ibis_room"}, "items": ["rope", "papyrus scroll"],
    },
    "shaft": {
        "desc": "A shaft cuts across the northern passage. A bronze ring is fixed securely to the wall. Deep scratches suggest someone once crossed on a rope. The gallery lies south.",
        "exits": {"south": "gallery", "north": "crypt"}, "items": [],
    },
    "crypt": {
        "desc": "You stand in a silent crypt beyond the shaft. A northern door bears a jewel-shaped keyhole. South, the shaft separates you from the gallery.",
        "exits": {"south": "shaft", "north": "vault"}, "items": ["lapis scarab"],
    },
    "vault": {
        "desc": "A circular pedestal stands beneath a ceiling carved with the rays of the sun. An inscription honors those who bring the scattered treasures back to the desert. The crypt is south.",
        "exits": {"south": "crypt"}, "items": ["sun disk"],
    },
})
TREASURES.extend(["papyrus scroll", "lapis scarab", "sun disk"])
INITIAL_ROOMS = copy.deepcopy(rooms)
SAVE_FILE = Path(__file__).with_name("pyramid-save.json")

# ---------------------------------------------------------------------------
# Game state
# ---------------------------------------------------------------------------

current_room = "entrance"
inventory = []
lamp_on = False
score = 0
moves = 0
mummy_active = True
mummy_stolen = False
discovered = set()
puzzles = {"bottle_filled": False, "basin_filled": False,
           "ibis_fed": False, "rope_tied": False, "vault_open": False}

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def has_light():
    return current_room == "entrance" or (
        lamp_on and ("lamp" in inventory or "lamp" in rooms[current_room]["items"])
    )


def update_score():
    global score
    score = 5 * len(discovered) + 15 * sum(
        item in TREASURES for item in rooms["entrance"]["items"]
    )


def resolve_item(noun, available):
    """Accept a unique noun, but never guess between two matching objects."""
    if noun in available:
        return noun
    matches = [item for item in available if noun and noun in item.split()]
    if len(matches) == 1:
        return matches[0]
    if len(matches) > 1:
        print("Which do you mean: " + ", ".join(matches) + "?")
        return None
    return noun


def print_room():
    room = rooms[current_room]
    if not has_light():
        print("\nDarkness conceals the passage. Moving without a light is dangerous.")
        return
    print("\n" + room["desc"])
    if current_room == "gallery" and puzzles["basin_filled"]:
        print("The basin is full. The northern door stands open.")
    elif current_room == "gallery":
        print("The basin is dry. The northern door is sealed.")
    elif current_room == "ibis_room" and puzzles["ibis_fed"]:
        print("The ibis has left its post to eat. The eastern doorway is clear.")
    elif current_room == "ibis_room":
        print("The ibis guards the eastern doorway. Its bowl is empty.")
    elif current_room == "shaft" and puzzles["rope_tied"]:
        print("Your rope spans the shaft. You can cross north.")
    elif current_room == "crypt" and puzzles["vault_open"]:
        print("The northern vault door stands open.")
    if room["items"]:
        print("You see:", ", ".join(room["items"]))
    exits = list(room["exits"].keys())
    if exits:
        print("Exits:", ", ".join(exits))

def get_item(item):
    global score
    room = rooms[current_room]
    if not has_light():
        print("You cannot see anything to take.")
        return
    item = resolve_item(item, room["items"])
    if item is None:
        return
    if item not in room["items"]:
        print("You don't see that here.")
        return
    if len(inventory) >= 6:
        print("You can't carry any more.")
        return
    room["items"].remove(item)
    inventory.append(item)
    print(f"You take the {item}.")
    if item in TREASURES:
        if item not in discovered:
            discovered.add(item)
            print("( +5 points for discovering a treasure )")
        update_score()

def drop_item(item):
    global score
    item = resolve_item(item, inventory)
    if item is None:
        return
    if item not in inventory:
        print("You aren't carrying that.")
        return
    inventory.remove(item)
    rooms[current_room]["items"].append(item)
    print(f"You drop the {item}.")
    if current_room == "entrance" and item in TREASURES:
        print("( +15 points for depositing a treasure at the entrance )")
    update_score()

def move(direction):
    global current_room, moves
    room = rooms[current_room]
    if direction not in room["exits"]:
        print("You can't go that way.")
        return
    if not has_light():
        print("You stumble in the darkness and hit your head. You are dead.")
        sys.exit(0)
    barriers = {
        ("gallery", "north"): ("basin_filled", "The stone door is shut. The basin and its inscription may explain why."),
        ("ibis_room", "east"): ("ibis_fed", "The ibis spreads its wings and snaps at you. Its feeding bowl is empty."),
        ("shaft", "north"): ("rope_tied", "You stop at the edge. You need a secure rope before crossing."),
        ("crypt", "north"): ("vault_open", "The vault is locked. Its keyhole has the shape of a jewel."),
    }
    barrier = barriers.get((current_room, direction))
    if barrier and not puzzles[barrier[0]]:
        print(barrier[1])
        return
    current_room = room["exits"][direction]
    moves += 1
    mummy_check()

def mummy_check():
    global mummy_stolen, score
    if not mummy_active or mummy_stolen or current_room == "entrance":
        return
    treasures_carried = [i for i in inventory if i in TREASURES]
    if treasures_carried and random.random() < 0.25:
        stolen = random.choice(treasures_carried)
        inventory.remove(stolen)
        rooms["hall"]["items"].append(stolen)
        update_score()
        mummy_stolen = True
        print(f"\nA mummy appears and snatches the {stolen} from you!")
        print("The mummy shambles away. It seems satisfied for now.")

def check_win():
    deposited = [i for i in rooms["entrance"]["items"] if i in TREASURES]
    if set(TREASURES).issubset(deposited):
        print("\n*** YOU WIN! ***")
        print(f"All treasures returned! Final score: {score} in {moves} moves.")
        sys.exit(0)

def show_help():
    print("""
Commands:
  LOOK / L          - Describe current location
  GO <dir> / <dir>  - Move (north, south, east, west, up, down)
  GET / TAKE <item> - Pick up an item
  DROP <item>       - Drop an item
  INVENTORY / I     - List what you are carrying
  LIGHT LAMP        - Turn the lamp on
  OFF LAMP          - Turn the lamp off
  EXAMINE <thing>   - Inspect an object or feature (X also works)
  READ <thing>      - Read an inscription or scroll
  FILL BOTTLE      - Collect water where it is available
  POUR WATER       - Empty the bottle into a suitable vessel
  FEED IBIS        - Offer food to the bird
  TIE ROPE         - Secure a crossing
  UNLOCK DOOR      - Use a suitable key
  HINT             - Ask for a small local clue
  SAVE / LOAD       - Save or restore your expedition
  SCORE             - Show your score
  HELP              - This message
  QUIT              - Exit the game
""")


def examine(noun):
    if not has_light():
        print("You need light to examine anything.")
        return
    features = {
        "gallery": {"basin": "The basin is linked to the door by a stone counterweight. It needs water.",
                    "inscription": "When the river returns to the stars, the road to the dead shall open.",
                    "door": "A counterweight holds the door shut. A channel runs from the basin to its base."},
        "cistern": {"water": "Clear water collects here. A bottle would carry enough for a small basin.",
                    "cistern": "Clear water collects here. A bottle would carry enough for a small basin."},
        "ibis_room": {"ibis": "The bird watches your provisions. Hunger seems stronger than duty.",
                      "bowl": "An empty bowl, just large enough for your food."},
        "archive": {"painting": "The rope is tied to a ring, allowing the explorer to cross the shaft.",
                    "desk": "The desk bears scratches from generations of scribes. Nothing is concealed in it."},
        "shaft": {"ring": "The bronze ring will hold your weight. You could tie a rope here.",
                  "shaft": "Far too wide to jump. A rope would make the crossing possible."},
        "crypt": {"door": "The keyhole is shaped like a jewel. The lock is still sound.",
                  "keyhole": "The keyhole is shaped like a jewel. The lock is still sound."},
        "vault": {"inscription": "The sun belongs above the sand. Bring its image back to the entrance."},
    }
    description = features.get(current_room, {}).get(noun)
    if description:
        print(description)
        return
    item = resolve_item(noun, inventory + rooms[current_room]["items"])
    if item is None:
        return
    if item not in inventory + rooms[current_room]["items"]:
        print("You see no such thing here.")
        return
    descriptions = {
        "bottle": "The bottle contains clear water." if puzzles["bottle_filled"] else "The bottle is empty.",
        "food": "Dry grain and dates, enough to interest a hungry bird.",
        "rope": "A stout rope, long enough to cross a narrow shaft.",
        "jewel key": "A key with an unusually shaped jeweled head.",
        "papyrus scroll": "The scribe advises: carry light, mark the maze with objects, and return each treasure to the desert entrance.",
        "lamp": "A brass lamp. It is " + ("lit." if lamp_on else "unlit."),
    }
    print(descriptions.get(item, "A treasure worth returning to the entrance."))


def interact(verb, noun):
    if not has_light():
        print("You need light to do that safely.")
        return
    if verb == "fill" and noun == "bottle":
        if "bottle" not in inventory:
            print("You need to carry the bottle.")
        elif current_room != "cistern":
            print("There is no water to collect here.")
        else:
            puzzles["bottle_filled"] = True
            print("You fill the bottle from the cistern.")
    elif verb in ("pour", "empty") and noun in ("water", "bottle"):
        if "bottle" not in inventory or not puzzles["bottle_filled"]:
            print("You are not carrying a bottle of water.")
        elif current_room != "gallery":
            print("There is no reason to waste the water here.")
        elif puzzles["basin_filled"]:
            print("The basin is already full.")
        else:
            puzzles["bottle_filled"] = False
            puzzles["basin_filled"] = True
            print("Water fills the basin. A counterweight sinks and the northern door slides open.")
    elif verb == "feed" and noun in ("ibis", "bird"):
        if current_room != "ibis_room":
            print("There is no bird here.")
        elif puzzles["ibis_fed"]:
            print("The ibis is content. It leaves the doorway clear.")
        elif "food" not in inventory:
            print("You need food to offer the ibis.")
        else:
            inventory.remove("food")
            puzzles["ibis_fed"] = True
            print("You put the food in the bowl. The ibis steps aside to eat.")
    elif verb == "tie" and noun == "rope":
        if current_room != "shaft":
            print("There is no suitable crossing here.")
        elif puzzles["rope_tied"]:
            print("The rope is already secure.")
        elif "rope" not in inventory:
            print("You need a rope.")
        else:
            inventory.remove("rope")
            puzzles["rope_tied"] = True
            print("You secure the rope to the bronze ring and cast its loop over a stone spur across the shaft. The crossing is safe.")
    elif verb in ("unlock", "open") and noun in ("door", "vault"):
        if current_room != "crypt":
            print("There is no key-operated door here.")
        elif puzzles["vault_open"]:
            print("The vault door is already open.")
        elif "jewel key" not in inventory:
            print("You need a key that fits the jewel-shaped lock.")
        else:
            puzzles["vault_open"] = True
            print("The jewel key turns. The vault door swings inward.")
    else:
        print("That action has no effect. Try examining the object first.")


def hint():
    if not has_light():
        print("Your lamp must be here, or in your hands, to light the way.")
        return
    clues = {
        "gallery": "Read the inscription and examine the basin.",
        "cistern": "An empty bottle makes a useful water carrier.",
        "ibis_room": "The painted scribe knew how to win a bird's cooperation.",
        "archive": "The wall painting explains a use for the rope.",
        "shaft": "Examine the bronze ring before trying to cross.",
        "crypt": "A treasure from the older chambers is also a useful tool.",
    }
    print(clues.get(current_room, "Make a map. Dropped objects can distinguish similar passages. Return treasures to the entrance."))


def save_game(path=None):
    path = Path(path) if path is not None else SAVE_FILE
    data = {
        "version": 2, "room": current_room, "inventory": inventory,
        "items": {name: room["items"] for name, room in rooms.items()},
        "lamp_on": lamp_on, "moves": moves, "mummy_active": mummy_active,
        "mummy_stolen": mummy_stolen, "discovered": sorted(discovered),
        "puzzles": puzzles,
    }
    temporary = path.with_suffix(path.suffix + ".tmp")
    try:
        temporary.write_text(json.dumps(data, indent=2), encoding="utf-8")
        temporary.replace(path)
    except OSError as error:
        print(f"Cannot save the expedition: {error}")
        return False
    print("Expedition saved.")
    return True


def load_game(path=None):
    global current_room, inventory, lamp_on, moves
    global mummy_active, mummy_stolen, discovered, puzzles
    path = Path(path) if path is not None else SAVE_FILE
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(data, dict) or type(data.get("version")) is not int or data["version"] != 2:
            raise ValueError("unsupported save format")
        if not isinstance(data["room"], str) or data["room"] not in rooms:
            raise ValueError("unknown room")
        if not isinstance(data["items"], dict) or set(data["items"]) != set(rooms):
            raise ValueError("room list does not match this adventure")
        containers = [data["inventory"], *data["items"].values()]
        if any(not isinstance(items, list) or any(not isinstance(item, str) for item in items)
               for items in containers):
            raise ValueError("invalid item list")
        expected = sorted(item for room in INITIAL_ROOMS.values() for item in room["items"])
        flags = data["puzzles"]
        if not isinstance(flags, dict) or set(flags) != set(puzzles) or any(type(flag) is not bool for flag in flags.values()):
            raise ValueError("invalid puzzle state")
        if flags["ibis_fed"]:
            expected.remove("food")
        if flags["rope_tied"]:
            expected.remove("rope")
        if sorted(item for items in containers for item in items) != expected:
            raise ValueError("missing or duplicated items")
        if len(data["inventory"]) > 6:
            raise ValueError("inventory is too large")
        if type(data["moves"]) is not int or data["moves"] < 0:
            raise ValueError("invalid move count")
        if any(type(data[key]) is not bool for key in ("lamp_on", "mummy_active", "mummy_stolen")):
            raise ValueError("invalid expedition flags")
        found = data["discovered"]
        if not isinstance(found, list) or any(not isinstance(item, str) or item not in TREASURES for item in found):
            raise ValueError("invalid discovery list")
        if len(set(found)) != len(found):
            raise ValueError("duplicate discovery")
        credited = set(data["inventory"] + data["items"]["entrance"]) & set(TREASURES)
        if not credited.issubset(found):
            raise ValueError("inconsistent discoveries")
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f"Cannot load the expedition: {error}")
        return False
    # Apply only after every field has passed validation.
    current_room = data["room"]
    inventory = data["inventory"]
    for name, items in data["items"].items():
        rooms[name]["items"] = items
    lamp_on, moves = data["lamp_on"], data["moves"]
    mummy_active, mummy_stolen = data["mummy_active"], data["mummy_stolen"]
    discovered = set(found)
    puzzles = flags
    update_score()
    print("Expedition restored.")
    return True

# ---------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------

def main():
    global lamp_on, moves
    print("PYRAMID - THE SCRIBE'S VAULT")
    print("An expanded Python tribute to Robert Arnstein's adventure.")
    print("Original-inspired prototype and new puzzles; not an exact port.")
    print("You are an explorer at the Great Lost Pyramid.")
    print(f"Return all {len(TREASURES)} treasures to the entrance. Maximum score: {20 * len(TREASURES)}.")
    print("Type HELP for commands. SAVE preserves your expedition.\n")
    print_room()

    while True:
        try:
            cmd = input("\n> ").strip().lower()
        except (EOFError, KeyboardInterrupt):
            print("\nGoodbye.")
            break

        if not cmd:
            continue

        parts = cmd.split(maxsplit=1)
        verb = parts[0]
        noun = parts[1] if len(parts) > 1 else ""

        if verb in ("quit", "exit", "q"):
            print(f"Score: {score}  Moves: {moves}")
            break
        elif verb in ("help", "?"):
            show_help()
        elif verb in ("look", "l"):
            examine(noun) if noun else print_room()
        elif verb in ("examine", "x", "read"):
            examine(noun) if noun else print("Examine what?")
        elif verb == "hint":
            hint()
        elif verb in ("fill", "pour", "empty", "feed", "tie", "unlock", "open"):
            interact(verb, noun)
        elif verb in ("inventory", "i", "inv"):
            if inventory:
                print("You are carrying:", ", ".join(inventory))
            else:
                print("You are carrying nothing.")
        elif verb == "score":
            print(f"Score: {score}  Moves: {moves}")
        elif verb == "save":
            save_game()
        elif verb == "load":
            if load_game():
                print_room()
                check_win()
        elif verb in ("go", "move") or verb in ("north", "south", "east", "west", "up", "down", "n", "s", "e", "w", "u", "d"):
            direction = noun if verb in ("go", "move") else verb
            direction = {"n": "north", "s": "south", "e": "east", "w": "west",
                         "u": "up", "d": "down"}.get(direction, direction)
            move(direction)
            print_room()
            check_win()
        elif verb in ("get", "take"):
            if noun:
                get_item(noun)
            else:
                print("Get what?")
        elif verb == "drop":
            if noun:
                drop_item(noun)
                check_win()
            else:
                print("Drop what?")
        elif verb in ("light", "on", "off", "extinguish") and noun == "lamp":
            if "lamp" in inventory or "lamp" in rooms[current_room]["items"]:
                lamp_on = verb in ("light", "on")
                print("The lamp is now " + ("lit." if lamp_on else "off."))
            else:
                print("You don't have a lamp.")
        else:
            print("I don't understand that command.")

if __name__ == "__main__":
    main()
