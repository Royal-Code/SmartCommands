using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoyalCode.SmartCommands.Generators.Commands;
using RoyalCode.SmartCommands.Generators.Models;
using System.Text;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal sealed class MapInformation : IEquatable<MapInformation>
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

    /// <summary>
    /// O nome do parâmetro de rota que carrega o id da entidade editada (DF4), resolvido no transform;
    /// <see langword="null"/> quando o comando não usa EditEntity ou o template não declara variáveis
    /// (o id é então vinculado por inferência).
    /// </summary>
    public string? EditRouteParameterName { get; set; }

    /// <summary>
    /// Localização do argumento do endpoint name no atributo Map*. Uso exclusivo do transform (vira snapshot
    /// no modelo do pipeline); não participa da igualdade porque a informação é transitória.
    /// </summary>
    public Location? EndpointNameLocation { get; set; }

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
            EditRouteParameterName == other.EditRouteParameterName &&
            SequenceEqual(AuthorizationPolicies, other.AuthorizationPolicies);
    }

    private static bool SequenceEqual(string[]? left, string[]? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);

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
        hashCode = hashCode * -1521134295 + (EditRouteParameterName?.GetHashCode() ?? 0);
        if (AuthorizationPolicies is not null)
            foreach (var policy in AuthorizationPolicies)
                hashCode = hashCode * -1521134295 + policy.GetHashCode();
        return hashCode;
    }

    internal void Generate(
        SourceProductionContext spc,
        GeneratorNodeList commands,
        GeneratorNodeList methods,
        bool withOpenApi,
        CommandHandlerInformation commandInfo,
        ReturnModel returnModel)
    {
        // nome do método que chamará o handler
        var handlerMethodName =
            $"{commandInfo.ModelType.Name}{(commandInfo.HandlerMustBeAsync ? "HandleAsync" : "Handle")}";

        // Cria comando que invoca o método de mapeamento do handler
        var methodInvoke = GenerateMapMethodInvoke(this, handlerMethodName, withOpenApi);
        var invokeCommand = new Command(methodInvoke);
        commands.Add(invokeCommand);

        // Cria o método do Handler.
        var handlerMethod = GenerateHandlerMethod(this, commandInfo, returnModel, handlerMethodName);
        methods.Add(handlerMethod);

        // se há ResponseValues, então cria a classe de resposta (com cabeçalho de arquivo gerado)
        if (ResponseValues is not null)
        {
            var responseClass = GenerateResponseClass(ResponseValues, commandInfo);
            responseClass.Generate(spc);
        }
    }

    private static MethodInvokeGenerator GenerateMapMethodInvoke(MapInformation mapInfo, string handlerMethodName, bool withOpenApi)
    {
        // os valores vêm dos TypedConstants (sem aspas); a emissão os formata como literais C#
        var methodInvoke = new MethodInvokeGenerator("group", $"Map{mapInfo.HttpMethod}");
        methodInvoke.AddArgument(SymbolDisplay.FormatLiteral(mapInfo.RoutePattern, quote: true));
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
        MapInformation mapInfo,
        CommandHandlerInformation commandInfo,
        ReturnModel returnModel,
        string handlerMethodName)
    {
        const string resultVarName = CommandHandlerGenerator.EndpointResultVarName;

        // tipo retornado pelo handler
        var handlerReturnType = DiscoveryReturnType(commandInfo, returnModel, mapInfo);
        var method = new MethodGenerator(handlerMethodName, handlerReturnType);
        method.Modifiers.Private();
        method.Modifiers.Static();
        if (commandInfo.HandlerMustBeAsync)
            method.Modifiers.Async();

        // adiciona os atributos, caso existam
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
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(
            handlerType, CommandHandlerGenerator.EndpointHandlerParameterName)));

        // depois são os parâmetros do método do handler; o parâmetro de rota do id da entidade editada
        // foi resolvido no transform (DF4) e chega pronto no modelo
        var editEntityRouteParameterName = mapInfo.EditRouteParameterName;

        // o comando só entra como parâmetro do endpoint quando tem corpo (setters públicos ou ctor com parâmetros);
        // sem corpo, é instanciado via new e a requisição pode ser enviada sem body.
        CommandHandlerGenerator.AddRequiredParameters(
            commandInfo,
            method,
            editEntityRouteParameterName,
            includeCommandParameter: commandInfo.HasBodyProperties,
            nullableCommandParameter: commandInfo.HasBodyProperties,
            includeBindingAttributes: true);

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
        var handlerInvoke = new MethodInvokeGenerator(
            CommandHandlerGenerator.EndpointHandlerParameterName,
            commandInfo.HandlerMustBeAsync ? "HandleAsync" : "Handle");

        if (commandInfo.EditType is not null)
            handlerInvoke.AddArgument($"{commandInfo.EditType.Parameter!.Name}Id");

        handlerInvoke.AddArgument("command");

        // externos do comando e dos validators (DF13), na mesma ordem/deduplicação da assinatura do handler
        var forwardedExternalNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in commandInfo.Parameters.Where(p => p.Type.IsHandlerParameter))
        {
            if (forwardedExternalNames.Add(p.Name))
                handlerInvoke.AddArgument(p.Name);
        }

        foreach (var validator in commandInfo.Validators)
        {
            foreach (var p in validator.Parameters.Where(p => p.Type.IsHandlerParameter))
            {
                if (forwardedExternalNames.Add(p.Name))
                    handlerInvoke.AddArgument(p.Name);
            }
        }

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

    private static TypeDescriptor DiscoveryReturnType(
        CommandHandlerInformation commandInfo,
        ReturnModel returnModel,
        MapInformation mapInfo)
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
        var hasValueType = returnModel.ValueType is not null;
        var valueType = returnModel.ValueType is null
            ? null
            : PipelineModelConversions.ToDescriptor(returnModel.ValueType);

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
        var routeTemplate = $"{mapInfo.GroupName}/{createdInfo.RoutePattern}";

        // escapa o conteúdo literal da string interpolada (aspas, contrabarras e chaves); os
        // placeholders {i} são escapados junto (viram {{i}}) e depois convertidos em interpolação
        var routeLiteral = SymbolDisplay.FormatLiteral(routeTemplate, quote: true);
        var escapedRoute = routeLiteral.Substring(1, routeLiteral.Length - 2)
            .Replace("{", "{{")
            .Replace("}", "}}");

        // por fim, para cada propriedade do MapCreatedRoute, substitui o placeholder escapado {{i}}
        // pela interpolação do valor da propriedade
        for (int i = 0; i < createdInfo.PropertiesNames.Length; i++)
            escapedRoute = escapedRoute.Replace($"{{{{{i}}}}}", $"{{v.{createdInfo.PropertiesNames[i]}}}");

        // adiciona o parâmetro da rota como expressão lambda
        methodInvoke.AddArgument(new StringValueNode($"v => $\"{escapedRoute}\""));

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

    private static ResponsePocoGenerator GenerateResponseClass(MapResponseValuesInformation responseValues,
        CommandHandlerInformation commandInfo)
    {
        var responseClass = new ResponsePocoGenerator($"{commandInfo.ModelType.Name}Response", commandInfo.Namespace)
        {
            FileName = HintName.Create(
                $"{commandInfo.Namespace}.{commandInfo.ModelType.Name}Response",
                $"{commandInfo.ModelType.Name}Response")
        };
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

internal sealed class CommandMapEndpointGenerator : IMapEndpointGenerator
{
    private readonly MapInformation map;
    private readonly CommandHandlerInformation command;
    private readonly ReturnModel returnModel;

    internal CommandMapEndpointGenerator(
        MapInformation map,
        CommandHandlerInformation command,
        ReturnModel returnModel)
    {
        this.map = map;
        this.command = command;
        this.returnModel = returnModel;
    }

    public string GroupName => map.GroupName;

    public void Generate(
        SourceProductionContext spc,
        GeneratorNodeList commands,
        GeneratorNodeList methods,
        bool withOpenApi) =>
        map.Generate(spc, commands, methods, withOpenApi, command, returnModel);
}
