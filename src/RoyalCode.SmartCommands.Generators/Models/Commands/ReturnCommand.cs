using Microsoft.CodeAnalysis;
using System.Text;

namespace Coreum.NewCommands.Generators.Models.Commands;

public class ReturnCommand : GeneratorNode
{
    private readonly ValueNode valueNode;

    public ReturnCommand(ValueNode valueNode)
    {
        this.valueNode = valueNode ?? throw new ArgumentNullException(nameof(valueNode));
    }

    public override void Write(StringBuilder sb, int ident = 0)
    {
        sb.Ident(ident).Append("return ").Append(valueNode.GetValue(ident)).AppendLine(";");
    }
}
