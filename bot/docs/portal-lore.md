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
