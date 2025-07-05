using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

public class FindInformation : IEquatable<FindInformation>, IMapEndpointGenerator
{
    private List<Diagnostic>? errors;

    public FindInformation(Diagnostic diagnostic)
    {
        errors = [diagnostic];
    }

    public FindInformation(
        TypeDescriptor entityType,
        TypeDescriptor idType,
        TypeDescriptor modelType,
        string endpointRoutePattern,
        string endpointName,
        string? description,
        string? groupName)
    {
        EntityType = entityType;
        IdType = idType;
        ModelType = modelType;
        EndpointRoutePattern = endpointRoutePattern;
        EndpointName = endpointName;
        Description = description;
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
    

    public bool Equals(FindInformation other)
    {
        if (other is null) 
            return false;

        return ReferenceEquals(this, other) || 
               EntityType.Equals(other.EntityType) && 
               ModelType.Equals(other.ModelType) && 
               EndpointRoutePattern == other.EndpointRoutePattern &&
               EndpointName == other.EndpointName &&
               Description == other.Description &&
               GroupName == other.GroupName &&
               EqualErrors(other);
    }

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
        hashCode = hashCode * -1521134295 + ModelType.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointRoutePattern.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointName.GetHashCode();
        hashCode = hashCode * -1521134295 + (Description?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (GroupName?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (errors?.GetHashCode() ?? 0);
        return hashCode;
    }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods)
    {
        if (errors is not null && errors.Count > 0)
        {
            errors.ForEach(spc.ReportDiagnostic);
            return;
        }

        return;

        var handlerMethodName = $"Find{EntityType.Name}HandleAsync";

        // Cria comando que invoca o método de mapeamento do handler
        var methodInvoke = GenerateMapMethodInvoke(this, handlerMethodName);
        var invokeCommand = new Command(methodInvoke);
        commands.Add(invokeCommand);

        // Cria o método do Handler.
        var handlerMethod = GenerateHandlerMethod(this, handlerMethodName);
        methods.Add(handlerMethod);
    }

    private static MethodInvokeGenerator GenerateMapMethodInvoke(FindInformation mapInfo, string handlerMethodName)
    {
        var methodInvoke = new MethodInvokeGenerator("group", $"MapGet");
        methodInvoke.AddArgument(mapInfo.EndpointRoutePattern);
        methodInvoke.AddArgument(handlerMethodName);

        methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithName", mapInfo.EndpointName)
        {
            LineIdent = true
        };

        if (mapInfo.Description is not null)
        {
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithDescription", mapInfo.Description)
            {
                LineIdent = true
            };
        }

        // por fim, chama WithOpenApi
        methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithOpenApi")
        {
            LineIdent = true
        };

        return methodInvoke;
    }

    private static MethodGenerator GenerateHandlerMethod(
        FindInformation mapInfo,
        string handlerMethodName)
    {
        // return type: Task<OkMatch<TModel>>
        var returnType = new TypeDescriptor(
            $"Task<OkMatch<{mapInfo.EntityType.Name}>>",
            ["System.Threading.Tasks", "RoyalCode.SmartCommands.HttpResults", ..mapInfo.EntityType.Namespaces]);

        var method = new MethodGenerator(handlerMethodName, returnType);
        method.Modifiers.Private();
        method.Modifiers.Static();
        method.Modifiers.Async();

        // atributo produce problems com NotFound
        var attribute = new AttributeGenerator("ProduceProblems", ["RoyalCode.SmartProblems"], "ProblemCategory.NotFound");
        method.Attributes.Add(attribute);

        // adiciona os parâmetros
        method.Parameters.InLine = false;

        // primeiro parâmetro: Id<TModel, TId>
        var idType = new TypeDescriptor(
            $"Id<{mapInfo.ModelType.Name}, {mapInfo.IdType.Name}>",
            ["RoyalCode.SmartProblems.Entities", ..mapInfo.ModelType.Namespaces, ..mapInfo.IdType.Namespaces]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(idType, "id")));

        // segundo parâmetro: IRepositoriesAccessor<TModel>
        var accessorType = new TypeDescriptor(
            $"IRepositoriesAccessor<{mapInfo.ModelType.Name}>",
            ["RoyalCode.SmartCommands", ..mapInfo.ModelType.Namespaces]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(accessorType, "accessor")));

        // terceiro parâmetro: CancellationToken
        var cancellationTokenType = new TypeDescriptor("CancellationToken", ["System.Threading"]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(cancellationTokenType, "ct")));

        // corpo do método




        throw new NotImplementedException("GenerateHandlerMethod is not implemented yet.");
    }
}
