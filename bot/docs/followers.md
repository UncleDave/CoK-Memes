# Followers

ConversationFollower can respond to messages from any human author in text channels. Production uses a single independent roll per eligible message: 1% for members of Discord role `819778028576964618`, and 0.1% for everyone else. There is no cooldown. Development keeps it disabled with a 0% chance and no role override.

`Followers:Conversation:Chance` is the default percentage from 0 to 100 and supports fractional values. Optional `TargetRoleId` selects a role-specific `TargetRoleChance` percentage (default 1%). Role membership is checked from the message author's guild roles without a separate API request. Authors outside the role, or without guild-role metadata, use the default chance. The override replaces the default chance; it is not a second roll or an author restriction. Each successful roll selects one of five personalities with equal probability: Sycophant, Contrarian, Disappointed Teacher, Condescending Teacher, or StonerBro. Personality selection does not add extra trigger rolls or replies. Context includes the triggering message plus up to ten previous messages.

ConversationFollower supplies system-level invocation context: make one spontaneous interjection about the final user message, treat earlier messages only as background, and do not inherit the personality of previous bot replies. This framing is not added to Lorekeeper conversations. The teacher prompts use classroom framing without assuming submitted work or mistakes; Contrarian can react playfully to messages with no claim to dispute.

Triggering messages with no text or supported image content receive an explicit placeholder, so an earlier message cannot become the apparent trigger. Unsupported file contents are not inferred.

ConversationFollower replaces the standalone Sycophant, Teacher, and StonerBro followers; deployments overriding their old configuration must use `Followers:Conversation:Chance` instead. GermanyBis and NoNutNovemberExpert have been retired.

Bot messages are excluded by the message event entry point. Followers do not respond in DMs or other non-text channels, and skip messages mentioning the bot in `Followers:IgnoreBotMentionsInChannelId`. Other followers retain their existing user targeting and probabilities.
