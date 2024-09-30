using Coreum.NewCommands.Generators.Models;
using Coreum.NewCommands.Generators.Models.Commands;
using Coreum.NewCommands.Generators.Models.Descriptors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

namespace Coreum.NewCommands.Generators.Generators;

//#pragma warning disable S125 // remover blocos de código comentados

public static class CommandHandlerGenerator
{
    public const string CommandAttributeName = "Coreum.NewCommands.CommandAttribute";

    private const string CommandNamespace = "Coreum.NewCommands";
    private const string WithValidateModelAttributeName = "WithValidateModel";
    private const string WithDecoratorsAttributeName = "WithDecorators";
    private const string WithUnitOfWorkAttributeName = "WithUnitOfWork";
    private const string WithFindEntitiesAttributeName = "WithFindEntities";
    private const string ProduceNewEntityAttributeName = "ProduceNewEntity";
    private const string MapIdResultValueAttributeName = "MapIdResultValue";
    private const string MapResponseValuesAttributeName = "MapResponseValues";
    private const string ProduceProblemsAttributeName = "ProduceProblems";
    private const string MapPostAttributeName = "MapPost";
    private const string MapPutAttributeName = "MapPut";
    private const string MapPatchAttributeName = "MapPatch";
    private const string MapDeleteAttributeName = "MapDelete";
    private const string MapGetAttributeName = "MapGet";
    private const string MapGroupAttributeName = "MapGroup";
    private const string MapCreatedRouteAttributeName = "MapCreatedRoute";

    private const string EditEntityAttributeName = "EditEntity";
    private const string ModelVarName = "command";
    private const string DecoratorsVarName = "decorators";
    private const string DecoratorsMediatorVarName = "decoratorsMediator";
    private const string CommandResultVarName = "commandResult";
    private const string DecoratorType = "IEnumerable<IDecorator<{0}, {1}>>";
    private const string AccessorVarName = "accessor";
    private const string UowAccessorType = "IUnitOfWorkAccessor<{0}>";
    private const string RepoAccessorType = "IRepositoriesAccessor<{0}>";

    public static bool Predicate(SyntaxNode node, CancellationToken token) => node is MethodDeclarationSyntax;

    public static TransformResult<CommandHandlerInformation> Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken token)
    {
        // método do comando, ou seja, que contém o attribute Command
        var method = (MethodDeclarationSyntax)context.TargetNode;

        // obtém a classe que contém o método
        if (method.Parent is not ClassDeclarationSyntax classDeclaration)
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: method.Identifier.GetLocation(),
                "The method does not have a class declaration");

            return diagnostic;
        }

        // nem o método nem a classe pode ter argumentos genéricos
        if (method.TypeParameterList is not null || classDeclaration.TypeParameterList is not null)
        {
            var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                location: method.Identifier.GetLocation(),
                "Neither the method nor the class can have generic arguments");

            return diagnostic;
        }

        // lista dos parâmetros de ProblemCategory que o comando pode produzir
        // esta lista é preenchida a partir do método HasProblems da classe e do método do comando
        // através do attribute ProduceProblems
        List<string> produceProblems = [];

        // verifica se existe problemas no método da do comando
        if (method.TryGetAttribute(ProduceProblemsAttributeName, out var produceProblemsAttr))
        {
            // se tem o attribute, extrai os parâmetros
            var problemsProduced = produceProblemsAttr!.ArgumentList?.Arguments.Select(a => a.Expression.ToString());
            if (problemsProduced is not null)
                produceProblems.AddRange(problemsProduced);
        }


        MethodDeclarationSyntax? hasProblemsMethod = null;

        // Verifica se o método possui o atributo WithValidateModel
        // Se tiver, busca pelo método na classe e já valida se está dentro do padrão
        var hasWithValidateModel = method.TryGetAttribute(WithValidateModelAttributeName, out var withValidateModelAttr);
        if (withValidateModelAttr is not null
            && !classDeclaration.ValidateClassWithHasProblemsMethod(
                withValidateModelAttr,
                out hasProblemsMethod,
                out var error))
            return error!;

        // se tem hasProblemsMethod, verifica se tem o attribute ProduceProblems
        if (hasProblemsMethod is not null )
        {
            if (hasProblemsMethod.TryGetAttribute(ProduceProblemsAttributeName, out produceProblemsAttr))
            {
                // se tem o attribute, extrai os parâmetros
                var problemsProduced = produceProblemsAttr!.ArgumentList?.Arguments.Select(a => a.Expression.ToString());
                if (problemsProduced is not null)
                    produceProblems.AddRange(problemsProduced);
            }
            else
            {
                // se não tem, adiciona o valor de ProblemCategory.InvalidParameter, pois é o padrão
                produceProblems.Add("ProblemCategory.InvalidParameter");
            }
        }

        // Verifica se o método possui o atributo WithDecorators
        var hasWithDecorators = method.TryGetAttribute(WithDecoratorsAttributeName, out _);

        // se tem WithDecorators, deve retornar algum tipo de dado (não pode ser Task ou void).
        if (hasWithDecorators && !method.ValidateReturnType(out error))
            return error!;

        // verifica se tem o attribute WithUnitOfWork
        TypeDescriptor? accessorType = null;
        var hasUow = method.TryGetAttribute(WithUnitOfWorkAttributeName, out var withUowAttr);
        if (hasUow)
        {
            // se tem uow, extrai o tipo do contexto
            var uowSyntaxType = ((GenericNameSyntax)withUowAttr!.Name).TypeArgumentList.Arguments[0];
            accessorType = TypeDescriptor.Create(uowSyntaxType, context.SemanticModel);
        }

        // verifica se tem WithFindEntities (se tiver hasUow, não precisa verificar)
        var hasFindEntities = false;
        if (!hasUow)
        {
            hasFindEntities = method.TryGetAttribute(WithFindEntitiesAttributeName, out var withFindEntitiesAttr);
            if (hasFindEntities)
            {
                // se tem with find entities, extrai o tipo do contexto
                var uowSyntaxType = ((GenericNameSyntax)withFindEntitiesAttr!.Name).TypeArgumentList.Arguments[0];
                accessorType = TypeDescriptor.Create(uowSyntaxType, context.SemanticModel);
            }
        }

        // verifica se retorna uma nova entidade
        var hasProduceNewEntity = method.TryGetAttribute(ProduceNewEntityAttributeName, out _);
        if (hasProduceNewEntity && !hasUow)
        {
            // quando retorna entidade, deve haver uow
            var diagnostic = Diagnostic.Create(
                CmdDiagnostics.ProduceNewEntityRequiresWithUnitOfWork,
                location: method.Identifier.GetLocation());

            return diagnostic;
        }

        // verifica se edita uma entidade existente
        EditTypeDescriptor? editType = null;
        var hasEditEntity = method.TryGetAttribute(EditEntityAttributeName, out var editEntityAttr);
        if (hasEditEntity)
        {
            // editar entidade requer uow
            if (!hasUow)
            {
                var diagnostic = Diagnostic.Create(
                    CmdDiagnostics.EditEntityRequiresWithUnitOfWork,
                    location: method.Identifier.GetLocation());

                return diagnostic;
            }

            // se já tem produce new entity, não pode ter edit entity
            if (hasProduceNewEntity)
            {
                var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                    location: method.Identifier.GetLocation(),
                    "The method cannot have both ProduceNewEntity and EditEntity attributes");

                return diagnostic;
            }

            editType = EditTypeDescriptor.Create(editEntityAttr!, context.SemanticModel);
        }

        // verifica se o método é assíncrono (se retorna Task)
        // By Design: Não irá checar namespaces
        var isAsync = method.ReturnType is GenericNameSyntax { Identifier.Text: "Task" };

        // obtém o retorno do método
        var methodReturnType = TypeDescriptor.Create(method.ReturnType, context.SemanticModel);

        // se produz uma nova entidade, então o retorno do método será a nova entidade
        TypeDescriptor? newEntityType = null;
        if (hasProduceNewEntity)
        {
            var newEntityTypeSyntax = method.ReturnType;

            // se for uma Task, extrai o tipo genérico, senão é o próprio tipo
            if (isAsync)
                newEntityTypeSyntax = newEntityTypeSyntax.GetInnerType();

            var newEntityTypeInfo = context.SemanticModel.GetTypeInfo(newEntityTypeSyntax);
            if (newEntityTypeInfo.Type is not INamedTypeSymbol newEntityNameSymbol)
            {
                var diagnostic = Diagnostic.Create(CmdDiagnostics.InvalidCommandType,
                    location: method.Identifier.GetLocation(),
                    "It was not possible to determine the return type of the new entity");

                return diagnostic;
            }

            // se for um Result, deve ser o tipo genérico
            if (newEntityNameSymbol.Name.StartsWith("Result"))
            {
                // O Result deve ser genérico, se não for, retorna um erro.
                if (!newEntityNameSymbol.IsGenericType)
                {
                    var diagnostic = Diagnostic.Create(
                        CmdDiagnostics.ProduceNewEntityMustReturnResultWithValue,
                        location: method.ReturnType.GetLocation());

                    return diagnostic;
                }

                // extrai o tipo genérico do Result
                newEntityTypeSyntax = newEntityTypeSyntax.GetInnerType();
            }

            // cria o tipo da nova entidade
            newEntityType = TypeDescriptor.Create(newEntityTypeSyntax, context.SemanticModel);
        }

        // lista dos parâmetros do método com o attribute Command
        var parameters = new List<ParameterDescriptor>(method.ParameterList.Parameters.Count);
        // lista de vínculo de parâmetros que são entidades e propriedades ID para a entidade.
        var idPropertiesBindings = new List<IdPropertyBoundToEntityParameter>();

        // obtém os parâmetros do método com o attribute Command
        var commandMethodParameters = method.ParameterList.Parameters;
        for (int paramIndex = 0; paramIndex < commandMethodParameters.Count; paramIndex++)
        {
            var p = commandMethodParameters[paramIndex];

            // cria o descritor
            var paramDescriptor = ParameterDescriptor.Create(p, context.SemanticModel);

            // valida CancellationToken, só pode haver caso o método seja assíncrono
            if (paramDescriptor.Type.IsCancellationToken && !isAsync)
            {
                var diagnostic = Diagnostic.Create(
                    CmdDiagnostics.CancellationTokenParameterMustBeAsync,
                    location: p.Identifier.GetLocation());

                return diagnostic;
            }

            if (paramIndex == 0 && editType is not null)
            {
                // quando há EditEntityAttribute o primeiro parâmetro deverá ser do mesmo tipo informado no attr.

                if (!Equals(paramDescriptor.Type, editType.EntityType))
                {
                    var diagnostic = Diagnostic.Create(
                    CmdDiagnostics.EditEntityRequiresFirstParameter,
                    location: p.Identifier.GetLocation());

                    return diagnostic;
                }

                paramDescriptor.Type.MarkAsEntity();
                editType.Parameter = paramDescriptor;
            }
            else if (hasUow || hasFindEntities)
            {
                // quando ter accessor valida se o parâmetro é uma entidade

                // se for entidade, adiciona a informação no parâmetro
                if (p.IsEntity(context.SemanticModel))
                {
                    paramDescriptor.Type.MarkAsEntity();

                    // tenta obter a propriedade com nome relacionado
                    var idProperty = classDeclaration.GetIdProperty(p.Identifier.Text, context.SemanticModel);
                    if (idProperty is null)
                    {
                        var diagnostic = Diagnostic.Create(
                            CmdDiagnostics.EntityTypeParameterDoesNotHaveIdProperty,
                            location: p.Identifier.GetLocation(),
                            p.Identifier.Text);

                        return diagnostic;
                    }

                    var binding = new IdPropertyBoundToEntityParameter(paramDescriptor, idProperty);
                    idPropertiesBindings.Add(binding);
                }
                else if (p.IsCollectionOfEntities(context.SemanticModel))
                {
                    paramDescriptor.Type.MarkAsCollectionOfEntities();

                    // tenta obter a propriedade com nome relacionado
                    var idsProperty = classDeclaration.GetIdsProperty(p.Identifier.Text, context.SemanticModel);
                    if (idsProperty is null)
                    {
                        var diagnostic = Diagnostic.Create(
                            CmdDiagnostics.EntityTypeParameterDoesNotHaveIdProperty,
                            location: p.Identifier.GetLocation(),
                            p.Identifier.Text);

                        return diagnostic;
                    }

                    var binding = new IdPropertyBoundToEntityParameter(paramDescriptor, idsProperty);
                    idPropertiesBindings.Add(binding);
                }
                else if (paramDescriptor.Type.Equals(accessorType))
                {
                    // quando é do tipo do contexto da unidade de trabalho
                    paramDescriptor.Type.MarkAsContext();
                }
            }

            // verifica se o parâmetro tem o attribute WithParameter
            if (p.TryGetAttribute("WithParameter", out _))
            {
                // se o parâmetro estiver marcado como alguma coisa, ele não pode ser marcado como WithParameter
                if (paramDescriptor.Type.IsEntity ||
                    paramDescriptor.Type.IsCollectionOfEntities ||
                    paramDescriptor.Type.IsContext)
                {
                    var diagnostic = Diagnostic.Create(
                        CmdDiagnostics.ParameterCannotBeMarkedWithParameter,
                        location: p.Identifier.GetLocation());

                    return diagnostic;
                }

                paramDescriptor.Type.MarkAsHandlerParameter();
            }

            parameters.Add(paramDescriptor);
        }

        // após processar os parâmetros e existir EditEntityType, valida se o nome do parâmetro foi preenchido
        if (editType is not null && editType.Parameter is null)
        {
            var diagnostic = Diagnostic.Create(
                CmdDiagnostics.EditEntityRequiresFirstParameter,
                location: method.Identifier.GetLocation());

            return diagnostic;
        }

        // obtém informações para geração do WasValidated, caso seja possível
        List<string> notNullProperties;
        if (classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            && hasProblemsMethod is not null)
        {
            // obtém atributos MemberNotNullWhen do método
            // depois obtém os parâmetros a partir do segundo parâmetro do atributo
            // e converte para string no formato de parâmetros
            notNullProperties = hasProblemsMethod.GetAttributes("MemberNotNullWhen")
                .Where(a => a.ArgumentList?.Arguments.Count > 1)
                .Select(a => string.Join(", ", a.ArgumentList!.Arguments.Skip(1).Select(m => m.Expression.ToString())))
                .ToList();
        }
        else
        {
            notNullProperties = [];
        }

        // nome da classe que tem o método com o attribute
        var modelName = classDeclaration.Identifier.Text;

        // verifica a necessidade do método do handler ser assíncrono
        var handlerMustBeAsync = isAsync || hasWithDecorators || hasUow || hasFindEntities;

        // Define o tipo de retorno do handler
        var handlerReturnType = methodReturnType;
        if (handlerMustBeAsync)
            handlerReturnType = handlerReturnType.MustBeTask();
        if (hasWithValidateModel || hasUow || hasFindEntities)
            handlerReturnType = handlerReturnType.MustBeResult();

        // lê atributo Map... da classe
        var mapInformation = ReadMap(classDeclaration, context.SemanticModel);

        // se map não for nulo, e tiver o MapIdResultValue, deve ser validado se o tipo retornado tem o campo Id
        if (mapInformation is not null &&
            mapInformation.MapIdResultValue &&
            !method.ReturnType.ValidateMapIdResultValue(context.SemanticModel, out error))
        {
            return error!;
        }

        // armazena todas as informações coletadas
        var info = new CommandHandlerInformation
        {
            ModelType = new(modelName, [classDeclaration.GetNamespace()]),
            HasWithValidateModel = hasWithValidateModel,
            HasWithDecorators = hasWithDecorators,
            MethodName = method.Identifier.Text,
            MethodReturnType = methodReturnType,
            HandlerReturnType = handlerReturnType,
            MethodIsAsync = isAsync,
            HandlerMustBeAsync = handlerMustBeAsync,
            Parameters = parameters,
            HandlerInterfaceName = $"I{modelName}Handler",
            HandlerImplementationName = $"{modelName}Handler",
            NotNullProperties = notNullProperties,
            HasWithUnitOfWork = hasUow,
            HasWithFindEntities = hasFindEntities,
            ContextAccessorType = accessorType,
            IdPropertiesBindings = idPropertiesBindings,
            ProduceProblems = produceProblems,
            ProduceNewEntityType = newEntityType,
            EditType = editType,
            MapInformation = mapInformation
        };

        return info;
    }

    private static MapInformation? ReadMap(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        AttributeSyntax? attr = null;
        string? httpMethod = null;
        string? description = null;
        string? groupName = null;
        MapCreatedInformation? createdInformation = null;
        bool hasMapIdResultValue = false;
        MapResponseValuesInformation? responseValues = null;

        if (classDeclaration.TryGetAttribute(MapPostAttributeName, out attr))
        {
            httpMethod = "Post";
        }
        else if (classDeclaration.TryGetAttribute(MapPutAttributeName, out attr))
        {
            httpMethod = "Put";
        }
        else if (classDeclaration.TryGetAttribute(MapPatchAttributeName, out attr))
        {
            httpMethod = "Patch";
        }
        else if (classDeclaration.TryGetAttribute(MapDeleteAttributeName, out attr))
        {
            httpMethod = "Delete";
        }
        else if (classDeclaration.TryGetAttribute(MapGetAttributeName, out attr))
        {
            httpMethod = "Get";
        }

        if (attr is null || httpMethod is null)
            return null;

        // deve ler os parâmetros do atributo
        var endpointRoutePattern = attr.ArgumentList?.Arguments[0].Expression.ToString();
        var endpointName = attr.ArgumentList?.Arguments[1].Expression.ToString();

        if (endpointRoutePattern is null || endpointName is null)
            return null;

        // tenta obter a descrição também
        if (classDeclaration.TryGetAttribute("Description", out var descAttr) && descAttr!.ArgumentList?.Arguments.Count is 1)
            description = descAttr.ArgumentList.Arguments[1].Expression.ToString();

        // tenta obter o MapGroup attribute
        if (classDeclaration.TryGetAttribute(MapGroupAttributeName, out var groupAttr) && groupAttr!.ArgumentList?.Arguments.Count is 1)
            groupName = groupAttr.ArgumentList.Arguments[0].Expression.ToString().RemoveQuotes();

        // tenta obter MapCreatedRoute
        if (classDeclaration.TryGetAttribute(MapCreatedRouteAttributeName, out var createdRouteAttr))
        {
            var arguments = createdRouteAttr!.ArgumentList?.Arguments;
            if (arguments is not null && arguments.Value.Count > 0)
            {
                // o primeiro parâmetro é a route pattern
                var routePattern = arguments.Value[0].Expression.ToString();
                // pode haver outros parâmetros vindos do (params string[])
                var propertiesNames = arguments.Value.Count > 1
                    ? arguments.Value.Skip(1).Select(a => a.Expression.ToString()).ToArray()
                    : [];

                createdInformation = new MapCreatedInformation(routePattern, propertiesNames);
            }
        }

        // tenta obter MapIdResultValue
        hasMapIdResultValue = classDeclaration.TryGetAttribute(MapIdResultValueAttributeName, out _);

        // tenta obter MapResponseValues e seus parâmetros
        if (classDeclaration.TryGetAttribute(MapResponseValuesAttributeName, out var resultValueAttr))
        {
            var arguments = resultValueAttr!.ArgumentList?.Arguments;
            if (arguments is not null && arguments.Value.Count > 0)
            {
                var propertiesNames = arguments.Value.Select(a => a.Expression.ToString()).ToArray();

                // para cada propriedade, obtém o membro do comando que corresponde a ela,
                // então valida se é uma propriedade, e cria um PropertyDescription
                var properties = propertiesNames.Select(name =>
                    {
                        // obtém o membro do comando que corresponde a ela
                        var property = classDeclaration.Members
                            .OfType<PropertyDeclarationSyntax>()
                            .FirstOrDefault(p => p.Identifier.Text == name);

                        // cria o PropertyDescription, quando existir a propriedade
                        return property is null
                            ? null
                            : PropertyDescriptor.Create(property, semanticModel);
                    })
                    .Where(p => p is not null)
                    .ToList();


                responseValues = new MapResponseValuesInformation(properties!);
            }
        }

        return new MapInformation
        {
            HttpMethod = httpMethod,
            RoutePattern = endpointRoutePattern,
            EndpointName = endpointName,
            Description = description,
            GroupName = groupName,
            CreatedInformation = createdInformation,
            MapIdResultValue = hasMapIdResultValue,
            ResponseValues = responseValues
        };
    }

    internal static ClassGenerator GenerateInterface(CommandHandlerInformation i)
    {
        // cria interface do handler
        var interfaceGen = new ClassGenerator(i.HandlerInterfaceName, i.Namespace, "interface");
        interfaceGen.Modifiers.Public();
        interfaceGen.Usings.AddNamespaces(i.HandlerReturnType.Namespaces);

        // cria o method para a interface
        var handlerMethodDef = new MethodGenerator(i.HandlerMustBeAsync ? "HandleAsync" : "Handle", i.HandlerReturnType)
        {
            IsAbstract = true
        };

        // método público
        handlerMethodDef.Modifiers.Public();

        AddRequiredParameters(i, handlerMethodDef);

        // adiciona o método
        interfaceGen.Methods.Add(handlerMethodDef);

        return interfaceGen;
    }

    internal static ClassGenerator GenerateImplementation(CommandHandlerInformation i, MethodGenerator interfaceHandlerMethod)
    {
        // cria classe que implementa o handler
        var handlerGen = new ClassGenerator(i.HandlerImplementationName, $"{i.Namespace}.Internals");
        handlerGen.Modifiers.Public();
        handlerGen.Hierarchy.AddImplements(i.HandlerInterfaceName);
        handlerGen.Usings.AddNamespace(i.Namespace);

        // cria os campos e o construtor
        var ctorGen = new ConstructorGenerator(i.HandlerImplementationName);
        ctorGen.Modifiers.Public();
        if (i.HasWithUnitOfWork)
        {
            var uowType = new TypeDescriptor(
                string.Format(UowAccessorType, i.ContextAccessorType!.Name),
                [CommandNamespace, .. i.ContextAccessorType.Namespaces]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(uowType, AccessorVarName, true));
            // adiciona o parametro
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(uowType, AccessorVarName)));
            // adiciona comando de atribuíção
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(AccessorVarName));
            // adiciona os namespaces
            handlerGen.Usings.AddNamespaces(uowType);
        }
        if (i.HasWithFindEntities)
        {
            var repoType = new TypeDescriptor(
                string.Format(RepoAccessorType, i.ContextAccessorType!.Name),
                [CommandNamespace, .. i.ContextAccessorType.Namespaces]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(repoType, AccessorVarName, true));
            // adiciona o parametro
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(repoType, AccessorVarName)));
            // adiciona comando de atribuíção
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(AccessorVarName));
            // adiciona os namespaces
            handlerGen.Usings.AddNamespaces(repoType);
        }
        if (i.HasWithDecorators)
        {
            var decoratorsType = new TypeDescriptor(
                string.Format(DecoratorType, i.ModelType.Name, i.MethodReturnType.Name.TryGetInnerTaskType()),
                [CommandNamespace]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(decoratorsType, DecoratorsVarName, true));
            // adiciona o parametro
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(decoratorsType, DecoratorsVarName)));
            // adiciona comando de atribuíção
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(DecoratorsVarName));
            // adiciona os namespaces
            handlerGen.Usings.AddNamespaces(decoratorsType);
        }

        // para cada parâmetro do método do comando, valida se é necessário adicionar como campo do construtor.
        foreach (var p in i.Parameters)
        {
            // não requer ct
            if (p.Type.IsCancellationToken)
                continue;

            // adiciona os namespaces
            handlerGen.Usings.AddNamespaces(p);

            // se for uma entidade, não deve recebê-la no construtor.
            if (p.Type.IsEntity || p.Type.IsCollectionOfEntities)
                continue;

            // se for o contexto, não deve recebê-la no construtor.
            if (p.Type.IsContext)
                continue;

            // se for um parâmetro marcado com WithParameter, não deve recebê-lo no construtor.
            if (p.Type.IsHandlerParameter)
                continue;

            // Adiciona o parâmetro como dependência do handler e o recebe no construtor.
            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(p.Type, p.Name, true));
            // adiciona o parâmetro
            ctorGen.Parameters.Add(new ParameterGenerator(p));
            // adiciona comando de atribuíção
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(p.Name));
        }
        // adiciona o ctor a classe apenas se tiver algum parâmetro, quando não há parâmetros, não há necessidade de ctor.
        if (ctorGen.Parameters.Any())
            handlerGen.Constructors.Add(ctorGen);



        // cria method da classe
        var handlerMethodImpl = handlerGen.CreateImplementation(interfaceHandlerMethod);
        if (i.HandlerMustBeAsync)
            handlerMethodImpl.Modifiers.Async();

        // adiciona comandos da implementação do método

        // comando de validação
        if (i.HasWithValidateModel)
            handlerMethodImpl.Commands.Add(new ValidateHasProblemsCommand(ModelVarName));

        // comando unit of work begin
        if (i.HasWithUnitOfWork)
            handlerMethodImpl.Commands.Add(new BeginUnitOfWorkCommand(AccessorVarName));

        // se tem entidades com Id, então cria variável de notFound
        if (i.IdPropertiesBindings.Count > 0 || i.EditType is not null)
        {
            handlerMethodImpl.Commands.Add(new DeclareNotFoundProblemsCommand());

            // carrega o parâmetro do entidade a ser editada (quando existe EditEntityAttribute)
            if (i.EditType is not null)
            {
                var findEditEntity = new FindEditEntityCommand(i.EditType, AccessorVarName);
                handlerMethodImpl.Commands.Add(findEditEntity);
            }

            // carrega os parâmetros que são entidades vinculados a propriedades
            foreach (var binding in i.IdPropertiesBindings)
            {
                GeneratorNode? findCmd = binding.Parameter.Type switch
                {
                    { IsEntity: true }
                        => new FindEntityCommand(binding.Parameter, binding.Property, AccessorVarName, ModelVarName),
                    { IsCollectionOfEntities: true }
                        => new FindEntitiesCommand(binding.Parameter, binding.Property, AccessorVarName, ModelVarName),
                    _ => null
                };

                if (findCmd is not null)
                    handlerMethodImpl.Commands.Add(findCmd);
            }
        }

        // Geração da chamada do comando no modelo e do retorno do método do handler
        // Dependendo do cenário, essa parte final pode váriar.
        // Essa parte final será representada por um GenerateNode e ela será incremental.
        GeneratorNode final;

        // flag que determina que o "final" requer o "return {final};"
        bool useReturn = true;

        // gera invoke do método do model
        var invoke = new MethodInvokeGenerator(ModelVarName, i.MethodName);
        if (i.MethodIsAsync)
            invoke.Await = true;
        // para cada parâmetro do método do comando
        foreach (var p in i.Parameters)
        {
            ValueNode argument;
            if (p.Type.IsCancellationToken)
                argument = "ct";
            else if (p.Type.IsContext)
                argument = $"this.{AccessorVarName}.Context";
            else if (p.Type is { IsCollectionOfEntities: true, IsArray: true })
                argument = $"{p.Name}.ToArray()";
            else
                argument = p.Name;

            invoke.AddArgument(argument);
        }

        // primeira parte da geração final, a chamada do comando do modelo
        final = invoke;

        if (i.HasWithDecorators)
        {
            var lambda = new LambdaGenerator();
            if (i.MethodIsAsync)
                lambda.Async = true;
            else if (i.HandlerMustBeAsync)
                final = new MethodInvokeGenerator("Task", "FromResult", final);
            lambda.Commands.Add(final);

            var newMediator = new MediatorCreateCommand(
                DecoratorsMediatorVarName,
                i.ModelType.Name,
                i.MethodReturnType.Name,
                $"this.{DecoratorsVarName}",
                lambda,
                ModelVarName,
                "ct");

            // adiciona o comando que cria nova instância do mediador.
            handlerMethodImpl.Commands.Add(newMediator);

            // a invocação do mediador passa a ser a geração final
            final = newMediator.CreateInvokeNextAsync();
        }

        if (i.HasWithUnitOfWork)
        {
            // a invocação do 'final' será assíncrona se o método do comando for assíncrono ou se tiver decorators.
            var isInvokeAsync = i.MethodIsAsync || i.HasWithDecorators;

            var produceNewEntity = i.ProduceNewEntityType is not null;

            final = new CompleteUnitOfWorkCommand(
                final, isInvokeAsync, i.MethodReturnType, AccessorVarName, CommandResultVarName, produceNewEntity);

            useReturn = false;
        }

        if (useReturn)
            final = new ReturnCommand(final);

        handlerMethodImpl.Commands.Add(final);

        return handlerGen;
    }

    internal static ClassGenerator? GenerateWasValidated(CommandHandlerInformation i)
    {
        if (i.NotNullProperties.Count is 0)
            return null;


        var partialClass = new ClassGenerator(i.ModelType.Name, i.Namespace)
        {
            FileName = $"{i.ModelType.Name}_WasValidated.g.cs"
        };
        partialClass.Modifiers.Public();
        partialClass.Modifiers.Partial();
        partialClass.Usings.AddNamespace("System.Runtime.CompilerServices");
        partialClass.Usings.AddNamespace("System.Diagnostics.CodeAnalysis");

        var method = new MethodGenerator("WasValidated", TypeDescriptor.Void);
        method.Modifiers.Internal();
        method.Modifiers.Protected();
        method.Attributes.Add(new AttributeGenerator("MethodImpl", "MethodImplOptions.AggressiveInlining"));
        partialClass.Methods.Add(method);

        // para cada atributo MemberNotNullWhen
        // criar um atributo MemberNotNull no método WasValidated
        foreach (var member in i.NotNullProperties)
        {
            method.Attributes.Add(new AttributeGenerator("MemberNotNull", member));
        }

        partialClass.Generating += (_, builder) =>
        {
            builder.AppendLine();
            builder.AppendLine("#nullable disable");
            builder.AppendLine("#pragma warning disable");
            builder.AppendLine();
        };

        return partialClass;
    }

    public static void AddRequiredParameters(CommandHandlerInformation commandInfo, MethodGenerator method)
    {
        // parâmetro do id da entidade a ser editada, quando necessário
        if (commandInfo.EditType is not null)
            method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(commandInfo.EditType.IdType, $"{commandInfo.EditType.Parameter.Name}Id")));

        // parâmetro do commando.
        method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(commandInfo.ModelType, ModelVarName)));

        // parâmetros com atributo WithParameter
        foreach (var p in commandInfo.Parameters.Where(p => p.Type.IsHandlerParameter))
            method.Parameters.Add(new ParameterGenerator(p));

        // cancellation token, quando necessário (async)
        if (commandInfo.HandlerMustBeAsync)
            method.Parameters.Add(new ParameterGenerator(ParameterDescriptor.CancellationToken));
    }
}
