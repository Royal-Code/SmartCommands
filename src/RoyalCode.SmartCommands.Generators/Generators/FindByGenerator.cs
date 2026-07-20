using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.SmartCommands.Generators.Models;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// Transform do <c>MapFindBy&lt;TEntity&gt;</c> (Fase 5, DF10): lê a entidade (argumento genérico), as
/// propriedades nomeadas da chave alternativa/composta e a rota, resolvendo tipos, nulabilidade e
/// acessibilidade semanticamente e congelando o snapshot symbol-free <see cref="FindByModel"/>. Espelha o
/// <see cref="FindGenerator"/> (por ID), mas com a lista de propriedades da chave.
/// </summary>
internal static class FindByGenerator
{
    // metadata name do atributo genérico (aridade 1)
    public const string FindByAttributeName = "RoyalCode.SmartCommands.MapFindByAttribute`1";

    // identificadores reservados pelo handler gerado; uma propriedade da chave não pode colidir com eles
    private static readonly HashSet<string> ReservedParameterNames =
        new(StringComparer.Ordinal) { "accessor", "ct", "e", "findResult", "notfoundProblem" };

    public static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return node is ClassDeclarationSyntax;
    }

    public static GenerationCandidate<FindByModel> Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var information = TransformWorking(context, cancellationToken);
        if (information is null)
        {
            // entrada incompleta (argumento não constante em digitação): o compilador já reporta;
            // rejeição silenciosa, sem modelo e sem RCCMD (DF9).
            return GenerationCandidate<FindByModel>.Invalid(default(EquatableArray<DiagnosticInfo>));
        }

        var diagnostics = PipelineDiagnostic.Snapshot(information.Diagnostics);
        return diagnostics.IsEmpty
            ? GenerationCandidate<FindByModel>.Valid(FindByModel.Create(information))
            : GenerationCandidate<FindByModel>.Invalid(diagnostics);
    }

    private static FindByInformation? TransformWorking(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;
        var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var classLocation = classDeclaration.Identifier.GetLocation();

        // lê o atributo MapFindBy<TEntity> pela identidade semântica (metadata name)
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.MapFindBy, out var mapFindByAttribute))
        {
            return new FindByInformation(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindByUsage,
                classLocation,
                "The MapFindByAttribute is not present in the class"));
        }

        // o tipo da entidade vem do argumento genérico do atributo (símbolo real)
        if (mapFindByAttribute!.AttributeClass is not
            { TypeKind: not TypeKind.Error, TypeArguments.Length: 1 } attributeClass ||
            attributeClass.TypeArguments[0] is not { TypeKind: not TypeKind.Error } entitySymbol)
        {
            // argumento genérico não resolvido: o compilador já reporta (DF9)
            return null;
        }

        var mapArguments = mapFindByAttribute.ConstructorArguments;
        if (mapArguments.Length != 3 ||
            mapArguments[0].Kind == TypedConstantKind.Error ||
            mapArguments[1].Kind == TypedConstantKind.Error ||
            mapArguments[2].Kind == TypedConstantKind.Error)
        {
            return null; // argumento não constante/incompleto: o compilador já reporta (DF9)
        }

        var endpointRoutePattern = KnownAttributes.GetString(mapArguments[0]);
        var endpointName = KnownAttributes.GetString(mapArguments[1]);
        if (endpointRoutePattern is null || endpointName is null)
        {
            return new FindByInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapFindByUsage,
                KnownAttributes.GetLocation(mapFindByAttribute, cancellationToken, classLocation),
                "MapFindByAttribute requires a route pattern and an endpoint name"));
        }

        if (!KnownAttributes.TryGetStrings(mapArguments[2], out var propertiesNames))
        {
            return new FindByInformation(CreateInvalidEndpointMetadataDiagnostic(
                mapFindByAttribute, "MapFindBy", "a non-null array of property names", classLocation,
                cancellationToken));
        }

        if (propertiesNames.Length == 0)
        {
            return new FindByInformation(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapFindByUsage,
                KnownAttributes.GetLocation(mapFindByAttribute, cancellationToken, classLocation),
                "MapFindByAttribute requires at least one entity property name to search by"));
        }

        string? description = null;
        string? summary = null;
        string[]? authorizationPolicies = null;
        string? groupName = null;

        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithDescription, out var descAttr))
        {
            if (!TryReadRequiredString(descAttr!, "WithDescription", "a non-null description", classLocation,
                    cancellationToken, out description, out var diagnostic))
                return diagnostic is null ? null : new FindByInformation(diagnostic);
        }

        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithSummary, out var summaryAttr))
        {
            if (!TryReadRequiredString(summaryAttr!, "WithSummary", "a non-null summary", classLocation,
                    cancellationToken, out summary, out var diagnostic))
                return diagnostic is null ? null : new FindByInformation(diagnostic);
        }

        // o grupo é opcional de forma consistente (sem MapGroup os endpoints são mapeados sem prefixo)
        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.MapGroup, out var groupAttr))
        {
            if (!TryReadRequiredString(groupAttr!, "MapGroup", "a non-null route prefix", classLocation,
                    cancellationToken, out groupName, out var diagnostic))
                return diagnostic is null ? null : new FindByInformation(diagnostic);
        }

        if (KnownAttributes.Has(classSymbol, KnownAttributes.WithAuthorization))
            authorizationPolicies = [];

        if (KnownAttributes.TryGet(classSymbol, KnownAttributes.WithPolicy, out var policyAttr))
        {
            if (policyAttr!.ConstructorArguments.Length != 1 ||
                policyAttr.ConstructorArguments[0].Kind == TypedConstantKind.Error)
                return null;
            if (!KnownAttributes.TryGetStrings(policyAttr.ConstructorArguments[0], out var policies))
            {
                return new FindByInformation(CreateInvalidEndpointMetadataDiagnostic(
                    policyAttr, "WithPolicy", "a non-null array of policy names", classLocation,
                    cancellationToken));
            }
            authorizationPolicies = policies.Length > 0 ? policies : authorizationPolicies ?? [];
        }

        var entityType = SemanticTypes.CreateDescriptor(entitySymbol);
        var modelType = TypeDescriptor.Create((ITypeSymbol)context.TargetSymbol);

        // validações acumuláveis
        var errors = new List<DiagnosticInfo>();

        EndpointNameRules.ValidateEndpointName(
            endpointName,
            "MapFindBy",
            KnownAttributes.GetArgumentLocation(mapFindByAttribute, 1, cancellationToken, classLocation),
            errors);

        // DF23: tags e filtros valem para todas as superfícies; WithResultStatus é só de command maps
        var tags = EndpointExtensibility.ReadTags(classSymbol, classLocation, errors, cancellationToken);
        var endpointFilters = EndpointExtensibility.ReadFilters(
            classSymbol, context.SemanticModel.Compilation, classLocation, errors, cancellationToken);
        EndpointExtensibility.DenyResultStatus(classSymbol, "MapFindBy", classLocation, errors, cancellationToken);

        if (groupName is not null && groupAttr is not null)
        {
            EndpointNameRules.ValidateGroupName(
                groupName,
                KnownAttributes.GetLocation(groupAttr, cancellationToken, classLocation),
                errors);
        }

        // o DTO é referenciado pelo delegate gerado em outra árvore: precisa ser top-level, não file-local
        // e não genérico (mesmas regras dos comandos e do MapFind)
        if (classSymbol.ContainingType is not null)
        {
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindByUsage,
                classLocation,
                "The class with MapFindByAttribute must be a top-level type, not a nested type"));
        }
        if (classSymbol.IsFileLocal)
        {
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindByUsage,
                classLocation,
                "The class with MapFindByAttribute must not be a file-local type (declared with the 'file' modifier)"));
        }
        if (classSymbol.Arity > 0)
        {
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindByUsage,
                classLocation,
                "The class with MapFindByAttribute must not be generic"));
        }

        var attributeLocation = KnownAttributes.GetLocation(mapFindByAttribute, cancellationToken, classLocation);

        // resolve cada propriedade da chave na entidade: existência, membro direto público de instância,
        // legível por getter público, tipo acessível e comparável por igualdade (SupportsEquality), nome não
        // reservado
        var properties = ResolveProperties(
            entitySymbol,
            entityType.Name,
            propertiesNames,
            attributeLocation,
            errors,
            context.SemanticModel.Compilation,
            cancellationToken);

        // a rota (grupo + pattern) precisa declarar exatamente um placeholder por propriedade, obrigatório,
        // não catch-all/opcional, e com constraint de tipo compatível quando presente
        ValidateFindByRoute(
            endpointRoutePattern,
            groupName,
            propertiesNames,
            properties,
            KnownAttributes.GetArgumentLocation(mapFindByAttribute, 0, cancellationToken, classLocation),
            errors);

        if (errors.Count > 0)
            return new FindByInformation(errors);

        return new FindByInformation(
            entityType,
            modelType,
            properties!,
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
                mapFindByAttribute, 1, cancellationToken, classLocation),
        };
    }

    /// <summary>
    /// Resolve cada propriedade nomeada da chave na entidade, validando existência, acessibilidade,
    /// legibilidade, tipo vinculável/equatável e nome não reservado, além de duplicação. Devolve
    /// <see langword="null"/> quando qualquer propriedade é inválida (evita seguir para emissão).
    /// </summary>
    private static IReadOnlyList<FindByProperty>? ResolveProperties(
        ITypeSymbol entitySymbol,
        string entityName,
        string[] propertiesNames,
        Location location,
        List<DiagnosticInfo> errors,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        void Fail(string reason) =>
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindByUsage, location, reason));

        // propriedade declarada mais de uma vez geraria parâmetros/critérios duplicados
        foreach (var duplicated in propertiesNames
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Fail($"the property '{duplicated.Key}' is declared more than once");
        }

        var entityProperties = entitySymbol.GetAllMembers().OfType<IPropertySymbol>().ToList();

        var resolved = new List<FindByProperty>(propertiesNames.Length);
        var valid = true;

        foreach (var propertyName in propertiesNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ReservedParameterNames.Contains(propertyName))
            {
                Fail($"the property '{propertyName}' uses a name reserved by the generated handler; " +
                    "rename the entity property to search by it");
                valid = false;
                continue;
            }

            var property = entityProperties.FirstOrDefault(p => p.Name == propertyName);
            if (property is null)
            {
                Fail($"the property '{propertyName}' was not found on the entity '{entityName}'");
                valid = false;
                continue;
            }

            if (GetKeyPropertyProblem(property, compilation) is { } problem)
            {
                Fail($"the property '{propertyName}' cannot be used to search by: {problem}");
                valid = false;
                continue;
            }

            resolved.Add(new FindByProperty(propertyName, SemanticTypes.CreateDescriptor(property.Type)));
        }

        return valid ? resolved : null;
    }

    /// <summary>
    /// <para>
    ///     Uma propriedade usada na chave de busca precisa ser de instância, pública e legível por um getter
    ///     público (o filtro <c>e =&gt; e.Prop == prop</c> é emitido em outra classe/árvore), de um tipo
    ///     acessível ao código gerado e que suporte o operador <c>==</c> exigido pelo filtro.
    /// </para>
    /// <para>
    ///     O generator valida somente o que é sua responsabilidade — a expressão de igualdade pode ser emitida
    ///     e compilada, o tipo é acessível e o membro pode ser lido (DF7). A capacidade geral de binding de
    ///     rota é do ASP.NET Core (um tipo referência vinculável, ex.: <c>IParsable&lt;T&gt;</c>, é aceito).
    ///     Tipos de erro (em digitação) são ignorados: o compilador já reporta (DF9).
    /// </para>
    /// </summary>
    private static string? GetKeyPropertyProblem(IPropertySymbol property, Compilation compilation)
    {
        if (property.IsStatic)
            return "the property is static";
        if (property.DeclaredAccessibility != Accessibility.Public)
            return "the property is not public";
        if (property.GetMethod is null)
            return "the property is not readable (it has no getter)";
        if (property.GetMethod.DeclaredAccessibility != Accessibility.Public)
            return "the property getter is not public";

        var type = property.Type;
        if (type.TypeKind == TypeKind.Error)
            return null; // o compilador já reporta o tipo não resolvido

        if (!IsEmittableType(type))
            return "the property type is not accessible to the generated code";

        if (!SupportsEquality(type, compilation))
        {
            return "the property type does not support the equality operator '==' used by the generated " +
                "filter (use a primitive, enum, string, Guid/DateTime-like value type, a record struct, a " +
                "struct that declares 'operator ==', or a reference type)";
        }

        return null;
    }

    /// <summary>
    /// O tipo suporta o operador <c>==</c> emitido no filtro <c>e =&gt; e.Prop == prop</c>: tipos referência
    /// (igualdade de referência ou sobrecarga) sempre suportam; value types suportam quando são primitivos,
    /// enums, <c>Nullable&lt;T&gt;</c> de um suportado, ou declaram <c>operator ==</c> (record struct,
    /// <c>Guid</c>, <c>DateTime</c>, etc.). Um <c>struct</c> comum sem <c>operator ==</c> não compilaria.
    /// </summary>
    private static bool SupportsEquality(ITypeSymbol type, Compilation compilation)
    {
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol { TypeArguments.Length: 1 } nullable)
        {
            return SupportsEquality(nullable.TypeArguments[0], compilation);
        }

        // Operações dinâmicas não podem aparecer em expression trees (CS1963).
        if (type.TypeKind == TypeKind.Dynamic)
            return false;

        // == sempre compila para tipos referência; binding de rota é responsabilidade do ASP.NET Core
        if (type.IsReferenceType)
            return true;

        if (type.TypeKind == TypeKind.Enum)
            return true;

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
                return true;
        }

        // Structs (record struct, Guid, DateTime, DateTimeOffset, TimeSpan, ...) precisam declarar um
        // operator == realmente aplicável aos dois operandos emitidos. Apenas encontrar qualquer
        // op_Equality não basta: um tipo pode declarar, por exemplo, operator ==(T, int).
        return type.GetMembers("op_Equality")
            .OfType<IMethodSymbol>()
            .Any(method =>
                method.IsStatic &&
                method.Parameters.Length == 2 &&
                method.DeclaredAccessibility == Accessibility.Public &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean &&
                compilation is CSharpCompilation csharpCompilation &&
                csharpCompilation.ClassifyConversion(type, method.Parameters[0].Type).IsImplicit &&
                csharpCompilation.ClassifyConversion(type, method.Parameters[1].Type).IsImplicit);
    }

    /// <summary>
    /// O código é gerado no mesmo assembly: tipos públicos e internos são utilizáveis; privados e protegidos
    /// (aninhados) não podem ser referenciados pela emissão. Espelha a regra das propriedades de resposta.
    /// </summary>
    private static bool IsEmittableType(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsEmittableType(array.ElementType);
            case INamedTypeSymbol named:
                for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
                {
                    if (current.DeclaredAccessibility is
                        Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                    {
                        return false;
                    }
                }

                return named.TypeArguments.All(IsEmittableType);
            default:
                return true;
        }
    }

    /// <summary>
    /// A rota completa (grupo + pattern) precisa declarar exatamente um placeholder por propriedade da chave,
    /// casando o nome sem diferenciar caixa, obrigatório, não catch-all/opcional/default e com constraint de
    /// tipo compatível com a propriedade quando presente. Placeholders sem propriedade correspondente e o
    /// inverso são diagnosticados (correspondência 1:1).
    /// </summary>
    private static void ValidateFindByRoute(
        string routePattern,
        string? groupName,
        string[] propertiesNames,
        IReadOnlyList<FindByProperty>? properties,
        Location location,
        List<DiagnosticInfo> errors)
    {
        var template = groupName is null ? routePattern : $"{groupName}/{routePattern}";
        var placeholders = RoutePatternParser.Parse(template);

        void Fail(string reason) =>
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidMapFindByUsage, location, reason));

        foreach (var duplicated in placeholders
            .GroupBy(placeholder => placeholder.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Fail($"the route template '{template}' declares the placeholder '{duplicated.Key}' more than once");
        }

        foreach (var placeholder in placeholders)
        {
            if (placeholder.IsOptional || placeholder.DefaultValue is not null)
                Fail($"the placeholder '{placeholder.Name}' must not be optional or have a default value: " +
                    "each key value is required");
            if (placeholder.IsCatchAll)
                Fail($"the placeholder '{placeholder.Name}' must not be a catch-all: it binds a single key value");
        }

        // correspondência 1:1 entre placeholders e propriedades declaradas (casadas sem diferenciar caixa)
        if (placeholders.Count != propertiesNames.Length)
        {
            Fail($"the route template '{template}' declares {placeholders.Count} placeholder(s) but " +
                $"{propertiesNames.Length} property name(s) were declared; each property must be matched by " +
                "exactly one route placeholder of the same name");
        }

        foreach (var propertyName in propertiesNames)
        {
            if (!placeholders.Any(placeholder =>
                    string.Equals(placeholder.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
            {
                Fail($"the property '{propertyName}' has no matching route placeholder '{{{propertyName}}}'");
            }
        }

        foreach (var placeholder in placeholders)
        {
            if (!propertiesNames.Any(name =>
                    string.Equals(name, placeholder.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Fail($"the placeholder '{{{placeholder.Name}}}' does not match any declared property; " +
                    "MapFindBy uses named placeholders matched, case-insensitively, to the properties " +
                    "declared with nameof");
            }
        }

        // constraint de tipo (quando presente) precisa ser compatível com o tipo da propriedade casada
        if (properties is null)
            return;

        // propriedades duplicadas já foram diagnosticadas; o dicionário deve tolerá-las sem lançar (CS8785)
        var typesByName = new Dictionary<string, TypeDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties)
            typesByName[property.Name] = property.Type;

        foreach (var placeholder in placeholders)
        {
            if (placeholder.Constraint is null ||
                !typesByName.TryGetValue(placeholder.Name, out var propertyType))
                continue;

            foreach (var constraint in placeholder.Constraint.Split(':'))
            {
                var baseName = constraint;
                var parenthesis = baseName.IndexOf('(');
                if (parenthesis >= 0)
                    baseName = baseName.Substring(0, parenthesis);

                if (!RouteConstraintTypes.TryGetClrTypeName(baseName.Trim(), out var expectedTypeName))
                    continue;

                // Nullable<T> ('int?') vincula normalmente uma rota '{prop:int}'
                var propertyTypeName = propertyType.Name.TrimEnd('?');
                if (!string.Equals(propertyTypeName, expectedTypeName, StringComparison.Ordinal))
                {
                    Fail($"the route constraint '{baseName}' on '{placeholder.Name}' expects " +
                        $"'{expectedTypeName}' but the property type is '{propertyType.Name}'");
                }
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
}
