# Followers

ConversationFollower can respond to messages from any human author in text channels. Production uses a single 0.1% independent chance per eligible message (about one trigger per 1,000 messages on average), with no cooldown. Development keeps it disabled with a 0% chance.

`Followers:Conversation:Chance` is a percentage from 0 to 100 and supports fractional values. There is no target user setting. Each successful roll selects one of five personalities with equal probability: Sycophant, Contrarian, Disappointed Teacher, Condescending Teacher, or StonerBro. Personality selection does not add extra trigger rolls or replies. Context includes the triggering message plus up to ten previous messages.

ConversationFollower supplies system-level invocation context: make one spontaneous interjection about the final user message, treat earlier messages only as background, and do not inherit the personality of previous bot replies. This framing is not added to Lorekeeper conversations. The teacher prompts use classroom framing without assuming submitted work or mistakes; Contrarian can react playfully to messages with no claim to dispute.

Triggering messages with no text or supported image content receive an explicit placeholder, so an earlier message cannot become the apparent trigger. Unsupported file contents are not inferred.

ConversationFollower replaces the standalone Sycophant, Teacher, and StonerBro followers; deployments overriding their old configuration must use `Followers:Conversation:Chance` instead. GermanyBis and NoNutNovemberExpert have been retired.

Bot messages are excluded by the message event entry point. Followers do not respond in DMs or other non-text channels, and skip messages mentioning the bot in `Followers:IgnoreBotMentionsInChannelId`. Other followers retain their existing user targeting and probabilities.
