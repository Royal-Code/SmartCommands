using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// <para>
///     Regras compartilhadas de nomes de endpoint e de grupo (Fase 9): endpoint names não podem ser vazios
///     (RCCMD044) e o prefixo de rota de <c>MapGroup</c> precisa derivar um identificador C# válido para a
///     classe/método gerados do grupo (RCCMD045).
/// </para>
/// <para>
///     A derivação do nome da classe do grupo (<c>Map{Nome}Api</c>/<c>Map{Nome}Group</c>) vive aqui para que
///     transform, agregação (detecção de colisão) e emissão usem exatamente a mesma normalização.
/// </para>
/// </summary>
internal static class EndpointNameRules
{
    /// <summary>Valida que o endpoint name declarado não é vazio nem apenas espaços (RCCMD044).</summary>
    internal static void ValidateEndpointName(
        string endpointName,
        string attributeName,
        Location location,
        List<DiagnosticInfo> errors)
    {
        if (string.IsNullOrWhiteSpace(endpointName))
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidEndpointName, location, attributeName));
    }

    /// <summary>
    /// Valida o prefixo de rota do grupo: não vazio e capaz de derivar um identificador C# válido
    /// (RCCMD045). A validação impede que a emissão gere classes/métodos com nomes inválidos ou que a
    /// normalização em PascalCase falhe (segmentos vazios).
    /// </summary>
    internal static void ValidateGroupName(string groupName, Location location, List<DiagnosticInfo> errors)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidGroupName,
                location,
                groupName,
                "the route prefix must not be empty or whitespace"));
            return;
        }

        if (!TryCreatePascalIdentifier(groupName, out _, out var reason))
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidGroupName,
                location,
                groupName,
                reason!));
        }
    }

    /// <summary>
    /// O nome da classe do grupo gerada (<c>Map{Nome}Api</c>) para um prefixo de grupo (ou o nome do host
    /// quando o grupo é nulo). Retorna <see langword="null"/> quando o nome não pode ser derivado — nesse
    /// caso o transform já produziu RCCMD045 e a entrada não chega à emissão.
    /// </summary>
    internal static string? GroupClassName(string? groupName, string hostFallbackName)
    {
        var source = groupName ?? hostFallbackName;
        if (!TryCreatePascalIdentifier(source, out var pascal, out _))
            return null;

        return pascal!.EndsWith("Api", StringComparison.Ordinal)
            ? $"Map{pascal}"
            : $"Map{pascal}Api";
    }

    /// <summary>O nome PascalCase usado no método <c>Map{Nome}Group</c>; mesma normalização da classe.</summary>
    internal static string? GroupPascalName(string? groupName, string hostFallbackName)
    {
        var source = groupName ?? hostFallbackName;
        return TryCreatePascalIdentifier(source, out var pascal, out _) ? pascal : null;
    }

    /// <summary>
    /// <para>
    ///     Normaliza um prefixo de grupo em PascalCase (separadores <c>-</c> e <c>/</c>) para formar um
    ///     identificador C# válido. Segmentos vazios (barra final, separadores repetidos) são ignorados;
    ///     segmentos que são parâmetros de rota (<c>{personId:int}</c>) usam o nome da variável; demais
    ///     caracteres que não formam identificador são removidos.
    /// </para>
    /// <para>
    ///     Falha somente quando nada resta para nomear a classe — prefixos distintos que normalizam para o
    ///     mesmo nome são tratados na agregação (RCCMD046).
    /// </para>
    /// </summary>
    private static bool TryCreatePascalIdentifier(string value, out string? pascal, out string? reason)
    {
        pascal = null;
        reason = null;

        var words = value.Split('-', '/');
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var word in words)
        {
            var cleaned = CleanSegment(word);
            if (cleaned.Length == 0)
                continue;

            builder.Append(char.ToUpperInvariant(cleaned[0]));
            if (cleaned.Length > 1)
                builder.Append(cleaned, 1, cleaned.Length - 1);
        }

        var candidate = builder.ToString();
        if (candidate.Length == 0)
        {
            reason = "the route prefix does not contain any character usable in the generated group class name";
            return false;
        }

        pascal = candidate;
        return true;
    }

    /// <summary>
    /// Limpa um segmento do prefixo: parâmetros de rota viram o nome da variável (sem catch-all,
    /// constraint, default ou marcador opcional) e caracteres fora de letra/dígito/sublinhado são removidos.
    /// </summary>
    private static string CleanSegment(string segment)
    {
        var text = segment;
        if (text.Length >= 2 && text[0] == '{' && text[text.Length - 1] == '}')
        {
            text = text.Substring(1, text.Length - 2).TrimStart('*');
            var cut = text.IndexOfAny([':', '=']);
            if (cut >= 0)
                text = text.Substring(0, cut);
            text = text.TrimEnd('?');
        }

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                builder.Append(c);
        }

        return builder.ToString();
    }
}
