using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// <para>
///     Identificação semântica dos atributos do SmartCommands: a comparação é feita pelo metadata name
///     completo (namespace + nome + aridade) do <see cref="AttributeData.AttributeClass"/>, nunca pelo texto
///     da sintaxe — aliases, nomes qualificados e <c>global::</c> resolvem para o mesmo símbolo.
/// </para>
/// <para>
///     Quando o compilador não resolve o atributo (ex.: <c>[WithUnitOfWork]</c> sem argumento genérico produz
///     um tipo de erro), o fallback compara o nome simples escrito pelo usuário, preservando os diagnósticos
///     de uso malformado em vez de ignorar o atributo silenciosamente.
/// </para>
/// </summary>
internal readonly struct AttributeSpec
{
    internal AttributeSpec(string @namespace, string simpleName, int arity = 0)
    {
        Namespace = @namespace;
        SimpleName = simpleName;
        MetadataName = arity > 0 ? $"{simpleName}Attribute`{arity}" : $"{simpleName}Attribute";
    }

    internal string Namespace { get; }

    /// <summary>Nome sem o sufixo <c>Attribute</c> (ex.: <c>WithUnitOfWork</c>).</summary>
    internal string SimpleName { get; }

    /// <summary>Metadata name com aridade (ex.: <c>WithUnitOfWorkAttribute`1</c>).</summary>
    internal string MetadataName { get; }

    internal bool Matches(AttributeData attribute)
    {
        if (attribute.AttributeClass is not { } attributeClass)
            return false;

        if (attributeClass.TypeKind == TypeKind.Error)
        {
            // atributo não resolvido pelo compilador: casa pelo nome simples escrito pelo usuário para
            // preservar diagnósticos de uso malformado (ex.: [WithUnitOfWork] sem argumento genérico).
            return attributeClass.Name == SimpleName || attributeClass.Name == $"{SimpleName}Attribute";
        }

        var definition = attributeClass.OriginalDefinition;
        return definition.MetadataName == MetadataName
            && definition.ContainingNamespace is { } ns
            && !ns.IsGlobalNamespace
            && ns.ToDisplayString() == Namespace;
    }
}

internal static class KnownAttributes
{
    private const string CommandsNamespace = "RoyalCode.SmartCommands";
    private const string ProblemsNamespace = "RoyalCode.SmartProblems";

    internal static readonly AttributeSpec Command = new(CommandsNamespace, "Command");
    internal static readonly AttributeSpec CommandValidation = new(CommandsNamespace, "CommandValidation");
    internal static readonly AttributeSpec WithValidateModel = new(CommandsNamespace, "WithValidateModel");
    internal static readonly AttributeSpec WithDecorators = new(CommandsNamespace, "WithDecorators");
    internal static readonly AttributeSpec WithUnitOfWork = new(CommandsNamespace, "WithUnitOfWork", 1);
    internal static readonly AttributeSpec WithDbContext = new(CommandsNamespace, "WithDbContext");
    internal static readonly AttributeSpec WithWorkContext = new(CommandsNamespace, "WithWorkContext");
    internal static readonly AttributeSpec WithRetryOnConcurrency = new(CommandsNamespace, "WithRetryOnConcurrency");
    internal static readonly AttributeSpec WithTransaction = new(CommandsNamespace, "WithTransaction");
    internal static readonly AttributeSpec WithFindEntities = new(CommandsNamespace, "WithFindEntities", 1);
    internal static readonly AttributeSpec ProduceNewEntity = new(CommandsNamespace, "ProduceNewEntity");
    internal static readonly AttributeSpec EditEntity = new(CommandsNamespace, "EditEntity", 2);
    internal static readonly AttributeSpec WithParameter = new(CommandsNamespace, "WithParameter");
    internal static readonly AttributeSpec ProduceProblems = new(ProblemsNamespace, "ProduceProblems");
    internal static readonly AttributeSpec MemberNotNullWhen = new("System.Diagnostics.CodeAnalysis", "MemberNotNullWhen");

    internal static readonly AttributeSpec MapPost = new(CommandsNamespace, "MapPost");
    internal static readonly AttributeSpec MapPut = new(CommandsNamespace, "MapPut");
    internal static readonly AttributeSpec MapPatch = new(CommandsNamespace, "MapPatch");
    internal static readonly AttributeSpec MapDelete = new(CommandsNamespace, "MapDelete");
    internal static readonly AttributeSpec MapGet = new(CommandsNamespace, "MapGet");
    internal static readonly AttributeSpec MapGroup = new(CommandsNamespace, "MapGroup");
    internal static readonly AttributeSpec MapCreatedRoute = new(CommandsNamespace, "MapCreatedRoute");
    internal static readonly AttributeSpec MapAcceptedRoute = new(CommandsNamespace, "MapAcceptedRoute");
    internal static readonly AttributeSpec MapIdResultValue = new(CommandsNamespace, "MapIdResultValue");
    internal static readonly AttributeSpec MapResponseValues = new(CommandsNamespace, "MapResponseValues");
    internal static readonly AttributeSpec WithDescription = new(CommandsNamespace, "WithDescription");
    internal static readonly AttributeSpec WithSummary = new(CommandsNamespace, "WithSummary");
    internal static readonly AttributeSpec WithAuthorization = new(CommandsNamespace, "WithAuthorization");
    internal static readonly AttributeSpec WithPolicy = new(CommandsNamespace, "WithPolicy");
    internal static readonly AttributeSpec WithOpenApi = new(CommandsNamespace, "WithOpenApi");
    internal static readonly AttributeSpec WithEndpointFilter = new(CommandsNamespace, "WithEndpointFilter", 1);
    internal static readonly AttributeSpec WithResultStatus = new(CommandsNamespace, "WithResultStatus");
    internal static readonly AttributeSpec WithTags = new(CommandsNamespace, "WithTags");

    internal static readonly AttributeSpec MapApiHandlers = new(CommandsNamespace, "MapApiHandlers");
    internal static readonly AttributeSpec AddHandlersServices = new(CommandsNamespace, "AddHandlersServices");

    internal static readonly AttributeSpec MapFind = new(CommandsNamespace, "MapFind");
    internal static readonly AttributeSpec EntityReference = new(CommandsNamespace, "EntityReference", 2);
    internal static readonly AttributeSpec MapSearch = new(CommandsNamespace, "MapSearch");
    internal static readonly AttributeSpec SearchReference1 = new(CommandsNamespace, "SearchReference", 1);
    internal static readonly AttributeSpec SearchReference2 = new(CommandsNamespace, "SearchReference", 2);
    internal static readonly AttributeSpec WithFilter = new(CommandsNamespace, "WithFilter");

    /// <summary>Todos os atributos <c>Map*</c> de verbo HTTP, na ordem de precedência de leitura.</summary>
    internal static readonly (AttributeSpec Spec, string HttpMethod)[] MapVerbs =
    [
        (MapPost, "Post"),
        (MapPut, "Put"),
        (MapPatch, "Patch"),
        (MapDelete, "Delete"),
        (MapGet, "Get"),
    ];

    internal static bool TryGet(ISymbol symbol, AttributeSpec spec, out AttributeData? attribute)
    {
        foreach (var candidate in symbol.GetAttributes())
        {
            if (spec.Matches(candidate))
            {
                attribute = candidate;
                return true;
            }
        }

        attribute = null;
        return false;
    }

    internal static bool Has(ISymbol symbol, AttributeSpec spec) => TryGet(symbol, spec, out _);

    /// <summary>
    /// Localização da aplicação do atributo no código do usuário, com fallback para
    /// <paramref name="fallback"/> quando a sintaxe não está disponível.
    /// </summary>
    internal static Location GetLocation(AttributeData attribute, CancellationToken cancellationToken, Location fallback) =>
        attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation() ?? fallback;

    /// <summary>
    /// Localização do argumento <paramref name="argumentIndex"/> do atributo, com fallback para a
    /// localização do próprio atributo.
    /// </summary>
    internal static Location GetArgumentLocation(
        AttributeData attribute,
        int argumentIndex,
        CancellationToken cancellationToken,
        Location fallback)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) is AttributeSyntax
            {
                ArgumentList.Arguments: { } arguments
            })
        {
            var parameterName = attribute.AttributeConstructor?.Parameters.Length > argumentIndex
                ? attribute.AttributeConstructor.Parameters[argumentIndex].Name
                : null;

            if (parameterName is not null)
            {
                var namedArgument = arguments.FirstOrDefault(argument =>
                    argument.NameColon?.Name.Identifier.ValueText == parameterName);
                if (namedArgument is not null)
                    return namedArgument.GetLocation();
            }

            var positionalArguments = arguments
                .Where(argument => argument.NameColon is null && argument.NameEquals is null)
                .ToArray();
            if (argumentIndex < positionalArguments.Length)
                return positionalArguments[argumentIndex].GetLocation();
        }

        return GetLocation(attribute, cancellationToken, fallback);
    }

    /// <summary>O valor string de um argumento constante; <see langword="null"/> quando não é constante válida.</summary>
    internal static string? GetString(TypedConstant constant) =>
        constant is { Kind: TypedConstantKind.Primitive, Value: string value } ? value : null;

    /// <summary>
    /// Extrai os valores string de um argumento que pode ser um único valor, um array explícito ou
    /// um <c>params</c> expandido. Valores não constantes são ignorados.
    /// </summary>
    internal static IEnumerable<string> GetStrings(TypedConstant constant)
    {
        return TryGetStrings(constant, out var values) ? values : [];
    }

    /// <summary>
    /// Tenta extrair uma string ou array de strings constante e não nulo. Diferentemente de
    /// <see cref="GetStrings"/>, permite ao reader distinguir uma coleção vazia válida de um valor inválido.
    /// </summary>
    internal static bool TryGetStrings(TypedConstant constant, out string[] values)
    {
        if (constant.Kind == TypedConstantKind.Array)
        {
            if (constant.IsNull || constant.Values.IsDefault)
            {
                values = [];
                return false;
            }

            values = new string[constant.Values.Length];
            for (var index = 0; index < constant.Values.Length; index++)
            {
                if (GetString(constant.Values[index]) is not { } value)
                {
                    values = [];
                    return false;
                }

                values[index] = value;
            }

            return true;
        }

        if (GetString(constant) is { } single)
        {
            values = [single];
            return true;
        }

        values = [];
        return false;
    }

    /// <summary>Comparação semântica de tipo por namespace + metadata name (nunca por nome simples).</summary>
    internal static bool IsType(ITypeSymbol type, string @namespace, string metadataName) =>
        type.OriginalDefinition is INamedTypeSymbol named &&
        named.MetadataName == metadataName &&
        named.ContainingNamespace is { IsGlobalNamespace: false } ns &&
        ns.ToDisplayString() == @namespace;

    /// <summary>
    /// Formata um argumento de enum como <c>NomeDoEnum.Membro</c> para reemissão em código gerado; valores
    /// combinados (flags) sem membro nomeado são emitidos como cast <c>(NomeDoEnum)valor</c>.
    /// Retorna <see langword="null"/> quando o argumento não é uma constante de enum válida.
    /// </summary>
    internal static string? FormatEnumMember(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Enum ||
            constant.Type is not INamedTypeSymbol enumType ||
            constant.Value is null)
        {
            return null;
        }

        var member = enumType.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(field => field.HasConstantValue && Equals(field.ConstantValue, constant.Value));

        return member is not null
            ? $"{enumType.Name}.{member.Name}"
            : $"({enumType.Name}){Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture)}";
    }
}
