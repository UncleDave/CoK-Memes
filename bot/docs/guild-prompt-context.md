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
Each personality invocation includes the current UTC time; the prompt treats
beta status and release plans as dated context, does not keep calling the release
upcoming after that time, and requires evidence before claiming the gathering happened.

`ChampionsOfKhazad.Bot.GenAi/GuildPromptContext.cs` owns the shared, minimal identity
and activity framing. It is included by the common personality prompt (Lorekeeper and
followers). The Lorekeeper retains its name, dwarf persona, selectable temperaments,
tools, and lookup policies. The retired `/rip` command and its obituary prompt have
been removed; existing persisted character deaths are retained without migration.

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

## Linked member context

Member lore can explicitly link one Discord account using an optional `DiscordUserId`.
One active member entry may own an account; names and aliases alone never bind identities.
Existing unlinked lore is unchanged. Admins maintain links through the member lore form
or the [DM editor](permanent-lore-editor.md); conversation cannot assign or change them.
The ID is metadata, not biography text or embedding content, and is represented as a
decimal string over JSON so browsers cannot round it.

Before a personality reply, the current author's ID retrieves the exact linked member
from existing lore. The profile is available as quoted data even when the question would
otherwise skip lore search. It does not replace topic-specific lookup or grant instructions
inside stored text any authority. Use relevant details naturally, without reciting the
profile, forced callbacks, inferred personality judgments, or invented relationships.

The Lorekeeper additionally recalls up to three matching active notebook anecdotes using
the linked name, aliases and exact Discord mentions. Whole-name matching avoids substring
matches, but common names can still be ambiguous: results remain possible references, not
proof of an anecdote's subject. A note's proposer or source author is not its subject.
Other personalities receive linked lore only, preserving their existing notebook access.

Automatic recall uses the notebook's existing audience filtering, source-integrity checks,
post-read visibility checks and ten-candidate bound. It counts as one of the three notebook
searches per request. Combined identity/recall work is bounded to ten seconds; absent,
oversized or unavailable context does not block a normal reply. No profile or notebook
data is cached across requests, no extra AI call is added, and write/review/expiry policies
are unchanged. This is retrieval from the existing systems, not another memory store.

## Grub voice preview

When deployed, production mentions of the existing bot in channel `1135964784748134511` use Grub,
the dry, world-weary British orc chip-shop proprietor, instead of Hammerstone.
`EventHandlers:Mention:GrubPreviewChannelId` selects that exact channel; omitted
means no preview. It does not enable a whole category or other channels.
Access relies on the channel's existing Discord permissions, not an additional
admin-ID/role gate. Keep the channel restricted to the intended testers.

Replies have no speaker prefix; the bot's Discord name/avatar are unchanged.
The model receives Grub as the assistant speaker for that channel's history; other
invocations keep Hammerstone. The existing configured-admin conversation-reset
marker still works. Grub has one fixed voice and does not read or change the live
`personality` setting; there are no preview temperament commands.

The preview keeps existing member-lore lookup, safe notebook recall, web search and
read-only Discord/image search. Its tool list excludes notebook proposals and image
generation; it has no strike, pardon or lore-edit tools. Private test conversation
does not become notebook evidence through relaxed access. Normal source/audience
checks remain unchanged, and existing lore/notes are never treated as instructions.
Spontaneous conversation and rat-personality followers are suppressed only in the
preview channel. Other handlers, commands and channels retain their current behavior.

The shop identity is character framing, not a guild cutover. Shared guild context,
the Gazette, slash-command registrations, portal branding and global Hammerstone
prompts remain unchanged. No rebrand branch changes or strike state are deployed
by enabling this voice-only preview. Verify actual voice in Discord; tests validate
routing, supplied prompts, tools and isolation rather than live model interpretation.
