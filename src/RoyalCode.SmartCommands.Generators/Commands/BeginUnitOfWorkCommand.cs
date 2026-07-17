using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

internal class BeginUnitOfWorkCommand : GeneratorNode
{
    private readonly string varName;
    private readonly bool requireTransaction;

    public BeginUnitOfWorkCommand(string varName, bool requireTransaction)
    {
        this.varName = varName;
        this.requireTransaction = requireTransaction;
    }

    public override void Write(StringBuilder sb, int indent = 0)
    {
        sb.Indent(indent).Append("await this.").Append(varName)
            .Append(".BeginAsync(requireTransaction: ").Append(requireTransaction ? "true" : "false")
            .AppendLine(", ct);");
        sb.AppendLine();
    }
}
