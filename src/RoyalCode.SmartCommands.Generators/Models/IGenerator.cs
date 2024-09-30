using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Generators.Models;

public interface IGenerator
{
    public bool HasErrors(SourceProductionContext spc, SyntaxToken mainToken);
    
    public void Generate(SourceProductionContext spc);
}
