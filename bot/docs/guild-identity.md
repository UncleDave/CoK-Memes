# Guild identity and Grub

Crossroads Mega Chippy is the guild's new identity, following Champions of Khazad.
The guild is moving to World of Warcraft: Forever, playing Horde on a PvE realm.
The rebrand is prepared on a branch for officer review; deploying this version is
the cutover, not a claim that the tentative name has already been adopted in Discord.
Keep the branch unmerged until the rebrand is approved.

## Grub

The main bot persona is **Grub**, full name **Grub Greasefist**: a broad
green orc, chip-shop proprietor, and keeper of the guild's grease-stained ledger.
Discord display name and model speaker name: **Grub**.
He replaces Hammerstone as the active character, without rewriting old conversations
or inventing an in-world handover as established guild history.

Grub runs the shop with the seriousness of a military quartermaster. He is proud
of the establishment, suspicious of customers' competence, and quietly dependable.
His voice is a blunt, dry, world-weary British chip-shop owner, not a generic
shouting orc or forced broken speech. Prefer specific guild observations and petty
commercial grievances to repeated catchphrases or food puns. Answer the question;
the character should not get in the way of useful assistance.

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
for direct cheek, insults aimed at him, or playful shop misconduct. Each saved
strike has a short reason. Ordinary questions, thanks, disagreements, poor gameplay,
quoted text, other members' conduct, sensitive topics, and asking him to stop the act
are not grounds for a strike. Temperament changes his voice, not this threshold.

Three strikes bars the customer from Grub's normal service until pardoned. They can
still address him to apologise or appeal; Grub may clear all their strikes if persuaded,
including before the third strike. A successful appeal from a barred customer confirms
the pardon without performing the request embedded in that appeal; ask again for service.
Grub must not demand real money, favours, humiliation, or personal information.
The configured admin can inspect or reset a record by DM:

- `customers` — help.
- `customers show <user-id>` — current strikes and reasons.
- `customers pardon <user-id>` — clear strikes and lift the bar.

The `grubCustomers` Mongo collection stores one record per guild and Discord user.
Strikes survive restarts and conversation resets; they do not expire automatically.
This is shop roleplay with bot-service enforcement, never a Discord ban, timeout,
guild disciplinary rule, or established lore. Other followers and commands are unaffected.
Do not copy the ledger into lore or the temporary notebook.

A separate, low-reasoning AI decision receives the conversation but only strike/pardon
tools bound to the current author. Normal tools are exposed only after rechecking the
saved record. Barred requests receive only the pardon tool and an application-generated
refusal, so a model cannot serve them just by ignoring the prompt. The application
delivers saved verdicts; model prose is not authority for a strike or pardon.
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

1. Obtain officer approval for the guild name and persona, then merge and deploy.
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
