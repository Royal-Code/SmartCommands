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
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithDescription, out var descAttr))
        {
            if (!TryReadRequiredString(descAttr!, "WithDescription", "a non-null description", classLocation,
                    cancellationToken, out description, out var diagnostic))
                return diagnostic is null ? null : new SearchInformation(diagnostic);
        }

        // tenta obter o summary
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithSummary, out var summaryAttr))
        {
            if (!TryReadRequiredString(summaryAttr!, "WithSummary", "a non-null summary", classLocation,
                    cancellationToken, out summary, out var diagnostic))
                return diagnostic is null ? null : new SearchInformation(diagnostic);
        }

        // tenta obter o MapGroup attribute; o grupo é opcional de forma consistente — sem MapGroup os
        // endpoints são mapeados sem prefixo, na classe nomeada a partir do host (Fase 9)
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.MapGroup, out var groupAttr))
        {
            if (!TryReadRequiredString(groupAttr!, "MapGroup", "a non-null route prefix", classLocation,
                    cancellationToken, out groupName, out var diagnostic))
                return diagnostic is null ? null : new SearchInformation(diagnostic);
        }

        // tenta obter o authorization
        if (KnownAttributes.Has(classSymbol, KnownAttributes.WithAuthorization))
            authorizationPolicies = [];

        // se tiver o attribute WithPolicy, deve obter o(s) nome(s) da(s) política(s) — aceita params e array explícito
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithPolicy, out var policyAttr))
        {
            if (policyAttr!.ConstructorArguments.Length != 1 ||
                policyAttr.ConstructorArguments[0].Kind == TypedConstantKind.Error)
                return null;
            if (!KnownAttributes.TryGetStrings(policyAttr.ConstructorArguments[0], out var policies))
            {
                return new SearchInformation(CreateInvalidEndpointMetadataDiagnostic(
                    policyAttr, "WithPolicy", "a non-null array of policy names", classLocation,
                    cancellationToken));
            }
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

        // validações acumuláveis: nome do endpoint, grupo e acessibilidade da classe do filtro
        var declarationErrors = new List<DiagnosticInfo>();

        EndpointNameRules.ValidateEndpointName(
            endpointName,
            "MapSearch",
            KnownAttributes.GetArgumentLocation(mapSearchAttribute, 1, cancellationToken, classLocation),
            declarationErrors);

        // DF23: tags e filtros valem para todas as superfícies; WithResultStatus é só de command maps
        var tags = EndpointExtensibility.ReadTags(classSymbol, classLocation, declarationErrors, cancellationToken);
        var endpointFilters = EndpointExtensibility.ReadFilters(
            classSymbol, classSymbol.ContainingAssembly, classLocation, declarationErrors, cancellationToken);
        EndpointExtensibility.DenyResultStatus(
            classSymbol, "MapSearch", classLocation, declarationErrors, cancellationToken);

        if (groupName is not null && groupAttr is not null)
        {
            EndpointNameRules.ValidateGroupName(
                groupName,
                KnownAttributes.GetLocation(groupAttr, cancellationToken, classLocation),
                declarationErrors);
        }

        // o filtro é referenciado pelo delegate gerado em outra árvore: precisa ser top-level,
        // não file-local e não genérico (mesmas regras dos comandos)
        if (classSymbol.ContainingType is not null)
        {
            declarationErrors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
                classLocation,
                "The class with MapSearchAttribute must be a top-level type, not a nested type"));
        }
        if (classSymbol.IsFileLocal)
        {
            declarationErrors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
                classLocation,
                "The class with MapSearchAttribute must not be a file-local type (declared with the 'file' modifier)"));
        }
        if (classSymbol.Arity > 0)
        {
            declarationErrors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapSearchUsage,
                classLocation,
                "The class with MapSearchAttribute must not be generic"));
        }

        if (declarationErrors.Count > 0)
            return new SearchInformation(declarationErrors);

        // obtém os métodos da classe, em busca de métodos anotados com WithFilter
        if (!TryCreateFilter(
            classSymbol,
            endpointRoutePattern,
            groupName,
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
            groupName,
            searchFilterInformation,
            endpointFilters.Length > 0 ? endpointFilters : null,
            tags.Length > 0 ? tags : null)
        {
            EndpointNameLocation = KnownAttributes.GetArgumentLocation(
                mapSearchAttribute, 1, cancellationToken, classLocation),
        };
    }

    public static bool TryCreateFilter(
        INamedTypeSymbol classSymbol,
        string routePattern,
        string? groupName,
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
                methodWithFilter, routePattern, groupName, errors, cancellationToken);

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
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        var parameters = method.Parameters
            .Select(parameterSymbol =>
            {
                var hasWithParameterAttribute = KnownAttributes.Has(parameterSymbol, KnownAttributes.WithParameter);

                var parameterDescriptor = new ParameterDescriptor(
                    SemanticTypes.CreateDescriptor(parameterSymbol.Type),
                    parameterSymbol.Name);

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
                else if (!KnownAttributes.IsType(parameterSymbol.Type, "RoyalCode.SmartSearch", "ICriteria`1") &&
                         !KnownAttributes.IsType(parameterSymbol.Type, "System.Threading", "CancellationToken") &&
                         !KnownAttributes.IsType(parameterSymbol.Type, "Microsoft.AspNetCore.Http", "HttpContext"))
                {
                    // sem [WithParameter], o parâmetro vem de DI ([FromServices]); atributos de binding
                    // seriam ignorados em silêncio — isso é diagnosticado, não descartado
                    var captured = BindingAttributes.Capture(parameterSymbol);
                    if (captured.SourceCount > 0 || captured.HasAsParameters)
                    {
                        var location = parameterSymbol.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
                        errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidWithFilterUsage,
                            location,
                            $"the parameter '{parameterSymbol.Name}' declares binding attributes but is not marked " +
                            "with WithParameterAttribute; without the marker the parameter is resolved from " +
                            "dependency injection and the binding attributes would be ignored"));
                    }
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

    private static bool TryReadRequiredString(
        AttributeData attribute,
        string attributeName,
        string requirement,
        Location fallback,
        CancellationToken cancellationToken,
        out string? value,
        out DiagnosticInfo? diagnostic)
    {
        value = null;
        diagnostic = null;
        if (attribute.ConstructorArguments.Length != 1 ||
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Error)
            return false;

        value = KnownAttributes.GetString(attribute.ConstructorArguments[0]);
        if (value is not null)
            return true;

        diagnostic = CreateInvalidEndpointMetadataDiagnostic(
            attribute, attributeName, requirement, fallback, cancellationToken);
        return false;
    }

    private static DiagnosticInfo CreateInvalidEndpointMetadataDiagnostic(
        AttributeData attribute,
        string attributeName,
        string requirement,
        Location fallback,
        CancellationToken cancellationToken) =>
        DiagnosticInfo.Create(
            CmdDiagnostics.InvalidEndpointMetadataArgument,
            KnownAttributes.GetLocation(attribute, cancellationToken, fallback),
            attributeName,
            requirement);
}
