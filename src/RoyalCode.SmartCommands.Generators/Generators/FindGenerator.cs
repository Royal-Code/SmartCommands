using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoyalCode.SmartCommands.Generators.Generators;

public static class FindGenerator
{
    public const string FindAttributeName = "RoyalCode.SmartCommands.MapFindAttribute";
    
    private const string MapFindAttributeName = "MapFind";
    private const string EntityReferenceAttributeName = "EntityReference";
    private const string MapGroupAttributeName = "MapGroup";
    private const string WithDescriptionAttributeName = "WithDescription";
    private const string WithSummaryAttributeName = "WithSummary";
    private const string WithAuthorizationAttributeName = "WithAuthorization";
    private const string WithPolicyAttributeName = "WithPolicy";

    public static bool Predicate(SyntaxNode node, CancellationToken _) => node is ClassDeclarationSyntax;

    public static FindInformation Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
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

        // lê o atributo EntityReferenceAttribute
        if (!classDeclaration.TryGetAttribute(EntityReferenceAttributeName, out AttributeSyntax? entityReferenceAttribute))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: classDeclaration.Identifier.GetLocation(),
                "The EntityReferenceAttribute is not present in the class");

            return new FindInformation(diagnostic);
        }

        // deve ler os parâmetros do atributo
        var endpointRoutePattern = mapFindAttribute!.ArgumentList?.Arguments[0].Expression.ToString();
        var endpointName = mapFindAttribute.ArgumentList?.Arguments[1].Expression.ToString();

        string? description = null;
        string? summary = null;
        string[]? authorizationPolicies = null;
        string? groupName = null;

        // tenta obter a description
        if (classDeclaration.TryGetAttribute(WithDescriptionAttributeName, out AttributeSyntax? descAttr) && descAttr!.ArgumentList?.Arguments.Count is 1)
            description = descAttr.ArgumentList.Arguments[0].Expression.ToString();

        // tenta obter o summary
        if (classDeclaration.TryGetAttribute(WithSummaryAttributeName, out AttributeSyntax? displayNameAttr) && displayNameAttr!.ArgumentList?.Arguments.Count is 1)
            summary = displayNameAttr.ArgumentList.Arguments[0].Expression.ToString();

        // tenta obter o MapGroup attribute
        if (classDeclaration.TryGetAttribute(MapGroupAttributeName, out AttributeSyntax? groupAttr) && groupAttr!.ArgumentList?.Arguments.Count is 1)
            groupName = groupAttr.ArgumentList.Arguments[0].Expression.ToString().RemoveQuotes();

        // tenta obter o authorization
        if (classDeclaration.TryGetAttribute(WithAuthorizationAttributeName, out AttributeSyntax? authAttr))
            authorizationPolicies = [];

        // se tiver o attribute WithPolicy, deve obter o(s) nome(s) da(s) política(s)
        if (classDeclaration.TryGetAttribute(WithPolicyAttributeName, out AttributeSyntax? policyAttr))
        {
            var arguments = policyAttr!.ArgumentList?.Arguments;
            if (arguments is not null && arguments.Value.Count > 0)
            {
                // obtém os nomes das políticas
                authorizationPolicies = arguments.Value.Select(a => a.Expression.ToString()).ToArray();
            }
            else
            {
                authorizationPolicies ??= [];
            }
        }

        // extrai o tipo da entidade buscada
        var syntax = (GenericNameSyntax)entityReferenceAttribute!.Name;
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
            summary,
            authorizationPolicies,
            groupName);
    }
}
