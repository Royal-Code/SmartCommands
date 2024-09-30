using System.Text;

namespace Coreum.NewCommands.Generators.Models;

public abstract class GeneratorNode
{
    public abstract void Write(StringBuilder sb, int ident = 0);
}
