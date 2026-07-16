using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.SmartCommands.Generators.Models;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal static class SearchGenerator
{
    public const string SearchAttributeName = "RoyalCode.SmartCommands.MapSearchAttribute";

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
        if (information is null)
        {
            // entrada incompleta (argumento não constante em digitação): o compilador já reporta;
            // rejeição silenciosa, sem modelo e sem RCCMD (DF9).
            return GenerationCandidate<SearchModel>.Invalid(default(EquatableArray<DiagnosticInfo>));
        }

        var diagnostics = PipelineDiagnostic.Snapshot(information.Diagnostics);
        return diagnostics.IsEmpty
            ? GenerationCandidate<SearchModel>.Valid(SearchModel.Create(information))
            : GenerationCandidate<SearchModel>.Invalid(diagnostics);
    }

    private static SearchInformation? TransformWorking(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        // classe que contém o atributo
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;
        var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var classLocation = classDeclaration.Identifier.GetLocation();

        // lê o atributo MapSearch pela identidade semântica (metadata name)
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.MapSearch, out var mapSearchAttribute))
        {
            return new SearchInformation(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
                classLocation,
                "The MapSearchAttribute is not present in the class"));
        }

        // lê o atributo SearchReferenceAttribute (aridade 1 ou 2)
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.SearchReference1, out var searchReferenceAttribute) &&
            !KnownAttributes.TryGet(classSymbol, KnownAttributes.SearchReference2, out searchReferenceAttribute))
        {
            return new SearchInformation(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
                classLocation,
                "The SearchReferenceAttribute is not present in the class"));
        }

        // a quantidade de argumentos escritos vem da sintaxe; os valores vêm dos TypedConstants
        var writtenArgumentCount =
            (mapSearchAttribute!.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax)?
                .ArgumentList?.Arguments.Count ?? 0;
        if (writtenArgumentCount != 2)
        {
            return new SearchInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapSearchUsage,
                KnownAttributes.GetLocation(mapSearchAttribute, cancellationToken, classLocation),
                "MapSearchAttribute requires a route pattern and an endpoint name"));
        }

        var mapArguments = mapSearchAttribute.ConstructorArguments;
        if (mapArguments.Length != 2 ||
            mapArguments[0].Kind == TypedConstantKind.Error ||
            mapArguments[1].Kind == TypedConstantKind.Error)
        {
            return null; // argumento não constante/incompleto: o compilador já reporta (DF9)
        }

        var endpointRoutePattern = KnownAttributes.GetString(mapArguments[0]);
        var endpointName = KnownAttributes.GetString(mapArguments[1]);
        if (endpointRoutePattern is null || endpointName is null)
        {
            // null é constante válida para o compilador, mas é uso inválido do atributo
            return new SearchInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapSearchUsage,
                KnownAttributes.GetLocation(mapSearchAttribute, cancellationToken, classLocation),
                "MapSearchAttribute requires a route pattern and an endpoint name"));
        }

        string? description = null;
        string? summary = null;
        string[]? authorizationPolicies = null;
        string? groupName = null;

        // tenta obter a description
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithDescription, out var descAttr) &&
            descAttr!.ConstructorArguments.Length == 1)
        {
            description = KnownAttributes.GetString(descAttr.ConstructorArguments[0]);
        }

        // tenta obter o summary
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithSummary, out var summaryAttr) &&
            summaryAttr!.ConstructorArguments.Length == 1)
        {
            summary = KnownAttributes.GetString(summaryAttr.ConstructorArguments[0]);
        }

        // tenta obter o MapGroup attribute
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.MapGroup, out var groupAttr) &&
            groupAttr!.ConstructorArguments.Length == 1)
        {
            groupName = KnownAttributes.GetString(groupAttr.ConstructorArguments[0]);
        }
        // NOTA (Fase 9): a obrigatoriedade de MapGroup para Search será decidida e diagnosticada lá;
        // hoje o grupo ausente segue nulo, comportamento preservado.

        // tenta obter o authorization
        if (KnownAttributes.Has(classSymbol, KnownAttributes.WithAuthorization))
            authorizationPolicies = [];

        // se tiver o attribute WithPolicy, deve obter o(s) nome(s) da(s) política(s) — aceita params e array explícito
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithPolicy, out var policyAttr))
        {
            var policies = policyAttr!.ConstructorArguments.Length > 0
                ? KnownAttributes.GetStrings(policyAttr.ConstructorArguments[0]).ToArray()
                : [];
            authorizationPolicies = policies.Length > 0 ? policies : authorizationPolicies ?? [];
        }

        // os tipos da entidade (e opcionalmente o de projeção) vêm dos argumentos genéricos do atributo
        if (searchReferenceAttribute!.AttributeClass is not
            {
                TypeKind: not TypeKind.Error,
                TypeArguments.Length: 1 or 2,
            } searchReferenceClass)
        {
            return new SearchInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapSearchUsage,
                KnownAttributes.GetLocation(searchReferenceAttribute!, cancellationToken, classLocation),
                "SearchReferenceAttribute requires one or two type arguments"));
        }

        var entitySymbol = searchReferenceClass.TypeArguments[0];
        var selectSymbol = searchReferenceClass.TypeArguments.Length == 2
            ? searchReferenceClass.TypeArguments[1]
            : null;

        var entityType = SemanticTypes.CreateDescriptor(entitySymbol);
        var selectType = selectSymbol is null ? null : SemanticTypes.CreateDescriptor(selectSymbol);
        var filterType = TypeDescriptor.Create((ITypeSymbol)context.TargetSymbol);

        // obtém os métodos da classe, em busca de métodos anotados com WithFilter
        if (!TryCreateFilter(
            classSymbol,
            endpointRoutePattern,
            groupName,
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
            endpointRoutePattern,
            endpointName,
            description,
            summary,
            authorizationPolicies,
            groupName!,
            searchFilterInformation)
        {
            EndpointNameLocation = KnownAttributes.GetArgumentLocation(
                mapSearchAttribute, 1, cancellationToken, classLocation),
        };
    }

    public static bool TryCreateFilter(
        INamedTypeSymbol classSymbol,
        string routePattern,
        string? groupName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out SearchFilterInformation? searchFilterInformation,
        out List<DiagnosticInfo>? diagnostics)
    {
        diagnostics = null;
        searchFilterInformation = null;

        var methods = classSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(candidate => KnownAttributes.Has(candidate, KnownAttributes.WithFilter))
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        if (methods.Count > 1)
        {
            diagnostics = methods.Select(candidate =>
            {
                var location = candidate.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
                return DiagnosticInfo.Create(CmdDiagnostics.InvalidWithFilterUsage,
                    location,
                    "Only one method with WithFilterAttribute is allowed in the search filter class");
            }).ToList();

            return false;
        }
        else if (methods.Count is 1)
        {
            var methodWithFilter = methods[0];

            var methodName = methodWithFilter.Name;
            // detecção semântica de assíncrono: pelo tipo de retorno (Task/ValueTask),
            // nunca pelo modificador 'async'
            var isAsync = IsAwaitableType(methodWithFilter.ReturnType);

            var errors = new List<DiagnosticInfo>();
            var parameters = CreateSearchFilterParameters(
                methodWithFilter, routePattern, groupName, semanticModel, errors, cancellationToken);

            if (errors.Count > 0)
            {
                diagnostics = errors;
                return false;
            }

            searchFilterInformation = new SearchFilterInformation(methodName, isAsync, parameters);
        }

        return true;
    }

    private static bool IsAwaitableType(ITypeSymbol returnType) =>
        KnownAttributes.IsType(returnType, "System.Threading.Tasks", "Task") ||
        KnownAttributes.IsType(returnType, "System.Threading.Tasks", "Task`1") ||
        KnownAttributes.IsType(returnType, "System.Threading.Tasks", "ValueTask") ||
        KnownAttributes.IsType(returnType, "System.Threading.Tasks", "ValueTask`1");

    private static SearchFilterParameterInformation[] CreateSearchFilterParameters(
        IMethodSymbol method,
        string routePattern,
        string? groupName,
        SemanticModel semanticModel,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        var parameters = method.Parameters
            .Select(parameterSymbol =>
            {
                var hasWithParameterAttribute = KnownAttributes.Has(parameterSymbol, KnownAttributes.WithParameter);

                // o descritor para emissão continua vindo da sintaxe (nome do tipo como escrito);
                // as classificações são semânticas, congeladas aqui como fatos booleanos.
                var parameterSyntax = parameterSymbol.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax(cancellationToken))
                    .OfType<ParameterSyntax>()
                    .FirstOrDefault();

                var parameterDescriptor = CreateParameterDescriptor(parameterSymbol, parameterSyntax, semanticModel);

                // DF3: captura e valida os bindings explícitos dos parâmetros [WithParameter] do filtro
                // (o search sempre participa de um endpoint mapeado)
                var bindings = default(RoyalCode.Extensions.SourceGenerator.Collections.EquatableArray<ParameterBindingModel>);
                if (hasWithParameterAttribute)
                {
                    var captured = BindingAttributes.Capture(parameterSymbol);
                    var location = parameterSymbol.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
                    BindingAttributes.Validate(captured, parameterSymbol.Name, location, routePattern, groupName, errors);
                    bindings = captured.Bindings;
                }

                return new SearchFilterParameterInformation(
                    hasWithParameterAttribute,
                    parameterDescriptor,
                    isCriteriaParameter: KnownAttributes.IsType(parameterSymbol.Type, "RoyalCode.SmartSearch", "ICriteria`1"),
                    isCancellationTokenParameter: KnownAttributes.IsType(parameterSymbol.Type, "System.Threading", "CancellationToken"),
                    isHttpContextParameter: KnownAttributes.IsType(parameterSymbol.Type, "Microsoft.AspNetCore.Http", "HttpContext"),
                    bindings);
            })
            .ToArray();

        return parameters;
    }

    private static ParameterDescriptor CreateParameterDescriptor(
        IParameterSymbol parameterSymbol,
        ParameterSyntax? parameterSyntax,
        SemanticModel semanticModel)
    {
        if (parameterSyntax is null)
            return new ParameterDescriptor(SemanticTypes.CreateDescriptor(parameterSymbol.Type), parameterSymbol.Name);

        // o método [WithFilter] pode estar declarado em outra árvore (classe partial em outro arquivo);
        // o semantic model precisa ser o da árvore do parâmetro, senão o Roslyn lança exceção.
        var parameterModel = parameterSyntax.SyntaxTree == semanticModel.SyntaxTree
            ? semanticModel
            : semanticModel.Compilation.GetSemanticModel(parameterSyntax.SyntaxTree);

        return ParameterDescriptor.Create(parameterSyntax, parameterModel);
    }
}
