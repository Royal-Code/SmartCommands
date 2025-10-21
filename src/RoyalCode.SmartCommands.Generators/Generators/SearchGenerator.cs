using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal static class SearchGenerator
{
    public const string SearchAttributeName = "RoyalCode.SmartCommands.MapSearchAttribute";

    private const string MapSearchAttributeName = "MapSearch";
    private const string SearchReferenceAttributeAttributeName = "SearchReference";

    private const string MapGroupAttributeName = "MapGroup";
    private const string WithDescriptionAttributeName = "WithDescription";
    private const string WithSummaryAttributeName = "WithSummary";
    private const string WithAuthorizationAttributeName = "WithAuthorization";
    private const string WithPolicyAttributeName = "WithPolicy";

    public static bool Predicate(SyntaxNode node, CancellationToken _) => node is ClassDeclarationSyntax;

    public static SearchInformation Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken _)
    {
        // classe que contém o atributo
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;

        // lê o atributo MapSearch
        if (!classDeclaration.TryGetAttribute(MapSearchAttributeName, out AttributeSyntax? mapSearchAttribute))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: classDeclaration.Identifier.GetLocation(),
                "The MapSearchAttribute is not present in the class");

            return new SearchInformation(diagnostic);
        }

        // lê o atributo SearchReferenceAttribute
        if (!classDeclaration.TryGetAttribute(SearchReferenceAttributeAttributeName, out AttributeSyntax? searchReferenceAttribute))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: classDeclaration.Identifier.GetLocation(),
                "The SearchReferenceAttribute is not present in the class");

            return new SearchInformation(diagnostic);
        }

        // deve ler os parâmetros do atributo
        var endpointRoutePattern = mapSearchAttribute!.ArgumentList?.Arguments[0].Expression.ToString();
        var endpointName = mapSearchAttribute.ArgumentList?.Arguments[1].Expression.ToString();

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
        {
            groupName = groupAttr.ArgumentList.Arguments[0].Expression.ToString().RemoveQuotes();
        }
        else
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: classDeclaration.Identifier.GetLocation(),
                "The MapGroupAttribute is not present in the class");
        }

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
        var syntax = (GenericNameSyntax)searchReferenceAttribute!.Name;
        var entitySyntaxType = syntax.TypeArgumentList.Arguments[0];

        TypeSyntax? selectSyntaxType = null;
        if (syntax.TypeArgumentList.Arguments.Count is 2)
            selectSyntaxType = syntax.TypeArgumentList.Arguments[1];

        var entityType = TypeDescriptor.Create(entitySyntaxType, context.SemanticModel);
        var selectType = selectSyntaxType is null ? null : TypeDescriptor.Create(selectSyntaxType, context.SemanticModel);
        var filterType = new TypeDescriptor(classDeclaration.Identifier.Text, [classDeclaration.GetNamespace()]);

        throw new NotImplementedException();
    }
}
