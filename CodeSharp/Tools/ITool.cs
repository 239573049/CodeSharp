using Microsoft.SemanticKernel;

namespace CodeSharp.Tools;

public interface ITool
{
    /// <summary>
    /// 插件名称
    /// </summary>
    public string Name { get; }
}