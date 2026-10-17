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

## Loading and recovery

Initial loading retains the portal header and displays a page-shaped skeleton
with an announced loading status rather than a blank viewport. Admin navigation
is not guessed while the current-user request is pending. Subsequent route loads keep the
current page and navigation visible with a progress indicator. Child-route failures
keep the shared header available and offer Retry without discarding successfully
loaded gallery pages. An initial current-user failure has the same recovery UI,
but cannot display role-based navigation until the user request succeeds.

Expired sessions offer a reload to sign in; forbidden and missing pages have
specific messages. Unexpected failures use generic copy rather than exposing
exception details. Unknown URLs use the missing-page state. An empty lore catalog
shows an explicit empty state alongside its creation actions.
