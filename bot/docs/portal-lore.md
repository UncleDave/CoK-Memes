# Portal lore management

The admin-only Lore page lists the existing guild and member lore entries in the
order returned by the API. Search lore by name filters that catalog immediately,
case-insensitively, using a trimmed substring. It does not make additional API
requests or change stored entries. Clear restores the full list and focuses the
search field.

The page announces the matching count or a no-matches message. An empty catalog
has a distinct empty state and retains the guild/member creation actions. Edit
links encode each complete entry name as a single URL component.

`frontend/tests/lore-list.test.mjs` covers filtering, ordering, empty catalogs,
accessible search controls, and encoded edit links. Server-side lore APIs retain
their admin authorization policy regardless of link visibility.

## Unsaved edits

Guild and member forms compare their current submitted text values with the
values present when the form opened. Text edits and adding/removing alias or role
rows mark the form dirty; restoring the original values clears that state.
Opening another entry initializes a fresh form and baseline.

Leaving a dirty form through Cancel, breadcrumbs, section navigation, or browser
Back/Forward opens a dialog with Keep editing and Discard changes. Changing the
creation type also counts as leaving; hash-only navigation does not. Refresh,
closing the tab, and other document navigation use the browser's own unsaved-change
warning, subject to its user-interaction requirements.

Save and Delete submissions to the current route are not blocked. Successful
mutations follow their normal redirect; failed mutations retain the draft and
its navigation protection. A warning does not save a draft, and neither form
values nor baselines are persisted in browser storage.

`frontend/tests/unsaved-lore.test.mjs` covers snapshots, repeated values, restoration,
and blocked/confirmed navigation contracts. Browser checks are also needed for
dynamic rows, dialog focus/actions, native unload warnings, and mutation redirects.
