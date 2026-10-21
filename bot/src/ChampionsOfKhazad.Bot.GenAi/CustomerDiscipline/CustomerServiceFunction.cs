using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal sealed class CustomerServiceFunction(AIFunction innerFunction, CustomerDisciplineRequest request) : DelegatingAIFunction(innerFunction)
{
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await request.CanServeAsync(cancellationToken))
            return "Normal service is unavailable. Do not perform or answer this request.";
        return await base.InvokeCoreAsync(arguments, cancellationToken);
    }
}
