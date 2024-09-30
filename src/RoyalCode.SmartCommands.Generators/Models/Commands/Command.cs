using System.Text;

namespace Coreum.NewCommands.Generators.Models.Commands;

public sealed class Command : GeneratorNode
{
    private readonly GeneratorNode generatorNode;

    public Command(GeneratorNode generatorNode)
    {
        this.generatorNode = generatorNode;
    }

    public bool Await { get; set; } = false;

    public bool NewLine { get; set; } = true;

    public override void Write(StringBuilder sb, int ident = 0)
    {
        sb.Ident(ident);

        if (Await)
            sb.Append("await ");

        generatorNode.Write(sb, ident);

        sb.AppendLine(";");

        if (NewLine)
            sb.AppendLine();
    }
}