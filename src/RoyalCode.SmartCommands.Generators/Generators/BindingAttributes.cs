using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Collections;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.SmartCommands.Generators.Models;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// Resultado da captura semântica dos atributos de binding de um parâmetro externo: os bindings suportados
/// e os fatos necessários para diagnóstico (múltiplas fontes explícitas e uso de <c>[AsParameters]</c>).
/// </summary>
internal readonly struct CapturedBindings
{
    internal CapturedBindings(EquatableArray<ParameterBindingModel> bindings, int sourceCount, bool hasAsParameters)
    {
        Bindings = bindings;
        SourceCount = sourceCount;
        HasAsParameters = hasAsParameters;
    }

    internal EquatableArray<ParameterBindingModel> Bindings { get; }

    internal int SourceCount { get; }

    internal bool HasAsParameters { get; }
}

/// <summary>
/// <para>
///     Leitura semântica dos atributos de binding do ASP.NET Core aplicados a parâmetros externos
///     (<c>[WithParameter]</c> de comandos e parâmetros de filtros do Search). DF3: os atributos suportados
///     são copiados somente para o parâmetro do delegate Minimal API, nunca para a interface do handler.
///     Sem atributo explícito, o ASP.NET Core infere a fonte (DF2).
/// </para>
/// </summary>
internal static class BindingAttributes
{
    private const string MvcNamespace = "Microsoft.AspNetCore.Mvc";

    // fontes de binding suportadas; Name é lido como named argument quando presente
    private static readonly (AttributeSpec Spec, string EmitName)[] Sources =
    [
        (new AttributeSpec(MvcNamespace, "FromRoute"), "FromRoute"),
        (new AttributeSpec(MvcNamespace, "FromQuery"), "FromQuery"),
        (new AttributeSpec(MvcNamespace, "FromHeader"), "FromHeader"),
        (new AttributeSpec(MvcNamespace, "FromForm"), "FromForm"),
        (new AttributeSpec(MvcNamespace, "FromBody"), "FromBody"),
        (new AttributeSpec(MvcNamespace, "FromServices"), "FromServices"),
    ];

    private static readonly AttributeSpec AsParameters = new("Microsoft.AspNetCore.Http", "AsParameters");

    /// <summary>Captura os bindings do parâmetro sem reportar diagnósticos (a validação é do chamador).</summary>
    internal static CapturedBindings Capture(IParameterSymbol parameter)
    {
        List<ParameterBindingModel>? bindings = null;
        var hasAsParameters = false;

        foreach (var attribute in parameter.GetAttributes())
        {
            if (AsParameters.Matches(attribute))
            {
                hasAsParameters = true;
                continue;
            }

            foreach (var (spec, emitName) in Sources)
            {
                if (!spec.Matches(attribute))
                    continue;

                var name = attribute.NamedArguments
                    .Where(argument => argument.Key == "Name")
                    .Select(argument => KnownAttributes.GetString(argument.Value))
                    .FirstOrDefault();

                (bindings ??= []).Add(new ParameterBindingModel(emitName, name));
                break;
            }
        }

        var sourceCount = bindings?.Count ?? 0;
        return new CapturedBindings(
            bindings is null ? default : new EquatableArray<ParameterBindingModel>(bindings),
            sourceCount,
            hasAsParameters);
    }

    /// <summary>
    /// Valida os bindings capturados de um parâmetro que participa de um endpoint mapeado: uma única fonte
    /// explícita, sem <c>[AsParameters]</c>, e <c>FromRoute</c> apontando para um parâmetro existente no
    /// template (grupo + rota).
    /// </summary>
    internal static void Validate(
        CapturedBindings captured,
        string parameterName,
        Location location,
        string routePattern,
        string? groupName,
        List<DiagnosticInfo> errors)
    {
        if (captured.HasAsParameters)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.AsParametersNotSupported, location, parameterName));
        }

        if (captured.SourceCount > 1)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.ConflictingBindingSources, location, parameterName));
        }

        foreach (var binding in captured.Bindings)
        {
            if (binding.Attribute != "FromRoute")
                continue;

            var routeName = binding.Name ?? parameterName;
            var template = groupName is null ? routePattern : $"{groupName}/{routePattern}";
            var exists = RoutePatternParser.Parse(template)
                .Any(parameter => string.Equals(parameter.Name, routeName, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.RouteParameterNotInTemplate, location, routeName, parameterName));
            }
        }
    }
}
