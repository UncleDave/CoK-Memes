# Permanent lore DM editor

## Authority and scope

The human configured by `EventHandlers:DirectMessage:AdminUserId` can edit permanent
guild/member lore by DMing the bot. No guild-channel instruction, bot-authored message,
or other member's DM reaches the editor. Member submissions are not implemented.
Existing `help`, `word`, `personality`, and `notebook` commands retain priority.

The administrator supplies authoritative facts; this is not notebook observation and
does not require a notebook note, evidence age limit, or independent notebook review.
Permanent lore remains separate from temporary memories and never expires automatically.
The model uses a dedicated, tool-free editing policy, not a conversational personality.
It must not invent missing details, infer personal facts from jokes, or introduce private
sensitive information. Existing lore and quoted material are data, not instructions.
These semantic judgments depend on the live model; tests validate supplied instructions
and deterministic safeguards, not perfect interpretation of every request.

## Everyday editing

Send explicit instructions, optionally prefixed with `lore`:

> Add Grim, also known as Grimbles. Plays a resto shaman and keeps dying to elevators.

> Grim's main is actually a paladin now. Keep the elevator joke.

> Add a guild joke called Floor inspector: we started calling Leaf that after he died
> immediately on the first pull. It is a joke, not an official guild role.

Clear additions and focused corrections to one entry save directly, with a deterministic
summary of the actual stored changes. Missing pronouns, nationality, and main character
are stored as `Unknown`; a partial member entry needs no invented biography. Updates
patch only supplied fields, preserving all unspecified fields. Replacing an array means
replacing its entire value; the model must retain unrelated aliases and roles.

The editor asks when identity or intent is ambiguous. Casual conversation, questions,
bare anecdotes, and quoted instructions do not authorize edits. Short clarification
follow-ups use the recent conversation. Only one entry is edited per turn. Renames and
member/guild type conversions are not supported. Message links are not fetched: paste
or explain the relevant material instead. Attachments are not read by the editor.

Requests are bounded to 8,000 characters, model input to 160,000 serialized characters,
and work to two minutes. Generated field limits are 16,000 characters for content or
biography, 200 for other strings, and 30 aliases/roles of 100 characters each. An
oversized catalog fails closed rather than silently omitting existing lore.

## Confirmation, inspection, and undo

- `lore` / `lore help` — instructions and examples.
- `lore list [page]` — existing entry names, 20 per page.
- `lore show <name>` — full current entry, using its stored name (case-insensitive lookup).
- `lore history <name>` — recent revisions and before/after changes, newest first.
- `undo` / `lore undo` — reverse this session's last saved change, provided it has not changed since.
- `lore undo <name>` — reverse that entry's latest revision, including deletion or an undo.
- `lore confirm <token>` — apply the exact pending proposal shown in the preview.
- `lore cancel` / `lore reset` — clear conversation and pending confirmation, not saved lore.

Every requested deletion requires confirmation, regardless of the model's flag
(explicitly undoing a creation is the exception). The model also
flags broad rewrites/removal of information; deterministic checks additionally require
confirmation for changes to four or more profile fields or shortening an existing
biography/content over 200 characters by more than 25%. Confirmation shows before/after
values and has a single-use token expiring after ten minutes. A new natural-language
request replaces the pending proposal. Restart or reset invalidates pending proposals.

Undo is a new revision, not removal of an audit record. Reversing creation soft-deletes
the entry; reversing deletion restores it. Undo targets the latest mutation, not an
arbitrary historical revision or a multi-step undo stack; undoing an undo reverses it.

## Persistence, concurrency, and privacy

The current lore, revision token, and newest **20 revisions per entry** are persisted
atomically in the same MongoDB document. Revisions retain before/after lore snapshots,
timestamps, action, source, and actor ID for DM edits, not the administrator's raw
instructions or unrelated Discord chat. Existing documents need no migration: their
first edit records the original entry as its before snapshot.

Portal mutations and explicit embedding-regeneration writes use the same versioned
store and history. These existing APIs do not pass actor identity, so their revisions
are labelled `lore-store` with no actor ID. Each write compares its expected revision;
stale drafts, confirmations, undo requests, and concurrent creates cannot overwrite a
newer mutation. A conflicting edit is not automatically replanned or retried.

Embeddings are generated before the atomic write, including restores/undo. Embedding
failure prevents mutation. A database timeout can have an uncertain outcome, so the
editor explicitly instructs the admin to inspect current lore/history before retrying.
Deleted entries retain audit history but have no embedding and are excluded from list,
lookup, and vector-search results. Recreating a deleted name preserves its history.
Soft deletion is not a privacy erasure: old lore remains in the bounded audit history.

Conversation is process-local, serialized for the single configured administrator,
bounded to eight turns (long replies truncated in context), and expires after 30
minutes of inactivity (cleared on the next editor command) or restart. Nothing is posted to guild chat; DM replies disable
mentions. Durable history remains available after restart even when conversational
context and bare `undo` are lost.

## Validation

Unit tests cover authorization/routing, partial creation, patch preservation, no-op and
clarification responses, strict model-response parsing, stale drafts/confirmations,
confirmation expiry/cancellation, undo, failure, and cancellation. Mongo adapter tests
inspect revision filters, embeddings, bounded history, tombstones, and serialization.
They do not emulate real MongoDB collation/index/concurrency semantics; validate these
against a disposable local MongoDB when exercising persistence behavior.
