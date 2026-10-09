# Generated images in Lorekeeper replies

The `generate_image` tool generates, uploads, and saves each successful image before
returning its public URL to the model. The model can work that URL into its natural
reply as before.

The shared personality prompt requests Discord-compatible Markdown: labelled links
(`[description](url)`) and bare image URLs can both produce image previews.
Discord does not support Markdown's inline image syntax
(`![description](url)`), which leaves a stray `!` before the clickable label.
After citation and emoji processing, `PersonalityBase` converts HTTP(S) Markdown
image links into labelled links without changing the labels or URLs. This also
applies to image-search replies. Escaped syntax and backtick-delimited code examples
are left untouched; this is not a general Markdown converter.

`PersonalityBase` also tracks successful image URLs through a request-local tool
callback. After response formatting, it appends any generated URL missing
from the reply as a bare link, so Discord can preview the image. An empty model
reply becomes the image link alone. Existing links are not repeated; failed or
denied generations add nothing, and images from earlier requests are not reused.
The collection is thread-safe because tools can run concurrently.

Each chat invocation sends at most one generation confirmation, even when the model
calls `generate_image` repeatedly or requests several images in one tool batch.
`PersonalityTools.RequestTools` owns the thread-safe, request-local confirmation gate;
denied calls do not consume it, and a new invocation starts with a fresh gate.
The confirmation is still sent only after allowance and in-progress checks succeed.
The gate records a send attempt, not confirmed delivery: if Discord throws, that
image follows the existing failure path and later tool calls do not retry the
confirmation. This avoids duplicate notifications after ambiguous delivery failures.
For limited allowances, its count describes the allowance after the first image,
not the projected balance after the entire batch. The confirmation prefix is kept
because Discord history filtering uses it to exclude these status messages.

This does not change generation quotas, the per-user in-progress lock, storage, or
image search. The missing-link safety net applies when the chat invocation completes;
chat or Discord send failures are still handled by the existing error paths, and
Discord image previews still depend on permissions, settings, and URL availability.

`GeneratedImageReplyTests` exercises function invocation with controlled OpenAI
and blob-storage HTTP responses, including missing/existing links, generated/searched
image-link normalization, empty replies, request isolation, allowance denials, and
generation failures. It also checks 19-image requests across tool rounds and within
one batch, confirmation isolation between requests, per-image allowances, repeated
generation failures, and failed confirmation sends. `PersonalityBaseTests` covers
response formatting and preserving ordinary text, links, and code examples.
