using System.Text;

namespace Coreum.NewCommands.Generators.Models.Commands;

public class DeclareNotFoundProblemsCommand : GeneratorNode
{
    public override void Write(StringBuilder sb, int ident = 0)
    {
        sb.Ident(ident);

        sb.AppendLine("Problems? notFoundProblems;");

        sb.AppendLine();
    }
}
