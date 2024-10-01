using System.Text;

namespace RoyalCode.SmartCommands.Generators.Models.Commands;

public class DeclareNotFoundProblemsCommand : GeneratorNode
{
    public override void Write(StringBuilder sb, int ident = 0)
    {
        sb.Ident(ident);

        sb.AppendLine("Problems? notFoundProblems;");

        sb.AppendLine();
    }
}
