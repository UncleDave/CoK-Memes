namespace ChampionsOfKhazad.Bot.LoreUploader;

public record Lore(string History, string[] Rules, Dictionary<string, string> Terms, Member[] Members);
