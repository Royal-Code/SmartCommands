namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// Um parâmetro declarado em um route pattern do ASP.NET Core (ex.: <c>{id:int?}</c>, <c>{**slug}</c>).
/// </summary>
internal sealed record RoutePatternParameter(
    string Name,
    string? Constraint,
    string? DefaultValue,
    bool IsOptional,
    bool IsCatchAll);

/// <summary>
/// <para>
///     Parser único de route patterns do Minimal API: extrai nomes de parâmetros, constraints, catch-all
///     (<c>*</c>/<c>**</c>), opcionalidade (<c>?</c>) e valores default (<c>=</c>), respeitando os escapes
///     <c>{{</c>/<c>}}</c> e constraints com argumentos (ex.: <c>{id:regex(^\\d{{4}}$)}</c>).
/// </para>
/// <para>
///     Todo código do generator que precisa inspecionar rotas deve usar este parser, nunca
///     <c>IndexOf</c>/<c>Substring</c> espalhados.
/// </para>
/// </summary>
internal static class RoutePatternParser
{
    /// <summary>O nome do primeiro parâmetro do pattern, ou <see langword="null"/> quando não há parâmetros.</summary>
    internal static string? FirstParameterName(string routePattern)
    {
        foreach (var parameter in Parse(routePattern))
            return parameter.Name;
        return null;
    }

    internal static IReadOnlyList<RoutePatternParameter> Parse(string routePattern)
    {
        var parameters = new List<RoutePatternParameter>();
        var index = 0;

        while (index < routePattern.Length)
        {
            if (routePattern[index] != '{')
            {
                index++;
                continue;
            }

            // '{{' é escape de literal
            if (index + 1 < routePattern.Length && routePattern[index + 1] == '{')
            {
                index += 2;
                continue;
            }

            var close = FindClosingBrace(routePattern, index + 1);
            if (close < 0)
                break;

            var content = routePattern.Substring(index + 1, close - index - 1);
            if (TryParseParameter(content, out var parameter))
                parameters.Add(parameter!);

            index = close + 1;
        }

        return parameters;
    }

    private static int FindClosingBrace(string pattern, int start)
    {
        // constraints podem conter chaves balanceadas em argumentos (ex.: regex(^\d{4}$) escapa como {{4}});
        // percorre respeitando profundidade e os escapes '{{'/'}}'
        var depth = 1;
        for (var i = start; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                // '}}' dentro do conteúdo é escape de literal
                if (i + 1 < pattern.Length && pattern[i + 1] == '}' && depth == 1)
                {
                    i++;
                    continue;
                }

                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static bool TryParseParameter(string content, out RoutePatternParameter? parameter)
    {
        parameter = null;
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var isCatchAll = false;
        var start = 0;
        while (start < content.Length && content[start] == '*')
        {
            isCatchAll = true;
            start++;
        }

        // separa nome de constraint/default; o default pode aparecer após a constraint
        var nameEnd = content.Length;
        string? constraint = null;
        string? defaultValue = null;

        var constraintStart = IndexOfTopLevel(content, ':', start);
        var defaultStart = IndexOfTopLevel(content, '=', start);

        if (constraintStart >= 0 && (defaultStart < 0 || constraintStart < defaultStart))
        {
            nameEnd = constraintStart;
            var constraintEnd = defaultStart >= 0 ? defaultStart : content.Length;
            constraint = content.Substring(constraintStart + 1, constraintEnd - constraintStart - 1);
        }
        else if (defaultStart >= 0)
        {
            nameEnd = defaultStart;
        }

        if (defaultStart >= 0)
            defaultValue = content.Substring(defaultStart + 1);

        var name = content.Substring(start, nameEnd - start);

        var isOptional = name.EndsWith("?", StringComparison.Ordinal);
        if (isOptional)
            name = name.Substring(0, name.Length - 1);

        // constraint também pode carregar o '?' final (ex.: {id:int?})
        if (constraint is not null && constraint.EndsWith("?", StringComparison.Ordinal))
        {
            isOptional = true;
            constraint = constraint.Substring(0, constraint.Length - 1);
        }

        name = name.Trim();
        if (name.Length == 0)
            return false;

        parameter = new RoutePatternParameter(
            name,
            string.IsNullOrWhiteSpace(constraint) ? null : constraint,
            string.IsNullOrWhiteSpace(defaultValue) ? null : defaultValue,
            isOptional,
            isCatchAll);
        return true;
    }

    private static int IndexOfTopLevel(string content, char target, int start)
    {
        // ignora o separador dentro de argumentos de constraint (parênteses), ex.: {id:regex(a=b)}
        var parenthesisDepth = 0;
        for (var i = start; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '(')
                parenthesisDepth++;
            else if (c == ')')
                parenthesisDepth--;
            else if (c == target && parenthesisDepth == 0)
                return i;
        }

        return -1;
    }
}
