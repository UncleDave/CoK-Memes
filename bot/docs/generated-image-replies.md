# Generated images in Lorekeeper replies

The `generate_image` tool generates, uploads, and saves each successful image before
returning its public URL to the model. The model can work that URL into its natural
reply as before.

`PersonalityBase` also tracks successful image URLs through a request-local tool
callback. After citation and emoji processing, it appends any generated URL missing
from the reply as a bare link, so Discord can preview the image. An empty model
reply becomes the image link alone. Existing links are not repeated; failed or
denied generations add nothing, and images from earlier requests are not reused.
The collection is thread-safe because tools can run concurrently.

This safety net does not change generation quotas, storage, image search, or the
initial generation confirmation. It applies when the chat invocation completes;
chat or Discord send failures are still handled by the existing error paths, and
Discord image previews still depend on permissions and URL availability.

`GeneratedImageReplyTests` exercises function invocation with controlled OpenAI
and blob-storage HTTP responses, including missing/existing links, empty replies,
request isolation, allowance denials, and generation failures.
