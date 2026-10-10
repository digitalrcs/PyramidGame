#!/usr/bin/env python3
"""
Pyramid 2000 - Python recreation
A classic text adventure inspired by the 1979 Radio Shack TRS-80 game by Robert Arnstein.
Goal: Collect all 11 treasures and return them to the entrance.
"""

import random
import sys

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

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def print_room():
    room = rooms[current_room]
    print("\n" + room["desc"])
    if current_room != "entrance" and not lamp_on:
        print("It is pitch dark. You are likely to be eaten by a grue.")
        return
    if room["items"]:
        print("You see:", ", ".join(room["items"]))
    exits = list(room["exits"].keys())
    if exits:
        print("Exits:", ", ".join(exits))

def get_item(item):
    global score
    room = rooms[current_room]
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
        score += 5
        print("( +5 points for carrying a treasure )")

def drop_item(item):
    global score
    if item not in inventory:
        print("You aren't carrying that.")
        return
    inventory.remove(item)
    rooms[current_room]["items"].append(item)
    print(f"You drop the {item}.")
    if current_room == "entrance" and item in TREASURES:
        score += 15  # net +20 total for depositing
        print("( +15 points for depositing a treasure at the entrance )")

def move(direction):
    global current_room, moves
    room = rooms[current_room]
    if direction not in room["exits"]:
        print("You can't go that way.")
        return
    if current_room != "entrance" and not lamp_on:
        print("You stumble in the darkness and hit your head. You are dead.")
        sys.exit(0)
    current_room = room["exits"][direction]
    moves += 1
    mummy_check()

def mummy_check():
    global mummy_stolen, score
    if not mummy_active or mummy_stolen:
        return
    treasures_carried = [i for i in inventory if i in TREASURES]
    if treasures_carried and random.random() < 0.25:
        stolen = random.choice(treasures_carried)
        inventory.remove(stolen)
        rooms["hall"]["items"].append(stolen)
        score -= 5
        mummy_stolen = True
        print(f"\nA mummy appears and snatches the {stolen} from you!")
        print("The mummy shambles away. It seems satisfied for now.")

def check_win():
    deposited = [i for i in rooms["entrance"]["items"] if i in TREASURES]
    if len(deposited) >= 11:
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
  SCORE             - Show your score
  HELP              - This message
  QUIT              - Exit the game
""")

# ---------------------------------------------------------------------------
# Main loop
# ---------------------------------------------------------------------------

def main():
    global lamp_on, moves
    print("PYRAMID 2000")
    print("You are an explorer at the Great Lost Pyramid.")
    print("Collect all eleven treasures and return them to the entrance.\n")
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
            print_room()
        elif verb in ("inventory", "i", "inv"):
            if inventory:
                print("You are carrying:", ", ".join(inventory))
            else:
                print("You are carrying nothing.")
        elif verb == "score":
            print(f"Score: {score}  Moves: {moves}")
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
        elif verb == "light" and noun == "lamp":
            if "lamp" in inventory:
                lamp_on = True
                print("The lamp is now lit.")
            else:
                print("You don't have a lamp.")
        else:
            print("I don't understand that command.")

if __name__ == "__main__":
    main()
