using Microsoft.CodeAnalysis;
using System.Text;

namespace Coreum.NewCommands.Generators.Models;

public class GeneratorNodeList : GeneratorNode
{
    private List<GeneratorNode>? nodes;

    public void Add(GeneratorNode generator)
    {
        nodes ??= [];
        nodes.Add(generator);
    }

    public IEnumerable<T> Enumerate<T>() => nodes?.OfType<T>() ?? [];

    public override void Write(StringBuilder sb, int ident = 0)
    {
        if (nodes is null)
            return;

        foreach (var node in nodes)
        {
            node.Write(sb, ident);
        }
    }
}
