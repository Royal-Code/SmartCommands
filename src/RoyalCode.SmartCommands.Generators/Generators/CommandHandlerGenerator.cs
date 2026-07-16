using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.SmartCommands.Generators.Commands;
using RoyalCode.SmartCommands.Generators.Models;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.Extensions.SourceGenerator.Descriptors.Snapshots;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using System.Reflection;
using static RoyalCode.SmartCommands.Generators.Generators.CommandHandlerInformation;

namespace RoyalCode.SmartCommands.Generators.Generators;

//#pragma warning disable S125 // remover blocos de código comentados

internal static class CommandHandlerGenerator
{
    public const string CommandAttributeName = "RoyalCode.SmartCommands.CommandAttribute";

    private const string CommandNamespace = "RoyalCode.SmartCommands";
    private const string WithValidateModelAttributeName = "WithValidateModel";
    private const string WithDecoratorsAttributeName = "WithDecorators";
    private const string WithUnitOfWorkAttributeName = "WithUnitOfWork";
    private const string WithDbContextAttributeName = "WithDbContext";
    private const string WithWorkContextAttributeName = "WithWorkContext";
    private const string WithRetryOnConcurrencyAttributeName = "WithRetryOnConcurrency";
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
    private const string WithDescriptionAttributeName = "WithDescription";
    private const string WithSummaryAttributeName = "WithSummary";
    private const string WithAuthorizationAttributeName = "WithAuthorization";
    private const string WithPolicyAttributeName = "WithPolicy";

    private const string EditEntityAttributeName = "EditEntity";
    private const string ModelVarName = "command";
    private const string CancellationTokenParameterName = "ct";
    private const string DecoratorsVarName = "decorators";
    private const string DecoratorsMediatorVarName = "decoratorsMediator";
    private const string CommandResultVarName = "commandResult";
    private const string DecoratorType = "IEnumerable<IDecorator<{0}, {1}>>";
    private const string AccessorVarName = "accessor";
    private const string RetryOptionsVarName = "retryOptions";
    private const string RetryProblemFactoryVarName = "retryProblemFactory";
    internal const string EndpointHandlerParameterName = "handler";
    internal const string EndpointResultVarName = "result";
    private const string UowAccessorType = "IUnitOfWorkAccessor<{0}>";
    private const string RepoAccessorType = "IRepositoriesAccessor<{0}>";

    public static bool Predicate(SyntaxNode node, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return node is MethodDeclarationSyntax;
    }

    public static GenerationCandidate<CommandModel> Transform(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var information = TransformWorking(context, cancellationToken);
        var diagnostics = PipelineDiagnostic.Snapshot(information.Diagnostics);
        return diagnostics.IsEmpty
            ? GenerationCandidate<CommandModel>.Valid(CommandModel.Create(information))
            : GenerationCandidate<CommandModel>.Invalid(diagnostics);
    }

    private static CommandHandlerInformation TransformWorking(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        // método do comando, ou seja, que contém o attribute Command
        var method = (MethodDeclarationSyntax)context.TargetNode;

        // obtém a classe que contém o método
        if (method.Parent is not ClassDeclarationSyntax classDeclaration)
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                location: method.Identifier.GetLocation(),
                "The method does not have a class declaration");

            return new CommandHandlerInformation(diagnostic);
        }

        // lista de erros
        var errors = new List<DiagnosticInfo>();

        var commandType = context.TargetSymbol.ContainingType;
        var commandMethodCount = commandType.GetMembers()
            .OfType<IMethodSymbol>()
            .Count(candidate => candidate.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == CommandAttributeName));
        if (commandMethodCount > 1)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.MultipleCommandMethods,
                method.Identifier.GetLocation(),
                commandType.Name));
        }

        var mapAttributeCount = commandType.GetAttributes().Count(attribute => IsMapAttribute(attribute.AttributeClass));
        if (mapAttributeCount > 1)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.ConflictingMapAttributes,
                classDeclaration.Identifier.GetLocation(),
                commandType.Name));
        }

        // nem o método nem a classe pode ter argumentos genéricos
        if (method.TypeParameterList is not null || classDeclaration.TypeParameterList is not null)
        {
            var diagnostic = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                location: method.Identifier.GetLocation(),
                "Neither the method nor the class can have generic arguments");

            errors.Add(diagnostic);
        }

        // a classe do comando não pode ser aninhada: o handler gerado referencia o tipo por nome simples
        // no namespace e um tipo aninhado não seria resolvível a partir daí.
        if (commandType.ContainingType is not null)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidCommandType,
                location: classDeclaration.Identifier.GetLocation(),
                "The command type must be a top-level type, not a nested type"));
        }

        // a classe do comando não pode ser file-local: o handler é emitido em outra árvore sintática
        // e não conseguiria referenciar o tipo.
        if (commandType.IsFileLocal)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidCommandType,
                location: classDeclaration.Identifier.GetLocation(),
                "The command type must not be a file-local type (declared with the 'file' modifier)"));
        }

        // o método do comando deve ser de instância, não abstrato e acessível ao handler gerado (mesmo assembly),
        // pois o handler o invoca como 'command.Metodo(...)' a partir de outra classe.
        var methodSymbol = (IMethodSymbol)context.TargetSymbol;
        if (methodSymbol.IsStatic)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidCommandType,
                location: method.Identifier.GetLocation(),
                "The command method must be an instance method"));
        }
        if (methodSymbol.IsAbstract)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidCommandType,
                location: method.Identifier.GetLocation(),
                "The command method must not be abstract"));
        }
        if (methodSymbol.DeclaredAccessibility is not (
                Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidCommandType,
                location: method.Identifier.GetLocation(),
                "The command method must be accessible to the generated handler (public or internal)"));
        }

        // lista dos parâmetros de ProblemCategory que o comando pode produzir
        // esta lista é preenchida a partir do método HasProblems da classe e do método do comando
        // através do attribute ProduceProblems
        List<string> produceProblems = [];

        // verifica se existe problemas no método da do comando
        if (method.TryGetAttribute(ProduceProblemsAttributeName, out AttributeSyntax? produceProblemsAttr))
        {
            // se tem o attribute, extrai os parâmetros
            var problemsProduced = produceProblemsAttr!.ArgumentList?.Arguments.Select(a => a.Expression.ToString());
            if (problemsProduced is not null)
                produceProblems.AddRange(problemsProduced);
        }

        MethodDeclarationSyntax? hasProblemsMethod = null;

        // Verifica se o método possui o atributo WithValidateModel
        // Se tiver, busca pelo método na classe e já valida se está dentro do padrão
        var hasWithValidateModel = method.TryGetAttribute(WithValidateModelAttributeName, out AttributeSyntax? withValidateModelAttr);
        if (withValidateModelAttr is not null
            && !classDeclaration.ValidateClassWithHasProblemsMethod(
                withValidateModelAttr,
                out hasProblemsMethod,
                out var error))
        {
            // quando há erros, não deve considerar o atributo, além de adicionar o diagnostic
            // isso não gerará o código de validação e mostrará o erro na saída.
            errors.Add(error!);
            hasWithValidateModel = false;
        }

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
        {
            // quando há erros, não deve considerar o atributo, além de adicionar o diagnostic
            // isso não gerará o código de decoradores e mostrará o erro na saída.
            errors.Add(error!);
            hasWithDecorators = false;
        }

        // verifica se tem o attribute WithUnitOfWork
        TypeDescriptor? accessorType = null;
        ContextAccessorModes contextAccessorMode = ContextAccessorModes.None;
        var hasUow = method.TryGetAttribute(WithUnitOfWorkAttributeName, out AttributeSyntax? withUowAttr);
        if (hasUow)
        {
            if (TryGetGenericTypeArguments(withUowAttr, 1, out var typeArguments))
            {
                accessorType = TypeDescriptor.Create(typeArguments[0], context.SemanticModel);
                contextAccessorMode = ContextAccessorModes.Specified;
            }
            else
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidCommandType,
                    withUowAttr?.GetLocation() ?? method.Identifier.GetLocation(),
                    "WithUnitOfWorkAttribute requires one context type argument"));
                hasUow = false;
            }
        }
        
        // verifica se tem o attribute WithDbContext
        var hasDbContext = method.TryGetAttribute(WithDbContextAttributeName, out AttributeSyntax? withDbContextAttr);
        if (hasDbContext)
        {
            if (withUowAttr is not null)
            {
                // se já tem WithUnitOfWork, não pode ter WithDbContext
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.WithDbContextCannotBeUsedWithWithUnitOfWork,
                    location: method.Identifier.GetLocation());
                errors.Add(error);
            }

            // se tem db context, determina o tipo do contexto para uow
            hasUow = true;
            contextAccessorMode = ContextAccessorModes.DbContext;
            accessorType = new TypeDescriptor("DbContext", ["Microsoft.EntityFrameworkCore"]);
        }
        
        // verifica se tem o attribute WithWorkContext
        var hasWorkContext = method.TryGetAttribute(WithWorkContextAttributeName, out AttributeSyntax? _);
        if (hasWorkContext)
        {
            if (withUowAttr is not null)
            {
                // se já tem WithUnitOfWork, não pode ter WithWorkContext
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.WithWorkContextCannotBeUsedWithWithUnitOfWork,
                    location: method.Identifier.GetLocation());

                errors.Add(error);
            }
            else if (withDbContextAttr is not null)
            {
                // se já tem WithDbContext, não pode ter WithWorkContext
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.WithWorkContextCannotBeUsedWithWithDbContext,
                    location: method.Identifier.GetLocation());

                errors.Add(error);
            }

            // se tem work context, determina o tipo do contexto para uow
            hasUow = true;
            contextAccessorMode = ContextAccessorModes.WorkContext;
            accessorType = new TypeDescriptor("IWorkContext", ["RoyalCode.WorkContext"]);
        }

        // verifica se tem WithRetryOnConcurrency (opt-in; só suportado com WorkContext nesta versão)
        var hasRetryOnConcurrency = method.TryGetAttribute(WithRetryOnConcurrencyAttributeName, out AttributeSyntax? retryAttr);
        int? retryMaxAttempts = null;
        string? retryOperation = null;
        if (hasRetryOnConcurrency)
        {
            if (!hasWorkContext)
            {
                // retry exige o auto-save do WorkContext (laço envolve Begin → finds → Execute → Complete)
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.RetryOnConcurrencyRequiresWorkContext,
                    location: method.Identifier.GetLocation());
                errors.Add(error);
                hasRetryOnConcurrency = false;
            }
            else if (retryAttr!.ArgumentList?.Arguments.Count > 0)
            {
                foreach (var argument in retryAttr.ArgumentList.Arguments)
                {
                    var argumentName = argument.NameEquals?.Name.Identifier.Text
                        ?? argument.NameColon?.Name.Identifier.Text;

                    var constant = context.SemanticModel.GetConstantValue(argument.Expression);
                    if (!constant.HasValue)
                        continue;

                    var isMaxAttemptsArgument = argumentName is null
                        || string.Equals(argumentName, "maxAttempts", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(argumentName, "MaxAttempts", StringComparison.Ordinal);

                    if (isMaxAttemptsArgument && constant.Value is int maxAttempts)
                    {
                        // valor explícito no atributo sobrescreve as options; <= 0 é inválido
                        if (maxAttempts <= 0)
                        {
                            error = DiagnosticInfo.Create(
                                CmdDiagnostics.RetryOnConcurrencyInvalidMaxAttempts,
                                location: method.Identifier.GetLocation());
                            errors.Add(error);
                        }
                        else
                        {
                            retryMaxAttempts = maxAttempts;
                        }

                        continue;
                    }

                    var isOperationArgument = argumentName is null
                        || string.Equals(argumentName, "operation", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(argumentName, "Operation", StringComparison.Ordinal);

                    if (isOperationArgument && constant.Value is string operation)
                    {
                        if (string.IsNullOrWhiteSpace(operation))
                        {
                            error = DiagnosticInfo.Create(
                                CmdDiagnostics.InvalidCommandType,
                                location: method.Identifier.GetLocation(),
                                "The retry operation must not be empty");
                            errors.Add(error);
                        }
                        else
                        {
                            retryOperation = operation;
                        }
                    }
                }
            }
        }

        // verifica se tem WithFindEntities (se tiver hasUow, não precisa verificar)
        var hasFindEntities = false;
        if (!hasUow)
        {
            hasFindEntities = method.TryGetAttribute(WithFindEntitiesAttributeName, out AttributeSyntax? withFindEntitiesAttr);
            if (hasFindEntities)
            {
                if (TryGetGenericTypeArguments(withFindEntitiesAttr, 1, out var typeArguments))
                {
                    accessorType = TypeDescriptor.Create(typeArguments[0], context.SemanticModel);
                }
                else
                {
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.InvalidCommandType,
                        withFindEntitiesAttr?.GetLocation() ?? method.Identifier.GetLocation(),
                        "WithFindEntitiesAttribute requires one context type argument"));
                    hasFindEntities = false;
                }
            }
        }

        // verifica se retorna uma nova entidade
        var hasProduceNewEntity = method.TryGetAttribute(ProduceNewEntityAttributeName, out _);
        if (hasProduceNewEntity && !hasUow)
        {
            // quando retorna entidade, deve haver uow
            error = DiagnosticInfo.Create(
                CmdDiagnostics.ProduceNewEntityRequiresWithUnitOfWork,
                location: method.Identifier.GetLocation());

            errors.Add(error);
        }

        // verifica se edita uma entidade existente
        EditTypeDescriptor? editType = null;
        var hasEditEntity = method.TryGetAttribute(EditEntityAttributeName, out AttributeSyntax? editEntityAttr);
        if (hasEditEntity)
        {
            // editar entidade requer uow
            if (!hasUow)
            {
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.EditEntityRequiresWithUnitOfWork,
                    location: method.Identifier.GetLocation());

                errors.Add(error);
            }

            // se já tem produce new entity, não pode ter edit entity
            if (hasProduceNewEntity)
            {
                error = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                    location: method.Identifier.GetLocation(),
                    "The method cannot have both ProduceNewEntity and EditEntity attributes");

                errors.Add(error);
            }

            if (TryGetGenericTypeArguments(editEntityAttr, 2, out var typeArguments))
            {
                editType = new EditTypeDescriptor(
                    TypeDescriptor.Create(typeArguments[0], context.SemanticModel),
                    TypeDescriptor.Create(typeArguments[1], context.SemanticModel));
            }
            else
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidCommandType,
                    editEntityAttr?.GetLocation() ?? method.Identifier.GetLocation(),
                    "EditEntityAttribute requires entity and id type arguments"));
            }
        }

        // Classificação semântica e symbol-free do retorno. A emissão usa Task como formato normalizado também
        // para métodos ValueTask, mas o modelo preserva o tipo declarado e seu payload.
        var declaredMethodReturnType = TypeDescriptor.Create(method.ReturnType, context.SemanticModel);
        var returnModel = ReturnModel.Create(TypeSnapshot.Create(declaredMethodReturnType));
        var isAsync = returnModel.IsAwaitable;
        var methodReturnType = PipelineModelConversions.ToLegacyMethodReturn(returnModel);

        // se produz uma nova entidade, então o retorno do método será a nova entidade
        TypeSyntax? newEntityTypeSyntax = null;
        TypeDescriptor? newEntityType = null;
        if (hasProduceNewEntity)
        {
            newEntityTypeSyntax = method.ReturnType;

            // se for uma Task, extrai o tipo genérico, senão é o próprio tipo
            if (isAsync)
                newEntityTypeSyntax = newEntityTypeSyntax.GetInnerType();

            var newEntityTypeInfo = context.SemanticModel.GetTypeInfo(newEntityTypeSyntax);
            if (newEntityTypeInfo.Type is not INamedTypeSymbol newEntityNameSymbol)
            {
                error = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                    location: method.Identifier.GetLocation(),
                    "It was not possible to determine the return type of the new entity");

                errors.Add(error);
            }
            else
            {
                // se for um Result, deve ser o tipo genérico
                if (newEntityNameSymbol.Name.StartsWith("Result"))
                {
                    // O Result deve ser genérico, se não for, retorna um erro.
                    if (!newEntityNameSymbol.IsGenericType)
                    {
                        error = DiagnosticInfo.Create(
                            CmdDiagnostics.ProduceNewEntityMustReturnResultWithValue,
                            location: method.ReturnType.GetLocation());

                        errors.Add(error);
                    }

                    // extrai o tipo genérico do Result
                    newEntityTypeSyntax = newEntityTypeSyntax.GetInnerType();
                }
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
            cancellationToken.ThrowIfCancellationRequested();
            var p = commandMethodParameters[paramIndex];

            // cria o descritor
            var paramDescriptor = ParameterDescriptor.Create(p, context.SemanticModel);

            // valida CancellationToken, só pode haver caso o método seja assíncrono
            if (paramDescriptor.Type.IsCancellationToken && !isAsync)
            {
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.CancellationTokenParameterMustBeAsync,
                    location: p.Identifier.GetLocation());

                errors.Add(error);
            }

            if (paramIndex == 0 && editType is not null)
            {
                // quando há EditEntityAttribute o primeiro parâmetro deverá ser do mesmo tipo informado no attr.

                if (!Equals(paramDescriptor.Type, editType.EntityType))
                {
                    error = DiagnosticInfo.Create(
                    CmdDiagnostics.EditEntityRequiresFirstParameter,
                    location: p.Identifier.GetLocation());

                    errors.Add(error);
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
                        error = DiagnosticInfo.Create(
                            CmdDiagnostics.EntityTypeParameterDoesNotHaveIdProperty,
                            location: p.Identifier.GetLocation(),
                            p.Identifier.Text);

                        errors.Add(error);
                    }
                    else
                    {
                        var binding = new IdPropertyBoundToEntityParameter(paramDescriptor, idProperty);
                        idPropertiesBindings.Add(binding);
                    }
                }
                else if (p.IsCollectionOfEntities(context.SemanticModel))
                {
                    paramDescriptor.Type.MarkAsCollectionOfEntities();

                    // tenta obter a propriedade com nome relacionado
                    var idsProperty = classDeclaration.GetIdsProperty(p.Identifier.Text, context.SemanticModel);
                    if (idsProperty is null)
                    {
                        error = DiagnosticInfo.Create(
                            CmdDiagnostics.EntityTypeParameterDoesNotHaveIdProperty,
                            location: p.Identifier.GetLocation(),
                            p.Identifier.Text);

                        errors.Add(error);
                    }
                    else
                    {
                        var binding = new IdPropertyBoundToEntityParameter(paramDescriptor, idsProperty);
                        idPropertiesBindings.Add(binding);
                    }
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
                    error = DiagnosticInfo.Create(
                        CmdDiagnostics.ParameterCannotBeMarkedWithParameter,
                        location: p.Identifier.GetLocation());

                    errors.Add(error);
                }
                else
                {
                    paramDescriptor.Type.MarkAsHandlerParameter();
                }
            }

            parameters.Add(paramDescriptor);
        }

        // após processar os parâmetros e existir EditEntityType, valida se o nome do parâmetro foi preenchido
        if (editType is not null && editType.Parameter is null)
        {
            error = DiagnosticInfo.Create(
                CmdDiagnostics.EditEntityRequiresFirstParameter,
                location: method.Identifier.GetLocation());

            errors.Add(error);
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

        // lê atributo Map... da classe do comando
        var mapInformation = ReadMap(
            classDeclaration,
            newEntityTypeSyntax ?? method.ReturnType.GetValueReturnType(),
            context.SemanticModel,
            errors);

        // DF5: os nomes que o handler gerado emite no mesmo escopo são reservados; um parâmetro do comando que
        // caia nesse escopo (WithParameter, dependência de DI ou entidade carregada) não pode colidir com eles.
        // Token e contexto não introduzem um identificador de usuário (viram 'ct'/'this.accessor.Context').
        var reservedNames = CollectReservedNames(
            handlerMustBeAsync,
            hasAccessor: hasUow || hasFindEntities,
            hasWithUnitOfWork: hasUow,
            hasWithDecorators,
            hasRetryOnConcurrency,
            retryMaxAttempts,
            retryOperation);

        // Quando o comando é mapeado, os parâmetros [WithParameter] são replicados na assinatura do método do
        // endpoint Minimal API, que também declara 'handler', a variável 'result' e, para EditEntity, o
        // parâmetro '{entidade}Id'; esses nomes são reservados apenas nesse escopo.
        var endpointReservedNames = mapInformation is not null
            ? CollectEndpointReservedNames(editType)
            : null;

        for (int reservedIndex = 0; reservedIndex < parameters.Count; reservedIndex++)
        {
            var reservedParameter = parameters[reservedIndex];
            if (reservedParameter.Type.IsCancellationToken || reservedParameter.Type.IsContext)
                continue;

            var collides = reservedNames.Contains(reservedParameter.Name)
                || (endpointReservedNames is not null
                    && reservedParameter.Type.IsHandlerParameter
                    && endpointReservedNames.Contains(reservedParameter.Name));

            if (collides)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.ReservedIdentifier,
                    location: commandMethodParameters[reservedIndex].Identifier.GetLocation(),
                    reservedParameter.Name));
            }
        }

        // se map não for nulo, e tiver o MapIdResultValue, deve ser validado se o tipo retornado tem o campo Id
        if (mapInformation is not null &&
            mapInformation.MapIdResultValue &&
            !method.ReturnType.ValidateMapIdResultValue(context.SemanticModel, out error))
        {
            errors.Add(error!);
        }

        // Define o tipo de retorno do handler
        var handlerReturnType = methodReturnType;
        if (handlerMustBeAsync)
            handlerReturnType = handlerReturnType.MustBeTask();
        if (hasWithValidateModel || hasUow || hasFindEntities || mapInformation is not null)
            handlerReturnType = handlerReturnType.MustBeResult();

        // detecta se o comando tem shape de body no minimal API.
        // Sem isso, o endpoint não precisa receber o comando como parâmetro (pode instanciar via new),
        // e uma requisição sem corpo é válida.
        var hasRequestBody = HasRequestBodyShape(classDeclaration, context.SemanticModel);

        // armazena todas as informações coletadas
        var info = new CommandHandlerInformation
        {
            ModelType = TypeDescriptor.Create(context.TargetSymbol.ContainingType),
            HasWithValidateModel = hasWithValidateModel,
            HasWithDecorators = hasWithDecorators,
            MethodName = method.Identifier.Text,
            MethodReturnType = methodReturnType,
            HandlerReturnType = handlerReturnType,
            ReturnModel = returnModel,
            MethodIsAsync = isAsync,
            HandlerMustBeAsync = handlerMustBeAsync,
            Parameters = parameters,
            HandlerInterfaceName = $"I{modelName}Handler",
            HandlerImplementationName = $"{modelName}Handler",
            NotNullProperties = notNullProperties,
            HasWithUnitOfWork = hasUow,
            HasWithFindEntities = hasFindEntities,
            ContextAccessorType = accessorType,
            ContextAccessorMode = contextAccessorMode,
            IdPropertiesBindings = idPropertiesBindings,
            ProduceProblems = produceProblems,
            ProduceNewEntityType = newEntityType,
            EditType = editType,
            MapInformation = mapInformation,
            HasRetryOnConcurrency = hasRetryOnConcurrency,
            RetryMaxAttempts = retryMaxAttempts,
            RetryOperation = retryOperation,
            HasBodyProperties = hasRequestBody
        };

        info.SetErrors(errors);

        return info;
    }

    private static HashSet<string> CollectReservedNames(
        bool handlerMustBeAsync,
        bool hasAccessor,
        bool hasWithUnitOfWork,
        bool hasWithDecorators,
        bool hasRetryOnConcurrency,
        int? retryMaxAttempts,
        string? retryOperation)
    {
        // nomes que o handler gerado emite como parâmetro/campo/local no mesmo escopo do comando.
        var reserved = new HashSet<string>(StringComparer.Ordinal) { ModelVarName };

        if (handlerMustBeAsync)
            reserved.Add(CancellationTokenParameterName);
        if (hasAccessor)
            reserved.Add(AccessorVarName);
        if (hasWithUnitOfWork)
            reserved.Add(CommandResultVarName);
        if (hasWithDecorators)
        {
            reserved.Add(DecoratorsVarName);
            reserved.Add(DecoratorsMediatorVarName);
        }
        if (hasRetryOnConcurrency && retryMaxAttempts is null)
            reserved.Add(RetryOptionsVarName);
        if (hasRetryOnConcurrency && retryOperation is not null)
            reserved.Add(RetryProblemFactoryVarName);

        return reserved;
    }

    private static HashSet<string> CollectEndpointReservedNames(EditTypeDescriptor? editType)
    {
        // nomes emitidos no escopo do método do endpoint Minimal API, onde os parâmetros [WithParameter]
        // são replicados ('command' e 'ct' já são reservados pelo escopo do handler).
        var reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            EndpointHandlerParameterName,
            EndpointResultVarName,
        };

        if (editType?.Parameter is not null)
            reserved.Add($"{editType.Parameter.Name}Id");

        return reserved;
    }

    private static bool HasRequestBodyShape(ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol commandSymbol)
            return false;

        var hasParameterizedConstructor = commandSymbol.Constructors
            .Any(c => !c.IsStatic
                && c.DeclaredAccessibility == Accessibility.Public
                && c.Parameters.Length > 0);

        if (hasParameterizedConstructor)
            return true;

        var type = (INamedTypeSymbol?)commandSymbol;
        while (type is not null && type.SpecialType != SpecialType.System_Object)
        {
            var hasSettable = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Any(p => !p.IsStatic
                    && p.DeclaredAccessibility == Accessibility.Public
                    && p.SetMethod is not null
                    && p.SetMethod.DeclaredAccessibility == Accessibility.Public);

            if (hasSettable)
                return true;

            type = type.BaseType;
        }

        return false;
    }

    private static bool IsMapAttribute(INamedTypeSymbol? attributeType) =>
        attributeType?.ToDisplayString() is
            "RoyalCode.SmartCommands.MapPostAttribute" or
            "RoyalCode.SmartCommands.MapPutAttribute" or
            "RoyalCode.SmartCommands.MapPatchAttribute" or
            "RoyalCode.SmartCommands.MapDeleteAttribute" or
            "RoyalCode.SmartCommands.MapGetAttribute";

    private static bool TryGetGenericTypeArguments(
        AttributeSyntax? attribute,
        int expectedCount,
        out SeparatedSyntaxList<TypeSyntax> arguments)
    {
        if (attribute?.Name is GenericNameSyntax genericName &&
            genericName.TypeArgumentList.Arguments.Count == expectedCount)
        {
            arguments = genericName.TypeArgumentList.Arguments;
            return true;
        }

        arguments = default;
        return false;
    }

    private static MapInformation? ReadMap(
        ClassDeclarationSyntax classDeclaration,
        TypeSyntax valueReturnType,
        SemanticModel semanticModel,
        List<DiagnosticInfo> errors)
    {
        string? httpMethod = null;
        string? description = null;
        string? summary = null;
        string? groupName = null;
        string[]? authorizationPolicies = null;
        MapCreatedInformation? createdInformation = null;
        TypeDescriptor? idResultValueType = null;
        MapResponseValuesInformation? responseValues = null;

        if (classDeclaration.TryGetAttribute(MapPostAttributeName, out AttributeSyntax? attr))
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

        var mapArguments = attr.ArgumentList?.Arguments;
        if (mapArguments is not { Count: 2 })
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapArguments,
                attr.GetLocation(),
                attr.Name.ToString()));
            return null;
        }

        var endpointRoutePattern = mapArguments.Value[0].Expression.ToString();
        var endpointName = mapArguments.Value[1].Expression.ToString();

        // tenta obter a descrição
        if (classDeclaration.TryGetAttribute(WithDescriptionAttributeName, out AttributeSyntax? descAttr) && descAttr!.ArgumentList?.Arguments.Count is 1)
            description = descAttr.ArgumentList.Arguments[0].Expression.ToString();

        // tenta obter o summary
        if (classDeclaration.TryGetAttribute(WithSummaryAttributeName, out AttributeSyntax? displayNameAttr) && displayNameAttr!.ArgumentList?.Arguments.Count is 1)
            summary = displayNameAttr.ArgumentList.Arguments[0].Expression.ToString();

        // tenta obter o authorization
        if (classDeclaration.TryGetAttribute(WithAuthorizationAttributeName, out AttributeSyntax? authAttr))
            authorizationPolicies = [];

        // se tiver o attribute WithPolicy, deve obter o(s) nome(s) da(s) política(s)
        if (classDeclaration.TryGetAttribute(WithPolicyAttributeName, out AttributeSyntax? policyAttr))
        {
            var arguments = policyAttr!.ArgumentList?.Arguments;
            if (arguments is not null && arguments.Value.Count > 0)
            {
                // obtém os nomes das políticas
                authorizationPolicies = arguments.Value.Select(a => a.Expression.ToString()).ToArray();
            }
            else
            {
                authorizationPolicies ??= [];
            }
        }

        // tenta obter o MapGroup attribute
        if (classDeclaration.TryGetAttribute(MapGroupAttributeName, out AttributeSyntax? groupAttr) && groupAttr!.ArgumentList?.Arguments.Count is 1)
            groupName = groupAttr.ArgumentList.Arguments[0].Expression.ToString().RemoveQuotes();

        // tenta obter MapCreatedRoute
        if (classDeclaration.TryGetAttribute(MapCreatedRouteAttributeName, out AttributeSyntax? createdRouteAttr))
        {
            var arguments = createdRouteAttr!.ArgumentList?.Arguments;
            if (arguments is not null && arguments.Value.Count > 0)
            {
                // o primeiro parâmetro é a route pattern
                var routePattern = arguments.Value[0].Expression.ToString().RemoveQuotes();
                // pode haver outros parâmetros vindos do (params string[])
                var propertiesNames = arguments.Value.Count > 1
                    ? arguments.Value.Skip(1).Select(a => a.Expression.ToString().RemoveQuotes()).ToArray()
                    : [];

                createdInformation = new MapCreatedInformation(routePattern, propertiesNames);
            }
        }

        // tenta obter MapIdResultValue
        if (classDeclaration.TryGetAttribute(MapIdResultValueAttributeName, out _))
        {
            // quando há o attribute MapIdResultValue, deve obter a propriedade e o tipo dela.
            var typeInfo = semanticModel.GetTypeInfo(valueReturnType);
            var idProperty = typeInfo.Type
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name == "Id");

            if (idProperty is not null)
            {
                idResultValueType = TypeDescriptor.Create(idProperty.Type);
            }
            else
            {
                // se não achar a propriedade, deveria gerar um diagnostico.
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.IdNotFoundInReturnedCommand,
                    valueReturnType.GetLocation()));
            }
        }

        // tenta obter MapResponseValues e seus parâmetros
        if (classDeclaration.TryGetAttribute(MapResponseValuesAttributeName, out AttributeSyntax? resultValueAttr))
        {
            var arguments = resultValueAttr!.ArgumentList?.Arguments;
            if (arguments is not null && arguments.Value.Count > 0)
            {
                var propertiesNames = arguments.Value.Select(a => a.Expression.ToString().RemoveQuotes()).ToArray();

                var returnTypeProperties = semanticModel.GetTypeInfo(valueReturnType).Type?
                    .GetAllMembers()
                    .OfType<IPropertySymbol>()
                    .ToList();

                if (returnTypeProperties is null)
                {
                    // deve gerar algum diagnostic error
                    // se não achar a propriedade, deveria gerar um diagnostico.
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.ReturnedCommandTypeNotFound,
                        valueReturnType.GetLocation()));
                }
                else
                {
                    // para cada propriedade, obtém o membro do comando que corresponde a ela,
                    // então valida se é uma propriedade, e cria um PropertyDescription
                    var properties = propertiesNames.Select(name =>
                        {
                            // obtém o membro do comando que corresponde a ela
                            var property = returnTypeProperties.Find(p => p.Name == name);

                            if (property is null)
                            {
                                // se não achar a propriedade, deve gerar um erro de diagnostico
                                errors.Add(DiagnosticInfo.Create(
                                    CmdDiagnostics.PropertyNotFoundInReturnedCommand,
                                    valueReturnType.GetLocation(),
                                    name));
                                return null;
                            }
                            else
                            {
                                // cria o PropertyDescription, quando existir a propriedade
                                return PropertyDescriptor.Create(property);
                            }
                        })
                        .Where(p => p is not null)
                        .ToList();

                    responseValues = new MapResponseValuesInformation(properties!);
                }
            }
        }

        return new MapInformation
        {
            HttpMethod = httpMethod,
            RoutePattern = endpointRoutePattern,
            EndpointName = endpointName,
            EndpointNameLocation = mapArguments.Value[1].GetLocation(),
            Description = description,
            Summary = summary,
            GroupName = groupName,
            CreatedInformation = createdInformation,
            IdResultValueType = idResultValueType,
            ResponseValues = responseValues,
            AuthorizationPolicies = authorizationPolicies
        };
    }

    internal static ClassGenerator GenerateInterface(CommandHandlerInformation i)
    {
        // cria interface do handler
        var interfaceGen = new ClassGenerator(i.HandlerInterfaceName, i.Namespace, "interface");
        interfaceGen.Modifiers.Public();
        //interfaceGen.Usings.AddNamespaces(i.HandlerReturnType.Namespaces);

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
        handlerGen.Hierarchy.AddImplements(new TypeDescriptor(i.HandlerInterfaceName, [i.Namespace]));

        var isGenericContextAccessor = i.ContextAccessorMode is ContextAccessorModes.DbContext or ContextAccessorModes.WorkContext;
        if (isGenericContextAccessor)
        {
            handlerGen.Generics.AddGeneric("TContext", i.ContextAccessorType!.Namespaces);
            handlerGen.Where.Add(new WhereGenerator("TContext", i.ContextAccessorType.Name));
        }

        // cria os campos e o construtor
        var ctorGen = new ConstructorGenerator(i.HandlerImplementationName);
        ctorGen.Modifiers.Public();
        if (i.HasWithUnitOfWork)
        {
            var uowType = new TypeDescriptor(
                string.Format(UowAccessorType, isGenericContextAccessor ? "TContext" : i.ContextAccessorType!.Name),
                [CommandNamespace, .. i.ContextAccessorType!.Namespaces]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(uowType, AccessorVarName, true));
            // adiciona o parameter
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(uowType, AccessorVarName)));
            // adiciona comando de atribuição
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(AccessorVarName));
        }
        if (i.HasWithFindEntities)
        {
            var repoType = new TypeDescriptor(
                string.Format(RepoAccessorType, i.ContextAccessorType!.Name),
                [CommandNamespace, .. i.ContextAccessorType.Namespaces]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(repoType, AccessorVarName, true));
            // adiciona o parameter
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(repoType, AccessorVarName)));
            // adiciona comando de atribuição
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(AccessorVarName));
        }
        if (i.HasWithDecorators)
        {
            var decoratorsType = new TypeDescriptor(
                string.Format(DecoratorType, i.ModelType.Name, i.MethodReturnType.Name.TryGetInnerTaskType()),
                [CommandNamespace]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(decoratorsType, DecoratorsVarName, true));
            // adiciona o parameter
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(decoratorsType, DecoratorsVarName)));
            // adiciona comando de atribuição
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(DecoratorsVarName));
        }
        if (i.HasRetryOnConcurrency && i.RetryMaxAttempts is null)
        {
            // sem valor explícito no atributo, o número de tentativas vem das options (appsettings)
            var optionsType = new TypeDescriptor(
                "IOptions<RetryOnConcurrencyOptions>",
                ["Microsoft.Extensions.Options", "RoyalCode.SmartCommands.WorkContext.Options"]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(optionsType, RetryOptionsVarName, true));
            // adiciona o parameter
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(optionsType, RetryOptionsVarName)));
            // adiciona comando de atribuição
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(RetryOptionsVarName));
        }
        if (i.HasRetryOnConcurrency && i.RetryOperation is not null)
        {
            var problemFactoryType = new TypeDescriptor(
                "IConcurrencyRetryProblemFactory",
                ["RoyalCode.SmartCommands.WorkContext"]);

            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(problemFactoryType, RetryProblemFactoryVarName, true));
            // adiciona o parameter
            ctorGen.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(problemFactoryType, RetryProblemFactoryVarName)));
            // adiciona comando de atribuição
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(RetryProblemFactoryVarName));
        }

        // para cada parâmetro do método do comando, valida se é necessário adicionar como campo do construtor.
        foreach (var p in i.Parameters)
        {
            // não requer ct
            if (p.Type.IsCancellationToken)
                continue;

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
            // adiciona comando de atribuição
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

        // comando de validação (fica sempre fora do laço de retry)
        if (i.HasWithValidateModel)
            handlerMethodImpl.Commands.Add(new ValidateHasProblemsCommand(ModelVarName));

        // quando há retry de concorrência, o corpo {Begin → finds → Execute → Complete} é coletado à parte
        // para ser envolvido por uma lambda passada à primitiva; senão, vai direto no corpo do método.
        var bodyTarget = i.HasRetryOnConcurrency ? new GeneratorNodeList() : handlerMethodImpl.Commands;

        // comando unit of work begin
        if (i.HasWithUnitOfWork)
            bodyTarget.Add(new BeginUnitOfWorkCommand(AccessorVarName));

        // se tem entidades com Id, então cria variável de notFound
        if (i.IdPropertiesBindings.Count > 0 || i.EditType is not null)
        {
            bodyTarget.Add(new DeclareNotFoundProblemsCommand());

            // carrega o parâmetro do entidade a ser editada (quando existe EditEntityAttribute)
            if (i.EditType is not null)
            {
                var findEditEntity = new FindEditEntityCommand(i.EditType, AccessorVarName);
                bodyTarget.Add(findEditEntity);
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
                    bodyTarget.Add(findCmd);
            }
        }

        // Geração da chamada do comando no modelo e do retorno do método do handler
        // Dependendo do cenário, essa parte final pode variar.
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
            
            if (i.MethodReturnType.IsVoid)
            {
                lambda.InLine = true;
                lambda.Block = true;

                lambda.Commands.Add(new Command(final) { InLine = true});

                var lambdaReturn = new ReturnCommand(
                    new MethodInvokeGenerator("Task", "FromResult",
                        new MethodInvokeGenerator("Result", "Ok")))
                {
                    AppendLine = false
                };

                lambda.Commands.Add(lambdaReturn);
            }
            else if (i.MethodReturnType.IsVoidTask)
            {
                lambda.InLine = true;
                lambda.Block = true;
                lambda.Async = true;

                lambda.Commands.Add(new Command(final) { InLine = true, Await = true });

                var lambdaReturn = new ReturnCommand(
                        new MethodInvokeGenerator("Result", "Ok"))
                {
                    AppendLine = false
                };

                lambda.Commands.Add(lambdaReturn);
            }
            else
            {
                if (i.MethodIsAsync)
                    lambda.Async = true;
                else if (i.HandlerMustBeAsync)
                    final = new MethodInvokeGenerator("Task", "FromResult", final);
                lambda.Commands.Add(final);
            }

            var newMediator = new MediatorCreateCommand(
                DecoratorsMediatorVarName,
                i.ModelType.Name,
                i.MethodReturnType.Name,
                $"this.{DecoratorsVarName}",
                lambda,
                ModelVarName,
                "ct");

            // adiciona o comando que cria nova instância do mediador.
            bodyTarget.Add(newMediator);

            // a invocação do mediador passa a ser a geração final
            final = newMediator.CreateInvokeNextAsync();
        }

        if (i.HasWithUnitOfWork)
        {
            // a invocação do 'final' será assíncrona se o método do comando for assíncrono ou se tiver decorators.
            var isInvokeAsync = i.MethodIsAsync || i.HasWithDecorators;

            var produceNewEntity = i.ProduceNewEntityType is not null;

            final = new CompleteUnitOfWorkCommand(
                final, 
                isInvokeAsync, 
                i.MethodReturnType,
                AccessorVarName, 
                CommandResultVarName, 
                produceNewEntity,
                i.HasWithDecorators);

            useReturn = false;
        }

        if (useReturn)
        {
            final = i.HandlerReturnType.IsVoid || i.HandlerReturnType.IsVoidTask
                ? new Command(final) { NewLine = false }
                : new ReturnCommand(final);
        }

        bodyTarget.Add(final);

        // quando há retry, envolve o corpo coletado numa lambda passada à primitiva RetryOnConcurrencyAsync
        if (i.HasRetryOnConcurrency)
        {
            var optionsArgument = i.RetryMaxAttempts is int maxAttempts
                ? $"new RetryOnConcurrencyOptions {{ MaxAttempts = {maxAttempts} }}"
                : $"this.{RetryOptionsVarName}.Value";

            var onExhaustedArgument = i.RetryOperation is not null
                ? $"this.{RetryProblemFactoryVarName}.Create({ModelVarName}, {SymbolDisplay.FormatLiteral(i.RetryOperation, quote: true)})"
                : null;

            // Quando o comando produz uma nova entidade (ProduceNewEntity), o corpo do retry devolve Result<T>
            // e a chamada precisa do overload generico RetryOnConcurrencyAsync<T>; caso contrario e Result (sem valor).
            var retryValueType = i.ProduceNewEntityType?.Name;

            handlerMethodImpl.Commands.Add(
                new RetryOnConcurrencyCommand(bodyTarget, AccessorVarName, optionsArgument, onExhaustedArgument, retryValueType));
        }

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

        var method = new MethodGenerator("WasValidated", TypeDescriptor.Void());
        method.Modifiers.Internal();
        method.Modifiers.Protected();
        method.Attributes.Add(new AttributeGenerator("MethodImpl", "MethodImplOptions.AggressiveInlining", ["System.Runtime.CompilerServices"]));
        partialClass.Methods.Add(method);

        // para cada atributo MemberNotNullWhen
        // criar um atributo MemberNotNull no método WasValidated
        foreach (var member in i.NotNullProperties)
        {
            method.Attributes.Add(new AttributeGenerator("MemberNotNull", member, ["System.Diagnostics.CodeAnalysis"]));
        }

        return partialClass;
    }

    public static void AddRequiredParameters(
        CommandHandlerInformation commandInfo,
        MethodGenerator method,
        string? editEntityRouteParameterName = null,
        bool includeCommandParameter = true,
        bool nullableCommandParameter = false)
    {
        // parâmetro do id da entidade a ser editada, quando necessário
        if (commandInfo.EditType is not null)
        {
            var editParameter = commandInfo.EditType.Parameter
                ?? throw new InvalidOperationException("An edit command must have an entity parameter before source generation.");

            var idParameter = new ParameterGenerator(
                new ParameterDescriptor(commandInfo.EditType.IdType, $"{editParameter.Name}Id"));

            if (!string.IsNullOrWhiteSpace(editEntityRouteParameterName))
            {
                idParameter.Attributes.Add(
                    new AttributeGenerator(
                        "FromRoute",
                        ["Microsoft.AspNetCore.Mvc"],
                        new StringValueNode($"Name = \"{editEntityRouteParameterName}\""))
                    {
                        InLine = true
                    });
            }

            method.Parameters.Add(idParameter);
        }

        // parâmetro do commando (omitido no endpoint quando o comando não tem corpo — ver includeCommandParameter).
        if (includeCommandParameter)
        {
            var commandType = nullableCommandParameter
                ? new TypeDescriptor($"{commandInfo.ModelType.Name}?", commandInfo.ModelType.Namespaces)
                : commandInfo.ModelType;

            method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(commandType, ModelVarName)));
        }

        // parâmetros com atributo WithParameter
        foreach (var p in commandInfo.Parameters.Where(p => p.Type.IsHandlerParameter))
            method.Parameters.Add(new ParameterGenerator(p));

        // cancellation token, quando necessário (async)
        if (commandInfo.HandlerMustBeAsync)
            method.Parameters.Add(new ParameterGenerator(ParameterDescriptor.CancellationToken()));
    }
}
