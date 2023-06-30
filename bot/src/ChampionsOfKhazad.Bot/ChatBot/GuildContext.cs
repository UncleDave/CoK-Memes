namespace ChampionsOfKhazad.Bot.ChatBot;

public static class GuildContext
{
    public static class Members
    {
        public static string Wooper =>
            "Wooper is the Guild Master of Champions of Khazad. He is a male Night Elf Balance Druid.";

        public static string Mardz =>
            "Mardz is an officer of Champions of Khazad. He is a male Dwarf Hunter. He jealously guards the contents of the guild bank, like a dragon on its pile of treasure.";

        public static string Cokebeard =>
            "Cokebeard is a Holy Priest, he is known for never showing up on time, usually because he's in the bath.";

        public static string Leaf =>
            "Leafhunter is a Hunter, he is known for exclusively eating pizza and being completely unable to spell.";
        
        public static string Shurikun =>
            "Shurikun is a Gnome Death Knight, he is known for his small stature and his inability to interrupt a spell.";
        
    }

    public static IReadOnlyDictionary<string, string> ContextMap { get; } =
        new Dictionary<string, string>
        {
            { "wooper", Members.Wooper },
            { "woop", Members.Wooper },
            { "mardz", Members.Mardz },
            { "mards", Members.Mardz },
            { "guild bank", Members.Mardz },
            { "bank", Members.Mardz },
            { "cokebeard", Members.Cokebeard },
            { "coke", Members.Cokebeard },
            { "leaf", Members.Leaf },
            { "leafhunter", Members.Leaf },
            { "shurikun", Members.Shurikun },
            { "shuri", Members.Shurikun }
        };
}
