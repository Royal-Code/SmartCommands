using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.SmartCommands.Generators.Models;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal static class FindGenerator
{
    public const string FindAttributeName = "RoyalCode.SmartCommands.MapFindAttribute";

    public static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return node is ClassDeclarationSyntax;
    }

    public static GenerationCandidate<FindModel> Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var information = TransformWorking(context, cancellationToken);
        if (information is null)
        {
            // entrada incompleta (argumento não constante em digitação): o compilador já reporta;
            // rejeição silenciosa, sem modelo e sem RCCMD (DF9).
            return GenerationCandidate<FindModel>.Invalid(default(EquatableArray<DiagnosticInfo>));
        }

        var diagnostics = PipelineDiagnostic.Snapshot(information.Diagnostics);
        return diagnostics.IsEmpty
            ? GenerationCandidate<FindModel>.Valid(FindModel.Create(information))
            : GenerationCandidate<FindModel>.Invalid(diagnostics);
    }

    private static FindInformation? TransformWorking(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // classe que contém o atributo
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;
        var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var classLocation = classDeclaration.Identifier.GetLocation();

        // lê o atributo MapFindAttribute pela identidade semântica (metadata name)
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.MapFind, out var mapFindAttribute))
        {
            return new FindInformation(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindUsage,
                classLocation,
                "The MapFindAttribute is not present in the class"));
        }

        // lê o atributo EntityReferenceAttribute
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.EntityReference, out var entityReferenceAttribute))
        {
            return new FindInformation(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindUsage,
                classLocation,
                "The EntityReferenceAttribute is not present in the class"));
        }

        // a quantidade de argumentos escritos vem da sintaxe; os valores vêm dos TypedConstants
        var writtenArgumentCount =
            (mapFindAttribute!.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax)?
                .ArgumentList?.Arguments.Count ?? 0;
        if (writtenArgumentCount != 2)
        {
            return new FindInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapFindUsage,
                KnownAttributes.GetLocation(mapFindAttribute, cancellationToken, classLocation),
                "MapFindAttribute requires a route pattern and an endpoint name"));
        }

        var mapArguments = mapFindAttribute.ConstructorArguments;
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
            return new FindInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapFindUsage,
                KnownAttributes.GetLocation(mapFindAttribute, cancellationToken, classLocation),
                "MapFindAttribute requires a route pattern and an endpoint name"));
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
                return diagnostic is null ? null : new FindInformation(diagnostic);
        }

        // tenta obter o summary
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithSummary, out var summaryAttr))
        {
            if (!TryReadRequiredString(summaryAttr!, "WithSummary", "a non-null summary", classLocation,
                    cancellationToken, out summary, out var diagnostic))
                return diagnostic is null ? null : new FindInformation(diagnostic);
        }

        // tenta obter o MapGroup attribute; o grupo é opcional de forma consistente — sem MapGroup os
        // endpoints são mapeados sem prefixo, na classe nomeada a partir do host (Fase 9)
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.MapGroup, out var groupAttr))
        {
            if (!TryReadRequiredString(groupAttr!, "MapGroup", "a non-null route prefix", classLocation,
                    cancellationToken, out groupName, out var diagnostic))
                return diagnostic is null ? null : new FindInformation(diagnostic);
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
                return new FindInformation(CreateInvalidEndpointMetadataDiagnostic(
                    policyAttr, "WithPolicy", "a non-null array of policy names", classLocation,
                    cancellationToken));
            }
            authorizationPolicies = policies.Length > 0 ? policies : authorizationPolicies ?? [];
        }

        // os tipos da entidade e do id vêm dos argumentos genéricos do atributo (símbolos reais)
        if (!TryGetEntityReferenceArguments(entityReferenceAttribute!, out var entitySymbol, out var idSymbol))
        {
            return new FindInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapFindUsage,
                KnownAttributes.GetLocation(entityReferenceAttribute!, cancellationToken, classLocation),
                "EntityReferenceAttribute requires entity and id type arguments"));
        }

        var entityType = SemanticTypes.CreateDescriptor(entitySymbol!);
        var idType = SemanticTypes.CreateDescriptor(idSymbol!);
        var modelType = TypeDescriptor.Create((ITypeSymbol)context.TargetSymbol);

        // validações acumuláveis: nome do endpoint, grupo, acessibilidade do DTO e rota do id
        var errors = new List<DiagnosticInfo>();

        EndpointNameRules.ValidateEndpointName(
            endpointName,
            "MapFind",
            KnownAttributes.GetArgumentLocation(mapFindAttribute, 1, cancellationToken, classLocation),
            errors);

        // DF23: tags e filtros valem para todas as superfícies; WithResultStatus é só de command maps
        var tags = EndpointExtensibility.ReadTags(classSymbol, classLocation, errors, cancellationToken);
        var endpointFilters = EndpointExtensibility.ReadFilters(
            classSymbol, classSymbol.ContainingAssembly, classLocation, errors, cancellationToken);
        EndpointExtensibility.DenyResultStatus(classSymbol, "MapFind", classLocation, errors, cancellationToken);

        if (groupName is not null && groupAttr is not null)
        {
            EndpointNameRules.ValidateGroupName(
                groupName,
                KnownAttributes.GetLocation(groupAttr, cancellationToken, classLocation),
                errors);
        }

        // o DTO é referenciado pelo delegate gerado em outra árvore: precisa ser top-level,
        // não file-local e não genérico (mesmas regras dos comandos)
        if (classSymbol.ContainingType is not null)
        {
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindUsage,
                classLocation,
                "The class with MapFindAttribute must be a top-level type, not a nested type"));
        }
        if (classSymbol.IsFileLocal)
        {
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindUsage,
                classLocation,
                "The class with MapFindAttribute must not be a file-local type (declared with the 'file' modifier)"));
        }
        if (classSymbol.Arity > 0)
        {
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindUsage,
                classLocation,
                "The class with MapFindAttribute must not be generic"));
        }

        // o delegate gerado declara o parâmetro 'id' (Id<TEntity, TId>): o template (grupo + rota) precisa
        // declarar a variável 'id' uma única vez, obrigatória, não catch-all e com constraint compatível
        ValidateFindRoute(
            endpointRoutePattern,
            groupName,
            idType,
            KnownAttributes.GetArgumentLocation(mapFindAttribute, 0, cancellationToken, classLocation),
            errors);

        if (errors.Count > 0)
            return new FindInformation(errors);

        return new FindInformation(
            entityType,
            idType,
            modelType,
            endpointRoutePattern,
            endpointName,
            description,
            summary,
            authorizationPolicies,
            groupName,
            endpointFilters.Length > 0 ? endpointFilters : null,
            tags.Length > 0 ? tags : null)
        {
            EndpointNameLocation = KnownAttributes.GetArgumentLocation(
                mapFindAttribute, 1, cancellationToken, classLocation),
        };
    }

    /// <summary>
    /// O handler gerado de Find vincula <c>Id&lt;TEntity, TId&gt;</c> ao parâmetro de rota <c>id</c>:
    /// o template completo (grupo + rota) precisa declarar a variável <c>id</c> exatamente uma vez,
    /// obrigatória, não catch-all e com constraint de tipo compatível com o id da entidade.
    /// </summary>
    private static void ValidateFindRoute(
        string routePattern,
        string? groupName,
        TypeDescriptor idType,
        Location location,
        List<DiagnosticInfo> errors)
    {
        var template = groupName is null ? routePattern : $"{groupName}/{routePattern}";
        var idParameters = RoutePatternParser.Parse(template)
            .Where(parameter => string.Equals(parameter.Name, "id", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        void Fail(string reason) =>
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindUsage, location, reason));

        if (idParameters.Length == 0)
        {
            Fail($"the route template '{template}' must declare the route parameter '{{id}}', bound to the entity id");
            return;
        }

        if (idParameters.Length > 1)
        {
            Fail($"the route template '{template}' declares the route parameter 'id' more than once");
            return;
        }

        var idParameter = idParameters[0];
        if (idParameter.IsOptional || idParameter.DefaultValue is not null)
            Fail("the route parameter 'id' must not be optional or have a default value: the entity id is required");
        if (idParameter.IsCatchAll)
            Fail("the route parameter 'id' must not be a catch-all: it binds a single entity id");

        if (idParameter.Constraint is null)
            return;

        // valida somente constraints de tipo conhecidas; múltiplas constraints são separadas por ':'
        foreach (var constraint in idParameter.Constraint.Split(':'))
        {
            var baseName = constraint;
            var parenthesis = baseName.IndexOf('(');
            if (parenthesis >= 0)
                baseName = baseName.Substring(0, parenthesis);

            if (!RouteConstraintTypes.TryGetClrTypeName(baseName.Trim(), out var expectedTypeName))
                continue;

            // Nullable<T> no id ('int?') vincula normalmente uma rota '{id:int}'
            var idTypeName = idType.Name.TrimEnd('?');
            if (!string.Equals(idTypeName, expectedTypeName, StringComparison.Ordinal))
            {
                Fail($"the route constraint '{baseName}' expects '{expectedTypeName}' " +
                    $"but the entity id type is '{idType.Name}'");
            }
        }
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

    private static bool TryGetEntityReferenceArguments(
        AttributeData attribute,
        out ITypeSymbol? entityType,
        out ITypeSymbol? idType)
    {
        if (attribute.AttributeClass is { TypeKind: not TypeKind.Error, TypeArguments.Length: 2 } attributeClass)
        {
            entityType = attributeClass.TypeArguments[0];
            idType = attributeClass.TypeArguments[1];
            return true;
        }

        entityType = null;
        idType = null;
        return false;
    }
}
