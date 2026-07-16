using System.Collections.Immutable;
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
            .Count(candidate => KnownAttributes.Has(candidate, KnownAttributes.Command));
        if (commandMethodCount > 1)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.MultipleCommandMethods,
                method.Identifier.GetLocation(),
                commandType.Name));
        }

        var mapAttributeCount = commandType.GetAttributes()
            .Count(attribute => KnownAttributes.MapVerbs.Any(verb => verb.Spec.Matches(attribute)));
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

        // Classificação semântica e symbol-free do retorno do método, decidida pelo símbolo (nunca pelo texto
        // nem pelo modificador 'async'): aliases, nomes qualificados e métodos que retornam Task via
        // Task.FromResult resolvem para o mesmo modelo. A emissão usa Task como formato normalizado
        // também para métodos ValueTask, mas o modelo preserva o tipo declarado e seu payload.
        var declaredMethodReturnType = SemanticTypes.CreateDescriptor(methodSymbol.ReturnType);
        var returnModel = ReturnModel.Create(TypeSnapshot.Create(declaredMethodReturnType));
        var isAsync = returnModel.IsAwaitable;
        var methodReturnType = PipelineModelConversions.ToLegacyMethodReturn(returnModel);

        // lista dos parâmetros de ProblemCategory que o comando pode produzir
        // esta lista é preenchida a partir do método HasProblems da classe e do método do comando
        // através do attribute ProduceProblems
        List<string> produceProblems = [];

        // verifica se existe problemas no método do comando
        if (KnownAttributes.TryGet(methodSymbol, KnownAttributes.ProduceProblems, out var produceProblemsAttr))
            AddProduceProblems(produceProblemsAttr!, produceProblems);

        IMethodSymbol? hasProblemsMethod = null;
        DiagnosticInfo? error = null;

        // Verifica se o método possui o atributo WithValidateModel
        // Se tiver, busca pelo método na classe e já valida se está dentro do padrão
        var hasWithValidateModel = KnownAttributes.TryGet(methodSymbol, KnownAttributes.WithValidateModel, out var withValidateModelAttr);
        if (withValidateModelAttr is not null
            && !commandType.ValidateTypeWithHasProblemsMethod(
                KnownAttributes.GetLocation(withValidateModelAttr, cancellationToken, method.Identifier.GetLocation()),
                out hasProblemsMethod,
                out error))
        {
            // quando há erros, não deve considerar o atributo, além de adicionar o diagnostic
            // isso não gerará o código de validação e mostrará o erro na saída.
            errors.Add(error!);
            hasWithValidateModel = false;
        }

        // se tem hasProblemsMethod, verifica se tem o attribute ProduceProblems
        if (hasProblemsMethod is not null)
        {
            if (KnownAttributes.TryGet(hasProblemsMethod, KnownAttributes.ProduceProblems, out produceProblemsAttr))
            {
                // se tem o attribute, extrai os parâmetros
                AddProduceProblems(produceProblemsAttr!, produceProblems);
            }
            else
            {
                // se não tem, adiciona o valor de ProblemCategory.InvalidParameter, pois é o padrão
                produceProblems.Add("ProblemCategory.InvalidParameter");
            }
        }

        // Verifica se o método possui o atributo WithDecorators
        var hasWithDecorators = KnownAttributes.Has(methodSymbol, KnownAttributes.WithDecorators);

        // se tem WithDecorators, deve retornar algum tipo de dado (não pode ser Task ou void) — decisão semântica.
        if (hasWithDecorators && returnModel.IsVoid)
        {
            // quando há erros, não deve considerar o atributo, além de adicionar o diagnostic
            // isso não gerará o código de decoradores e mostrará o erro na saída.
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidReturnType,
                method.Identifier.GetLocation()));
            hasWithDecorators = false;
        }

        // verifica se tem o attribute WithUnitOfWork
        TypeDescriptor? accessorType = null;
        ContextAccessorModes contextAccessorMode = ContextAccessorModes.None;
        var hasUow = KnownAttributes.TryGet(methodSymbol, KnownAttributes.WithUnitOfWork, out var withUowAttr);
        if (hasUow)
        {
            if (TryGetTypeArguments(withUowAttr!, 1, out var typeArguments))
            {
                accessorType = SemanticTypes.CreateDescriptor(typeArguments[0]);
                contextAccessorMode = ContextAccessorModes.Specified;
            }
            else
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidCommandType,
                    KnownAttributes.GetLocation(withUowAttr!, cancellationToken, method.Identifier.GetLocation()),
                    "WithUnitOfWorkAttribute requires one context type argument"));
                hasUow = false;
            }
        }

        // verifica se tem o attribute WithDbContext
        var hasDbContext = KnownAttributes.TryGet(methodSymbol, KnownAttributes.WithDbContext, out var withDbContextAttr);
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
        var hasWorkContext = KnownAttributes.Has(methodSymbol, KnownAttributes.WithWorkContext);
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
        var hasRetryOnConcurrency = KnownAttributes.TryGet(methodSymbol, KnownAttributes.WithRetryOnConcurrency, out var retryAttr);
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
            else
            {
                ReadRetryArguments(retryAttr!, method, errors, ref retryMaxAttempts, ref retryOperation, cancellationToken);
            }
        }

        // verifica se tem WithFindEntities (se tiver hasUow, não precisa verificar)
        var hasFindEntities = false;
        if (!hasUow)
        {
            hasFindEntities = KnownAttributes.TryGet(methodSymbol, KnownAttributes.WithFindEntities, out var withFindEntitiesAttr);
            if (hasFindEntities)
            {
                if (TryGetTypeArguments(withFindEntitiesAttr!, 1, out var typeArguments))
                {
                    accessorType = SemanticTypes.CreateDescriptor(typeArguments[0]);
                }
                else
                {
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.InvalidCommandType,
                        KnownAttributes.GetLocation(withFindEntitiesAttr!, cancellationToken, method.Identifier.GetLocation()),
                        "WithFindEntitiesAttribute requires one context type argument"));
                    hasFindEntities = false;
                }
            }
        }

        // verifica se retorna uma nova entidade
        var hasProduceNewEntity = KnownAttributes.Has(methodSymbol, KnownAttributes.ProduceNewEntity);
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
        ITypeSymbol? editEntityTypeSymbol = null;
        string? editRouteParameterNameExplicit = null;
        var hasEditEntity = KnownAttributes.TryGet(methodSymbol, KnownAttributes.EditEntity, out var editEntityAttr);
        if (hasEditEntity)
        {
            // DF4: seleção explícita do parâmetro de rota do id, quando informada
            editRouteParameterNameExplicit = editEntityAttr!.NamedArguments
                .Where(argument => argument.Key == "RouteParameterName")
                .Select(argument => KnownAttributes.GetString(argument.Value))
                .FirstOrDefault();

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

            if (TryGetTypeArguments(editEntityAttr!, 2, out var typeArguments))
            {
                editEntityTypeSymbol = typeArguments[0];
                editType = new EditTypeDescriptor(
                    SemanticTypes.CreateDescriptor(typeArguments[0]),
                    SemanticTypes.CreateDescriptor(typeArguments[1]));
            }
            else
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidCommandType,
                    KnownAttributes.GetLocation(editEntityAttr!, cancellationToken, method.Identifier.GetLocation()),
                    "EditEntityAttribute requires entity and id type arguments"));
            }
        }

        // Desembrulho semântico do retorno (Task/ValueTask e Result) para conhecer o tipo de valor real,
        // usado por ProduceNewEntity e pelos mapeamentos de resposta (MapIdResultValue/MapResponseValues).
        var valueReturnTypeSymbol = UnwrapValueReturnType(methodSymbol.ReturnType);

        // se produz uma nova entidade, então o retorno do método será a nova entidade
        TypeDescriptor? newEntityType = null;
        if (hasProduceNewEntity)
        {
            // quando o comando retorna Result sem valor, não há entidade produzida a retornar
            if (returnModel.IsResult && returnModel.ValueType is null)
            {
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.ProduceNewEntityMustReturnResultWithValue,
                    location: method.ReturnType.GetLocation());

                errors.Add(error);
            }
            else if (valueReturnTypeSymbol is null)
            {
                error = DiagnosticInfo.Create(CmdDiagnostics.InvalidCommandType,
                    location: method.Identifier.GetLocation(),
                    "It was not possible to determine the return type of the new entity");

                errors.Add(error);
            }
            else
            {
                // cria o tipo da nova entidade
                newEntityType = SemanticTypes.CreateDescriptor(valueReturnTypeSymbol);
            }
        }

        // lista dos parâmetros do método com o attribute Command
        var parameters = new List<ParameterDescriptor>(method.ParameterList.Parameters.Count);
        // lista de vínculo de parâmetros que são entidades e propriedades ID para a entidade.
        var idPropertiesBindings = new List<IdPropertyBoundToEntityParameter>();

        // obtém os parâmetros do método com o attribute Command
        var commandMethodParameters = method.ParameterList.Parameters;
        var parameterSymbols = methodSymbol.Parameters;

        // bindings de [WithParameter] capturados para o delegate Minimal API (DF3); a validação e a
        // emissão ocorrem apenas quando o comando é mapeado
        var capturedBindings = new List<(string Name, Location Location, CapturedBindings Captured)>();
        for (int paramIndex = 0; paramIndex < commandMethodParameters.Count; paramIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var p = commandMethodParameters[paramIndex];
            var parameterSymbol = paramIndex < parameterSymbols.Length ? parameterSymbols[paramIndex] : null;

            // cria o descritor
            var paramDescriptor = ParameterDescriptor.Create(p, context.SemanticModel);

            // valida CancellationToken (decisão semântica), só pode haver caso o método seja assíncrono
            var isCancellationTokenParameter = parameterSymbol is not null
                ? IsType(parameterSymbol.Type, "System.Threading", "CancellationToken")
                : paramDescriptor.Type.IsCancellationToken;
            if (isCancellationTokenParameter && !isAsync)
            {
                error = DiagnosticInfo.Create(
                    CmdDiagnostics.CancellationTokenParameterMustBeAsync,
                    location: p.Identifier.GetLocation());

                errors.Add(error);
            }

            if (paramIndex == 0 && editType is not null)
            {
                // quando há EditEntityAttribute o primeiro parâmetro deverá ser do mesmo tipo informado no attr;
                // a comparação é por símbolo, então aliases e nomes qualificados são equivalentes.
                var matchesEditEntityType = editEntityTypeSymbol is not null && parameterSymbol is not null
                    ? SymbolEqualityComparer.Default.Equals(parameterSymbol.Type, editEntityTypeSymbol)
                    : Equals(paramDescriptor.Type, editType.EntityType);

                if (!matchesEditEntityType)
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

            // verifica se o parâmetro tem o attribute WithParameter (identificação semântica)
            if (parameterSymbol is not null && KnownAttributes.Has(parameterSymbol, KnownAttributes.WithParameter))
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

                    // DF3: captura os atributos de binding do parâmetro-fonte
                    var captured = BindingAttributes.Capture(parameterSymbol);
                    if (captured.SourceCount > 0 || captured.HasAsParameters)
                        capturedBindings.Add((paramDescriptor.Name, p.Identifier.GetLocation(), captured));
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
            // obtém atributos MemberNotNullWhen do método HasProblems e extrai os nomes dos membros
            // (a partir do segundo argumento) como constantes reais, reemitidos como literais string
            notNullProperties = hasProblemsMethod.GetAttributes()
                .Where(attribute => KnownAttributes.MemberNotNullWhen.Matches(attribute))
                .Where(attribute => attribute.ConstructorArguments.Length > 1)
                .Select(attribute => string.Join(", ",
                    KnownAttributes.GetStrings(attribute.ConstructorArguments[1])
                        .Select(member => SymbolDisplay.FormatLiteral(member, quote: true))))
                .Where(joined => joined.Length > 0)
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

        // lê atributo Map... da classe do comando (leitura semântica por símbolo)
        var mapInformation = ReadMap(
            commandType,
            valueReturnTypeSymbol,
            method,
            errors,
            cancellationToken);

        // DF4: resolve o parâmetro de rota que carrega o id da entidade editada e valida
        // constraint/opcionalidade quando determináveis
        if (mapInformation is not null && editType?.Parameter is not null)
        {
            mapInformation.EditRouteParameterName = ResolveEditRouteParameter(
                mapInformation,
                editType,
                editRouteParameterNameExplicit,
                KnownAttributes.GetLocation(editEntityAttr!, cancellationToken, method.Identifier.GetLocation()),
                errors);
        }

        // DF3: bindings explícitos só têm efeito (e são validados) quando o comando é mapeado
        if (mapInformation is not null)
        {
            foreach (var (parameterName, parameterLocation, captured) in capturedBindings)
            {
                BindingAttributes.Validate(
                    captured,
                    parameterName,
                    parameterLocation,
                    mapInformation.RoutePattern,
                    mapInformation.GroupName,
                    errors);
            }
        }

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
            retryOperation,
            // o handler de EditEntity sempre declara '{entidade}Id' na assinatura (mapeado ou não)
            editEntityIdParameterName: editType?.Parameter is not null ? $"{editType.Parameter.Name}Id" : null);

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

        // GET/DELETE não podem inferir body: o ASP.NET Core lança na inicialização do app.
        // Comandos com BindAsync/TryParse estáticos próprios usam binding customizado e não inferem body.
        if (mapInformation is not null && hasRequestBody &&
            mapInformation.HttpMethod is "Get" or "Delete" &&
            !HasCustomBindingMethod(commandType))
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.ImplicitBodyNotAllowed,
                classDeclaration.Identifier.GetLocation(),
                commandType.Name,
                $"Map{mapInformation.HttpMethod}"));
        }

        // uma única fonte de body por endpoint: FromBody explícitos conflitam entre si e com o body implícito
        // do comando; FromForm conflita com qualquer body JSON (o ASP.NET Core lança na inicialização)
        if (mapInformation is not null)
        {
            var fromBodyParameters = capturedBindings
                .Where(captured => captured.Captured.Bindings.Any(binding => binding.Attribute == "FromBody"))
                .ToList();
            var fromFormParameters = capturedBindings
                .Where(captured => captured.Captured.Bindings.Any(binding => binding.Attribute == "FromForm"))
                .ToList();

            var jsonBodySources = fromBodyParameters.Count + (hasRequestBody ? 1 : 0);

            if (jsonBodySources > 1)
            {
                foreach (var (parameterName, parameterLocation, _) in fromBodyParameters)
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.MultipleBodySources, parameterLocation, parameterName));
            }

            if (fromFormParameters.Count > 0 && jsonBodySources > 0)
            {
                foreach (var (parameterName, parameterLocation, _) in fromFormParameters)
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.MultipleBodySources, parameterLocation, parameterName));
            }
        }

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
            HasBodyProperties = hasRequestBody,
            ParameterBindings = capturedBindings.Count > 0
                ? capturedBindings.ToDictionary(
                    captured => captured.Name,
                    captured => captured.Captured.Bindings,
                    StringComparer.Ordinal)
                : null
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
        string? retryOperation,
        string? editEntityIdParameterName = null)
    {
        // nomes que o handler gerado emite como parâmetro/campo/local no mesmo escopo do comando.
        var reserved = new HashSet<string>(StringComparer.Ordinal) { ModelVarName };

        if (editEntityIdParameterName is not null)
            reserved.Add(editEntityIdParameterName);

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

    /// <summary>
    /// DF4: resolve o parâmetro de rota que carrega o id da entidade editada, na ordem:
    /// <c>RouteParameterName</c> explícito; única variável do template; <c>{parâmetroDaEntidade}Id</c>;
    /// <c>parâmetroDaEntidade</c>. Sem correspondência única, emite RCCMD031 e a geração é bloqueada.
    /// Template sem variáveis: o id é vinculado por inferência (sem atributo, sem diagnóstico).
    /// </summary>
    private static string? ResolveEditRouteParameter(
        MapInformation mapInformation,
        EditTypeDescriptor editType,
        string? explicitRouteParameterName,
        Location location,
        List<DiagnosticInfo> errors)
    {
        // o template completo (grupo + rota) é a fonte de variáveis, o mesmo usado pelo binding em runtime
        // e pelo RCCMD035
        var template = mapInformation.GroupName is null
            ? mapInformation.RoutePattern
            : $"{mapInformation.GroupName}/{mapInformation.RoutePattern}";
        var routeParameters = RoutePatternParser.Parse(template);

        RoutePatternParameter? resolved;

        if (explicitRouteParameterName is not null)
        {
            resolved = routeParameters.FirstOrDefault(parameter =>
                string.Equals(parameter.Name, explicitRouteParameterName, StringComparison.OrdinalIgnoreCase));

            if (resolved is null)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.EditEntityRouteParameterNotResolved,
                    location,
                    template,
                    $"the route parameter '{explicitRouteParameterName}' specified in RouteParameterName does not exist in the template"));
                return null;
            }
        }
        else if (routeParameters.Count == 0)
        {
            // sem variável de rota, o id é vinculado por inferência (query para tipos parseáveis);
            // comportamento preservado, sem diagnóstico.
            return null;
        }
        else if (routeParameters.Count == 1)
        {
            resolved = routeParameters[0];
        }
        else
        {
            var entityParameterName = editType.Parameter!.Name;
            resolved = routeParameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, $"{entityParameterName}Id", StringComparison.OrdinalIgnoreCase))
                ?? routeParameters.FirstOrDefault(parameter =>
                    string.Equals(parameter.Name, entityParameterName, StringComparison.OrdinalIgnoreCase));

            if (resolved is null)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.EditEntityRouteParameterNotResolved,
                    location,
                    template,
                    $"the template has multiple route parameters and none matches '{entityParameterName}Id' or '{entityParameterName}'; specify RouteParameterName on EditEntityAttribute"));
                return null;
            }
        }

        ValidateEditRouteParameter(resolved, editType, location, errors);
        return resolved.Name;
    }

    /// <summary>Valida opcionalidade e constraints de tipo conhecidas contra o tipo do id da entidade.</summary>
    private static void ValidateEditRouteParameter(
        RoutePatternParameter routeParameter,
        EditTypeDescriptor editType,
        Location location,
        List<DiagnosticInfo> errors)
    {
        if (routeParameter.IsOptional)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.EditEntityRouteParameterIncompatible,
                location,
                routeParameter.Name,
                "the entity id is required, but the route parameter is optional"));
        }

        if (routeParameter.IsCatchAll)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.EditEntityRouteParameterIncompatible,
                location,
                routeParameter.Name,
                "the route parameter is a catch-all and cannot bind a single entity id"));
        }

        if (routeParameter.Constraint is null)
            return;

        // valida somente constraints de tipo conhecidas; múltiplas constraints são separadas por ':'
        foreach (var constraint in routeParameter.Constraint.Split(':'))
        {
            var baseName = constraint;
            var parenthesis = baseName.IndexOf('(');
            if (parenthesis >= 0)
                baseName = baseName.Substring(0, parenthesis);

            if (!ConstraintTypeNames.TryGetValue(baseName.Trim(), out var expectedTypeName))
                continue;

            // Nullable<T> no id ('int?') vincula normalmente uma rota '{id:int}'
            var idTypeName = editType.IdType.Name.TrimEnd('?');
            if (!string.Equals(idTypeName, expectedTypeName, StringComparison.Ordinal))
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.EditEntityRouteParameterIncompatible,
                    location,
                    routeParameter.Name,
                    $"the route constraint '{baseName}' expects '{expectedTypeName}' but the entity id type is '{editType.IdType.Name}'"));
            }
        }
    }

    /// <summary>Constraints de rota que determinam um tipo CLR (nome na forma mínima do C#).</summary>
    private static readonly Dictionary<string, string> ConstraintTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["int"] = "int",
        ["long"] = "long",
        ["guid"] = "Guid",
        ["bool"] = "bool",
        ["datetime"] = "DateTime",
        ["decimal"] = "decimal",
        ["double"] = "double",
        ["float"] = "float",
    };

    private static HashSet<string> CollectEndpointReservedNames(EditTypeDescriptor? editType)
    {
        // nomes emitidos no escopo do método do endpoint Minimal API, onde os parâmetros [WithParameter]
        // são replicados ('command', 'ct' e o '{entidade}Id' de EditEntity já são reservados pelo escopo
        // do handler).
        _ = editType;
        return new HashSet<string>(StringComparer.Ordinal)
        {
            EndpointHandlerParameterName,
            EndpointResultVarName,
        };
    }

    /// <summary>
    /// Um tipo com <c>BindAsync</c> ou <c>TryParse</c> públicos e estáticos usa o binding customizado do
    /// próprio tipo no Minimal API (o body não é inferido).
    /// </summary>
    private static bool HasCustomBindingMethod(INamedTypeSymbol commandType) =>
        commandType.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(candidate => candidate.IsStatic &&
                candidate.DeclaredAccessibility == Accessibility.Public &&
                candidate.Name is "BindAsync" or "TryParse");

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

    /// <summary>
    /// Obtém os argumentos de tipo do atributo genérico pela identidade semântica. Tipos de argumento não
    /// resolvidos (em digitação) passam adiante — o compilador já reporta o erro no código do usuário.
    /// </summary>
    private static bool TryGetTypeArguments(
        AttributeData attribute,
        int expectedCount,
        out ImmutableArray<ITypeSymbol> typeArguments)
    {
        if (attribute.AttributeClass is { TypeKind: not TypeKind.Error } attributeClass &&
            attributeClass.TypeArguments.Length == expectedCount)
        {
            typeArguments = attributeClass.TypeArguments;
            return true;
        }

        typeArguments = default;
        return false;
    }

    /// <summary>Comparação semântica de tipo por namespace + metadata name (nunca por nome simples).</summary>
    private static bool IsType(ITypeSymbol type, string @namespace, string metadataName) =>
        KnownAttributes.IsType(type, @namespace, metadataName);

    /// <summary>
    /// Desembrulha semanticamente o tipo de valor do retorno do método: <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c>
    /// e <c>Result&lt;T&gt;</c> resolvem para <c>T</c>; <c>void</c>, <c>Task</c>, <c>ValueTask</c> e <c>Result</c>
    /// (sem valor) resolvem para <see langword="null"/>.
    /// </summary>
    private static ITypeSymbol? UnwrapValueReturnType(ITypeSymbol returnType)
    {
        var current = returnType;

        if (IsType(current, "System.Threading.Tasks", "Task") || IsType(current, "System.Threading.Tasks", "ValueTask"))
            return null;

        if (IsType(current, "System.Threading.Tasks", "Task`1") || IsType(current, "System.Threading.Tasks", "ValueTask`1"))
            current = ((INamedTypeSymbol)current).TypeArguments[0];

        if (IsType(current, "RoyalCode.SmartProblems", "Result"))
            return null;

        if (IsType(current, "RoyalCode.SmartProblems", "Result`1"))
            return ((INamedTypeSymbol)current).TypeArguments[0];

        return current.SpecialType == SpecialType.System_Void ? null : current;
    }

    /// <summary>
    /// Extrai os valores de <c>ProduceProblems(params ProblemCategory[])</c> como <c>ProblemCategory.Membro</c>,
    /// aceitando <c>params</c> expandido e array explícito; valores não constantes são ignorados.
    /// </summary>
    private static void AddProduceProblems(AttributeData attribute, List<string> produceProblems)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Kind == TypedConstantKind.Array)
            {
                foreach (var item in argument.Values)
                {
                    if (KnownAttributes.FormatEnumMember(item) is { } value)
                        produceProblems.Add(value);
                }
            }
            else if (KnownAttributes.FormatEnumMember(argument) is { } single)
            {
                produceProblems.Add(single);
            }
        }
    }

    /// <summary>
    /// Lê os argumentos de <c>WithRetryOnConcurrency</c> (maxAttempts/operation) por <see cref="TypedConstant"/>,
    /// validando os valores; argumentos não constantes são ignorados (o compilador já os reporta).
    /// </summary>
    private static void ReadRetryArguments(
        AttributeData attribute,
        MethodDeclarationSyntax method,
        List<DiagnosticInfo> errors,
        ref int? retryMaxAttempts,
        ref string? retryOperation,
        CancellationToken cancellationToken)
    {
        var constructorParameters = attribute.AttributeConstructor?.Parameters
            ?? ImmutableArray<IParameterSymbol>.Empty;

        // argumentos posicionais do construtor (nome vem do parâmetro do construtor resolvido)
        for (var index = 0; index < attribute.ConstructorArguments.Length && index < constructorParameters.Length; index++)
        {
            var location = KnownAttributes.GetArgumentLocation(
                attribute, index, cancellationToken, method.Identifier.GetLocation());
            ApplyRetryArgument(
                constructorParameters[index].Name,
                attribute.ConstructorArguments[index],
                location,
                errors,
                ref retryMaxAttempts,
                ref retryOperation);
        }

        // argumentos nomeados de propriedade (ex.: Operation = "...", MaxAttempts = 3)
        var attributeLocation = KnownAttributes.GetLocation(attribute, cancellationToken, method.Identifier.GetLocation());
        foreach (var namedArgument in attribute.NamedArguments)
        {
            ApplyRetryArgument(
                namedArgument.Key,
                namedArgument.Value,
                attributeLocation,
                errors,
                ref retryMaxAttempts,
                ref retryOperation);
        }
    }

    private static void ApplyRetryArgument(
        string name,
        TypedConstant argument,
        Location location,
        List<DiagnosticInfo> errors,
        ref int? retryMaxAttempts,
        ref string? retryOperation)
    {
        if (string.Equals(name, "maxAttempts", StringComparison.OrdinalIgnoreCase) &&
            argument is { Kind: TypedConstantKind.Primitive, Value: int maxAttempts })
        {
            // valor explícito no atributo sobrescreve as options; <= 0 é inválido
            if (maxAttempts <= 0)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.RetryOnConcurrencyInvalidMaxAttempts,
                    location));
            }
            else
            {
                retryMaxAttempts = maxAttempts;
            }
        }
        else if (string.Equals(name, "operation", StringComparison.OrdinalIgnoreCase) &&
            argument is { Kind: TypedConstantKind.Primitive, Value: string operation })
        {
            if (string.IsNullOrWhiteSpace(operation))
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidCommandType,
                    location,
                    "The retry operation must not be empty"));
            }
            else
            {
                retryOperation = operation;
            }
        }
    }

    private static MapInformation? ReadMap(
        INamedTypeSymbol commandType,
        ITypeSymbol? valueReturnType,
        MethodDeclarationSyntax method,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        string? description = null;
        string? summary = null;
        string? groupName = null;
        string[]? authorizationPolicies = null;
        MapCreatedInformation? createdInformation = null;
        TypeDescriptor? idResultValueType = null;
        MapResponseValuesInformation? responseValues = null;

        // localiza o atributo Map* de verbo HTTP pela identidade semântica (metadata name)
        AttributeData? attr = null;
        string? httpMethod = null;
        foreach (var (spec, verb) in KnownAttributes.MapVerbs)
        {
            if (KnownAttributes.TryGet(commandType, spec, out attr))
            {
                httpMethod = verb;
                break;
            }
        }

        if (attr is null || httpMethod is null)
            return null;

        var attributeLocation = KnownAttributes.GetLocation(attr, cancellationToken, method.Identifier.GetLocation());

        // a quantidade de argumentos escritos vem da sintaxe (o construtor pode nem ter sido resolvido);
        // os valores vêm dos TypedConstants — argumento não constante/incompleto já é erro do compilador
        // e bloqueia o mapeamento sem RCCMD (DF9).
        var writtenArgumentCount =
            (attr.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax)?
                .ArgumentList?.Arguments.Count ?? 0;
        if (writtenArgumentCount != 2)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapArguments,
                attributeLocation,
                $"Map{httpMethod}"));
            return null;
        }

        var mapArguments = attr.ConstructorArguments;
        if (mapArguments.Length != 2 ||
            mapArguments[0].Kind == TypedConstantKind.Error ||
            mapArguments[1].Kind == TypedConstantKind.Error)
        {
            // argumento não constante/incompleto: o compilador já reporta; sem RCCMD (DF9)
            return null;
        }

        var endpointRoutePattern = KnownAttributes.GetString(mapArguments[0]);
        var endpointName = KnownAttributes.GetString(mapArguments[1]);
        if (endpointRoutePattern is null || endpointName is null)
        {
            // null é constante válida para o compilador, mas é uso inválido do atributo:
            // sem RCCMD o endpoint sumiria em silêncio.
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.InvalidMapArguments,
                attributeLocation,
                $"Map{httpMethod}"));
            return null;
        }

        // tenta obter a descrição
        if (KnownAttributes.TryGet(commandType, KnownAttributes.WithDescription, out var descAttr) &&
            descAttr!.ConstructorArguments.Length == 1)
        {
            description = KnownAttributes.GetString(descAttr.ConstructorArguments[0]);
        }

        // tenta obter o summary
        if (KnownAttributes.TryGet(commandType, KnownAttributes.WithSummary, out var summaryAttr) &&
            summaryAttr!.ConstructorArguments.Length == 1)
        {
            summary = KnownAttributes.GetString(summaryAttr.ConstructorArguments[0]);
        }

        // tenta obter o authorization
        if (KnownAttributes.Has(commandType, KnownAttributes.WithAuthorization))
            authorizationPolicies = [];

        // se tiver o attribute WithPolicy, deve obter o(s) nome(s) da(s) política(s) — aceita params e array explícito
        if (KnownAttributes.TryGet(commandType, KnownAttributes.WithPolicy, out var policyAttr))
        {
            var policies = policyAttr!.ConstructorArguments.Length > 0
                ? KnownAttributes.GetStrings(policyAttr.ConstructorArguments[0]).ToArray()
                : [];
            authorizationPolicies = policies.Length > 0 ? policies : authorizationPolicies ?? [];
        }

        // tenta obter o MapGroup attribute
        if (KnownAttributes.TryGet(commandType, KnownAttributes.MapGroup, out var groupAttr) &&
            groupAttr!.ConstructorArguments.Length == 1)
        {
            groupName = KnownAttributes.GetString(groupAttr.ConstructorArguments[0]);
        }

        // tenta obter MapCreatedRoute — (route pattern, params nomes de propriedades)
        if (KnownAttributes.TryGet(commandType, KnownAttributes.MapCreatedRoute, out var createdRouteAttr) &&
            createdRouteAttr!.ConstructorArguments.Length > 0 &&
            KnownAttributes.GetString(createdRouteAttr.ConstructorArguments[0]) is { } createdRoutePattern)
        {
            var propertiesNames = createdRouteAttr.ConstructorArguments.Length > 1
                ? KnownAttributes.GetStrings(createdRouteAttr.ConstructorArguments[1]).ToArray()
                : [];

            createdInformation = new MapCreatedInformation(createdRoutePattern, propertiesNames);
        }

        // tenta obter MapIdResultValue
        if (KnownAttributes.Has(commandType, KnownAttributes.MapIdResultValue))
        {
            // quando há o attribute MapIdResultValue, deve obter a propriedade Id e o tipo dela no tipo de valor retornado.
            var idProperty = valueReturnType?
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name == "Id");

            if (idProperty is not null)
            {
                idResultValueType = TypeDescriptor.Create(idProperty.Type);
            }
            else
            {
                // se não achar a propriedade, gera o diagnóstico.
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.IdNotFoundInReturnedCommand,
                    method.ReturnType.GetLocation()));
            }
        }

        // tenta obter MapResponseValues e seus parâmetros
        if (KnownAttributes.TryGet(commandType, KnownAttributes.MapResponseValues, out var resultValueAttr))
        {
            var propertiesNames = resultValueAttr!.ConstructorArguments.Length > 0
                ? KnownAttributes.GetStrings(resultValueAttr.ConstructorArguments[0]).ToArray()
                : [];

            if (propertiesNames.Length > 0)
            {
                var returnTypeProperties = valueReturnType?
                    .GetAllMembers()
                    .OfType<IPropertySymbol>()
                    .ToList();

                if (returnTypeProperties is null)
                {
                    // sem tipo de valor retornado não há como projetar as propriedades.
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.ReturnedCommandTypeNotFound,
                        method.ReturnType.GetLocation()));
                }
                else
                {
                    // para cada propriedade, obtém o membro do tipo retornado que corresponde a ela,
                    // então valida se é uma propriedade, e cria um PropertyDescription
                    var properties = propertiesNames.Select(name =>
                        {
                            // obtém o membro do tipo retornado que corresponde a ela
                            var property = returnTypeProperties.Find(p => p.Name == name);

                            if (property is null)
                            {
                                // se não achar a propriedade, deve gerar um erro de diagnostico
                                errors.Add(DiagnosticInfo.Create(
                                    CmdDiagnostics.PropertyNotFoundInReturnedCommand,
                                    method.ReturnType.GetLocation(),
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
            EndpointNameLocation = KnownAttributes.GetArgumentLocation(attr, 1, cancellationToken, attributeLocation),
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
        // cria interface do handler; o hint name usa o nome completo (namespace + tipo) para que
        // classes homônimas em namespaces diferentes gerem fontes distintas
        var interfaceGen = new ClassGenerator(i.HandlerInterfaceName, i.Namespace, "interface")
        {
            FileName = HintName.Create(
                $"{i.Namespace}.{i.HandlerInterfaceName}",
                i.HandlerInterfaceName)
        };
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
        // cria classe que implementa o handler; o hint name usa o nome completo (namespace + tipo)
        var handlerGen = new ClassGenerator(i.HandlerImplementationName, $"{i.Namespace}.Internals")
        {
            FileName = HintName.Create(
                $"{i.Namespace}.Internals.{i.HandlerImplementationName}",
                i.HandlerImplementationName)
        };
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
                returnsResult: i.ReturnModel.IsResult,
                resultHasValue: i.ReturnModel.IsResult && i.ReturnModel.ValueType is not null,
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
            FileName = HintName.Create(
                $"{i.Namespace}.{i.ModelType.Name}_WasValidated",
                $"{i.ModelType.Name}_WasValidated")
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
        bool nullableCommandParameter = false,
        bool includeBindingAttributes = false)
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
                        new StringValueNode(
                            $"Name = {SymbolDisplay.FormatLiteral(editEntityRouteParameterName!, quote: true)}"))
                    {
                        InLine = true
                    });
            }

            method.Parameters.Add(idParameter);
        }

        // parâmetro do comando (omitido no endpoint quando o comando não tem corpo — ver includeCommandParameter).
        if (includeCommandParameter)
        {
            var commandType = nullableCommandParameter
                ? new TypeDescriptor($"{commandInfo.ModelType.Name}?", commandInfo.ModelType.Namespaces)
                : commandInfo.ModelType;

            method.Parameters.Add(new ParameterGenerator(new ParameterDescriptor(commandType, ModelVarName)));
        }

        // parâmetros com atributo WithParameter; no delegate Minimal API os bindings explícitos do
        // parâmetro-fonte são copiados (DF3); sem binding, o ASP.NET Core infere a fonte (DF2)
        foreach (var p in commandInfo.Parameters.Where(p => p.Type.IsHandlerParameter))
        {
            var parameterGenerator = new ParameterGenerator(p);

            if (includeBindingAttributes &&
                commandInfo.ParameterBindings is not null &&
                commandInfo.ParameterBindings.TryGetValue(p.Name, out var bindings))
            {
                foreach (var binding in bindings)
                    parameterGenerator.Attributes.Add(CreateBindingAttribute(binding));
            }

            method.Parameters.Add(parameterGenerator);
        }

        // cancellation token, quando necessário (async)
        if (commandInfo.HandlerMustBeAsync)
            method.Parameters.Add(new ParameterGenerator(ParameterDescriptor.CancellationToken()));
    }

    /// <summary>Emite um atributo de binding capturado (DF3), preservando o argumento <c>Name</c> quando presente.</summary>
    internal static AttributeGenerator CreateBindingAttribute(ParameterBindingModel binding)
    {
        return binding.Name is null
            ? new AttributeGenerator(binding.Attribute, ["Microsoft.AspNetCore.Mvc"])
            {
                InLine = true
            }
            : new AttributeGenerator(
                binding.Attribute,
                ["Microsoft.AspNetCore.Mvc"],
                new StringValueNode($"Name = {SymbolDisplay.FormatLiteral(binding.Name, quote: true)}"))
            {
                InLine = true
            };
    }
}
