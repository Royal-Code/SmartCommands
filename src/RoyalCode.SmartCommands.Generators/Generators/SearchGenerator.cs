using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.SmartCommands.Generators.Models;

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
    private const string WithFilterAttributeName = "WithFilter";
    private const string WithParameterAttributeName = "WithParameter";

    public static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return node is ClassDeclarationSyntax;
    }

    public static GenerationCandidate<SearchModel> Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var information = TransformWorking(context, cancellationToken);
        var diagnostics = PipelineDiagnostic.Snapshot(information.Diagnostics);
        return diagnostics.IsEmpty
            ? GenerationCandidate<SearchModel>.Valid(SearchModel.Create(information))
            : GenerationCandidate<SearchModel>.Invalid(diagnostics);
    }

    private static SearchInformation TransformWorking(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        // classe que contém o atributo
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;

        // lê o atributo MapSearch
        if (!classDeclaration.TryGetAttribute(MapSearchAttributeName, out AttributeSyntax? mapSearchAttribute))
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
                location: classDeclaration.Identifier.GetLocation(),
                "The MapSearchAttribute is not present in the class");

            return new SearchInformation(diagnostic);
        }

        // lê o atributo SearchReferenceAttribute
        if (!classDeclaration.TryGetAttribute(SearchReferenceAttributeAttributeName, out AttributeSyntax? searchReferenceAttribute))
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
                location: classDeclaration.Identifier.GetLocation(),
                "The SearchReferenceAttribute is not present in the class");

            return new SearchInformation(diagnostic);
        }

        if (mapSearchAttribute!.ArgumentList?.Arguments is not { Count: 2 } mapArguments)
        {
            var diagnostic = DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapSearchUsage,
                mapSearchAttribute.GetLocation(),
                "MapSearchAttribute requires a route pattern and an endpoint name");
            return new SearchInformation(diagnostic);
        }

        var endpointRoutePattern = mapArguments[0].Expression.ToString();
        var endpointName = mapArguments[1].Expression.ToString();

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
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
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

        if (searchReferenceAttribute!.Name is not GenericNameSyntax syntax ||
            syntax.TypeArgumentList.Arguments.Count is < 1 or > 2)
        {
            var diagnostic = DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapSearchUsage,
                searchReferenceAttribute.GetLocation(),
                "SearchReferenceAttribute requires one or two type arguments");
            return new SearchInformation(diagnostic);
        }

        var entitySyntaxType = syntax.TypeArgumentList.Arguments[0];

        TypeSyntax? selectSyntaxType = null;
        if (syntax.TypeArgumentList.Arguments.Count is 2)
            selectSyntaxType = syntax.TypeArgumentList.Arguments[1];

        var entityType = TypeDescriptor.Create(entitySyntaxType, context.SemanticModel);
        var selectType = selectSyntaxType is null ? null : TypeDescriptor.Create(selectSyntaxType, context.SemanticModel);
        var filterType = TypeDescriptor.Create((ITypeSymbol)context.TargetSymbol);

        // obtém os métodos da classe, em busca de métodos anotados com WithFilter
        if (!TryCreateFilter(
            classDeclaration,
            context.SemanticModel,
            cancellationToken,
            out SearchFilterInformation? searchFilterInformation,
            out List<DiagnosticInfo>? errors))
        {
            return new SearchInformation(errors!);
        }

        return new SearchInformation(
            entityType,
            selectType,
            filterType,
            endpointRoutePattern ?? string.Empty,
            endpointName ?? string.Empty,
            description,
            summary,
            authorizationPolicies,
            groupName!,
            searchFilterInformation)
        {
            EndpointNameLocation = mapArguments[1].GetLocation(),
        };
    }


    public static bool TryCreateFilter(
        ClassDeclarationSyntax classDeclarationSyntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SearchFilterInformation? searchFilterInformation,
        out List<DiagnosticInfo>? diagnostics)
    {
        diagnostics = null;
        searchFilterInformation = null;

        var methods = classDeclarationSyntax.Members.OfType<MethodDeclarationSyntax>()
            .Select(m =>
            {
                m.TryGetAttribute(WithFilterAttributeName, out AttributeSyntax? attr);
                return (m, attr);
            })
            .Where(m => m.attr is not null)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        if (methods.Count > 1)
        {
            diagnostics = methods.Select(m =>
            {
                return DiagnosticInfo.Create(CmdDiagnostics.InvalidWithFilterUsage,
                    location: m.m.Identifier.GetLocation(),
                    "Only one method with WithFilterAttribute is allowed in the search filter class");
            }).ToList();

            return false;
        }
        else if (methods.Count is 1)
        {
            var methodWithFilter = methods[0].m;

            var methodName = methodWithFilter.Identifier.Text;
            var isAsync = methodWithFilter.Modifiers.Any(SyntaxKind.AsyncKeyword);
            var parameters = CreateSearchFilterParameters(methodWithFilter, semanticModel);

            searchFilterInformation = new SearchFilterInformation(methodName, isAsync, parameters);
        }

        return true;
    }

    private static SearchFilterParameterInformation[] CreateSearchFilterParameters(
        MethodDeclarationSyntax method, SemanticModel semanticModel)
    {
        var parameters = method.ParameterList.Parameters
            .Select(p =>
            {
                var hasWithParameterAttribute = p.TryGetAttribute(WithParameterAttributeName, out AttributeSyntax? _);
                var parameterDescriptor = ParameterDescriptor.Create(p, semanticModel);
                return new SearchFilterParameterInformation(hasWithParameterAttribute, parameterDescriptor);
            })
            .ToArray();

        return parameters;
    }
}
