using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.Extensions.SourceGenerator.Generation;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// O status de sucesso selecionado explicitamente por <c>WithResultStatus</c> (DF23); symbol-free e
/// equatável para retenção no pipeline. <see langword="null"/> no modelo significa "sem seleção" e preserva
/// a inferência atual.
/// </summary>
internal enum HttpResultStatusModel
{
    Ok,
    Created,
    NoContent,
}

/// <summary>
/// <para>
///     Leitura e emissão compartilhadas da extensibilidade HTTP comum da Fase 10 (DF23):
///     <c>[WithEndpointFilter&lt;T&gt;]</c> repetível, <c>[WithTags]</c> e <c>[WithResultStatus]</c>.
///     Command, Find e Search usam exatamente as mesmas regras; nenhuma superfície ganha um caminho paralelo.
/// </para>
/// <para>
///     Os filtros são emitidos com o nome globalmente qualificado (<c>global::Ns.Tipo</c>): a classe do grupo
///     não precisa de novos usings e tipos homônimos em namespaces diferentes não colidem.
/// </para>
/// </summary>
internal static class EndpointExtensibility
{
    /// <summary>
    /// Lê os atributos <c>[WithEndpointFilter&lt;T&gt;]</c> na ordem declarada e valida o contrato do tipo:
    /// classe concreta, não file-local, acessível ao código gerado e
    /// implementando <c>Microsoft.AspNetCore.Http.IEndpointFilter</c> (RCCMD051). Tipos de erro (em digitação)
    /// são ignorados — o compilador já reporta (DF9).
    /// </summary>
    internal static string[] ReadFilters(
        INamedTypeSymbol classSymbol,
        Compilation compilation,
        Location fallback,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        List<string>? filters = null;

        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (!KnownAttributes.WithEndpointFilter.Matches(attribute))
                continue;

            var location = KnownAttributes.GetLocation(attribute, cancellationToken, fallback);

            if (attribute.AttributeClass is not { TypeArguments.Length: 1 } attributeClass ||
                attributeClass.TypeArguments[0] is not { } filterType ||
                filterType.TypeKind == TypeKind.Error)
            {
                // argumento genérico não resolvido: o compilador já reporta (DF9)
                continue;
            }

            if (GetFilterTypeProblem(filterType, compilation) is { } problem)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidEndpointFilter,
                    location,
                    filterType.Name,
                    problem));
                continue;
            }

            filters ??= [];
            filters.Add(filterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return filters?.ToArray() ?? [];
    }

    private static string? GetFilterTypeProblem(ITypeSymbol filterType, Compilation compilation)
    {
        if (filterType is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class)
            return "the filter must be a class";
        if (named.IsAbstract)
            return "the filter must not be abstract or static";
        if (named.IsFileLocal)
            return "the filter must not be a file-local type (declared with the 'file' modifier)";
        if (!compilation.IsSymbolAccessibleWithin(named, compilation.Assembly))
            return "the filter type is not accessible to the generated code";
        if (!named.AllInterfaces.Any(candidate =>
                KnownAttributes.IsType(candidate, "Microsoft.AspNetCore.Http", "IEndpointFilter")))
        {
            return "the filter must implement Microsoft.AspNetCore.Http.IEndpointFilter";
        }

        return null;
    }

    /// <summary>
    /// Lê o atributo <c>[WithTags]</c>: exige pelo menos uma tag e nenhuma vazia ou composta apenas por
    /// espaços (RCCMD041); a ordem declarada é preservada.
    /// </summary>
    internal static string[] ReadTags(
        INamedTypeSymbol classSymbol,
        Location fallback,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.WithTags, out var attribute))
            return [];

        var location = KnownAttributes.GetLocation(attribute!, cancellationToken, fallback);

        void Fail(string requirement) =>
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidEndpointMetadataArgument, location, "WithTags", requirement));

        if (attribute!.ConstructorArguments.Length != 1 ||
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Error)
        {
            return []; // argumento não constante/incompleto: o compilador já reporta (DF9)
        }

        if (!KnownAttributes.TryGetStrings(attribute.ConstructorArguments[0], out var tags))
        {
            Fail("a non-null array of tags");
            return [];
        }

        if (tags.Length == 0)
        {
            Fail("at least one tag");
            return [];
        }

        if (tags.Any(string.IsNullOrWhiteSpace))
        {
            Fail("tags that are not empty or whitespace");
            return [];
        }

        return tags;
    }

    /// <summary>
    /// Lê o <c>[WithResultStatus]</c> de um command map; valores fora do enum público produzem RCCMD052.
    /// </summary>
    internal static HttpResultStatusModel? ReadResultStatus(
        INamedTypeSymbol classSymbol,
        Location fallback,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.WithResultStatus, out var attribute))
            return null;

        var location = KnownAttributes.GetLocation(attribute!, cancellationToken, fallback);

        if (attribute!.ConstructorArguments.Length != 1 ||
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Error)
        {
            return null; // argumento não constante/incompleto: o compilador já reporta (DF9)
        }

        // o valor do enum público usa os códigos HTTP (200/201/204)
        return attribute.ConstructorArguments[0].Value switch
        {
            200 => HttpResultStatusModel.Ok,
            201 => HttpResultStatusModel.Created,
            204 => HttpResultStatusModel.NoContent,
            _ => Unknown(),
        };

        HttpResultStatusModel? Unknown()
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidResultStatus,
                location,
                "the value is not a known HttpResultStatus (Ok, Created or NoContent)"));
            return null;
        }
    }

    /// <summary>
    /// <c>WithResultStatus</c> aplica-se apenas a command maps; em classes de <c>MapFind</c>/<c>MapSearch</c>
    /// o atributo seria ignorado em silêncio — é diagnosticado (RCCMD052).
    /// </summary>
    internal static void DenyResultStatus(
        INamedTypeSymbol classSymbol,
        string surface,
        Location fallback,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        if (!KnownAttributes.TryGet(classSymbol, KnownAttributes.WithResultStatus, out var attribute))
            return;

        errors.Add(DiagnosticInfo.Create(
            CmdDiagnostics.InvalidResultStatus,
            KnownAttributes.GetLocation(attribute!, cancellationToken, fallback),
            $"the attribute applies only to command maps and is not supported on {surface} classes"));
    }

    /// <summary>Emite <c>.WithTags("a", "b")</c> preservando a ordem declarada.</summary>
    internal static MethodInvokeGenerator EmitTags(MethodInvokeGenerator chain, string[]? tags)
    {
        if (tags is null || tags.Length == 0)
            return chain;

        var arguments = new ArgumentsGenerator();
        foreach (var tag in tags)
            arguments.AddArgument(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(tag, quote: true));

        return new MethodInvokeGenerator(chain, "WithTags", arguments)
        {
            LineIdent = true
        };
    }

    /// <summary>
    /// Emite <c>.AddEndpointFilter&lt;global::Ns.Tipo&gt;()</c> para cada filtro, na ordem declarada
    /// (a ordem de execução do ASP.NET Core segue a ordem de registro).
    /// </summary>
    internal static MethodInvokeGenerator EmitFilters(MethodInvokeGenerator chain, string[]? filters)
    {
        if (filters is null)
            return chain;

        foreach (var filter in filters)
        {
            chain = new MethodInvokeGenerator(chain, $"AddEndpointFilter<{filter}>")
            {
                LineIdent = true
            };
        }

        return chain;
    }
}
