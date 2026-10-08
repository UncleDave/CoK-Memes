# Guild context in AI prompts

Champions of Khazad is transitioning to **World of Warcraft: Forever**. Its Mists of
Pandaria era has effectively ended; prompts must not present it as a currently active
MoP raiding guild or assume officer-organised events and regular in-game activity.
This does not establish that nobody logs in, all members have moved, or the guild has
disbanded. Forever schedules, progression, participation, and mechanics must not be invented.

The administrator's update dated **8 October 2026** records some members already playing
the Forever beta. The administrator reports that beta access requires paying roughly
**€60**. Guild banter about paying far too much to “daddy Blizzard” refers to that real,
approximate cost; “far too much” is the comic judgment, not a denial of the access fee.
Many members plan to play the release together at **00:00 CET on 5 November 2026**:
the same instant is **23:00 GMT on 4 November 2026** for UK members. This is a guild
plan, not a verified official launch announcement, organised raid, or guaranteed turnout.
Each personality/obituary invocation includes the current UTC time; the prompt treats
beta status and release plans as dated context, does not keep calling the release
upcoming after that time, and requires evidence before claiming the gathering happened.

`ChampionsOfKhazad.Bot.GenAi/GuildPromptContext.cs` owns the shared, minimal identity
and activity framing. It is included by the common personality prompt (Lorekeeper and
followers) and the standalone `/rip` obituary prompt. The Lorekeeper retains its name,
dwarf persona, selectable temperaments, tools, and lookup policies. Obituary requirements
and command behaviour are unchanged.

Past-expansion anecdotes remain guild history, not events to relabel as Forever stories.
Changing member details and specific guild plans belong in permanent lore, with current
evidence used to distinguish them from historical activity. Existing stored lore is not
rewritten by changing these prompts; update relevant entries through the
[DM lore editor](permanent-lore-editor.md).

The DM editor updates stored lore, not code-defined prompt context. Raid-name/description
lists, summon jokes, configured event/follower targets, and the independent Leaf site
are also unchanged. This transition does not automatically disable scheduled events or
remove old jokes. Notebook discovery/review and DM editing use expansion-neutral tasks
and do not contain a separate MoP/Classic guild identity to replace.

Existing tests check that the shared context reaches actual model requests for every
Lorekeeper temperament and the five conversation follower personalities. They test
prompt assembly, not the spelling of individual lore facts; live model interpretation
is not guaranteed by these tests.
