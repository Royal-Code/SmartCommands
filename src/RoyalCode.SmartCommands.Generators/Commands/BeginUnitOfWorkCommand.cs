using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

public class BeginUnitOfWorkCommand : GeneratorNode
{
    private readonly string varName;

    public BeginUnitOfWorkCommand(string varName)
    {
        this.varName = varName;
    }

    public override void Write(StringBuilder sb, int indent = 0)
    {
        sb.Indent(indent).Append("await this.").Append(varName).AppendLine(".BeginAsync(ct);");
        sb.AppendLine();
    }
}
