using System.Text.RegularExpressions;

namespace ChampionsOfKhazad.Bot.BlizzardProbe;

public sealed record ProbeOptions(string Region, string StaticBuild, string? CharacterSlug, string? RealmSlug, string? GuildSlug)
{
    public static ProbeOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            if (
                args[i] is not ("--region" or "--static-build" or "--character" or "--realm" or "--guild")
                || i + 1 >= args.Length
                || !values.TryAdd(args[i], args[i + 1])
            )
                throw new ArgumentException(
                    "Unknown, repeated, or missing option; use --help. Credentials are not accepted as command-line options."
                );
        }

        var region = values.GetValueOrDefault("--region", "eu");
        var build = values.GetValueOrDefault("--static-build", "1.60.1_70399");
        if (region is not ("eu" or "us" or "tw" or "kr"))
            throw new ArgumentException("Region must be eu, us, tw, or kr.");
        if (!Regex.IsMatch(build, @"\A[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}_[0-9]{1,10}\z"))
            throw new ArgumentException("Static build must look like 1.60.1_70399.");

        foreach (var key in new[] { "--character", "--realm", "--guild" })
            if (values.TryGetValue(key, out var slug) && !Regex.IsMatch(slug, @"\A[\p{L}\p{M}\p{N}][\p{L}\p{M}\p{N}-]{0,127}\z"))
                throw new ArgumentException("Character, realm, and guild options must be slugs, not URLs or paths.");

        var realm = values.GetValueOrDefault("--realm");
        var guild = values.GetValueOrDefault("--guild");
        if ((realm is null) != (guild is null))
            throw new ArgumentException("Supply --realm and --guild together for the provisional realm-based guild routes.");

        return new(region, build, values.GetValueOrDefault("--character"), realm, guild);
    }
}
