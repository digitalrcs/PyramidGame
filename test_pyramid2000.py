import contextlib
import copy
import importlib
import io
import json
import tempfile
import unittest
from collections import deque
from pathlib import Path
from unittest.mock import patch

import pyramid2000


class ExpeditionTests(unittest.TestCase):
    def setUp(self):
        self.game = importlib.reload(pyramid2000)
        self.output = contextlib.redirect_stdout(io.StringIO())
        self.output.__enter__()
        self.addCleanup(self.output.__exit__, None, None, None)

    def test_score_cannot_be_farmed_and_deposit_requires_treasure_at_entrance(self):
        g = self.game
        g.current_room = "hall"
        g.inventory.append("lamp")
        g.rooms["entrance"]["items"].remove("lamp")
        g.lamp_on = True
        g.get_item("ruby")
        self.assertEqual(g.score, 5)
        g.current_room = "entrance"
        for _ in range(4):
            g.drop_item("ruby")
            self.assertEqual(g.score, 20)
            g.get_item("ruby")
            self.assertEqual(g.score, 5)

    def test_light_stays_with_lamp(self):
        g = self.game
        g.get_item("lamp")
        g.lamp_on = True
        g.move("north")
        g.drop_item("lamp")
        self.assertTrue(g.has_light())
        g.move("east")
        self.assertFalse(g.has_light())
        g.get_item("pearl")
        self.assertNotIn("pearl", g.inventory)
        with contextlib.redirect_stdout(io.StringIO()) as output:
            g.print_room()
        self.assertNotIn("SARCOPHAGUS", output.getvalue())
        self.assertNotIn("GOLD NUGGET", output.getvalue())

    def test_unique_nouns_and_ambiguity(self):
        g = self.game
        self.assertEqual(g.resolve_item("nugget", g.TREASURES), "gold nugget")
        self.assertIsNone(g.resolve_item("golden", g.TREASURES))

    def test_mummy_does_not_attack_at_entrance(self):
        g = self.game
        g.inventory.append("ruby")
        with patch.object(g.random, "random", return_value=0):
            g.mummy_check()
        self.assertIn("ruby", g.inventory)
        self.assertFalse(g.mummy_stolen)

    def test_stolen_treasure_can_be_recovered_without_extra_discovery_points(self):
        g = self.game
        g.get_item("lamp")
        g.lamp_on = True
        g.move("north")
        g.get_item("ruby")
        with patch.object(g.random, "random", return_value=0):
            g.mummy_check()
        self.assertNotIn("ruby", g.inventory)
        g.get_item("ruby")
        self.assertEqual(g.score, 5)

    def test_save_round_trip(self):
        g = self.game
        g.get_item("lamp")
        g.lamp_on = True
        g.move("north")
        g.get_item("ruby")
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "save.json"
            self.assertTrue(g.save_game(path))
            before = path.read_text()
            g.drop_item("ruby")
            g.lamp_on = False
            self.assertTrue(g.load_game(path))
            self.assertTrue(g.save_game(path))
            self.assertEqual(before, path.read_text())
            self.assertEqual(g.score, 5)

    def test_invalid_save_leaves_game_unchanged(self):
        g = self.game
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "save.json"
            g.save_game(path)
            valid = json.loads(path.read_text())
            corruptions = [None, {}, {**valid, "room": "missing"},
                           {**valid, "moves": True}, {**valid, "lamp_on": "yes"},
                           {**valid, "inventory": ["ruby"]},
                           {**valid, "discovered": ["ruby", "ruby"]}]
            before = copy.deepcopy((g.rooms, g.inventory, g.current_room, g.score))
            for data in corruptions:
                path.write_text(json.dumps(data))
                self.assertFalse(g.load_game(path))
                self.assertEqual(before, (g.rooms, g.inventory, g.current_room, g.score))

    def test_all_treasures_can_be_recovered(self):
        g = self.game
        g.mummy_active = False
        g.get_item("lamp")
        g.lamp_on = True

        def travel(target):
            queue = deque([(g.current_room, [])])
            seen = set()
            while queue:
                room, path = queue.popleft()
                if room == target:
                    for direction in path:
                        g.move(direction)
                    return
                if room in seen:
                    continue
                seen.add(room)
                for direction, destination in g.rooms[room]["exits"].items():
                    queue.append((destination, path + [direction]))
            self.fail("Unreachable room: " + target)

        g.get_item("food")
        g.get_item("bottle")
        travel("cistern")
        g.interact("fill", "bottle")
        travel("gallery")
        g.interact("pour", "water")
        travel("ibis_room")
        g.interact("feed", "ibis")
        travel("archive")
        g.get_item("rope")
        travel("shaft")
        g.interact("tie", "rope")
        travel("treasure_room")
        g.get_item("key")
        travel("crypt")
        g.interact("unlock", "door")
        travel("entrance")
        g.drop_item("key")
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "solved.json"
            self.assertTrue(g.save_game(path))
            g.puzzles = {key: False for key in g.puzzles}
            self.assertTrue(g.load_game(path))
            self.assertTrue(g.puzzles["rope_tied"])
            self.assertTrue(g.puzzles["vault_open"])

        for treasure in g.TREASURES:
            target = next(name for name, room in g.rooms.items() if treasure in room["items"])
            travel(target)
            g.get_item(treasure)
            self.assertIn(treasure, g.inventory)
            travel("entrance")
            g.drop_item(treasure)
        self.assertEqual(g.score, 280)
        with self.assertRaises(SystemExit):
            g.check_win()

    def test_puzzle_gates_require_solutions(self):
        g = self.game
        g.get_item("lamp")
        g.lamp_on = True
        for room, direction in [("gallery", "north"), ("ibis_room", "east"),
                                ("shaft", "north"), ("crypt", "north")]:
            g.current_room = room
            before = g.moves
            g.move(direction)
            self.assertEqual(g.current_room, room)
            self.assertEqual(g.moves, before)

    def test_wrong_puzzle_location_does_not_consume_supplies(self):
        g = self.game
        g.get_item("food")
        g.get_item("bottle")
        g.puzzles["bottle_filled"] = True
        g.interact("feed", "ibis")
        g.interact("pour", "water")
        self.assertIn("food", g.inventory)
        self.assertTrue(g.puzzles["bottle_filled"])

    def test_terminal_wraps_to_64_columns(self):
        with contextlib.redirect_stdout(io.StringIO()) as output:
            self.game.print_room()
            self.game.show_help()
        self.assertTrue(all(len(line) <= 64 for line in output.getvalue().splitlines()))
        self.assertEqual(output.getvalue(), output.getvalue().upper())

    def test_interactive_commands_reach_the_expansion(self):
        commands = iter(["get lamp", "light lamp", "take bottle", "get food", "n", "n",
                         "read inscription", "w", "fill bottle", "e", "pour water",
                         "e", "feed ibis", "e", "get rope", "w", "w", "n", "tie rope", "q"])
        with patch("builtins.input", side_effect=lambda _: next(commands)):
            self.game.main()
        self.assertEqual(self.game.current_room, "shaft")
        self.assertTrue(self.game.puzzles["rope_tied"])


if __name__ == "__main__":
    unittest.main()
