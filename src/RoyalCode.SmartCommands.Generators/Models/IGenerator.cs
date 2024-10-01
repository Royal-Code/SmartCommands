using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Models;

public interface IGenerator
{
    public bool HasErrors(SourceProductionContext spc, SyntaxToken mainToken);
    
    public void Generate(SourceProductionContext spc);
}
