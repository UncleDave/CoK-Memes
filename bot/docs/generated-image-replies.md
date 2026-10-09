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

This safety net does not change generation quotas, storage, image search, or the
initial generation confirmation. It applies when the chat invocation completes;
chat or Discord send failures are still handled by the existing error paths, and
Discord image previews still depend on permissions, settings, and URL availability.

`GeneratedImageReplyTests` exercises function invocation with controlled OpenAI
and blob-storage HTTP responses, including missing/existing links, generated/searched
image-link normalization, empty replies, request isolation, allowance denials, and
generation failures. `PersonalityBaseTests` covers response formatting and preserving
ordinary text, links, and code examples.
