using Microsoft.CodeAnalysis;
using RoyalCode.SmartCommands.Generators.Commands;
using System.Text;

namespace RoyalCode.SmartCommands.Generators.Generators;

public sealed class MapInformation : IEquatable<MapInformation>, IMapEndpointGenerator
{

#nullable disable

    public string HttpMethod { get; set; }

    public string RoutePattern { get; set; }

    public string EndpointName { get; set; }

    public string GroupName { get; set; }

#nullable enable

    public string? Description { get; set; }

    public string? Summary { get; set; }

    public MapCreatedInformation? CreatedInformation { get; set; }

    public TypeDescriptor? IdResultValueType { get; set; }

    public bool MapIdResultValue => IdResultValueType is not null;

    public MapResponseValuesInformation? ResponseValues { get; set; }

    public string[]? AuthorizationPolicies { get; set; }

    internal CommandHandlerInformation? CommandInfo { get; set; }

    public bool Equals(MapInformation? other)
    {
        return other is not null &&
            HttpMethod == other.HttpMethod &&
            RoutePattern == other.RoutePattern &&
            EndpointName == other.EndpointName &&
            Description == other.Description &&
            Summary == other.Summary &&
            GroupName == other.GroupName &&
            Equals(CreatedInformation, other.CreatedInformation) &&
            Equals(IdResultValueType, other.IdResultValueType) &&
            Equals(ResponseValues, other.ResponseValues) &&
            AuthorizationPolicies.SequenceEqual(other.AuthorizationPolicies);
    }

    public override bool Equals(object? obj)
    {
        return obj is MapInformation description && Equals(description);
    }

    public override int GetHashCode()
    {
        int hashCode = 216910772;
        hashCode = hashCode * -1521134295 + HttpMethod.GetHashCode();
        hashCode = hashCode * -1521134295 + RoutePattern.GetHashCode();
        hashCode = hashCode * -1521134295 + EndpointName.GetHashCode();
        hashCode = hashCode * -1521134295 + Description?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + Summary?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + GroupName?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + CreatedInformation?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + IdResultValueType?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + ResponseValues?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + AuthorizationPolicies?.GetHashCode() ?? 0;
        return hashCode;
    }

    public void Generate(SourceProductionContext spc, GeneratorNodeList commands, GeneratorNodeList methods, bool withOpenApi)
    {
        // nome do método que chamará o handler
        var handlerMethodName =
            $"{CommandInfo!.ModelType.Name}{(CommandInfo.HandlerMustBeAsync ? "HandleAsync" : "Handle")}";

        // Cria comando que invoca o método de mapeamento do handler
        var methodInvoke = GenerateMapMethodInvoke(this, handlerMethodName, withOpenApi);
        var invokeCommand = new Command(methodInvoke);
        commands.Add(invokeCommand);

        // Cria o método do Handler.
        var handlerMethod = GenerateHandlerMethod(this, CommandInfo, handlerMethodName);
        methods.Add(handlerMethod);

        // se há ResponseValues, então cria a classe de resposta
        if (ResponseValues is not null)
        {
            var responseClass = GenerateReponseClass(ResponseValues, CommandInfo);
            responseClass.Generate(spc);
        }
    }

    private static MethodInvokeGenerator GenerateMapMethodInvoke(MapInformation mapInfo, string handlerMethodName, bool withOpenApi)
    {
        var methodInvoke = new MethodInvokeGenerator("group", $"Map{mapInfo.HttpMethod}");
        methodInvoke.AddArgument(mapInfo.RoutePattern);
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
        
        if (mapInfo.Summary is not null)
        {
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithSummary", mapInfo.Summary)
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
                arguments.AddArgument(policy);
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
        MapInformation mapInfo,
        CommandHandlerInformation commandInfo,
        string handlerMethodName)
    {
        const string resultVarName = "result";

        // tipo retornado pelo handler
        var handlerReturnType = DiscoveryReturnType(commandInfo, mapInfo);
        var method = new MethodGenerator(handlerMethodName, handlerReturnType);
        method.Modifiers.Private();
        method.Modifiers.Static();
        if (commandInfo.HandlerMustBeAsync)
            method.Modifiers.Async();

        // adiciona os atributos, caso exitam
        if (commandInfo.ProduceProblems?.Count > 0)
        {
            // deverá gerar algo como: [ProduceProblems(ProblemCategory.InvalidParameter)] onde
            // cada valor será ProblemCategory.InvalidParameter, segundo o exemplo.
            var attrArguments = commandInfo.ProduceProblems.Select(ValueNode (p) => new StringValueNode(p)).ToArray();
            var attribute = new AttributeGenerator("ProduceProblems", ["RoyalCode.SmartProblems"], attrArguments);
            method.Attributes.Add(attribute);
        }

        // adiciona os parâmetros
        method.Parameters.InLine = false;

        // o primeiro parâmetro é a interface do handler
        var handlerType = new TypeDescriptor(commandInfo.HandlerInterfaceName, [commandInfo.Namespace]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(handlerType, "handler")));

        // depois são os parâmetros do método do handler
        var editEntityRouteParameterName = commandInfo.EditType is not null
            ? GetFirstRouteParameterName(mapInfo.RoutePattern)
            : null;

        // o comando só entra como parâmetro do endpoint quando tem corpo (setters públicos ou ctor com parâmetros);
        // sem corpo, é instanciado via new e a requisição pode ser enviada sem body.
        CommandHandlerGenerator.AddRequiredParameters(
            commandInfo, method, editEntityRouteParameterName, includeCommandParameter: commandInfo.HasBodyProperties);

        // implementação do método

        // com corpo: protege contra body ausente (400). Sem corpo: instancia o comando via new.
        if (commandInfo.HasBodyProperties)
        {
            method.Commands.Add(new RequireBodyCommand("command", "The request body is required."));
        }
        else
        {
            method.Commands.Add(new AssignValueCommand(
                new StringValueNode("var command"),
                new StringValueNode($"new {commandInfo.ModelType.Name}()"))
            {
                AppendLine = true
            });
        }

        // chama o handler, sempre passando 'command' (parâmetro do body ou instância local),
        // na ordem esperada: [id da entidade editada], command, [WithParameters], [ct].
        var handlerInvoke =
            new MethodInvokeGenerator("handler", commandInfo.HandlerMustBeAsync ? "HandleAsync" : "Handle");

        if (commandInfo.EditType is not null)
            handlerInvoke.AddArgument($"{commandInfo.EditType.Parameter!.Name}Id");

        handlerInvoke.AddArgument("command");

        foreach (var p in commandInfo.Parameters.Where(p => p.Type.IsHandlerParameter))
            handlerInvoke.AddArgument(p.Name);

        if (commandInfo.HandlerMustBeAsync)
            handlerInvoke.AddArgument("ct");

        if (commandInfo.HandlerMustBeAsync)
            handlerInvoke.Await = true;

        // a invocação do handler é atribuída a uma variável 'result'
        var resultAssignment = new AssignValueCommand(
            new StringValueNode($"var {resultVarName}"),
            handlerInvoke);

        // adiciona o comando de atribuição ao método
        method.Commands.Add(resultAssignment);

        // por fim, retorna o resultado

        // se houver informações para retornar created, então invoca CreatedMatch
        if (mapInfo.CreatedInformation is not null)
        {
            var createdInvoke = GenerateCreatedMatchInvoke(
                mapInfo.CreatedInformation,
                mapInfo,
                commandInfo,
                resultVarName);
            var returnCommand = new ReturnCommand(createdInvoke);
            method.Commands.Add(returnCommand);
        }
        // senão, verifica se mapeia o Id
        else if (mapInfo.MapIdResultValue)
        {
            var mapInvoke = GenerateMapIdInvoke(resultVarName);
            var returnCommand = new ReturnCommand(mapInvoke);
            method.Commands.Add(returnCommand);
        }
        // senão, verifica se tem MapResponseValues
        else if (mapInfo.ResponseValues is not null)
        {
            var mapInvoke = GenerateMapInvoke(
                mapInfo.ResponseValues,
                commandInfo,
                resultVarName);

            var returnCommand = new ReturnCommand(mapInvoke);
            method.Commands.Add(returnCommand);
        }
        // senão, retorna o resultado
        else
        {
            var returnCommand = new ReturnCommand(new StringValueNode(resultVarName));
            method.Commands.Add(returnCommand);
        }

        return method;
    }

    private static string? GetFirstRouteParameterName(string routePattern)
    {
        var open = routePattern.IndexOf('{');
        if (open < 0)
            return null;

        var close = routePattern.IndexOf('}', open + 1);
        if (close < 0)
            return null;

        var parameter = routePattern.Substring(open + 1, close - open - 1).TrimStart('*').TrimEnd('?');
        var constraintStart = parameter.IndexOf(':');
        if (constraintStart >= 0)
            parameter = parameter.Substring(0, constraintStart);

        var defaultValueStart = parameter.IndexOf('=');
        if (defaultValueStart >= 0)
            parameter = parameter.Substring(0, defaultValueStart);

        return string.IsNullOrWhiteSpace(parameter)
            ? null
            : parameter;
    }

    private static TypeDescriptor DiscoveryReturnType(CommandHandlerInformation commandInfo, MapInformation mapInfo)
    {
        TypeDescriptor typeDescriptor;
        const string ns = "RoyalCode.SmartProblems.HttpResults";
        if (mapInfo.HttpMethod == "Delete")
        {
            typeDescriptor = new TypeDescriptor("NoContentMatch", [ns]);

            if (commandInfo.HandlerMustBeAsync)
                typeDescriptor = typeDescriptor.MustBeTask();

            return typeDescriptor;
        }

        // tenta obter o tipo de retorno
        bool hasValueType = commandInfo.MethodReturnType.HasValueType(out var valueType);

        if (hasValueType)
        {
            if (mapInfo.MapIdResultValue)
            {
                // quando há MapIdResultValue, então retornará CreatedMatch<TId>
                valueType = mapInfo.IdResultValueType;
            }
            else if (mapInfo.ResponseValues is not null)
            {
                // quando há MapResponseValuesInformation, então retornará CreatedMatch<T>
                // onde o T será o tipo Response gerado
                //
                valueType = new TypeDescriptor($"{commandInfo.ModelType.Name}Response", [commandInfo.Namespace]);
            }
        }

        // verifica se deve retornar CreatedMatch ou OkMatch
        if (mapInfo.CreatedInformation is not null)
        {
            // se tipo de valor, então retornará CreatedMatch<T>
            // se não tiver tipo de valor, então retornará CreatedMatch
            typeDescriptor = hasValueType
                ? valueType!.Wrap("CreatedMatch", ns)
                : new TypeDescriptor("CreatedMatch", [ns]);
        }
        else
        {
            // se tipo de valor, então retornará OkMatch<T>
            // se não tiver tipo de valor, então retornará OkMatch
            typeDescriptor = hasValueType
                ? valueType!.Wrap("OkMatch", ns)
                : new TypeDescriptor("OkMatch", [ns]);
        }

        if (commandInfo.HandlerMustBeAsync)
            typeDescriptor = typeDescriptor.MustBeTask();

        return typeDescriptor;
    }

    private static MethodInvokeGenerator GenerateCreatedMatchInvoke(
        MapCreatedInformation createdInfo,
        MapInformation mapInfo,
        CommandHandlerInformation commandInfo,
        string varName)
    {
        // cria invocação do método CreatedMatch
        var methodInvoke = new MethodInvokeGenerator(varName, "CreatedMatch");

        // montando a rota

        // primeiro monta a rota com o nome do grupo e o padrão da rota informado no atributo MapCreatedRoute
        var routePattern = $"{mapInfo.GroupName}/{createdInfo.RoutePattern}";

        // depois, para cada propriedade do MapCreatedRoute, substitui o {i} pelo valor da propriedade
        for (int i = 0; i < createdInfo.PropertiesNames.Length; i++)
        {
            var prop = createdInfo.PropertiesNames[i];
            routePattern = routePattern.Replace($"{{{i}}}", $"{{v.{prop}}}");
        }

        // adiciona o parâmetro da rota como expressão lambda
        methodInvoke.AddArgument(new StringValueNode($"v => $\"{routePattern}\""));

        // verifica se tem mapeamento de retorno

        // se tiver mapeamento por Id, adiciona a expressão lambda como parâmetro
        if (mapInfo.MapIdResultValue)
        {
            methodInvoke.AddArgument(new StringValueNode("v => v.Id"));
        }
        // senão, verifica se tem MapResponseValues
        else if (mapInfo.ResponseValues is not null)
        {
            AddResponseValuesParameter(methodInvoke, mapInfo.ResponseValues, commandInfo.ModelType.Name);
        }

        return methodInvoke;
    }

    private static void AddResponseValuesParameter(
        MethodInvokeGenerator methodInvoke,
        MapResponseValuesInformation responseValues,
        string modelTypeName)
    {
        // deverá gerar algo como: v => new NomeCommandResponse(v.Prop1, v.Prop2, ...)
        StringBuilder sb = new();
        sb.Append("v => new ");
        sb.Append(modelTypeName);
        sb.Append("Response(");
        foreach (var prop in responseValues.PropertiesNames)
        {
            sb.Append($"v.{prop.Name}, ");
        }

        sb.Remove(sb.Length - 2, 2);
        sb.Append(")");

        methodInvoke.AddArgument(new StringValueNode(sb.ToString()));
    }

    private static MethodInvokeGenerator GenerateMapIdInvoke(string varName)
    {
        // cria invocação do método Map sobre o Result
        var methodInvoke = new MethodInvokeGenerator(varName, "Map");

        methodInvoke.AddArgument(new StringValueNode("v => v.Id"));

        return methodInvoke;
    }

    private static MethodInvokeGenerator GenerateMapInvoke(
        MapResponseValuesInformation responseValues,
        CommandHandlerInformation commandInfo,
        string varName)
    {
        // cria invocação do método Map sobre o Result
        var methodInvoke = new MethodInvokeGenerator(varName, "Map");

        AddResponseValuesParameter(methodInvoke, responseValues, commandInfo.ModelType.Name);

        return methodInvoke;
    }

    private static PocoGenerator GenerateReponseClass(MapResponseValuesInformation responseValues,
        CommandHandlerInformation commandInfo)
    {
        var responseClass = new PocoGenerator($"{commandInfo.ModelType.Name}Response", commandInfo.Namespace);
        responseClass.Modifiers.Public();
        responseClass.Modifiers.Partial();

        // adiciona as propriedades
        foreach (var prop in responseValues.PropertiesNames)
        {
            responseClass.AddProperty(prop);
        }

        return responseClass;
    }
}
