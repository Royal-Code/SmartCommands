using System.Text;

namespace Coreum.NewCommands.Generators.Models.Commands;

public class BeginUnitOfWorkCommand : GeneratorNode
{
    private readonly string varName;

    public BeginUnitOfWorkCommand(string varName)
    {
        this.varName = varName;
    }

    public override void Write(StringBuilder sb, int ident = 0)
    {
        sb.Ident(ident).Append("await this.").Append(varName).AppendLine(".BeginAsync(ct);");
        sb.AppendLine();
    }
}
