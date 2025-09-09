using Microsoft.SemanticKernel;

namespace CodeSharp.Infrastructure;

public class DefaultFunctionInvocationFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        await next(context);
    }
}