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

    /// <summary>
    /// A rota de Location do <c>MapAcceptedRoute</c> (DF17/Fase 3); reusa a estrutura de
    /// <see cref="MapCreatedInformation"/>. Mutuamente exclusiva com <see cref="CreatedInformation"/>
    /// (RCCMD055). <see langword="null"/> quando o comando não declara Location de 202.
    /// </summary>
    public MapCreatedInformation? AcceptedInformation { get; set; }

    public TypeDescriptor? IdResultValueType { get; set; }

    public bool MapIdResultValue => IdResultValueType is not null;

    public MapResponseValuesInformation? ResponseValues { get; set; }

    public string[]? AuthorizationPolicies { get; set; }

    /// <summary>Status de sucesso explícito (DF23); <see langword="null"/> preserva a inferência atual.</summary>
    public HttpResultStatusModel? ResultStatus { get; set; }

    /// <summary>Filtros de endpoint (DF23), nomes globalmente qualificados, na ordem declarada.</summary>
    public string[]? EndpointFilters { get; set; }

    /// <summary>Tags OpenAPI (DF23), na ordem declarada.</summary>
    public string[]? Tags { get; set; }

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
            Equals(AcceptedInformation, other.AcceptedInformation) &&
            Equals(IdResultValueType, other.IdResultValueType) &&
            Equals(ResponseValues, other.ResponseValues) &&
            EditRouteParameterName == other.EditRouteParameterName &&
            SequenceEqual(AuthorizationPolicies, other.AuthorizationPolicies) &&
            ResultStatus == other.ResultStatus &&
            SequenceEqual(EndpointFilters, other.EndpointFilters) &&
            SequenceEqual(Tags, other.Tags);
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
        hashCode = hashCode * -1521134295 + (Description?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (Summary?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (GroupName?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (CreatedInformation?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (AcceptedInformation?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (IdResultValueType?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (ResponseValues?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (EditRouteParameterName?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + (ResultStatus?.GetHashCode() ?? 0);
        if (AuthorizationPolicies is not null)
            foreach (var policy in AuthorizationPolicies)
                hashCode = hashCode * -1521134295 + policy.GetHashCode();
        if (EndpointFilters is not null)
            foreach (var filter in EndpointFilters)
                hashCode = hashCode * -1521134295 + filter.GetHashCode();
        if (Tags is not null)
            foreach (var tag in Tags)
                hashCode = hashCode * -1521134295 + tag.GetHashCode();
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
        var methodInvoke = GenerateMapMethodInvoke(
            this,
            handlerMethodName,
            withOpenApi,
            ProducesNoContent(this, returnModel),
            ProducesAccepted(this) && returnModel.ValueType is null);
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

    /// <summary>
    /// O endpoint responde 204 No Content: seleção explícita (DF23) ou, por inferência, Delete cujo
    /// contrato não retorna valor nem created. A escolha de status vive aqui e em
    /// <see cref="DiscoveryReturnType"/>.
    /// </summary>
    private static bool ProducesNoContent(MapInformation mapInfo, ReturnModel returnModel) =>
        mapInfo.ResultStatus == HttpResultStatusModel.NoContent ||
        (mapInfo.ResultStatus is null &&
         mapInfo.HttpMethod == "Delete" &&
         returnModel.ValueType is null &&
         mapInfo.CreatedInformation is null);

    /// <summary>
    /// O endpoint responde 201 Created: <c>MapCreatedRoute</c> (com Location) ou seleção explícita (DF23,
    /// sem Location). Conflitos entre os dois caminhos e <c>Ok</c>/<c>NoContent</c> são diagnosticados no
    /// transform (RCCMD053) e não chegam aqui.
    /// </summary>
    private static bool ProducesCreated(MapInformation mapInfo) =>
        mapInfo.CreatedInformation is not null ||
        mapInfo.ResultStatus == HttpResultStatusModel.Created;

    /// <summary>
    /// O endpoint responde 202 Accepted: <c>MapAcceptedRoute</c> (com Location, opcional) ou seleção
    /// explícita (DF23, sem Location). Diferentemente do 201, a Location do 202 é opcional. Conflitos com
    /// <c>Ok</c>/<c>Created</c>/<c>NoContent</c> e com <c>MapCreatedRoute</c> são diagnosticados no transform
    /// (RCCMD053/RCCMD055) e não chegam aqui.
    /// </summary>
    private static bool ProducesAccepted(MapInformation mapInfo) =>
        mapInfo.AcceptedInformation is not null ||
        mapInfo.ResultStatus == HttpResultStatusModel.Accepted;

    private static MethodInvokeGenerator GenerateMapMethodInvoke(
        MapInformation mapInfo,
        string handlerMethodName,
        bool withOpenApi,
        bool producesNoContent,
        bool producesBodylessAccepted)
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

        if (producesNoContent)
        {
            // a metadata 204 do NoContentMatch não declara content-type e é descartada pelo ApiExplorer;
            // o Produces explícito garante a resposta de sucesso no OpenAPI
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "Produces", "204")
            {
                LineIdent = true
            };
        }

        if (producesBodylessAccepted)
        {
            // o AcceptedMatch (não-genérico) declara a metadata 202 sem content-type, descartada pelo
            // ApiExplorer exatamente como o 204; o Produces explícito garante a resposta no OpenAPI
            methodInvoke = new MethodInvokeGenerator(methodInvoke, "Produces", "202")
            {
                LineIdent = true
            };
        }

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
                returnModel,
                resultVarName);
            var returnCommand = new ReturnCommand(createdInvoke);
            method.Commands.Add(returnCommand);
        }
        // se houver informações para retornar accepted (Location), então invoca AcceptedMatch
        else if (mapInfo.AcceptedInformation is not null)
        {
            var acceptedInvoke = GenerateAcceptedMatchInvoke(
                mapInfo.AcceptedInformation,
                mapInfo,
                commandInfo,
                returnModel,
                resultVarName);
            method.Commands.Add(new ReturnCommand(acceptedInvoke));
        }
        // Created explícito sem MapCreatedRoute (DF23): 201 sem Location
        else if (mapInfo.ResultStatus == HttpResultStatusModel.Created)
        {
            method.Commands.Add(new ReturnCommand(
                GenerateCreatedWithoutLocation(mapInfo, commandInfo, returnModel, resultVarName)));
        }
        // Accepted explícito sem MapAcceptedRoute (DF23): 202 sem Location (opcional para o 202)
        else if (mapInfo.ResultStatus == HttpResultStatusModel.Accepted)
        {
            method.Commands.Add(new ReturnCommand(
                GenerateAcceptedWithoutLocation(mapInfo, commandInfo, returnModel, resultVarName)));
        }
        // NoContent explícito com Result<T> (DF23): descarta deliberadamente o valor de sucesso,
        // preservando os problemas (conversão Result<T> -> Result do SmartProblems)
        else if (mapInfo.ResultStatus == HttpResultStatusModel.NoContent && returnModel.ValueType is not null)
        {
            method.Commands.Add(new ReturnCommand(new StringValueNode($"(Result){resultVarName}")));
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

    /// <summary>
    /// <para>
    ///     Emissão do 201 sem Location (DF23): o <c>CreatedMatch</c> atual do SmartProblems exige um
    ///     path no construtor de <c>Result</c>, então o sucesso é convertido via <c>Match</c> +
    ///     <c>TypedResults.Created()</c> (sem URI) e os problemas seguem para <c>MatchErrorResult</c>,
    ///     usando o construtor público <c>CreatedMatch(IResult)</c> — sem mudança no SmartProblems.
    /// </para>
    /// <para>
    ///     As projeções de <c>MapIdResultValue</c>/<c>MapResponseValues</c> são aplicadas antes, por
    ///     <c>Map</c>, exatamente como no caminho com Location.
    /// </para>
    /// </summary>
    private static ValueNode GenerateCreatedWithoutLocation(
        MapInformation mapInfo,
        CommandHandlerInformation commandInfo,
        ReturnModel returnModel,
        string resultVarName)
    {
        if (returnModel.ValueType is null)
        {
            return new StringValueNode(
                $"new CreatedMatch({resultVarName}.Match<IResult>(" +
                "static () => TypedResults.Created(), " +
                "static problems => new MatchErrorResult(problems)))");
        }

        string source;
        string valueTypeName;
        if (mapInfo.MapIdResultValue)
        {
            source = $"{resultVarName}.Map(v => v.Id)";
            valueTypeName = mapInfo.IdResultValueType!.Name;
        }
        else if (mapInfo.ResponseValues is not null)
        {
            var projection = new StringBuilder();
            projection.Append(resultVarName).Append(".Map(v => new ")
                .Append(commandInfo.ModelType.Name).Append("Response(");
            foreach (var property in mapInfo.ResponseValues.PropertiesNames)
                projection.Append("v.").Append(property.Name).Append(", ");
            projection.Remove(projection.Length - 2, 2);
            projection.Append("))");

            source = projection.ToString();
            valueTypeName = $"{commandInfo.ModelType.Name}Response";
        }
        else
        {
            source = resultVarName;
            valueTypeName = PipelineModelConversions.ToDescriptor(returnModel.ValueType).Name;
        }

        return new StringValueNode(
            $"new CreatedMatch<{valueTypeName}>({source}.Match<IResult>(" +
            "static value => TypedResults.Created((string?)null, value), " +
            "static problems => new MatchErrorResult(problems)))");
    }

    private static TypeDescriptor DiscoveryReturnType(
        CommandHandlerInformation commandInfo,
        ReturnModel returnModel,
        MapInformation mapInfo)
    {
        TypeDescriptor typeDescriptor;
        const string ns = "RoyalCode.SmartProblems.HttpResults";

        // tenta obter o tipo de retorno
        var hasValueType = returnModel.ValueType is not null;

        // Delete responde 204 No Content somente quando o contrato não retorna valor nem created;
        // com valor, o comportamento é o mesmo dos demais verbos (o valor não é descartado em silêncio).
        if (ProducesNoContent(mapInfo, returnModel))
        {
            typeDescriptor = new TypeDescriptor("NoContentMatch", [ns]);

            if (commandInfo.HandlerMustBeAsync)
                typeDescriptor = typeDescriptor.MustBeTask();

            return typeDescriptor;
        }
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

        // verifica se deve retornar CreatedMatch, AcceptedMatch ou OkMatch (MapCreatedRoute/MapAcceptedRoute
        // ou status explícito, DF23)
        if (ProducesCreated(mapInfo))
        {
            // se tipo de valor, então retornará CreatedMatch<T>
            // se não tiver tipo de valor, então retornará CreatedMatch
            typeDescriptor = hasValueType
                ? valueType!.Wrap("CreatedMatch", ns)
                : new TypeDescriptor("CreatedMatch", [ns]);
        }
        else if (ProducesAccepted(mapInfo))
        {
            // se tipo de valor, então retornará AcceptedMatch<T>
            // se não tiver tipo de valor, então retornará AcceptedMatch
            typeDescriptor = hasValueType
                ? valueType!.Wrap("AcceptedMatch", ns)
                : new TypeDescriptor("AcceptedMatch", [ns]);
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
        ReturnModel returnModel,
        string varName)
    {
        // cria invocação do método CreatedMatch
        var methodInvoke = new MethodInvokeGenerator(varName, "CreatedMatch");

        // sem valor de sucesso (plain Result + rota estática): o único overload não-genérico é
        // CreatedMatch(this Result, string), que exige a rota como string literal — nunca uma lambda
        // (uma lambda aqui não compila: CS1660). Sem valor não há placeholder nem projeção possível.
        if (returnModel.ValueType is null)
        {
            methodInvoke.AddArgument(new StringValueNode(BuildStaticRouteLiteral(createdInfo, mapInfo)));
            return methodInvoke;
        }

        // com valor: a rota é uma lambda interpolada sobre o valor de sucesso ('v')
        methodInvoke.AddArgument(new StringValueNode($"v => $\"{BuildRouteInterpolationBody(createdInfo, mapInfo)}\""));

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

    /// <summary>
    /// <para>
    ///     Emissão do <c>AcceptedMatch</c>/<c>AcceptedMatch&lt;T&gt;</c> com Location (MapAcceptedRoute).
    ///     Diferentemente do 201, o 202 não expõe overloads de extensão com <c>selector</c>; por isso as
    ///     projeções de <c>MapIdResultValue</c>/<c>MapResponseValues</c> usam o construtor + <c>Match</c>,
    ///     formando a Location e a projeção a partir do mesmo valor de sucesso.
    /// </para>
    /// <para>
    ///     Sem valor de sucesso (plain Result), a rota é estática e usa a extensão
    ///     <c>AcceptedMatch(this Result, string?)</c> com a rota literal.
    /// </para>
    /// </summary>
    private static ValueNode GenerateAcceptedMatchInvoke(
        MapCreatedInformation acceptedInfo,
        MapInformation mapInfo,
        CommandHandlerInformation commandInfo,
        ReturnModel returnModel,
        string varName)
    {
        // sem valor de sucesso (plain Result + rota estática): AcceptedMatch(this Result, string?)
        if (returnModel.ValueType is null)
        {
            var noValueInvoke = new MethodInvokeGenerator(varName, "AcceptedMatch");
            noValueInvoke.AddArgument(new StringValueNode(BuildStaticRouteLiteral(acceptedInfo, mapInfo)));
            return noValueInvoke;
        }

        var hasPlaceholders = RoutePatternParser.Parse(acceptedInfo.RoutePattern).Count > 0;

        // a Location como expressão sobre o valor de sucesso 'v' (rota dinâmica) ou literal (rota estática)
        var locationExpression = hasPlaceholders
            ? $"$\"{BuildRouteInterpolationBody(acceptedInfo, mapInfo)}\""
            : BuildStaticRouteLiteral(acceptedInfo, mapInfo);

        // com projeção por Id: sem overload de extensão com selector, usa o construtor + Match
        if (mapInfo.MapIdResultValue)
        {
            return new StringValueNode(
                $"new AcceptedMatch<{mapInfo.IdResultValueType!.Name}>({varName}.Match<IResult>(" +
                $"v => TypedResults.Accepted({locationExpression}, v.Id), " +
                "static problems => new MatchErrorResult(problems)))");
        }

        // com projeção por MapResponseValues: idem, projetando para o tipo Response gerado
        if (mapInfo.ResponseValues is not null)
        {
            return new StringValueNode(
                $"new AcceptedMatch<{commandInfo.ModelType.Name}Response>({varName}.Match<IResult>(" +
                $"v => TypedResults.Accepted({locationExpression}, " +
                $"{BuildResponseConstructorExpression(mapInfo.ResponseValues, commandInfo.ModelType.Name)}), " +
                "static problems => new MatchErrorResult(problems)))");
        }

        // sem projeção: extensão AcceptedMatch<T>(this Result<T>, Func|string)
        var invoke = new MethodInvokeGenerator(varName, "AcceptedMatch");
        invoke.AddArgument(new StringValueNode(hasPlaceholders
            ? $"v => $\"{BuildRouteInterpolationBody(acceptedInfo, mapInfo)}\""
            : BuildStaticRouteLiteral(acceptedInfo, mapInfo)));
        return invoke;
    }

    /// <summary>
    /// Emissão do 202 sem Location (DF23): a Location do 202 é opcional, então as extensões
    /// <c>AcceptedMatch()</c>/<c>AcceptedMatch&lt;T&gt;()</c> bastam. As projeções de
    /// <c>MapIdResultValue</c>/<c>MapResponseValues</c> são aplicadas antes por <c>Map</c> (sem Location,
    /// não é preciso o valor original depois da projeção), exatamente como no 201 sem Location.
    /// </summary>
    private static ValueNode GenerateAcceptedWithoutLocation(
        MapInformation mapInfo,
        CommandHandlerInformation commandInfo,
        ReturnModel returnModel,
        string resultVarName)
    {
        // plain Result -> AcceptedMatch(this Result, string? = null)
        if (returnModel.ValueType is null)
            return new StringValueNode($"{resultVarName}.AcceptedMatch()");

        string source;
        if (mapInfo.MapIdResultValue)
        {
            source = $"{resultVarName}.Map(v => v.Id)";
        }
        else if (mapInfo.ResponseValues is not null)
        {
            source = $"{resultVarName}.Map(v => " +
                $"{BuildResponseConstructorExpression(mapInfo.ResponseValues, commandInfo.ModelType.Name)})";
        }
        else
        {
            source = resultVarName;
        }

        // AcceptedMatch<T>(this Result<T>, string? = null) — 202 sem Location
        return new StringValueNode($"{source}.AcceptedMatch()");
    }

    /// <summary>
    /// A rota de Location como string literal C# (grupo + pattern), sem placeholders — usada quando o comando
    /// não retorna valor de sucesso (rota necessariamente estática) ou quando a rota é estática.
    /// </summary>
    private static string BuildStaticRouteLiteral(MapCreatedInformation info, MapInformation mapInfo)
    {
        var routeTemplate = mapInfo.GroupName is null
            ? info.RoutePattern
            : CombineRoutePatterns(mapInfo.GroupName, info.RoutePattern);

        return SymbolDisplay.FormatLiteral(routeTemplate, quote: true);
    }

    /// <summary>
    /// O corpo de uma string interpolada C# (sem as aspas nem o <c>$</c>) da rota de Location (grupo +
    /// pattern), com cada placeholder nomeado convertido em interpolação da propriedade declarada que casa
    /// com ele (<c>{v.Prop}</c>, sem diferenciar maiúsculas). O transform já garantiu a correspondência.
    /// </summary>
    private static string BuildRouteInterpolationBody(MapCreatedInformation info, MapInformation mapInfo)
    {
        var routeTemplate = mapInfo.GroupName is null
            ? info.RoutePattern
            : CombineRoutePatterns(mapInfo.GroupName, info.RoutePattern);

        // escapa o conteúdo literal da string interpolada (aspas, contrabarras e chaves); os
        // placeholders nomeados são escapados junto (viram {{name}}) e depois convertidos em interpolação
        var routeLiteral = SymbolDisplay.FormatLiteral(routeTemplate, quote: true);
        var escapedRoute = routeLiteral.Substring(1, routeLiteral.Length - 2)
            .Replace("{", "{{")
            .Replace("}", "}}");

        foreach (var placeholder in RoutePatternParser.Parse(info.RoutePattern))
        {
            var propertyName = info.PropertiesNames.FirstOrDefault(name =>
                string.Equals(name, placeholder.Name, StringComparison.OrdinalIgnoreCase));
            if (propertyName is null)
                continue;

            escapedRoute = escapedRoute.Replace($"{{{{{placeholder.Name}}}}}", $"{{v.{propertyName}}}");
        }

        return escapedRoute;
    }

    /// <summary>
    /// Junta o prefixo de <c>MapGroup</c> ao padrão de <c>MapCreatedRoute</c>/<c>MapAcceptedRoute</c>,
    /// normalizando somente a barra criada na fronteira entre os dois valores. A validade dos padrões
    /// continua sendo responsabilidade do roteamento do ASP.NET Core.
    /// </summary>
    private static string CombineRoutePatterns(string groupName, string routePattern) =>
        $"{groupName.TrimEnd('/')}/{routePattern.TrimStart('/')}";

    /// <summary>
    /// A expressão do construtor da resposta gerada — <c>new NomeCommandResponse(v.Prop1, v.Prop2, ...)</c> —
    /// sobre o valor de sucesso <c>v</c>. Compartilhada entre a projeção via <c>Map</c> e a via <c>Match</c>.
    /// </summary>
    private static string BuildResponseConstructorExpression(
        MapResponseValuesInformation responseValues,
        string modelTypeName)
    {
        StringBuilder sb = new();
        sb.Append("new ").Append(modelTypeName).Append("Response(");
        foreach (var prop in responseValues.PropertiesNames)
            sb.Append("v.").Append(prop.Name).Append(", ");
        sb.Remove(sb.Length - 2, 2);
        sb.Append(')');
        return sb.ToString();
    }

    private static void AddResponseValuesParameter(
        MethodInvokeGenerator methodInvoke,
        MapResponseValuesInformation responseValues,
        string modelTypeName)
    {
        // deverá gerar algo como: v => new NomeCommandResponse(v.Prop1, v.Prop2, ...)
        methodInvoke.AddArgument(new StringValueNode(
            "v => " + BuildResponseConstructorExpression(responseValues, modelTypeName)));
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

    public string? GroupName => map.GroupName;

    public void Generate(
        SourceProductionContext spc,
        GeneratorNodeList commands,
        GeneratorNodeList methods,
        bool withOpenApi) =>
        map.Generate(spc, commands, methods, withOpenApi, command, returnModel);
}
