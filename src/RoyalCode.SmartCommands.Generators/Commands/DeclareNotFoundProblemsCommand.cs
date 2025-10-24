using System.Text;

namespace RoyalCode.SmartCommands.Generators.Commands;

public class DeclareNotFoundProblemsCommand : GeneratorNode
{
    public override void Write(StringBuilder sb, int indent = 0)
    {
        sb.Indent(indent);

        sb.AppendLine("Problem? notFoundProblem;");

        sb.AppendLine();
    }
}
