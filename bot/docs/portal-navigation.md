# Portal navigation

The shared portal header links to Images (`/images`) for all signed-in portal
users and Lore (`/lore`) for users whose `/api/me` response includes the `Admin`
role. The root route loads the current user through the shared API request
helper and forwards route cancellation to that request.

The active section is highlighted and marked with `aria-current="page"`,
including Lore while creating or editing an entry. The header wraps on narrow
screens, while breadcrumbs remain available within lore editing pages.

Navigation visibility is not authorization. Lore APIs retain their existing
server-side admin policy; hiding the link does not grant or revoke access.

`frontend/tests/navigation.test.mjs` covers role-specific links, active sections,
navigation between sections, and the current-user request contract.
