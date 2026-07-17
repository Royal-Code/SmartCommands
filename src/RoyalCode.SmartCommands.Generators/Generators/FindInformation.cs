using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal class FindInformation : IEquatable<FindInformation>, IMapEndpointGenerator
{
    private readonly List<DiagnosticInfo>? errors;

    internal IReadOnlyList<DiagnosticInfo> Diagnostics => errors ?? (IReadOnlyList<DiagnosticInfo>)Array.Empty<DiagnosticInfo>();

    public FindInformation(DiagnosticInfo diagnostic)
    {
        errors = [diagnostic];
    }

    public FindInformation(List<DiagnosticInfo> diagnostics)
    {
        errors = diagnostics;
    }

    public FindInformation(
        TypeDescriptor entityType,
        TypeDescriptor idType,
        TypeDescriptor modelType,
        string endpointRoutePattern,
        string endpointName,
        string? description,
        string? summary,
        string[]? authorizationPolicies,
        string? groupName)
    {
        EntityType = entityType;
        IdType = idType;
        ModelType = modelType;
        EndpointRoutePattern = endpointRoutePattern;
        EndpointName = endpointName;
        Description = description;
        Summary = summary;
        AuthorizationPolicies = authorizationPolicies;
        GroupName = groupName;
    }

#nullable disable

    public TypeDescriptor EntityType { get; }

    public TypeDescriptor IdType { get; }

    public TypeDescriptor ModelType { get; }

    public string EndpointRoutePattern { get; }

    public string EndpointName { get; }

    public string GroupName { get; }

#nullable enable

    public string? Description { get; }

    public string? Summary { get; }

    public string[]? AuthorizationPolicies { get; set; }

    /// <summary>
    /// Localização do argumento do endpoint name no atributo. Uso exclusivo do transform (vira snapshot no
    /// modelo do pipeline); não participa da igualdade porque a informação é transitória.
    /// </summary>
    public Location? EndpointNameLocation { get; set; }

    public bool Equals(FindInformation other)
    {
        if (other is null)
            return false;

        return ReferenceEquals(this, other) ||
                EntityType.Equals(other.EntityType) &&
                IdType.Equals(other.IdType) &&
                ModelType.Equals(other.ModelType) &&
                EndpointRoutePattern == other.EndpointRoutePattern &&
                EndpointName == other.EndpointName &&
                Description == other.Description &&
                Summary == other.Summary &&
                GroupName == other.GroupName &&
                SequenceEqual(AuthorizationPolicies, other.AuthorizationPolicies) &&
                EqualErrors(other);
    }

    private static bool SequenceEqual(string[]? left, string[]? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);

    private bool EqualErrors(FindInformation other)
    {
        if (errors is null)
            return other.errors is null;

        if (other.errors is null)
            return false;

        return errors.SequenceEqual(other.errors);
    }

    public override bool Equals(object obj)
    {
        return obj is FindInformation fi && Equals(fi);
    }

    public override int GetHashCode()
    {
        int hashCode = -737078483;
        hashCode = hashCode * -1521134295 + EntityType.GetHashCode();
        hashCode = hashCode * -1521134295 + IdType.GetHashCode();
        hashCode = hashCode * -1521134295 + ModelType.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointRoutePattern.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointName.GetHashCode();
        hashCode = hashCode * -1521134295 + (Description?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (Summary?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (GroupName?.GetHashCode() ?? 0);
        if (errors is not null)
            foreach (var error in errors) hashCode = hashCode * -1521134295 + error.GetHashCode();
        if (AuthorizationPolicies is not null)
            foreach (var policy in AuthorizationPolicies) hashCode = hashCode * -1521134295 + policy.GetHashCode();
        return hashCode;
    }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods, bool withOpenApi)
    {
        if (errors is not null && errors.Count > 0)
        {
            errors.ForEach(error => spc.ReportDiagnostic(error.ToDiagnostic(CmdDiagnostics.Get)));
            return;
        }

        var handlerMethodName = $"Find{EntityType.Name}HandleAsync";

        // Cria comando que invoca o método de mapeamento do handler
        var methodInvoke = GenerateMapMethodInvoke(this, handlerMethodName, withOpenApi);
        var invokeCommand = new Command(methodInvoke);
        commands.Add(invokeCommand);

        // Cria o método do Handler.
        var handlerMethod = GenerateHandlerMethod(this, handlerMethodName);
        methods.Add(handlerMethod);
    }

    private static MethodInvokeGenerator GenerateMapMethodInvoke(FindInformation mapInfo, string handlerMethodName, bool withOpenApi)
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
        FindInformation mapInfo,
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

        // primeiro parâmetro: Id<TEntity, TId>
        var idType = new TypeDescriptor(
            $"Id<{mapInfo.EntityType.Name}, {mapInfo.IdType.Name}>",
            ["RoyalCode.SmartProblems.Entities", .. mapInfo.EntityType.Namespaces, .. mapInfo.IdType.Namespaces]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(idType, "id")));

        // segundo parâmetro: IRepositoryAccessor<TEntity>
        var accessorType = new TypeDescriptor(
            $"IRepositoryAccessor<{mapInfo.EntityType.Name}>",
            ["RoyalCode.SmartCommands", .. mapInfo.EntityType.Namespaces]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(accessorType, "accessor")));

        // terceiro parâmetro: CancellationToken
        var cancellationTokenType = new TypeDescriptor("CancellationToken", ["System.Threading"]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(cancellationTokenType, "ct")));

        // corpo do método

        // primeiro, chama o método FindEntityAsync<TDto, TId> do accessor
        var findInvoke = new MethodInvokeGenerator(
            "accessor", 
            $"FindEntityAsync<{mapInfo.ModelType.Name}, {mapInfo.IdType.Name}>");
        findInvoke.AddArgument("id");
        findInvoke.AddArgument("ct");
        findInvoke.Await = true;

        // a invocação é atribuída a uma variável chamada findResult
        var findResult = new AssignValueCommand(
            new StringValueNode($"var findResult"),
            findInvoke);

        method.Commands.Add(findResult);

        // segundo, verifica se o resultado não foi encontrado, fazendo o if com o método NotFound
        var notFoundInvoke = new MethodInvokeGenerator("findResult", "NotFound");
        notFoundInvoke.AddArgument("out var notfoundProblem");
        var ifCommand = new IfCommand(notFoundInvoke);
        ifCommand.AddCommand(new ReturnCommand("notfoundProblem"));


        method.Commands.Add(ifCommand);

        // terceiro, retorna o resultado encontrado
        var returnCommand = new ReturnCommand("findResult.Entity");

        method.Commands.Add(returnCommand);

        return method;
    }
}
