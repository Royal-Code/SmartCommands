using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoyalCode.SmartCommands.Generators.Commands;
using RoyalCode.SmartCommands.Generators.Models;
using RoyalCode.Extensions.SourceGenerator.Generation;
using RoyalCode.Extensions.SourceGenerator.Collections;
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
            AddProduceProblems(
                produceProblemsAttr!, produceProblems, errors, method.Identifier.GetLocation(), cancellationToken);

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
                AddProduceProblems(
                    produceProblemsAttr!,
                    produceProblems,
                    errors,
                    hasProblemsMethod.Locations.FirstOrDefault(location => location.IsInSource) ?? method.Identifier.GetLocation(),
                    cancellationToken);
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

        // DF21: verifica se tem WithTransaction (transação exigida pelo comando, independente das options)
        var requiresTransaction = KnownAttributes.Has(methodSymbol, KnownAttributes.WithTransaction);
        if (requiresTransaction && !hasUow)
        {
            // a transação é iniciada pelo BeginAsync do accessor; sem UoW não há accessor
            error = DiagnosticInfo.Create(
                CmdDiagnostics.WithTransactionRequiresUnitOfWork,
                location: method.Identifier.GetLocation());
            errors.Add(error);
            requiresTransaction = false;
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
            var paramDescriptor = parameterSymbol is null
                ? ParameterDescriptor.Create(p, context.SemanticModel)
                : new ParameterDescriptor(
                    SemanticTypes.CreateDescriptor(parameterSymbol.Type),
                    parameterSymbol.Name);

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

        // DF13: descobre e valida os métodos [CommandValidation]; executam após HasProblems e antes de
        // UoW/retry (uma vez, fora do laço). Parâmetros usam o mesmo modelo do comando.
        var validatorParameterChecks = new List<(ParameterDescriptor Parameter, Location Location)>();
        var validators = DiscoverValidators(
            commandType,
            methodSymbol,
            accessorType,
            context.SemanticModel,
            parameters,
            capturedBindings,
            validatorParameterChecks,
            produceProblems,
            errors,
            method.Identifier.GetLocation(),
            cancellationToken);

        // Um nome representa um único parâmetro lógico no handler/endpoint. Declarações repetidas com o mesmo
        // binding são deduplicadas; fontes distintas são preservadas para RCCMD033, sem chaves duplicadas no DTO.
        var normalizedCapturedBindings = NormalizeCapturedBindings(capturedBindings);

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
        var handlerMustBeAsync = isAsync || hasWithDecorators || hasUow || hasFindEntities
            || validators.Any(validator => validator.IsAwaitable);

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
            foreach (var (parameterName, parameterLocation, captured) in normalizedCapturedBindings)
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
            editEntityIdParameterName: editType?.Parameter is not null ? $"{editType.Parameter.Name}Id" : null,
            hasWithValidateModel: hasWithValidateModel,
            validatorCount: validators.Count);

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

        // os parâmetros dos validators (DF13) compartilham os mesmos escopos do handler/endpoint
        foreach (var (validatorParameter, validatorParameterLocation) in validatorParameterChecks)
        {
            if (validatorParameter.Type.IsCancellationToken || validatorParameter.Type.IsContext)
                continue;

            var collides = reservedNames.Contains(validatorParameter.Name)
                || (endpointReservedNames is not null
                    && validatorParameter.Type.IsHandlerParameter
                    && endpointReservedNames.Contains(validatorParameter.Name));

            if (collides)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.ReservedIdentifier,
                    validatorParameterLocation,
                    validatorParameter.Name));
            }
        }

        // Define o tipo de retorno do handler
        var handlerReturnType = methodReturnType;
        if (handlerMustBeAsync)
            handlerReturnType = handlerReturnType.MustBeTask();
        if (hasWithValidateModel || hasUow || hasFindEntities || mapInformation is not null || validators.Count > 0)
            handlerReturnType = handlerReturnType.MustBeResult();

        // detecta se o comando tem shape de body no minimal API.
        // Sem isso, o endpoint não precisa receber o comando como parâmetro (pode instanciar via new),
        // e uma requisição sem corpo é válida.
        var hasRequestBody = HasRequestBodyShape(classDeclaration, context.SemanticModel);

        // GET/DELETE não podem inferir body: o ASP.NET Core lança na inicialização do app.
        // Comandos com BindAsync/TryParse válidos usam binding customizado e não inferem body.
        var commandUsesCustomBinding = HasValidCustomBindingMethod(commandType);
        if (mapInformation is not null && hasRequestBody &&
            mapInformation.HttpMethod is "Get" or "Delete" &&
            !commandUsesCustomBinding)
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

            var commandUsesJsonBody = hasRequestBody && !commandUsesCustomBinding;
            var jsonBodySources = fromBodyParameters.Count + (commandUsesJsonBody ? 1 : 0);

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
            RequiresTransaction = requiresTransaction,
            HasWithFindEntities = hasFindEntities,
            ContextAccessorType = accessorType,
            ContextAccessorMode = contextAccessorMode,
            IdPropertiesBindings = idPropertiesBindings,
            // categorias agregadas do comando, do HasProblems e dos validators, sem duplicatas
            ProduceProblems = produceProblems.Distinct().ToList(),
            Validators = validators,
            ProduceNewEntityType = newEntityType,
            EditType = editType,
            MapInformation = mapInformation,
            HasRetryOnConcurrency = hasRetryOnConcurrency,
            RetryMaxAttempts = retryMaxAttempts,
            RetryOperation = retryOperation,
            HasBodyProperties = hasRequestBody,
            ParameterBindings = normalizedCapturedBindings.Count > 0
                ? normalizedCapturedBindings.ToDictionary(
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
        string? editEntityIdParameterName = null,
        bool hasWithValidateModel = false,
        int validatorCount = 0)
    {
        // nomes que o handler gerado emite como parâmetro/campo/local no mesmo escopo do comando.
        var reserved = new HashSet<string>(StringComparer.Ordinal) { ModelVarName };

        if (editEntityIdParameterName is not null)
            reserved.Add(editEntityIdParameterName);

        // local emitido por WithValidateModel (ValidateHasProblemsCommand)
        if (hasWithValidateModel)
            reserved.Add("validationProblems");

        // locais emitidos por cada validação adicional (DF13)
        for (var index = 1; index <= validatorCount; index++)
        {
            reserved.Add($"validationResult{index}");
            reserved.Add($"validationProblems{index}");
        }

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
        if (hasRetryOnConcurrency)
            reserved.Add(RetryProblemFactoryVarName);

        return reserved;
    }

    /// <summary>
    /// <para>
    ///     DF13: descobre os métodos <c>[CommandValidation]</c> da classe do comando, valida a declaração
    ///     (instância, não genérico, acessível, retorno <c>Result</c>/<c>Task&lt;Result&gt;</c>/<c>ValueTask&lt;Result&gt;</c>,
    ///     sem <c>ref</c>/<c>out</c>/<c>in</c>/<c>params</c>) e classifica os parâmetros com o mesmo modelo do
    ///     comando (token, <c>[WithParameter]</c> com bindings, dependência DI); entidades, contextos e
    ///     acessores são rejeitados (a validação executa antes de qualquer carregamento).
    /// </para>
    /// <para>
    ///     Retorna os validators ordenados por <c>Order</c> (padrão 10) com desempate determinístico pela
    ///     assinatura totalmente qualificada — sem precedência observável para o usuário.
    /// </para>
    /// </summary>
    private static List<CommandValidationInformation> DiscoverValidators(
        INamedTypeSymbol commandType,
        IMethodSymbol commandMethod,
        TypeDescriptor? accessorType,
        SemanticModel semanticModel,
        List<ParameterDescriptor> commandParameters,
        List<(string Name, Location Location, CapturedBindings Captured)> capturedBindings,
        List<(ParameterDescriptor Parameter, Location Location)> validatorParameterChecks,
        List<string> produceProblems,
        List<DiagnosticInfo> errors,
        Location fallbackLocation,
        CancellationToken cancellationToken)
    {
        var discovered = new List<(int Order, string SortKey, CommandValidationInformation Information)>();

        // mesmo nome de parâmetro entre comando e validators compartilha uma única dependência: o tipo deve
        // ser o mesmo (RCCMD040)
        var parametersByName = new Dictionary<string, (TypeDescriptor Type, ParameterRole Role)>(StringComparer.Ordinal);
        foreach (var commandParameter in commandParameters)
            parametersByName[commandParameter.Name] = (commandParameter.Type, GetParameterRole(commandParameter));

        foreach (var candidate in commandType.GetMembers().OfType<IMethodSymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!KnownAttributes.TryGet(candidate, KnownAttributes.CommandValidation, out var validationAttr))
                continue;

            var methodLocation = candidate.Locations.FirstOrDefault(l => l.IsInSource) ?? fallbackLocation;

            string? declarationProblem = null;
            if (SymbolEqualityComparer.Default.Equals(candidate, commandMethod))
                declarationProblem = "the command method cannot also be a validation method";
            else if (candidate.MethodKind != MethodKind.Ordinary)
                declarationProblem = "the validation must be an ordinary method";
            else if (candidate.IsStatic)
                declarationProblem = "the validation method must be an instance method";
            else if (candidate.IsAbstract)
                declarationProblem = "the validation method must not be abstract";
            else if (candidate.Arity > 0)
                declarationProblem = "the validation method must not be generic";
            else if (candidate.DeclaredAccessibility is not (
                Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
                declarationProblem = "the validation method must be accessible to the generated handler (public or internal)";
            else if (candidate.Parameters.Any(parameter => parameter.RefKind != RefKind.None || parameter.IsParams))
                declarationProblem = "the validation method must not have ref/out/in/params parameters";

            var isAwaitable = false;
            if (declarationProblem is null && !TryClassifyValidatorReturn(candidate.ReturnType, out isAwaitable))
                declarationProblem = "the validation method must return Result, Task<Result> or ValueTask<Result>";

            if (declarationProblem is not null)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidCommandValidation,
                    methodLocation,
                    candidate.Name,
                    declarationProblem));
                continue;
            }

            // parâmetros: mesmo modelo do comando (token, [WithParameter] com bindings, DI)
            var descriptors = new List<ParameterDescriptor>(candidate.Parameters.Length);
            foreach (var parameterSymbol in candidate.Parameters)
            {
                var parameterSyntax = parameterSymbol.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax(cancellationToken))
                    .OfType<ParameterSyntax>()
                    .FirstOrDefault();

                var parameterModel = parameterSyntax is null || parameterSyntax.SyntaxTree == semanticModel.SyntaxTree
                    ? semanticModel
                    : semanticModel.Compilation.GetSemanticModel(parameterSyntax.SyntaxTree);

                var descriptor = new ParameterDescriptor(
                    SemanticTypes.CreateDescriptor(parameterSymbol.Type),
                    parameterSymbol.Name);

                var parameterLocation = parameterSyntax?.Identifier.GetLocation() ?? methodLocation;

                if (IsType(parameterSymbol.Type, "System.Threading", "CancellationToken"))
                {
                    // mesmo contrato do comando: token só em método assíncrono
                    if (!isAwaitable)
                    {
                        errors.Add(DiagnosticInfo.Create(
                            CmdDiagnostics.CancellationTokenParameterMustBeAsync,
                            parameterLocation));
                    }
                }
                else if (IsForbiddenValidationParameter(parameterSymbol, parameterSyntax, parameterModel, descriptor, accessorType))
                {
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.InvalidCommandValidationParameter,
                        parameterLocation,
                        parameterSymbol.Name,
                        candidate.Name));
                }
                else
                {
                    if (KnownAttributes.Has(parameterSymbol, KnownAttributes.WithParameter))
                    {
                        descriptor.Type.MarkAsHandlerParameter();

                        var captured = BindingAttributes.Capture(parameterSymbol);
                        if (captured.SourceCount > 0 || captured.HasAsParameters)
                            capturedBindings.Add((descriptor.Name, parameterLocation, captured));
                    }

                    // mesmo nome exige o mesmo tipo (dependência/parâmetro compartilhado no handler)
                    var role = GetParameterRole(descriptor);
                    if (parametersByName.TryGetValue(descriptor.Name, out var existing))
                    {
                        if (!existing.Type.Equals(descriptor.Type))
                        {
                            errors.Add(DiagnosticInfo.Create(
                                CmdDiagnostics.ConflictingParameterTypes,
                                parameterLocation,
                                descriptor.Name));
                        }
                        else if (existing.Role != role)
                        {
                            errors.Add(DiagnosticInfo.Create(
                                CmdDiagnostics.ConflictingParameterRoles,
                                parameterLocation,
                                descriptor.Name,
                                FormatParameterRole(existing.Role),
                                FormatParameterRole(role)));
                        }
                    }
                    else
                    {
                        parametersByName[descriptor.Name] = (descriptor.Type, role);
                    }
                }

                descriptors.Add(descriptor);
                validatorParameterChecks.Add((descriptor, parameterLocation));
            }

            // agrega os ProduceProblems declarados no validator à metadata do endpoint
            if (KnownAttributes.TryGet(candidate, KnownAttributes.ProduceProblems, out var validatorProduceProblems))
                AddProduceProblems(
                    validatorProduceProblems!, produceProblems, errors, methodLocation, cancellationToken);

            var order = validationAttr!.NamedArguments
                .Where(argument => argument.Key == "Order")
                .Select(argument => argument.Value is { Kind: TypedConstantKind.Primitive, Value: int value } ? value : 10)
                .DefaultIfEmpty(10)
                .First();

            // desempate por assinatura totalmente qualificada — apenas para determinismo da saída
            var sortKey = $"{candidate.Name}({string.Join(",", candidate.Parameters.Select(p => p.Type.ToDisplayString()))})";

            discovered.Add((order, sortKey, new CommandValidationInformation(candidate.Name, isAwaitable, descriptors)));
        }

        return discovered
            .OrderBy(entry => entry.Order)
            .ThenBy(entry => entry.SortKey, StringComparer.Ordinal)
            .Select(entry => entry.Information)
            .ToList();
    }

    private static bool TryClassifyValidatorReturn(ITypeSymbol returnType, out bool isAwaitable)
    {
        isAwaitable = false;

        if (IsType(returnType, "RoyalCode.SmartProblems", "Result"))
            return true;

        if ((IsType(returnType, "System.Threading.Tasks", "Task`1") ||
             IsType(returnType, "System.Threading.Tasks", "ValueTask`1")) &&
            returnType is INamedTypeSymbol named &&
            IsType(named.TypeArguments[0], "RoyalCode.SmartProblems", "Result"))
        {
            isAwaitable = true;
            return true;
        }

        return false;
    }

    private enum ParameterRole
    {
        Dependency,
        External,
        CancellationToken,
        Context,
        Entity,
    }

    private static ParameterRole GetParameterRole(ParameterDescriptor parameter)
    {
        if (parameter.Type.IsCancellationToken)
            return ParameterRole.CancellationToken;
        if (parameter.Type.IsHandlerParameter)
            return ParameterRole.External;
        if (parameter.Type.IsContext)
            return ParameterRole.Context;
        if (parameter.Type.IsEntity || parameter.Type.IsCollectionOfEntities)
            return ParameterRole.Entity;
        return ParameterRole.Dependency;
    }

    private static string FormatParameterRole(ParameterRole role) => role switch
    {
        ParameterRole.Dependency => "dependency injection",
        ParameterRole.External => "WithParameter",
        ParameterRole.CancellationToken => "CancellationToken",
        ParameterRole.Context => "context",
        ParameterRole.Entity => "entity",
        _ => role.ToString(),
    };

    private static List<(string Name, Location Location, CapturedBindings Captured)> NormalizeCapturedBindings(
        List<(string Name, Location Location, CapturedBindings Captured)> capturedBindings)
    {
        var normalized = new List<(string Name, Location Location, CapturedBindings Captured)>();

        foreach (var group in capturedBindings.GroupBy(binding => binding.Name, StringComparer.Ordinal))
        {
            var bindings = group
                .SelectMany(binding => binding.Captured.Bindings)
                .Distinct()
                .ToArray();
            var hasAsParameters = group.Any(binding => binding.Captured.HasAsParameters);
            var first = group.First();

            normalized.Add((
                group.Key,
                first.Location,
                new CapturedBindings(
                    new EquatableArray<ParameterBindingModel>(bindings),
                    bindings.Length,
                    hasAsParameters)));
        }

        return normalized;
    }

    /// <summary>
    /// Entidades, coleções de entidades, contextos e acessores não estão disponíveis na validação
    /// pré-carregamento (DF13); uma fase pós-carregamento é backlog separado.
    /// </summary>
    private static bool IsForbiddenValidationParameter(
        IParameterSymbol parameterSymbol,
        ParameterSyntax? parameterSyntax,
        SemanticModel parameterModel,
        ParameterDescriptor descriptor,
        TypeDescriptor? accessorType)
    {
        if (accessorType is not null && descriptor.Type.Equals(accessorType))
            return true;

        if (parameterSyntax is not null &&
            (parameterSyntax.IsEntity(parameterModel) || parameterSyntax.IsCollectionOfEntities(parameterModel)))
        {
            return true;
        }

        var type = parameterSymbol.Type;
        if (IsType(type, "RoyalCode.SmartCommands", "IUnitOfWorkAccessor`1") ||
            IsType(type, "RoyalCode.SmartCommands", "IRepositoriesAccessor`1") ||
            IsType(type, "RoyalCode.WorkContext", "IWorkContext") ||
            type.AllInterfaces.Any(candidate => IsType(candidate, "RoyalCode.WorkContext", "IWorkContext")))
        {
            return true;
        }

        for (var baseType = type; baseType is not null; baseType = baseType.BaseType)
        {
            if (IsType(baseType, "Microsoft.EntityFrameworkCore", "DbContext"))
                return true;
        }

        return false;
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
            var matches = routeParameters.Where(parameter =>
                string.Equals(parameter.Name, explicitRouteParameterName, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (matches.Length != 1)
            {
                var reason = matches.Length == 0
                    ? $"the route parameter '{explicitRouteParameterName}' specified in RouteParameterName does not exist in the template"
                    : $"the route parameter '{explicitRouteParameterName}' specified in RouteParameterName occurs more than once in the template";
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.EditEntityRouteParameterNotResolved,
                    location,
                    template,
                    reason));
                return null;
            }

            resolved = matches[0];
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
            var conventionalName = $"{entityParameterName}Id";
            var matches = routeParameters.Where(parameter =>
                string.Equals(parameter.Name, conventionalName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0)
            {
                conventionalName = entityParameterName;
                matches = routeParameters.Where(parameter =>
                    string.Equals(parameter.Name, conventionalName, StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            if (matches.Length != 1)
            {
                var reason = matches.Length == 0
                    ? $"the template has multiple route parameters and none matches '{entityParameterName}Id' or '{entityParameterName}'; specify RouteParameterName on EditEntityAttribute"
                    : $"the route parameter '{conventionalName}' occurs more than once in the template; specify a unique RouteParameterName on EditEntityAttribute";
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.EditEntityRouteParameterNotResolved,
                    location,
                    template,
                    reason));
                return null;
            }

            resolved = matches[0];
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

            if (!RouteConstraintTypes.TryGetClrTypeName(baseName.Trim(), out var expectedTypeName))
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
    /// Determina se o tipo possui uma das assinaturas de binding customizado reconhecidas pelo Minimal API:
    /// <c>BindAsync(HttpContext[, ParameterInfo])</c>, retornando <c>ValueTask&lt;T&gt;</c>, ou
    /// <c>TryParse(string[, IFormatProvider], out T)</c>, retornando <c>bool</c>.
    /// </summary>
    private static bool HasValidCustomBindingMethod(INamedTypeSymbol commandType) =>
        commandType.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(candidate => IsValidBindAsync(candidate, commandType) || IsValidTryParse(candidate, commandType));

    private static bool IsValidBindAsync(IMethodSymbol method, INamedTypeSymbol targetType)
    {
        if (!IsPublicStaticOrdinaryMethod(method, "BindAsync") ||
            method.ReturnType is not INamedTypeSymbol returnType ||
            !IsType(returnType, "System.Threading.Tasks", "ValueTask`1") ||
            returnType.TypeArguments.Length != 1 ||
            !SymbolEqualityComparer.Default.Equals(returnType.TypeArguments[0], targetType))
        {
            return false;
        }

        return method.Parameters.Length is 1 or 2 &&
            IsType(method.Parameters[0].Type, "Microsoft.AspNetCore.Http", "HttpContext") &&
            (method.Parameters.Length == 1 ||
             IsType(method.Parameters[1].Type, "System.Reflection", "ParameterInfo")) &&
            method.Parameters.All(parameter => parameter.RefKind == RefKind.None);
    }

    private static bool IsValidTryParse(IMethodSymbol method, INamedTypeSymbol targetType)
    {
        if (!IsPublicStaticOrdinaryMethod(method, "TryParse") ||
            method.ReturnType.SpecialType != SpecialType.System_Boolean ||
            method.Parameters.Length is not (2 or 3) ||
            method.Parameters[0].Type.SpecialType != SpecialType.System_String ||
            method.Parameters[0].RefKind != RefKind.None)
        {
            return false;
        }

        var result = method.Parameters[method.Parameters.Length - 1];
        if (result.RefKind != RefKind.Out ||
            !SymbolEqualityComparer.Default.Equals(result.Type, targetType))
        {
            return false;
        }

        return method.Parameters.Length == 2 ||
            (method.Parameters[1].RefKind == RefKind.None &&
             IsType(method.Parameters[1].Type, "System", "IFormatProvider"));
    }

    private static bool IsPublicStaticOrdinaryMethod(IMethodSymbol method, string name) =>
        method.Name == name &&
        method.IsStatic &&
        method.MethodKind == MethodKind.Ordinary &&
        method.DeclaredAccessibility == Accessibility.Public &&
        !method.IsGenericMethod;

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
    private static void AddProduceProblems(
        AttributeData attribute,
        List<string> produceProblems,
        List<DiagnosticInfo> errors,
        Location fallbackLocation,
        CancellationToken cancellationToken)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.IsNull)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidEndpointMetadataArgument,
                    KnownAttributes.GetLocation(attribute, cancellationToken, fallbackLocation),
                    "ProduceProblems",
                    "a non-null collection of problem categories"));
                continue;
            }

            if (argument.Kind == TypedConstantKind.Array)
            {
                if (argument.Values.IsDefault)
                {
                    errors.Add(DiagnosticInfo.Create(
                        CmdDiagnostics.InvalidEndpointMetadataArgument,
                        KnownAttributes.GetLocation(attribute, cancellationToken, fallbackLocation),
                        "ProduceProblems",
                        "a non-null collection of problem categories"));
                    continue;
                }

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

        // o endpoint name alimenta WithName e a deduplicação global (RCCMD030); vazio é inválido
        EndpointNameRules.ValidateEndpointName(
            endpointName,
            $"Map{httpMethod}",
            KnownAttributes.GetArgumentLocation(attr, 1, cancellationToken, attributeLocation),
            errors);

        // tenta obter a descrição
        if (KnownAttributes.TryGet(commandType, KnownAttributes.WithDescription, out var descAttr))
        {
            if (!TryReadRequiredString(descAttr!, "WithDescription", "a non-null description", method, errors,
                    cancellationToken, out description))
                return null;
        }

        // tenta obter o summary
        if (KnownAttributes.TryGet(commandType, KnownAttributes.WithSummary, out var summaryAttr))
        {
            if (!TryReadRequiredString(summaryAttr!, "WithSummary", "a non-null summary", method, errors,
                    cancellationToken, out summary))
                return null;
        }

        // tenta obter o authorization
        if (KnownAttributes.Has(commandType, KnownAttributes.WithAuthorization))
            authorizationPolicies = [];

        // se tiver o attribute WithPolicy, deve obter o(s) nome(s) da(s) política(s) — aceita params e array explícito
        if (KnownAttributes.TryGet(commandType, KnownAttributes.WithPolicy, out var policyAttr))
        {
            if (policyAttr!.ConstructorArguments.Length != 1 ||
                policyAttr.ConstructorArguments[0].Kind == TypedConstantKind.Error)
                return null;

            if (!KnownAttributes.TryGetStrings(policyAttr.ConstructorArguments[0], out var policies))
            {
                AddInvalidEndpointMetadataDiagnostic(
                    policyAttr, "WithPolicy", "a non-null array of policy names", method, errors, cancellationToken);
                return null;
            }
            authorizationPolicies = policies.Length > 0 ? policies : authorizationPolicies ?? [];
        }

        // tenta obter o MapGroup attribute
        if (KnownAttributes.TryGet(commandType, KnownAttributes.MapGroup, out var groupAttr))
        {
            if (!TryReadRequiredString(groupAttr!, "MapGroup", "a non-null route prefix", method, errors,
                    cancellationToken, out groupName))
                return null;

            // o prefixo do grupo deriva a classe/método gerados (Map{Nome}Api); precisa formar identificador
            EndpointNameRules.ValidateGroupName(
                groupName!,
                KnownAttributes.GetLocation(groupAttr!, cancellationToken, method.Identifier.GetLocation()),
                errors);
        }

        // tenta obter MapCreatedRoute — (route pattern, params nomes de propriedades)
        if (KnownAttributes.TryGet(commandType, KnownAttributes.MapCreatedRoute, out var createdRouteAttr))
        {
            if (createdRouteAttr!.ConstructorArguments.Length == 0 ||
                createdRouteAttr.ConstructorArguments[0].Kind == TypedConstantKind.Error)
                return null;

            var createdRoutePattern = KnownAttributes.GetString(createdRouteAttr.ConstructorArguments[0]);
            if (createdRoutePattern is null)
            {
                AddInvalidEndpointMetadataDiagnostic(
                    createdRouteAttr, "MapCreatedRoute", "a non-null route pattern", method, errors, cancellationToken);
                return null;
            }

            string[] propertiesNames = [];
            if (createdRouteAttr.ConstructorArguments.Length > 1)
            {
                var propertiesArgument = createdRouteAttr.ConstructorArguments[1];
                if (propertiesArgument.Kind == TypedConstantKind.Error)
                    return null;
                if (!KnownAttributes.TryGetStrings(propertiesArgument, out propertiesNames))
                {
                    AddInvalidEndpointMetadataDiagnostic(
                        createdRouteAttr, "MapCreatedRoute", "a non-null array of property names", method, errors,
                        cancellationToken);
                    return null;
                }
            }

            // DF17: somente placeholders nomeados, casados sem diferenciar maiúsculas com as propriedades
            // declaradas; quantidade, nome, duplicação e propriedade incompatível são diagnosticados
            ValidateCreatedRoute(
                createdRouteAttr!,
                createdRoutePattern,
                propertiesNames,
                valueReturnType,
                method,
                errors,
                cancellationToken);

            createdInformation = new MapCreatedInformation(createdRoutePattern, propertiesNames);
        }

        // MapIdResultValue e MapResponseValues são mapeamentos de resposta mutuamente exclusivos;
        // com os dois presentes, um deles seria ignorado em silêncio
        var hasMapIdResultValue = KnownAttributes.Has(commandType, KnownAttributes.MapIdResultValue);
        var hasMapResponseValues = KnownAttributes.TryGet(
            commandType, KnownAttributes.MapResponseValues, out var resultValueAttr);
        if (hasMapIdResultValue && hasMapResponseValues)
        {
            errors.Add(DiagnosticInfo.Create(
                CmdDiagnostics.ConflictingResponseMappings,
                attributeLocation,
                commandType.Name));
            return null;
        }

        // tenta obter MapIdResultValue
        if (hasMapIdResultValue)
        {
            // quando há o attribute MapIdResultValue, deve obter a propriedade Id e o tipo dela no tipo de valor retornado.
            var idProperty = valueReturnType?
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(p => p.Name == "Id");

            if (idProperty is null)
            {
                // se não achar a propriedade, gera o diagnóstico.
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.IdNotFoundInReturnedCommand,
                    method.ReturnType.GetLocation()));
            }
            else if (GetResponsePropertyProblem(idProperty) is { } idPropertyProblem)
            {
                errors.Add(DiagnosticInfo.Create(
                    CmdDiagnostics.InvalidResponseProperty,
                    method.ReturnType.GetLocation(),
                    "Id",
                    idPropertyProblem));
            }
            else
            {
                idResultValueType = TypeDescriptor.Create(idProperty.Type);
            }
        }

        // tenta obter MapResponseValues e seus parâmetros
        if (hasMapResponseValues)
        {
            if (resultValueAttr!.ConstructorArguments.Length == 0 ||
                resultValueAttr.ConstructorArguments[0].Kind == TypedConstantKind.Error)
                return null;

            if (!KnownAttributes.TryGetStrings(resultValueAttr.ConstructorArguments[0], out var propertiesNames))
            {
                AddInvalidEndpointMetadataDiagnostic(
                    resultValueAttr, "MapResponseValues", "a non-null array of property names", method, errors,
                    cancellationToken);
                return null;
            }

            if (propertiesNames.Length == 0)
            {
                // lista vazia era ignorada em silêncio; o atributo declara uma projeção e precisa de propriedades
                AddInvalidEndpointMetadataDiagnostic(
                    resultValueAttr, "MapResponseValues", "a non-empty array of property names", method, errors,
                    cancellationToken);
            }
            else
            {
                // nomes duplicados gerariam propriedades/parâmetros repetidos no POCO de resposta
                foreach (var duplicated in propertiesNames
                    .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1))
                {
                    AddInvalidEndpointMetadataDiagnostic(
                        resultValueAttr,
                        "MapResponseValues",
                        $"unique property names (the property '{duplicated.Key}' is declared more than once)",
                        method,
                        errors,
                        cancellationToken);
                }

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
                    // valida existência, leitura pública e tipo emitível, e cria um PropertyDescriptor
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

                            if (GetResponsePropertyProblem(property) is { } propertyProblem)
                            {
                                errors.Add(DiagnosticInfo.Create(
                                    CmdDiagnostics.InvalidResponseProperty,
                                    method.ReturnType.GetLocation(),
                                    name,
                                    propertyProblem));
                                return null;
                            }

                            // cria o PropertyDescriptor, quando a propriedade é utilizável
                            return PropertyDescriptor.Create(property);
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

    /// <summary>
    /// DF17: valida o pattern do <c>MapCreatedRoute</c> — somente placeholders nomeados simples, sem
    /// duplicação, na mesma quantidade das propriedades declaradas, cada placeholder casando (sem diferenciar
    /// maiúsculas) com uma propriedade legível existente no tipo de valor retornado.
    /// </summary>
    private static void ValidateCreatedRoute(
        AttributeData attribute,
        string routePattern,
        string[] propertiesNames,
        ITypeSymbol? valueReturnType,
        MethodDeclarationSyntax method,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        var location = KnownAttributes.GetLocation(attribute, cancellationToken, method.Identifier.GetLocation());
        var placeholders = RoutePatternParser.Parse(routePattern);

        void Fail(string reason) =>
            errors.Add(DiagnosticInfo.Create(CmdDiagnostics.InvalidCreatedRoute, location, routePattern, reason));

        foreach (var placeholder in placeholders)
        {
            if (placeholder.Constraint is not null || placeholder.DefaultValue is not null ||
                placeholder.IsOptional || placeholder.IsCatchAll)
            {
                Fail($"the placeholder '{placeholder.Name}' must be a simple name, without constraints, " +
                    "default values, optional or catch-all markers");
            }
        }

        foreach (var duplicated in placeholders
            .GroupBy(placeholder => placeholder.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Fail($"the placeholder '{duplicated.Key}' occurs more than once");
        }

        foreach (var duplicated in propertiesNames
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1))
        {
            Fail($"the property '{duplicated.Key}' is declared more than once");
        }

        if (placeholders.Count != propertiesNames.Length)
        {
            Fail($"the pattern declares {placeholders.Count} placeholder(s) but {propertiesNames.Length} " +
                "property name(s) were declared; each placeholder must match exactly one property declared with nameof");
        }

        foreach (var placeholder in placeholders)
        {
            if (!propertiesNames.Any(name => string.Equals(name, placeholder.Name, StringComparison.OrdinalIgnoreCase)))
            {
                Fail($"the placeholder '{{{placeholder.Name}}}' does not match any declared property; " +
                    "MapCreatedRoute uses named placeholders (e.g. \"{id}\") matched, case-insensitively, " +
                    "to properties declared with nameof");
            }
        }

        if (propertiesNames.Length == 0)
            return;

        if (valueReturnType is null)
        {
            Fail("the command does not return a value; there are no properties to fill the placeholders");
            return;
        }

        // tipo de valor não resolvido (em digitação): o compilador já reporta o erro; sem RCCMD extra (DF9)
        if (valueReturnType.TypeKind == TypeKind.Error)
            return;

        foreach (var propertyName in propertiesNames)
        {
            var property = valueReturnType
                .GetAllMembers()
                .OfType<IPropertySymbol>()
                .FirstOrDefault(candidate => candidate.Name == propertyName);

            if (property is null)
                Fail($"the property '{propertyName}' was not found on the returned value type '{valueReturnType.Name}'");
            else if (GetResponsePropertyProblem(property) is { } problem)
                Fail($"the property '{propertyName}' cannot be used: {problem}");
        }
    }

    /// <summary>
    /// Uma propriedade usada na resposta gerada (Location de created, projeção de <c>MapResponseValues</c> ou
    /// <c>MapIdResultValue</c>) precisa ser de instância, pública, legível e de tipo acessível ao código gerado.
    /// </summary>
    private static string? GetResponsePropertyProblem(IPropertySymbol property)
    {
        if (property.IsStatic)
            return "the property is static";
        if (property.DeclaredAccessibility != Accessibility.Public)
            return "the property is not public";
        if (property.GetMethod is null)
            return "the property does not have a getter";
        if (property.GetMethod.DeclaredAccessibility != Accessibility.Public)
            return "the property getter is not public";
        if (property.Type.TypeKind == TypeKind.Error)
            return "the property type cannot be resolved";
        if (!IsEmittableType(property.Type))
            return "the property type is not accessible to the generated code";
        return null;
    }

    /// <summary>
    /// O código é gerado no mesmo assembly: tipos públicos e internos são utilizáveis; privados e protegidos
    /// (aninhados) não podem ser referenciados pela emissão.
    /// </summary>
    private static bool IsEmittableType(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return IsEmittableType(array.ElementType);
            case INamedTypeSymbol named:
                for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
                {
                    if (current.DeclaredAccessibility is
                        Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
                    {
                        return false;
                    }
                }

                return named.TypeArguments.All(IsEmittableType);
            default:
                return true;
        }
    }

    private static bool TryReadRequiredString(
        AttributeData attribute,
        string attributeName,
        string requirement,
        MethodDeclarationSyntax method,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken,
        out string? value)
    {
        value = null;
        if (attribute.ConstructorArguments.Length != 1 ||
            attribute.ConstructorArguments[0].Kind == TypedConstantKind.Error)
            return false;

        value = KnownAttributes.GetString(attribute.ConstructorArguments[0]);
        if (value is not null)
            return true;

        AddInvalidEndpointMetadataDiagnostic(
            attribute, attributeName, requirement, method, errors, cancellationToken);
        return false;
    }

    private static void AddInvalidEndpointMetadataDiagnostic(
        AttributeData attribute,
        string attributeName,
        string requirement,
        MethodDeclarationSyntax method,
        List<DiagnosticInfo> errors,
        CancellationToken cancellationToken)
    {
        errors.Add(DiagnosticInfo.Create(
            CmdDiagnostics.InvalidEndpointMetadataArgument,
            KnownAttributes.GetLocation(attribute, cancellationToken, method.Identifier.GetLocation()),
            attributeName,
            requirement));
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
        if (i.HasRetryOnConcurrency)
        {
            // a factory é sempre injetada: sem Operation explícita, o handler usa a chave default
            // ({namespace}.{Comando}), e as options ExhaustedProblemDetail/TypeId valem em todos os caminhos
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

        // dependências injetadas (comando + validators), sem duplicar campos/parâmetros do construtor
        var injectedDependencyNames = new HashSet<string>(StringComparer.Ordinal);

        void AddDependency(ParameterDescriptor p)
        {
            // não requer ct
            if (p.Type.IsCancellationToken)
                return;

            // se for uma entidade, não deve recebê-la no construtor.
            if (p.Type.IsEntity || p.Type.IsCollectionOfEntities)
                return;

            // se for o contexto, não deve recebê-la no construtor.
            if (p.Type.IsContext)
                return;

            // se for um parâmetro marcado com WithParameter, não deve recebê-lo no construtor.
            if (p.Type.IsHandlerParameter)
                return;

            // dependência compartilhada por nome (o conflito de tipos é diagnosticado no transform)
            if (!injectedDependencyNames.Add(p.Name))
                return;

            // Adiciona o parâmetro como dependência do handler e o recebe no construtor.
            // adiciona o campo
            handlerGen.Fields.Add(new FieldGenerator(p.Type, p.Name, true));
            // adiciona o parâmetro
            ctorGen.Parameters.Add(new ParameterGenerator(p));
            // adiciona comando de atribuição
            ctorGen.Commands.Add(AssignValueCommand.CreateParameterAssignField(p.Name));
        }

        // para cada parâmetro do método do comando, valida se é necessário adicionar como campo do construtor.
        foreach (var p in i.Parameters)
            AddDependency(p);

        // dependências dos validators (DF13)
        foreach (var validator in i.Validators)
            foreach (var p in validator.Parameters)
                AddDependency(p);

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

        // DF13: validações adicionais rodam após HasProblems e antes de UoW/retry (fora do laço, uma vez);
        // o primeiro Result com problemas encerra o handler
        for (var validatorIndex = 0; validatorIndex < i.Validators.Count; validatorIndex++)
        {
            var validator = i.Validators[validatorIndex];
            var resultVarName = $"validationResult{validatorIndex + 1}";
            var problemsVarName = $"validationProblems{validatorIndex + 1}";

            var validatorInvoke = new MethodInvokeGenerator(ModelVarName, validator.MethodName)
            {
                Await = validator.IsAwaitable
            };
            foreach (var validatorParameter in validator.Parameters)
            {
                validatorInvoke.AddArgument(validatorParameter.Type.IsCancellationToken
                    ? CancellationTokenParameterName
                    : validatorParameter.Name);
            }

            handlerMethodImpl.Commands.Add(new AssignValueCommand(
                new StringValueNode($"var {resultVarName}"),
                validatorInvoke));

            var problemsCheck = new MethodInvokeGenerator(resultVarName, "HasProblems");
            problemsCheck.AddArgument($"out var {problemsVarName}");
            var shortCircuit = new IfCommand(problemsCheck);
            shortCircuit.AddCommand(new ReturnCommand(problemsVarName));
            handlerMethodImpl.Commands.Add(shortCircuit);
        }

        // quando há retry de concorrência, o corpo {Begin → finds → Execute → Complete} é coletado à parte
        // para ser envolvido por uma lambda passada à primitiva; senão, vai direto no corpo do método.
        var bodyTarget = i.HasRetryOnConcurrency ? new GeneratorNodeList() : handlerMethodImpl.Commands;

        // comando unit of work begin
        if (i.HasWithUnitOfWork)
            bodyTarget.Add(new BeginUnitOfWorkCommand(AccessorVarName, i.RequiresTransaction));

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

            // sem Operation explícita, a chave default é o nome qualificado do comando — estável,
            // não localizada, e utilizável em AddConcurrencyRetryProblem para registro por comando
            var retryOperation = i.RetryOperation ?? $"{i.Namespace}.{i.ModelType.Name}";
            var onExhaustedArgument =
                $"this.{RetryProblemFactoryVarName}.Create({ModelVarName}, {SymbolDisplay.FormatLiteral(retryOperation, quote: true)})";

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

        // parâmetros com atributo WithParameter (do comando e dos validators, sem duplicar nomes);
        // no delegate Minimal API os bindings explícitos do parâmetro-fonte são copiados (DF3);
        // sem binding, o ASP.NET Core infere a fonte (DF2)
        var externalParameterNames = new HashSet<string>(StringComparer.Ordinal);

        void AddExternalParameter(ParameterDescriptor p)
        {
            if (!externalParameterNames.Add(p.Name))
                return;

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

        foreach (var p in commandInfo.Parameters.Where(p => p.Type.IsHandlerParameter))
            AddExternalParameter(p);

        foreach (var validator in commandInfo.Validators)
            foreach (var p in validator.Parameters.Where(p => p.Type.IsHandlerParameter))
                AddExternalParameter(p);

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
