# Generated-image gallery

The portal route loads the first page before showing the gallery. Infinite scrolling
requests inclusive index ranges; a short response marks the end of the current results.
At the store boundary, a zero page size returns no images without querying Mongo;
it must never become Mongo's special unlimited `Limit(0)` query.
Search, Only mine, and Oldest first reset the grid and restart paging. Reapplying the
current filter is a no-op, including an unchanged or already-empty search.

Changing filters or leaving the page cancels the previous fetch. Responses from older
requests cannot append images or display errors in the new grid. Load failures preserve
the images already shown and offer Retry for the failed page rather than silently
showing an empty result. Initial route-load failures use the router's error handling.

`frontend/tests/generated-images.test.mjs` covers filter/paging state, stale responses,
retry, end-of-results handling, and request encoding/cancellation contracts.
`frontend/tests/joy-controls.test.mjs` renders selected filters and multi-input icons
inside the real Joy theme. `MongoGeneratedImageStoreTests` checks zero-size short-circuiting
and the nonzero database limit/offset contract with a narrow test double. Live Discord
authentication, profile resolution, Mongo search ordering, and external image delivery
still require an appropriately configured portal.

Portal controls use Joy's `SvgIcon` so their icons share the application's Joy
theme. Material icons from a different MUI major must not be mixed directly into
these controls: styling a selected filter can otherwise crash at runtime even
when TypeScript and the production build pass.
