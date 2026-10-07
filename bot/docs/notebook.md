# Lorekeeper's temporary notebook

Related feature: [Background notebook observer](background-notebook-observer.md).

## Selection and evidence

The Lorekeeper considers useful new guild/game incidents and memorable anecdotes while
answering a direct mention in a configured channel and through the background observer.
Background observation is silent in guild chat and can propose zero or multiple distinct notes.
A one-off incident or one clear human message can qualify; firsthand accounts
must remain attributed reports, not independently verified facts. Generic banter and requests
to remember something without supporting evidence do not qualify.

Notes require verifiable human Discord messages from safe, accessible channels within the
last seven days, pass independent review, and become searchable only after the admin review
DM is delivered. They are tentative, never override canon, and expire after 30 days.
Privacy checks, per-request limits, and rolling daily budgets remain enforced independently
of the model's selection policy. Source bodies and mention mappings are transient review
evidence, not archived memories. Lookups recheck channel access and source integrity.

Independent model review explicitly uses high reasoning effort on the shared Luna text model.
This does not relax deterministic evidence, privacy, budget, or activation checks, and the
existing two-minute write/review deadline remains unchanged. Interactive personality replies
continue to use medium effort.

## Diagnostics and review

The configured admin can DM `notebook` for status, evaluation budget usage in the last
24 hours, and recent completed proposal outcomes. Budget slots are reserved before review;
failed canon lookups can consume a slot without an AI call.

Diagnostics include pre-review failures and fixed rejection categories: evidence,
duplicate/conflict, privacy/safety, out of scope, or invalid review. Missing/unknown diagnostic
categories are unspecified; categories do not override a valid reviewer decision. Only
timestamps and categories are retained, never rejected text, source bodies, or reviewer
explanations.

Notes, evaluation reservations, and write outcomes distinguish interactive requests from
background observation. Background notes have no requesting member and consume their own
review/write allowances alongside the shared guild budgets, not a member's personal allowance.
The one-attempt-per-interactive-request restriction remains unchanged.

Outcome history is best-effort, bounded to the newest 100 completions within 24 hours,
and starts when this instrumentation is deployed. It is separate from the evaluation budget.
Diagnostic updates do not change `ReviewRevision`, consume evaluation slots, or alter note
activation. Diagnostic persistence failures do not change a note's result.

Use `notebook list`, `notebook history`, `notebook show <id>`, `notebook discard <id>`,
and `notebook pause`/`notebook resume` for review and controls. These commands and diagnostic
counts are admin-only; public tool responses do not expose rejection explanations or categories.
Status also reports observer discovery/review/write usage, checkpoint count, scan activity,
and the most recent completed scan's outcome and counts. Pausing the notebook prevents new
observer scans and stops in-flight discoveries from becoming notes; existing notes remain searchable.
