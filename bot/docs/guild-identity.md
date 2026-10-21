# Guild identity and Grub

Crossroads Mega Chippy is the guild's new identity, following Champions of Khazad.
The guild is moving to World of Warcraft: Forever, playing Horde on a PvE realm.
The rebrand is prepared on a branch for officer review; deploying this version is
the cutover, not a claim that the tentative name has already been adopted in Discord.
Keep the rollout off `main` until the rebrand is approved.

This branch is the post-test rollout: Grub handles all normally configured mention
channels, with full tools, selectable temperaments, customer strikes and pardons.
It retains `main`'s Discord-account links and author-aware lore/notebook recall.
The temporary channel-only voice preview on `main` is removed here, including its
extra reply label, read-only tool mode and follower suppression. Preview configuration
left in deployment settings has no effect after the cutover.

## Grub

The main bot persona is **Grub**, full name **Grub Greasefist**: a broad
green orc, chip-shop proprietor, and keeper of the guild's grease-stained ledger.
Discord display name and model speaker name: **Grub**.
He replaces Hammerstone as the active character, without rewriting old conversations
or inventing an in-world handover as established guild history.

Grub runs the shop with the seriousness of a military quartermaster. He is proud
of the establishment, suspicious of customers' competence, and quietly dependable.
His voice is an orc first and a world-weary chip-shop proprietor second: gruff,
gravelly, short and weighty, with rough contractions, blunt judgments and impatient
confidence. Dry British counter-service humour is seasoning, not polished pub banter
or clever paperwork wordplay. Occasional grunts and images of pride, guts, tusks or
strength add texture without constant shouting, broken grammar or repeated Warcraft
catchphrases. Orcish does not mean stupid; the selected temperament still controls
hostility. Prefer specific guild observations and petty commercial grievances to
food puns. Answer the question; the character should not obstruct useful assistance.

The shop, ledger, and commercial grievances are character framing, not evidence
that real members bought anything or owe money. Accuracy, lore lookup, attribution,
privacy, sensitive-topic handling, and tool/resource policies are unchanged.

### Temperaments and other voices

- `baseline`: weary professionalism and dry understatement.
- `grouchy`: foul-mouthed, argumentative counter service, while remaining helpful.
- `furious`: incandescent outrage across the counter, still accurate and useful.

Existing `personality` DM commands, expiry, and persistence are unchanged. Earlier
bot messages must not pull Grub back into Hammerstone's identity or another tone.
The five spontaneous conversation follower personalities retain their alternate
comic voices and receive the shared guild context; they are not all rewritten as
Grub. The Crossroads Gazette keeps its own affectionate local-newspaper voice.

### Visual direction

Stained apron, battered paper hat, frying basket, grease-stained ledger, and
Horde-red shop signage. This description is available to the main personality;
it does not generate or upload a Discord avatar automatically.

### Customer strikes and pardons

Grub may give the customer currently addressing him an occasional comic strike
for unmistakable direct taunts, personal insults aimed at him, or explicit roleplayed
shop misconduct. Default to no strike when the target or intent is unclear. Each saved
strike has a short, concrete reason; calling something playful is not grounds to penalise it.
Ordinary questions, thanks, disagreements, criticism, service complaints, poor gameplay,
quoted text, other members' conduct, sensitive topics, and asking him to stop the act
are not grounds for a strike. Neither are nostalgia, bot preferences or comparisons:
"Shame. I liked the old clanker more" should get a counter-quip, not a strike for
insulting Grub. Temperament changes his voice, not this threshold.
Before striking, Grub must have a coherent objection to the specific conduct; accepting
it as harmless or simply agreeing with it is a reason not to strike. This remains a
model judgment, not a deterministic text classifier.

Three strikes bars the customer from Grub's normal service until pardoned. They can
still address him to apologise or appeal; Grub may clear all their strikes if persuaded,
including before the third strike. A successful appeal from a barred customer confirms
the pardon without performing the request embedded in that appeal; ask again for service.
Grub must not demand real money, favours, humiliation, or personal information.
The configured admin can inspect or reset a record by DM:

- `customers` — help.
- `customers show <user-id>` — current strikes and reasons.
- `customers pardon <user-id>` — clear strikes and lift the bar.

Normal Grub replies also have a read-only `list_customer_strikes` tool for questions
such as "Who has strikes?" and "Who is barred?". It is bound to the current Discord
guild, lists only active strikes, and optionally filters to barred customers. Each
page contains up to 25 customers with exact Discord IDs/mentions, counts and barred
status; `nextOffset` explicitly identifies another page. Cleared records are excluded.
This public lookup returns summaries, not strike reasons from potentially private
conversations. Detailed reasons remain in the current customer's saved verdict and
the admin DM lookup. It grants no cross-customer strike/pardon authority and is not
available to spontaneous followers, ledger decisions or barred counter banter.

The `grubCustomers` Mongo collection stores one record per guild and Discord user.
Strikes survive restarts and conversation resets; they do not expire automatically.
This is shop roleplay with bot-service enforcement, never a Discord ban, timeout,
guild disciplinary rule, or established lore. Other followers and commands are unaffected.
Do not copy the ledger into lore or the temporary notebook.

A separate, low-reasoning AI decision receives the conversation but only strike/pardon
tools bound to the current author. Normal tools are exposed only after rechecking the
saved record. Barred customers' decisions receive only the pardon tool; they never
reach the normal service pipeline. The application delivers saved verdicts and a
fixed barred status; model prose is not authority for a strike or pardon.

After a successful decision and status recheck, a short, low-reasoning, tool-free
call adds contextual counter banter to that status. The third strike gets a send-off
in the selected temperament, with its saved reason; repeat visits use the latest
message and recent replies to vary the refusal. This call shares Grub's character
and temperament, not his normal service instructions. It does not retrieve linked
lore/notebook context or expose image, search, memory, strike or pardon tools.
The prompt restricts the prose to a refusal, not an answer to the barred request;
live semantic compliance and humour remain model judgments, not deterministic guarantees.

Only the saved verdict and status are fixed; successful banter does not append the
same pardon-help checklist each time. Failed, empty, oversized, truncated or unexpected
tool-call output falls back to the fixed refusal and appeal instructions without losing
a saved strike. Decision failures skip the extra call. A successful appeal still gets
the application's pardon confirmation and must submit a fresh request for normal service.

The normal reply receives a separate application-generated context message containing
the current count and this request's confirmed saved action, count and reason, if any.
Grub treats that action as his own and continues the printed verdict naturally in his
selected voice, rather than disowning it as another system's decision. A no-op supplies
no saved action; a concurrent change can make the current count differ from the count
immediately after his action. Reasons are quoted data, never instructions. The receipt
and service checks remain deterministic; tests verify the context handoff, not live
model consistency or delivery quality.
Only replies with a confirmed saved strike receive the additional stance guidance,
including third-strike send-offs. They should object to the specific conduct rather
than endorse it and pivot to punishment, without fabricating disagreement with a valid
factual point. They do not soften the verdict with "playful strike" disclaimers or
repeat the receipt's reason and count. No-op, pardon and repeat-barred replies do not
receive instructions to defend a newly issued strike.
When no action was saved, ordinary replies do not volunteer no-change disclaimers
or reassurances that no strikes are being added. Direct questions about current
status and meaningful failures still receive accurate answers.

At most one change attempt is permitted per request. Revision-based writes prevent
lost increments, duplicate message IDs prevent repeat strikes, and pardon message IDs
reject stale strikes from requests predating a pardon. A pardon cannot clear a strike
from its own message or a newer message. Ledger failures refuse service
without claiming a change succeeded. These safeguards do not prove a live model's
judgment of comic misconduct; review that during private rollout testing.

Admin pardons also advance the stale-strike barrier when there are no strikes to clear;
the response reports unchanged service status rather than announcing a new pardon.
If an AI follow-up fails after a saved change, the application still delivers its verdict
with a service-failure response where relevant.

Optional persistence tests use `COK_CUSTOMERS_MONGO_TEST_CONNECTION` pointing to a
disposable local Mongo instance. They create and remove only their own unique database.

## Retained history and commands

The former guild name, past expansions, historical characters, and old anecdotes
remain history. Do not relabel them as Horde or Forever events. Live stored lore
is not migrated by deployment: review current identity and character facts using
the lore editor, preserving historical context. The DM editor cannot rename entries.

`/raids` is de-registered, including removal of an existing guild registration at
startup. Its implementation remains for possible level-60 use. Before restoring
it, confirm the Raid Helper template, raid title, reset day, time, duration,
channels, mentions, and schedule: it still creates MoP Siege of Orgrimmar sign-ups
and clears configured channel messages first. Add it back to `GuildCommands` only
after that review; registration cleanup skips the command when it is enabled.
`/rip` has been removed separately; persisted character deaths are untouched.

## Rollout

1. Obtain officer approval for the guild name and persona, rebase onto current `main`,
   then fast-forward `main` and deploy without a merge commit.
2. Change the existing bot's Discord profile/application presentation to Grub
   and set an approved avatar. Rename the guild server separately if wanted.
   Keep the same application and server IDs; no token replacement is required for
   a cosmetic rebrand. Review any visible webhook/Auth0 application branding manually.
3. Confirm mentions, admin help, the portal title/header/access-denied copy, and a
   private Gazette draft. Do not publish a trial draft without normal approval.
4. Review live permanent lore for current guild/member facts. Do not bulk-rewrite
   historical entries or purge the notebook, generated images, or death records.
5. Check that `/raids` is absent after bot startup, while `/summarise` and `/suggest`
   remain available.

Domains, image URLs, Auth0 tenant, project/namespace names, Docker images, Azure
resources, databases, and the repository name remain unchanged. Gazette issue
numbers and publication history continue under the existing persistent state ID.
