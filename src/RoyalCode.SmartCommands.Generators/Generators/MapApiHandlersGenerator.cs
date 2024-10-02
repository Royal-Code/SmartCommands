using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.SmartCommands.Generators.Models;
using RoyalCode.SmartCommands.Generators.Models.Commands;
using RoyalCode.SmartCommands.Generators.Models.Descriptors;

namespace RoyalCode.SmartCommands.Generators.Generators;

public static class MapApiHandlersGenerator
{
    public const string AddHandlersServicesAttributeName = "RoyalCode.SmartCommands.MapApiHandlersAttribute";

    public static bool Predicate(SyntaxNode node, CancellationToken token) => node is ClassDeclarationSyntax;

    public static TransformResult<MapApiHandlersInformation> TransformMapHandlers(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        var classSyntax = (ClassDeclarationSyntax)context.TargetNode;

        // if (!classSyntax.Modifiers.Any(SyntaxKind.PartialKeyword))
        // {
        //     var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
        //         location: classSyntax.Identifier.GetLocation(),
        //         "The class with MapHandlersAttribute must be partial");
        //
        //     return diagnostic;
        // }

        // ??? não precisaria, mas deveria ser static???
        if (!classSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: classSyntax.Identifier.GetLocation(),
                "The class with MapHandlersAttribute must be static");

            return diagnostic;
        }

        var handlerType = new TypeDescriptor(classSyntax.Identifier.Text, [classSyntax.GetNamespace()]);
        return new MapApiHandlersInformation(handlerType);
    }

    public static void Generate(
        SourceProductionContext spc,
        MapApiHandlersInformation left,
        IList<CommandHandlerInformation> right)
    {
        // Api's agrupadas
        var commandGroup = right.GroupBy(m => m.MapInformation!.GroupName);

        // para cada grupo, deve ser criado uma classe como, por exemplo: MapMeuGrupoApi
        foreach (var group in commandGroup)
        {
            var groupName = group.Key ?? left.ClassType.Name;
            var safeGroupName = groupName.ToPascalCase();
            var className = safeGroupName.EndsWith("Api")
                ? safeGroupName
                : $"{safeGroupName}Api";

            // a classe terá um método estático que mapeará os handlers
            var (classGenerator, methodGenerator) = CreateGroupClassAndMethod(
                className: $"Map{className}",
                classNamespace: left.ClassType.Namespaces[0],
                methodName: $"Map{safeGroupName}Group");

            // comando que cria o group
            // deve gerar algo como: var group = builder.MapGroup("MyGroup")
            var assigment = new AssignValueCommand(
                new StringValueNode("var group"),
                new StringValueNode($"builder.MapGroup(\"{groupName}\")"))
            {
                AppendLine = true
            };
            methodGenerator.Commands.Add(assigment);

            // Para cada comando, será gerado um método que chamará o handler
            // para o método que mapeia o handlers, será criado um comando de mapeamento.
            foreach (var commandInfo in group)
            {
                var mapInfo = commandInfo.MapInformation!;

                // nome do método que chamará o handler
                var handlerMethodName =
                    $"{commandInfo.ModelType.Name}{(commandInfo.HandlerMustBeAsync ? "HandleAsync" : "Handle")}";

                // Cria comando que invoca o método de mapeamento do handler
                var methodInvoke = GenerateMapMethodInvoke(mapInfo, handlerMethodName);
                var invokeCommand = new Command(methodInvoke);
                methodGenerator.Commands.Add(invokeCommand);

                // Cria o método do Handler.
                var handlerMethod = GenerateHandlerMethod(mapInfo, commandInfo, handlerMethodName);
                classGenerator.Methods.Add(handlerMethod);
                handlerMethod.AddUsings(classGenerator.Usings);

                // se há ResponseValues, então cria a classe de resposta
                if (mapInfo.ResponseValues is not null)
                {
                    var responseClass = GenerateReponseClass(mapInfo.ResponseValues, commandInfo);
                    responseClass.Generate(spc);
                }
            }

            // por fim, finaliza o método retornando o group
            var returnCommand = new ReturnCommand(new StringValueNode("group"));
            methodGenerator.Commands.Add(returnCommand);

            // finaliza, gera a classe
            classGenerator.Generate(spc);
        }
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

    private static MethodGenerator GenerateHandlerMethod(
        MapInformation mapInfo,
        CommandHandlerInformation commandInfo,
        string handlerMethodName)
    {
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
            var attribute = new AttributeGenerator("ProduceProblems", attrArguments);
            method.Attributes.Add(attribute);
            // adiciona o namespace
            method.Usings.Add("RoyalCode.SmartProblems");
        }

        // adiciona os parâmetros
        method.Parameters.InLine = false;

        // o primeiro parâmetro é a interface do handler
        var handlerType = new TypeDescriptor(commandInfo.HandlerInterfaceName, [commandInfo.Namespace]);
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(handlerType, "handler")));

        // depois são os parâmetros do método do handler
        CommandHandlerGenerator.AddRequiredParameters(commandInfo, method);

        // implementação do método

        // primeiro, chama o handler
        var handlerInvoke =
            new MethodInvokeGenerator("handler", commandInfo.HandlerMustBeAsync ? "HandleAsync" : "Handle");
        foreach (var param in method.Parameters.GetDescriptors().Skip(1))
        {
            handlerInvoke.AddArgument(param.Name);
        }

        if (commandInfo.HandlerMustBeAsync)
            handlerInvoke.Await = true;

        // a invocação do handler é atribuída a uma variável 'result'
        var resultAssignment = new AssignValueCommand(
            new StringValueNode("var result"),
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
                "result");
            var returnCommand = new ReturnCommand(createdInvoke);
            method.Commands.Add(returnCommand);
        }
        // senão, verifica se tem MapResponseValues
        else if (mapInfo.ResponseValues is not null)
        {
            var mapInvoke = GenerateMapInvoke(
                mapInfo.ResponseValues,
                commandInfo,
                "result");

            var returnCommand = new ReturnCommand(mapInvoke);
            method.Commands.Add(returnCommand);
        }
        // senão, retorna o resultado
        else
        {
            var returnCommand = new ReturnCommand(new StringValueNode("result"));
            method.Commands.Add(returnCommand);
        }

        return method;
    }

    private static MethodInvokeGenerator GenerateMapInvoke(
        MapResponseValuesInformation responseValues,
        CommandHandlerInformation commandInfo,
        string varName)
    {
        // cria invocação do método CreatedMatch
        var methodInvoke = new MethodInvokeGenerator(varName, commandInfo.HandlerMustBeAsync ? "MapAsync" : "Map");

        AddResponseValuesParameter(methodInvoke, responseValues, commandInfo.ModelType.Name);

        return methodInvoke;
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
            sb.Append($"v.{prop}, ");
        }

        sb.Remove(sb.Length - 2, 2);
        sb.Append(")");

        methodInvoke.AddArgument(new StringValueNode(sb.ToString()));
    }

    private static MethodInvokeGenerator GenerateMapMethodInvoke(MapInformation mapInfo, string handlerMethodName)
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

        // por fim, chama WithOpenApi
        methodInvoke = new MethodInvokeGenerator(methodInvoke, "WithOpenApi")
        {
            LineIdent = true
        };

        return methodInvoke;
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

    private static (ClassGenerator, MethodGenerator) CreateGroupClassAndMethod(
        string className, string classNamespace, string methodName)
    {
        var classGenerator = new ClassGenerator(className, classNamespace);
        classGenerator.Modifiers.Public();
        classGenerator.Modifiers.Static();
        classGenerator.Modifiers.Partial();

        var endpointRouteBuilder = new TypeDescriptor("IEndpointRouteBuilder", ["Microsoft.AspNetCore.Routing", "Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Http"]);
        var routeGroupBuilder = new TypeDescriptor("RouteGroupBuilder", ["Microsoft.AspNetCore.Routing"]);

        var mapMethod = new MethodGenerator(methodName, routeGroupBuilder);
        mapMethod.Modifiers.Public();
        mapMethod.Modifiers.Static();
        mapMethod.Parameters.Add(
            new ParameterGenerator(new ParameterDescriptor(endpointRouteBuilder, "builder"))
            {
                ThisModifier = true
            });

        classGenerator.Methods.Add(mapMethod);
        mapMethod.AddUsings(classGenerator.Usings);

        return (classGenerator, mapMethod);
    }
}