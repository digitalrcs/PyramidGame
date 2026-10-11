namespace Pyramid.Web.Game;

/// <summary>Authored scenery states. Loose objects are listed from the save by LOOK, never from this prose.</summary>
public static class RoomNarrative
{
    public static string Describe(Expedition s)
    {
        var level=Engine.Current(s);
        bool Has(string flag)=>s.Flags.Contains(flag);
        string description=(s.Level,s.Room) switch
        {
            (1,"west") when Has("west-open") => "Water fills the counterweight basin. Its mechanism has opened the northern passage to the river shrine.",
            (1,"east") when Has("east-open") => "Water collects beneath a stone carving of an ibis. Nearby, the living ibis, a long-billed bird, eats contentedly from its feeding bowl. The northern arch is clear. A sturdy peg projects from the wall.",
            (1,"gallery") when Has("north-open") => "A secure rope crossing spans the shaft from the bronze ring to the stone spur. The sun terrace is accessible to the east.",
            (2,"west") when Has("west-open") => "The oiled sluice wheel stands at its open setting. Water has drained from the northern stacks, leaving the passage clear.",
            (2,"west") when Has("oiled") => "The sluice wheel is oiled and turns freely. The northern stacks are still flooded; the wheel is ready to open the sluice.",
            (2,"east") when Has("east-open") => "The scales stand balanced. The gate to the heron's vault is open to the north. A stone bench stands beside the scales.",
            (2,"gallery") when Has("north-open") => "A cedar crossing bridges the two ledges. The canal bridge now leads safely east to the crystal grotto.",
            (3,"west") when Has("west-open") => "The polished mirror reflects moonlight through the open shutter. The lunar shrine lies north.",
            (3,"east") when Has("east-open") => "The repaired star engine turns beneath the constellation Orion. The sapphire dome is open to the north. The engraved instruction still reads: 'Read the chart. Set the constellation, not its brightest star.'",
            (3,"east") when Has("cog-fit") => "The engine's gear train turns again. Its constellation dial awaits a setting. The engraved instruction reads: 'Read the chart. Set the constellation, not its brightest star.'",
            (3,"gallery") when Has("north-open") => "The mirror faces east, directing its beam into the eye carving. The eye chamber is open. The mirror's dial reads NORTH, EAST, SOUTH, WEST.",
            (4,"west") when Has("west-open") => "The bell has sounded SUN, RIVER, MOON in order. Its final note has opened the northern mask vault. Beneath the marks: 'Strike day, then water, then night.'",
            (4,"east") when Has("east-open") => "The reed door has answered the flute and stands open to the musician's tomb. A velvet tray rests beside the recess. The inscription says: 'Only a reed may answer a reed.'",
            (4,"gallery") when Has("north-open") => "The resonating bridge is visible in vibrating dust and leads safely east. The stone plate hums with the tuning fork's note.",
            (5,"west") when Has("west-open") => "The heart scales stand balanced, their feather-marked pan level with the stone heart. The jade sanctuary is open to the north.",
            (5,"east") when Has("east-open") => "The veil has risen at the name AMEN, revealing the northern royal crypt. The backward inscription NEMA remains above the doorway.",
            (5,"east") when Has("name-seen") => "The name above the veil reads NEMA backward; your mirror has revealed AMEN. The veil remains lowered, waiting for the name to be spoken.",
            (5,"gallery") when Has("north-open") => "The stone serpent studies its own reflected gaze. The eastern passage is clear beyond its coils. The wall says: 'Let the watcher behold itself.'",
            (6,"west") when Has("west-open") => "Stone balances stone on the scales of eternity. The feather shrine is open to the north. The instruction reads: 'Balance stone with stone; judgment comes later.'",
            (6,"east") when Has("east-open") => "The ankh mechanism's circuit is complete. Its straight conductor glows, and the chamber of ages is open to the north.",
            (6,"gallery") when Has("north-open") => "The solar iris faces east, toward sunrise. Its open passage leads to the heart of the sun. The engraving reads: 'Return the eye to the place of sunrise.'",
            _ => level.Rooms[s.Room].Description
        };

        if(s.Room=="east" && s.Level==1 && Has("water"))
            description+=" You have filled the bottle from the cistern.";
        if(s.Room=="camp")
        {
            var secured=level.Relics.Where(s.Banked.Contains).ToArray();
            description+=secured.Length>0?" Secured offerings: "+string.Join(", ",secured)+".":" No offerings have been secured on this level yet.";
        }
        if(s.Room=="gate")
        {
            // The gate's instructions become status once the player has met each requirement.
            var remaining=level.Relics.Where(x=>!s.Banked.Contains(x)).ToArray();
            description=s.Level==6?"Five niches overlook the throne of the gods.":"The ascent waits beyond the ancient gate.";
            description+=remaining.Length==0?" All three offerings are secured.":" Offerings still to secure at the landing: "+string.Join(", ",remaining)+".";
            description+=Has("word")?" The spoken riddle answer has been accepted.":" The gate still awaits the answer to the study's riddle.";
            if(s.Level<6)
                description+=s.Journal.Contains(Engine.Tablet(s,s.Level))?" This level's tablet is recorded in your journal.":" Record this level's tablet in your journal before ascending.";
            else
                description+=" Niches: "+string.Join(" | ",Enumerable.Range(1,5).Select(n=>$"{n}: {s.Arrangement.FirstOrDefault(x=>x.Value==n).Key??"empty"}"))+". Read your journal; SET <GOD> <NUMBER>, then INVOKE.";
            if(s.Won)description+=" The gods have answered. Your expedition is complete.";
            else if(s.Level<6 && remaining.Length==0 && Has("word") && s.Journal.Contains(Engine.Tablet(s,s.Level)))description+=" The way is ready. ASCEND.";
        }
        return description;
    }
}
