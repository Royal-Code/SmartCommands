using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;

namespace RoyalCode.SmartCommands.Generators;

internal static class CommandHelpers
{
    /// <summary>
    /// <para>
    ///     Localiza e valida o método <c>HasProblems</c> exigido por <c>WithValidateModel</c>, por símbolo:
    ///     deve retornar <c>bool</c> e ter um único parâmetro <c>out RoyalCode.SmartProblems.Problems</c>.
    ///     A validação semântica aceita aliases e nomes qualificados.
    /// </para>
    /// </summary>
    public static bool ValidateTypeWithHasProblemsMethod(
        this INamedTypeSymbol commandType,
        Location attributeLocation,
        out IMethodSymbol? hasProblemsMethod,
        out DiagnosticInfo? diagnostic)
    {
        // Tenta obter o método HasProblems
        hasProblemsMethod = commandType.GetMembers("HasProblems")
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        if (hasProblemsMethod is null)
        {
            diagnostic = DiagnosticInfo.Create(
                CmdDiagnostics.HasProblemsMethodNotFound,
                attributeLocation);
            return false;
        }

        var methodLocation = hasProblemsMethod.Locations.FirstOrDefault(l => l.IsInSource) ?? attributeLocation;

        // Valida o retorno do método HasProblems, deve retornar um bool
        if (hasProblemsMethod.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            diagnostic = DiagnosticInfo.Create(
                CmdDiagnostics.HasProblemsMethodDoesNotReturnBool,
                methodLocation);
            return false;
        }

        // valida o parâmetro: deve ter um, ser out e do tipo Problems (comparação por símbolo)
        var parameters = hasProblemsMethod.Parameters;
        if (parameters.Length != 1 ||
            parameters[0].RefKind != RefKind.Out ||
            !IsProblemsType(parameters[0].Type))
        {
            diagnostic = DiagnosticInfo.Create(
                CmdDiagnostics.HasProblemsMethodDoesNotHaveOutParameterProblems,
                methodLocation);
            return false;
        }

        diagnostic = null;
        return true;
    }

    private static bool IsProblemsType(ITypeSymbol type)
    {
        // quando 'Problems' não resolve, o compilador enlaça 'Problems?' como Nullable<Problems>
        // (não sabe se é reference type); desembrulha para inspecionar o tipo interno.
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } nullable)
            type = nullable.TypeArguments[0];

        // tipo não resolvido (em digitação/using ausente): aceita pelo nome escrito para não
        // gerar RCCMD004 enquanto o compilador já reporta o tipo desconhecido.
        if (type.TypeKind == TypeKind.Error)
            return type.Name == "Problems";

        return type.OriginalDefinition is INamedTypeSymbol named &&
            named.MetadataName == "Problems" &&
            named.ContainingNamespace is { IsGlobalNamespace: false } ns &&
            ns.ToDisplayString() == "RoyalCode.SmartProblems";
    }
}
