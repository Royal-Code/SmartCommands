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
        // NOTA (Fase 9): a obrigatoriedade de MapGroup para Find será decidida e diagnosticada lá;
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

        return new FindInformation(
            entityType,
            idType,
            modelType,
            endpointRoutePattern,
            endpointName,
            description,
            summary,
            authorizationPolicies,
            groupName)
        {
            EndpointNameLocation = KnownAttributes.GetArgumentLocation(
                mapFindAttribute, 1, cancellationToken, classLocation),
        };
    }

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
