using CodeSharp.Infrastructure;
using Microsoft.SemanticKernel;

namespace CodeSharp;

public class KernelFactory
{
    public static Kernel CreateKernel(string modelId,
        Action<IKernelBuilder>? kernelBuilderAction,
        Action<Kernel>? kernelAction)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        kernelBuilderAction?.Invoke(kernelBuilder);

        kernelBuilder.AddOpenAIChatCompletion(modelId, new Uri("https://api.token-ai.cn/v1"),
            ConfigService.GetConfig().ApiKey, httpClient: new HttpClient(new AIHttpClientHandler()));

        var kernel = kernelBuilder.Build();

        kernelAction?.Invoke(kernel);

        kernel.FunctionInvocationFilters.Add(new DefaultFunctionInvocationFilter());

        return kernel;
    }
}