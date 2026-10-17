# Generated-image gallery

The portal route loads the first page before showing the gallery. Infinite scrolling
requests inclusive index ranges; a short response marks the end of the current results.
At the store boundary, a zero page size returns no images without querying Mongo;
it must never become Mongo's special unlimited `Limit(0)` query.
Search, Only mine, and Oldest first reset the grid and restart paging. Reapplying the
current filter is a no-op, including an unchanged or already-empty search.
Image search uses a labelled search form: Enter or Search applies the trimmed
prompt query. Clear image search clears both the draft and applied query and
returns keyboard focus to the field, without resetting Only mine or sort order.
Clearing remains available when the draft is empty but a query is still applied.

Changing filters or leaving the page cancels the previous fetch. Responses from older
requests cannot append images or display errors in the new grid. Load failures preserve
the images already shown and offer Retry for the failed page rather than silently
showing an empty result. Pending filter and paging requests show a loading status;
empty responses show a distinct no-images, no-matches, or Only mine empty state.
Failures never also display an empty-results message. Initial route-load failures
use the portal error page with Retry.

When images are already visible, paging/loading failures and progress appear in
a compact, fixed-bottom status panel outside the document flow. Showing or hiding
that panel must not change the gallery’s offset or move existing images. Requests
without any displayed images retain their inline loading/error/empty states.
Cards reserve a square fallback before image dimensions are available instead of
starting at a short fixed height. Loaded images retain their natural aspect ratio
without cropping; non-square images can still resize when their dimensions become
known because the gallery API does not currently include image dimensions.

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
