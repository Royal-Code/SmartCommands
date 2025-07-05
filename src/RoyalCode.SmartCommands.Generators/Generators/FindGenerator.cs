using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoyalCode.SmartCommands.Generators.Generators;

public static class FindGenerator
{
    public const string FindAttributeName = "RoyalCode.SmartCommands.Generators.Attributes.MapFindAttribute";
    
    private const string MapFindAttributeName = "MapFindAttribute";
    private const string MapGroupAttributeName = "MapGroup";
    private const string DescriptionAttributeName = "Description";

    public static bool Predicate(SyntaxNode node, CancellationToken _) => node is ClassDeclarationSyntax;

    public static FindInformation Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken __)
    {
        // classe que contém o atributo
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;

        // lê o atributo MapFindAttribute
        if (!classDeclaration.TryGetAttribute(MapFindAttributeName, out AttributeSyntax? mapFindAttribute))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: classDeclaration.Identifier.GetLocation(),
                "The MapFindAttribute is not present in the class");

            return new FindInformation(diagnostic);
        }

        // deve ler os parâmetros do atributo
        var endpointRoutePattern = mapFindAttribute!.ArgumentList?.Arguments[0].Expression.ToString();
        var endpointName = mapFindAttribute.ArgumentList?.Arguments[1].Expression.ToString();

        string? description = null;
        string? groupName = null;

        // tenta obter a descrição também
        if (classDeclaration.TryGetAttribute(DescriptionAttributeName, out AttributeSyntax? descAttr) && descAttr!.ArgumentList?.Arguments.Count is 1)
            description = descAttr.ArgumentList.Arguments[0].Expression.ToString();

        // tenta obter o MapGroup attribute
        if (classDeclaration.TryGetAttribute(MapGroupAttributeName, out AttributeSyntax? groupAttr) && groupAttr!.ArgumentList?.Arguments.Count is 1)
            groupName = groupAttr.ArgumentList.Arguments[0].Expression.ToString().RemoveQuotes();

        // extrai o tipo da entidade buscada
        var syntax = (GenericNameSyntax)mapFindAttribute.Name;
        var entitySyntaxType = syntax.TypeArgumentList.Arguments[0];
        var idSyntaxType = syntax.TypeArgumentList.Arguments[1];

        var entityType = TypeDescriptor.Create(entitySyntaxType, context.SemanticModel);
        var idType = TypeDescriptor.Create(idSyntaxType, context.SemanticModel);
        var modelType = new TypeDescriptor(classDeclaration.Identifier.Text, [classDeclaration.GetNamespace()]);

        return new FindInformation(
            entityType,
            idType,
            modelType,
            endpointRoutePattern ?? string.Empty,
            endpointName ?? string.Empty,
            description,
            groupName);
    }
}
