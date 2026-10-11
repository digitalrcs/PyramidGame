namespace Pyramid.Web.Game;

public record Room(string Id, string Name, string Description, Dictionary<string,string> Exits, string[] Items, Dictionary<string,string> Features);
public record Puzzle(string Room, string Command, string[] Needs, string[] Requires, string Flag, string Response, string[] Hints);
public record Level(int Number, string Name, string Objective, string Word, string[] Relics, Dictionary<string,Room> Rooms, Puzzle[] Puzzles);

public static class World
{
    // Stable art numbering; do not derive it from mutable dictionary order.
    public static readonly string[] RoomOrder = ["camp","hub","west","west-shrine","east","east-shrine","gallery","study","north-shrine","gate"];
    public static int RoomNumber(int level,string room) => (level-1)*RoomOrder.Length+Array.IndexOf(RoomOrder,room)+1;
    public static readonly Level[] Levels = Build();
    private static Level[] Build()
    {
        return [
            Make(1,"The Buried Gate","Restore the river, feed its guardian, and bridge the shaft.","dawn",
                ["golden scarab","ivory ibis","sun disk"],
                ["Desert threshold","Hall of dust","Star basin","River shrine","Cistern of reeds","Ibis sanctuary","Broken crossing","Scribe's alcove","Sun terrace","Buried gate"],
                ["The desert has kept its secret for four thousand years. This threshold is your safe place to deposit relics.",
                 "Sand whispers across a mosaic of the river. West lies a counterweight basin; east you hear dripping water. A northern gallery opens toward a shaft.",
                 "A dry basin hangs from a counterweight. A small opening north is sealed by its mechanism.",
                 "Water murmurs beneath a shrine carved with river boats. A narrow offering niche is cut into the shrine.",
                 "Water collects beneath a stone carving of an ibis. Nearby, a living ibis, a long-billed bird, guards the northern arch beside an empty feeding bowl. A sturdy peg projects from the wall.",
                 "The ibis has allowed you into its sanctuary. An offering altar bears delicate carvings.",
                 "A shaft severs the eastern terrace. A bronze ring is fastened to the wall. Scratches on a stone spur across the gap suggest a rope crossing.",
                 "A scribe has drawn five empty niches. Below them: 'Name the hour when night yields to the first light.' READ TABLET records a stranger clue.",
                 "A stone sun hangs above the terrace. An offering bench stands beneath it.",
                 "Three offering hollows surround the buried gate. Recover the level's relics, deposit them at the threshold, and speak the scribe's answer. Then ASCEND."],
                ["lamp","bottle"], ["grain"], ["rope"], [],
                [new("east","fill bottle",["bottle"],[],"water","The bottle fills with clear water.",["The cistern can replenish a small vessel.","Carry the bottle from the threshold.","FILL BOTTLE here."]),
                 new("west","pour water",["bottle"],["water"],"west-open","The counterweight sinks. The river shrine opens.",["The basin wants its river back.","Bring a filled bottle from the cistern.","FILL BOTTLE in the cistern, then POUR WATER here."]),
                 new("east","feed ibis",["grain"],[],"east-open","The ibis turns to its meal. The northern arch is clear.",["The bird's bowl is empty.","Grain waits at the star basin.","Carry GRAIN here and FEED IBIS."]),
                 new("gallery","tie rope",["rope"],[],"north-open","The rope holds. You can cross east to the sun terrace.",["Inspect the bronze ring.","A rope hangs in the cistern.","Carry ROPE here and TIE ROPE."])]),
            Make(2,"The Drowned Archive","Recover the archive by mastering water and counterweights.","memory",
                ["lapis tablet","bronze heron","crystal vessel"],
                ["Archive landing","Hall of currents","Sluice works","Flooded stacks","Weighing room","Heron's vault","Canal bridge","Keeper's study","Crystal grotto","Archive stair"],
                ["The air is cool and wet. Your landing has a dry shelf for recovered relics.",
                 "A map of old waterways covers the floor. The works lie west, scales east, and a canal bridge north.",
                 "A sluice wheel has rusted in place. Oil would free it.",
                 "The water has drained from the stacks. A high shelf sits above the mud line.",
                 "A gate hangs from unbalanced scales. One pan bears a mark shaped like an iron weight. A stone bench stands beside the scales.",
                 "Balancing stones surround an offering plinth carved with reeds.",
                 "The bridge is missing its central plank. Two sound ledges remain, close enough to support a board.",
                 "An archivist's desk stands against the wall. The stair riddle reads: 'I keep what time takes. I live in minds and marks. Name me.' READ TABLET for a divine relation.",
                 "The water here is still. Ripples reflect your lamplight across the grotto walls.",
                 "The archive stair awaits its three relics and the keeper's spoken answer."],
                [],["iron weight"],["oil flask"],["cedar plank"],
                [new("west","oil wheel",["oil flask"],[],"oiled","Oil works into the rust. The wheel will turn.",["Rust binds the wheel.","The weighing room holds oil.","Carry OIL FLASK here and OIL WHEEL."]),
                 new("west","turn wheel",[],["oiled"],"west-open","The sluice drains the northern stacks.",["Free the rust before forcing the wheel.","OIL WHEEL with the flask first.","TURN WHEEL after oiling it."]),
                 new("east","place weight",["iron weight"],[],"east-open","The pans settle level. The heron's vault opens.",["The empty scale bears the outline of a weight.","The sluice works hold an iron weight.","Carry IRON WEIGHT here and PLACE WEIGHT."]),
                 new("gallery","lay plank",["cedar plank"],[],"north-open","The cedar bridges the ledges. The grotto is accessible east.",["The gap is narrow enough for a board.","Search the keeper's study.","Carry CEDAR PLANK here and LAY PLANK."])]),
            Make(3,"The Observatory","Restore the ancient sky and follow the path of reflected light.","shadow",
                ["silver crescent","star sapphire","obsidian eye"],
                ["Observatory landing","Hall of dusk","Mirror workshop","Lunar shrine","Star engine","Sapphire dome","Light well","Astronomer's study","Eye chamber","Stair of eclipses"],
                ["A cold draft carries the scent of stone. Deposit this level's relics on the landing's velvet cloth.",
                 "Mechanical stars hang overhead. Mirrors west, a star engine east, and a light well north once made a single instrument.",
                 "A tarnished mirror faces a shuttered lunar shrine.",
                 "The restored mirror casts a pale crescent over an offering pedestal.",
                 "The engine has a socket for a small cog. Its engraved instruction says: 'Read the chart. Set the constellation, not its brightest star.'",
                 "Stars of blue glass surround a small offering socket.",
                 "A beam strikes the floor. The mirror's dial reads NORTH, EAST, SOUTH, WEST. A carving shows the rising sun illuminating an eye.",
                 "An ancient telescope points into the dark. The stair asks: 'Born of light, I follow your feet, yet vanish when light leaves. What am I?' READ TABLET preserves a divine relation.",
                 "Light from the east-facing mirror reveals an eye-shaped offering niche within the wall.",
                 "An eclipse is cut into the stair door. Return three relics and name the astronomer's companion."],
                [],["star chart"],["polishing cloth"],["brass cog"],
                [new("west","polish mirror",["polishing cloth"],[],"west-open","Moonlight returns to the mirror. The lunar shutter opens.",["The mirror cannot reflect through tarnish.","There is a polishing cloth at the star engine.","Carry POLISHING CLOTH and POLISH MIRROR here."]),
                 new("east","fit cog",["brass cog"],[],"cog-fit","The engine begins to turn, awaiting a constellation.",["Inspect the missing gear socket.","Find the cog in the study.","Carry BRASS COG here and FIT COG."]),
                 new("east","set orion",["star chart"],["cog-fit"],"east-open","Orion rises. The sapphire dome rotates open.",["Read the star chart.","Repair the engine before setting Orion.","FIT COG, carry STAR CHART, then SET ORION."]),
                 new("gallery","turn east",[],[],"north-open","The beam strikes the eye. The eastern chamber opens.",["The carving depicts sunrise.","Sunrise indicates east.","TURN EAST here."])]),
            Make(4,"The Halls of Echoes","Listen, mark your way, and open the silent king's passages.","silence",
                ["amber mask","golden reed","echo pearl"],
                ["Echo landing","Whispering hall","Bell chamber","Mask vault","Reed door","Musician's tomb","Echo crossing","Silent study","Pearl niche","Hushed stair"],
                ["Your footfalls return twice from the dark. This landing remains a safe place for recovered relics.",
                 "Three passages breathe at different pitches. West a bell waits; east a reed door hums. North, an echo crosses a black ravine.",
                 "A bell carries three marks: SUN, RIVER, MOON. Beneath them: 'Strike day, then water, then night.'",
                 "The final vibration has opened a mask-shaped offering niche.",
                 "A reed-shaped recess fronts a sealed tomb. A velvet tray rests beside the recess. The inscription says: 'Only a reed may answer a reed.'",
                 "A carved musician holds out his hands for an offering.",
                 "An invisible bridge hums faintly. A stone plate bears the outline of a tuning fork. A warning says to make the bridge sing before crossing.",
                 "A riddle is carved into the desk: 'Say my name and you break me.' READ TABLET adds another divine relation.",
                 "The resonating bridge leads to a rounded offering niche. Its walls remember every sound.",
                 "Even your lamp seems quiet here. Return the three relics and speak the paradox that opens the stair."],
                [],["reed flute"],["tuning fork"],["wooden mallet"],
                [new("west","strike sun river moon",["wooden mallet"],[],"west-open","Three notes resolve into one. The mask vault opens.",["Read the order beneath the bell.","You need the wooden mallet from the study.","Carry WOODEN MALLET and STRIKE SUN RIVER MOON."]),
                 new("east","play flute",["reed flute"],[],"east-open","The reed door answers your tune and opens.",["A reed must answer the door.","A flute rests in the bell chamber.","Carry REED FLUTE here and PLAY FLUTE."]),
                 new("gallery","sound fork",["tuning fork"],[],"north-open","The bridge becomes visible in vibrating dust. Cross east.",["The plate depicts a tuning fork.","Find the fork at the reed door.","Carry TUNING FORK here and SOUND FORK."])]),
            Make(5,"The Hall of Judgment","Balance truth, reveal the hidden name, and break the serpent's vigil.","truth",
                ["jade heart","royal seal","serpent crown"],
                ["Judgment landing","Hall of witnesses","Heart scales","Jade sanctuary","Veiled doorway","Royal crypt","Serpent passage","Judge's study","Crown shrine","Final ascent"],
                ["You have climbed beyond the reach of daylight. A basalt bench is reserved for recovered relics.",
                 "Stone witnesses watch every passage. Scales wait west, a veiled doorway east, and the serpent gallery lies north.",
                 "The scale is heavy with a stone heart. Its empty pan is carved with a feather.",
                 "The balanced scales have opened a sanctuary with a heart-shaped offering niche.",
                 "A name is written backward above the veil: NEMA. Read the name with the proper instrument, then speak it.",
                 "The crypt bears no king's name. Its offering plinth belongs to whoever reaches this place.",
                 "A stone serpent blocks the east. Its eyes shine in your lamplight. The wall says: 'Let the watcher behold itself.'",
                 "A riddle asks: 'A lie may hide me, yet cannot change me. I remain when witnesses disagree.' READ TABLET for the last divine relation.",
                 "The serpent has turned inward. A crown-shaped offering plinth stands beyond its coils.",
                 "Five divine tablets must accompany you into the final chamber. Recover three relics, speak the judge's answer, and ASCEND."],
                [],["bronze mirror"],["feather"],[],
                [new("west","place feather",["feather"],[],"west-open","The scales balance. The jade sanctuary opens.",["The carving shows what balances a heart.","Find the feather by the veiled doorway.","Carry FEATHER here and PLACE FEATHER."]),
                 new("east","reflect name",["bronze mirror"],[],"name-seen","In the mirror you read AMEN. Speak it here.",["The name is backward.","A mirror rests beside the heart scales.","Carry BRONZE MIRROR here and REFLECT NAME."]),
                 new("east","say amen",[],["name-seen"],"east-open","The veil rises at its ancient name.",["Reflect the name before speaking.","The reflected letters form AMEN.","REFLECT NAME with the mirror, then SAY AMEN."]),
                 new("gallery","show mirror",["bronze mirror"],[],"north-open","The serpent studies its own gaze. You slip past to the east.",["The watcher must behold itself.","Bring the mirror from the scales.","Carry BRONZE MIRROR here and SHOW MIRROR."])]),
            Make(6,"Chamber of the Gods","Recover the final offerings and place the five gods in their only possible order.","eternity",
                ["feather of maat","ankh of ages","solar heart"],
                ["Gods' threshold","Hall of five names","Scales of eternity","Feather shrine","Ankh mechanism","Chamber of ages","Solar passage","Divine archive","Heart of the sun","Throne of the gods"],
                ["Six levels have led you here. Three final offerings must rest at this threshold before the gods will hear you.",
                 "Five names circle an empty throne. The archive holds the rules; the shrines hold the offerings. Every answer you need lies in this pyramid.",
                 "The scale's empty pan bears a stone outline. Its instruction reads: 'Balance stone with stone; judgment comes later.'",
                 "A crystal globe hangs over an offering stand. The globe opens at your touch.",
                 "The ankh mechanism is missing a straight conductor.",
                 "Time feels still. An ankh-shaped offering recess hangs above a timeless altar.",
                 "A solar iris points west into darkness. An engraving commands: 'Return the eye to the place of sunrise.'",
                 "Five gods belong in five niches, numbered 1 to 5 from left to right. The five tablets describe a single possible order. SET <GOD> <NUMBER> at the throne, then INVOKE. The final riddle is: 'I have neither beginning nor end; time is only my shadow.'",
                 "The sun's offering pedestal is warm even here beneath the mountain.",
                 "Five niches overlook a silent throne. Read your journal and assemble the gods. The final offerings must be deposited, and the last riddle spoken, before INVOKE can succeed."],
                [],["copper rod"],["stone cube"],[],
                [new("west","place cube",["stone cube"],[],"west-open","The scales settle and the feather shrine opens.",["The scale calls for stone, not a feather.","Find the stone cube at the ankh mechanism.","Carry STONE CUBE here and PLACE CUBE."]),
                 new("east","fit rod",["copper rod"],[],"east-open","The copper rod completes the circuit. The chamber of ages opens.",["The gap calls for a conductor.","A copper rod lies by the scales.","Carry COPPER ROD here and FIT ROD."]),
                 new("gallery","turn east",[],[],"north-open","The iris turns toward sunrise. The solar passage opens east.",["The eye must face sunrise.","Sunrise is east.","TURN EAST here."])])
        ];
    }

    private static Level Make(int number,string name,string objective,string word,string[] relics,string[] names,string[] descriptions,string[] camp,string[] west,string[] east,string[] study,Puzzle[] puzzles)
    {
        string[] ids=["camp","hub","west","west-shrine","east","east-shrine","gallery","study","north-shrine","gate"];
        Dictionary<string,string>[] exits=[new(){{"north","hub"}},new(){{"south","camp"},{"west","west"},{"east","east"},{"north","gallery"}},new(){{"east","hub"},{"north","west-shrine"}},new(){{"south","west"}},new(){{"west","hub"},{"north","east-shrine"}},new(){{"south","east"}},new(){{"south","hub"},{"west","study"},{"east","north-shrine"},{"north","gate"}},new(){{"east","gallery"}},new(){{"west","gallery"}},new(){{"south","gallery"}}];
        string[][] items=[camp,[],west,[relics[0]],east,[relics[1]],[],study,[relics[2]],[]];
        var rooms=new Dictionary<string,Room>();
        for(int i=0;i<ids.Length;i++) rooms[ids[i]]=new(ids[i],names[i],descriptions[i],exits[i],items[i],new());
        return new(number,name,objective,word,relics,rooms,puzzles);
    }
}
