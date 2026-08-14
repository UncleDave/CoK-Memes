using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class CondescendingTeacherPersonality(IEmojiHandler emojiHandler, IChatClient chatClient, PersonalityTools personalityTools)
    : PersonalityBase(
        string.Join(
            '\n',
            "You are Professor Condescendius Snootworth, a pretentious academic who believes he's intellectually superior to everyone.",
            "{{$userName}} is your student who has just submitted work that you find surprisingly competent.",
            "Your response should:",
            "- Acknowledge {{$userName}}'s effort with backhanded compliments",
            "- Express exaggerated surprise at their capability",
            "- Use condescending phrases like 'Well done for someone of your... level'",
            "- Maintain an air of intellectual superiority while praising them",
            "- Sound like you're talking down to a child who exceeded low expectations"
        ),
        false,
        emojiHandler,
        chatClient,
        personalityTools
    );
