using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using System.Text;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// Uma propriedade da chave alternativa/composta de <c>MapFindBy</c> (DF10): o nome direto da propriedade da
/// entidade e o tipo derivado semanticamente, que vira o parâmetro de rota do handler gerado.
/// </summary>
internal sealed class FindByProperty : IEquatable<FindByProperty>
{
    public FindByProperty(string name, TypeDescriptor type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }

    public TypeDescriptor Type { get; }

    public bool Equals(FindByProperty? other) =>
        other is not null && Name == other.Name && Type.Equals(other.Type);

    public override bool Equals(object? obj) => obj is FindByProperty other && Equals(other);

    public override int GetHashCode() => Name.GetHashCode() * -1521134295 + Type.GetHashCode();
}

/// <summary>
/// Modelo de emissão do <c>MapFindBy</c> (Fase 5): busca por chave alternativa/composta projetada em DTO.
/// Espelha <see cref="FindInformation"/> (por ID), mas monta um filtro de igualdade a partir das propriedades
/// nomeadas e a lista de <c>FindCriterion</c> na ordem declarada, chamando a variante por predicado do
/// <c>IRepositoryAccessor&lt;TEntity&gt;</c> (contrato da Fase 4).
/// </summary>
internal class FindByInformation : IEquatable<FindByInformation>, IMapEndpointGenerator
{
    private readonly List<DiagnosticInfo>? errors;

    internal IReadOnlyList<DiagnosticInfo> Diagnostics =>
        errors ?? (IReadOnlyList<DiagnosticInfo>)Array.Empty<DiagnosticInfo>();

    public FindByInformation(DiagnosticInfo diagnostic)
    {
        errors = [diagnostic];
    }

    public FindByInformation(List<DiagnosticInfo> diagnostics)
    {
        errors = diagnostics;
    }

    public FindByInformation(
        TypeDescriptor entityType,
        TypeDescriptor modelType,
        IReadOnlyList<FindByProperty> properties,
        string endpointRoutePattern,
        string endpointName,
        string? description,
        string? summary,
        string[]? authorizationPolicies,
        string? groupName,
        string[]? endpointFilters = null,
        string[]? tags = null)
    {
        EntityType = entityType;
        ModelType = modelType;
        Properties = properties;
        EndpointRoutePattern = endpointRoutePattern;
        EndpointName = endpointName;
        Description = description;
        Summary = summary;
        AuthorizationPolicies = authorizationPolicies;
        GroupName = groupName;
        EndpointFilters = endpointFilters;
        Tags = tags;
    }

#nullable disable

    public TypeDescriptor EntityType { get; }

    public TypeDescriptor ModelType { get; }

    public IReadOnlyList<FindByProperty> Properties { get; }

    public string EndpointRoutePattern { get; }

    public string EndpointName { get; }

    public string GroupName { get; }

#nullable enable

    public string? Description { get; }

    public string? Summary { get; }

    public string[]? AuthorizationPolicies { get; set; }

    /// <summary>Filtros de endpoint (DF23), nomes globalmente qualificados, na ordem declarada.</summary>
    public string[]? EndpointFilters { get; }

    /// <summary>Tags OpenAPI (DF23), na ordem declarada.</summary>
    public string[]? Tags { get; }

    /// <summary>
    /// Localização do argumento do endpoint name no atributo. Uso exclusivo do transform (vira snapshot no
    /// modelo do pipeline); não participa da igualdade porque a informação é transitória.
    /// </summary>
    public Location? EndpointNameLocation { get; set; }

    public bool Equals(FindByInformation? other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) ||
                EntityType.Equals(other.EntityType) &&
                ModelType.Equals(other.ModelType) &&
                (Properties?.SequenceEqual(other.Properties) ?? other.Properties is null) &&
                EndpointRoutePattern == other.EndpointRoutePattern &&
                EndpointName == other.EndpointName &&
                Description == other.Description &&
                Summary == other.Summary &&
                GroupName == other.GroupName &&
                SequenceEqual(AuthorizationPolicies, other.AuthorizationPolicies) &&
                SequenceEqual(EndpointFilters, other.EndpointFilters) &&
                SequenceEqual(Tags, other.Tags) &&
                EqualErrors(other);
    }

    private static bool SequenceEqual(string[]? left, string[]? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);

    private bool EqualErrors(FindByInformation other)
    {
        if (errors is null)
            return other.errors is null;

        if (other.errors is null)
            return false;

        return errors.SequenceEqual(other.errors);
    }

    public override bool Equals(object obj)
    {
        return obj is FindByInformation fi && Equals(fi);
    }

    public override int GetHashCode()
    {
        int hashCode = 197542013;
        hashCode = hashCode * -1521134295 + EntityType.GetHashCode();
        hashCode = hashCode * -1521134295 + ModelType.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointRoutePattern.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointName.GetHashCode();
        hashCode = hashCode * -1521134295 + (Description?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (Summary?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (GroupName?.GetHashCode() ?? 0);
        if (Properties is not null)
            foreach (var property in Properties) hashCode = hashCode * -1521134295 + property.GetHashCode();
        if (errors is not null)
            foreach (var error in errors) hashCode = hashCode * -1521134295 + error.GetHashCode();
        if (AuthorizationPolicies is not null)
            foreach (var policy in AuthorizationPolicies) hashCode = hashCode * -1521134295 + policy.GetHashCode();
        if (EndpointFilters is not null)
            foreach (var filter in EndpointFilters) hashCode = hashCode * -1521134295 + filter.GetHashCode();
        if (Tags is not null)
            foreach (var tag in Tags) hashCode = hashCode * -1521134295 + tag.GetHashCode();
        return hashCode;
    }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods, bool withOpenApi)
    {
        if (errors is not null && errors.Count > 0)
        {
            errors.ForEach(error => spc.ReportDiagnostic(error.ToDiagnostic(CmdDiagnostics.Get)));
            return;
        }

        var handlerMethodName = HandlerMethodName();

        // Cria comando que invoca o método de mapeamento do handler
        var methodInvoke = GenerateMapMethodInvoke(this, handlerMethodName, withOpenApi);
        var invokeCommand = new Command(methodInvoke);
        commands.Add(invokeCommand);

        // Cria o método do Handler.
        var handlerMethod = GenerateHandlerMethod(this, handlerMethodName);
        methods.Add(handlerMethod);
    }

    /// <summary>
    /// Nome do método handler gerado. Baseia-se no nome do <b>DTO</b> (validado top-level e não genérico,
    /// portanto um identificador simples e seguro), o que evita colisões artificiais entre chaves cujas
    /// propriedades concatenadas coincidem e entre dois DTOs que projetam a mesma entidade pela mesma chave —
    /// cada DTO declara no máximo um <c>MapFindBy</c>. As propriedades entram como sufixo de legibilidade
    /// (embutidas em um identificador maior, seguras mesmo se forem keywords). Colisões reais (dois DTOs de
    /// mesmo nome simples no mesmo grupo) continuam sendo diagnosticadas por RCCMD047.
    /// </summary>
    internal string HandlerMethodName() =>
        $"Find{ModelType.Name}By{string.Concat(Properties.Select(p => p.Name))}Async";

    private static MethodInvokeGenerator GenerateMapMethodInvoke(FindByInformation mapInfo, string handlerMethodName, bool withOpenApi)
    {
        // os valores vêm dos TypedConstants (sem aspas); a emissão os formata como literais C#
        var methodInvoke = new MethodInvokeGenerator("group", $"MapGet");
        methodInvoke.AddArgument(SymbolDisplay.FormatLiteral(mapInfo.EndpointRoutePattern, quote: true));
        methodInvoke.AddArgument(handlerMethodName);

        methodInvoke = new MethodInvokeGenerator(
            methodInvoke, "WithName", SymbolDisplay.FormatLiteral(mapInfo.EndpointName, quote: true))
        {
            LineIdent = true
        };

        if (mapInfo.Description is not null)
        {
            methodInvoke = new MethodInvokeGenerator(
                methodInvoke, "WithDescription", SymbolDisplay.FormatLiteral(mapInfo.Description, quote: true))
            {
                LineIdent = true
            };
        }

        if (mapInfo.Summary is not null)
        {
            methodInvoke = new MethodInvokeGenerator(
                methodInvoke, "WithSummary", SymbolDisplay.FormatLiteral(mapInfo.Summary, quote: true))
            {
                LineIdent = true
            };
        }

        // tags e filtros (DF23), na ordem declarada
        methodInvoke = EndpointExtensibility.EmitTags(methodInvoke, mapInfo.Tags);
        methodInvoke = EndpointExtensibility.EmitFilters(methodInvoke, mapInfo.EndpointFilters);

        if (mapInfo.AuthorizationPolicies is not null)
        {
            // adiciona os AuthorizationPolicies, se houver
            ArgumentsGenerator arguments = new();
            foreach (var policy in mapInfo.AuthorizationPolicies)
            {
                arguments.AddArgument(SymbolDisplay.FormatLiteral(policy, quote: true));
            }
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "RequireAuthorization", arguments)
            {
                LineIdent = true
            };
        }

        // por fim, chama WithOpenApi
        if (withOpenApi)
        {
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithOpenApi")
            {
                LineIdent = true
            };
        }

        return methodInvoke;
    }

    private static MethodGenerator GenerateHandlerMethod(
        FindByInformation mapInfo,
        string handlerMethodName)
    {
        // return type: Task<OkMatch<TModel>>
        var returnType = new TypeDescriptor(
            $"Task<OkMatch<{mapInfo.ModelType.Name}>>",
            ["System.Threading.Tasks", "RoyalCode.SmartProblems.HttpResults", .. mapInfo.ModelType.Namespaces]);

        var method = new MethodGenerator(handlerMethodName, returnType);
        method.Modifiers.Private();
        method.Modifiers.Static();
        method.Modifiers.Async();

        // atributo produce problems com NotFound
        var attribute = new AttributeGenerator("ProduceProblems", ["RoyalCode.SmartProblems"], "ProblemCategory.NotFound");
        method.Attributes.Add(attribute);

        // adiciona os parâmetros
        method.Parameters.InLine = false;

        // primeiro, um parâmetro por propriedade da chave (vinculado à rota pelo nome), na ordem declarada;
        // o nome é escapado com '@' quando é keyword C# (o binding do ASP.NET usa o nome sem o '@')
        foreach (var property in mapInfo.Properties)
            method.Parameters.Add(new ParameterGenerator(
                new ParameterDescriptor(property.Type, EscapeIdentifier(property.Name))));

        // depois: IRepositoryAccessor<TEntity>
        var accessorType = new TypeDescriptor(
            $"IRepositoryAccessor<{mapInfo.EntityType.Name}>",
            ["RoyalCode.SmartCommands", .. mapInfo.EntityType.Namespaces]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(accessorType, "accessor")));

        // por fim: CancellationToken
        var cancellationTokenType = new TypeDescriptor("CancellationToken", ["System.Threading"]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(cancellationTokenType, "ct")));

        // corpo do método

        // chama a variante por predicado do accessor: FindEntityAsync<TDto>(filter, criteria, ct)
        var findInvoke = new MethodInvokeGenerator(
            "accessor",
            $"FindEntityAsync<{mapInfo.ModelType.Name}>");
        findInvoke.AddArgument(new StringValueNode(BuildFilterLambda(mapInfo.Properties)));
        findInvoke.AddArgument(new StringValueNode(BuildCriteria(mapInfo.Properties)));
        findInvoke.AddArgument("ct");
        findInvoke.Await = true;

        // a invocação é atribuída a uma variável chamada findResult
        var findResult = new AssignValueCommand(
            new StringValueNode($"var findResult"),
            findInvoke);

        method.Commands.Add(findResult);

        // verifica se o resultado não foi encontrado, retornando o problema NotFound rico (nomeia a entidade)
        var notFoundInvoke = new MethodInvokeGenerator("findResult", "NotFound");
        notFoundInvoke.AddArgument("out var notfoundProblem");
        var ifCommand = new IfCommand(notFoundInvoke);
        ifCommand.AddCommand(new ReturnCommand("notfoundProblem"));

        method.Commands.Add(ifCommand);

        // retorna o DTO projetado
        var returnCommand = new ReturnCommand("findResult.Entity");

        method.Commands.Add(returnCommand);

        return method;
    }

    /// <summary>
    /// Monta a lambda de filtro <c>e =&gt; e.Prop1 == prop1 &amp;&amp; e.Prop2 == prop2</c> a partir das
    /// propriedades da chave (AND, na ordem declarada). O provider traduz a expressão; nada é materializado.
    /// </summary>
    private static string BuildFilterLambda(IReadOnlyList<FindByProperty> properties)
    {
        var sb = new StringBuilder("e => ");
        for (var i = 0; i < properties.Count; i++)
        {
            if (i > 0)
                sb.Append(" && ");
            // 'e.Prop' acessa o membro (nome real, escapado se keyword) e o operando é o parâmetro escapado
            var identifier = EscapeIdentifier(properties[i].Name);
            sb.Append("e.").Append(identifier).Append(" == ").Append(identifier);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Monta o array de <c>FindCriterion</c> (nome da propriedade + valor vinculado), na ordem declarada,
    /// usado pelo contrato da Fase 4 para gerar o problema <c>NotFound</c> rico sem analisar a expressão.
    /// </summary>
    private static string BuildCriteria(IReadOnlyList<FindByProperty> properties)
    {
        const string criterion = "global::RoyalCode.SmartProblems.Entities.FindCriterion";
        var sb = new StringBuilder();
        sb.Append("new ").Append(criterion).Append("[] { ");
        for (var i = 0; i < properties.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            // o primeiro argumento é o nome real da propriedade (string literal); o segundo é o parâmetro
            // (identificador escapado se keyword)
            sb.Append("new ").Append(criterion).Append('(')
                .Append(SymbolDisplay.FormatLiteral(properties[i].Name, quote: true))
                .Append(", ").Append(EscapeIdentifier(properties[i].Name)).Append(')');
        }
        sb.Append(" }");
        return sb.ToString();
    }

    /// <summary>
    /// Escapa um nome de propriedade para uso como identificador C# no código gerado: prefixa <c>@</c> quando
    /// o nome é uma keyword reservada (ex.: <c>class</c> → <c>@class</c>). O binding de rota do ASP.NET Core
    /// usa o nome sem o <c>@</c>, então a correspondência com o placeholder é preservada.
    /// </summary>
    private static string EscapeIdentifier(string name) =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(name)
            != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ||
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetContextualKeywordKind(name)
            != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? "@" + name
            : name;
}
