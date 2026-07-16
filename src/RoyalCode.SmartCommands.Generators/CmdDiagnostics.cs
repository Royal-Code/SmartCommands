using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators;

internal static class CmdDiagnostics
{
    private const string Category = "Usage";

    public static readonly DiagnosticDescriptor InvalidCommandType = new(
        id: "RCCMD000",
        title: "Invalid command type",
        messageFormat: "Invalid use of CommandAttribute: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidReturnType = new(
        id: "RCCMD001",
        title: "Invalid return type",
        messageFormat: "When using CommandAttribute the method must not return Task or void",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor HasProblemsMethodNotFound = new(
        id: "RCCMD002",
        title: "HasProblems method not found",
        messageFormat: "The class must have a HasProblems method when using WithValidateModelAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor HasProblemsMethodDoesNotReturnBool = new(
        id: "RCCMD003",
        title: "HasProblems method does not return bool",
        messageFormat: "The HasProblems method must return a bool when using WithValidateModelAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor HasProblemsMethodDoesNotHaveOutParameterProblems = new(
        id: "RCCMD004",
        title: "HasProblems method does not have out parameter Problems",
        messageFormat: "The HasProblems method must have an out parameter of type Problems when using WithValidateModelAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
    
    public static readonly DiagnosticDescriptor EntityTypeParameterDoesNotHaveIdProperty = new(
        id: "RCCMD005",
        title: "The entity type parameter does not have a corresponding id property",
        messageFormat: "The {0} parameter is an entity type and requires a corresponding id property",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProduceNewEntityRequiresWithUnitOfWork = new(
        id: "RCCMD006",
        title: "The ProduceNewEntityAttribute requires WithUnitOfWorkAttribute",
        messageFormat: "The ProduceNewEntityAttribute requires WithUnitOfWorkAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProduceNewEntityMustReturnResultWithValue = new(
        id: "RCCMD007",
        title: "The ProduceNewEntityAttribute must return a Result with value",
        messageFormat: "When the command has the ProduceNewEntityAttribute and return a Result it must have a value",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CancellationTokenParameterMustBeAsync = new(
        id: "RCCMD008",
        title: "CancellationToken can only be used in async methods",
        messageFormat: "CancellationToken can only be used in async methods",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EditEntityRequiresWithUnitOfWork = new(
        id: "RCCMD009",
        title: "The EditEntityAttribute requires WithUnitOfWorkAttribute",
        messageFormat: "The EditEntityAttribute requires WithUnitOfWorkAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EditEntityRequiresFirstParameter = new(
        id: "RCCMD010",
        title: "When EditEntityAttribute is used, the first parameter must be of the same type as the entity entered in the attribute",
        messageFormat: "When EditEntityAttribute is used, the first parameter must be of the same type as the entity entered in the attribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ParameterCannotBeMarkedWithParameter = new(
        id: "RCCMD011",
        title: "The parameter cannot be marked with WithParameterAttribute",
        messageFormat: "You can't use WithParameterAttribute when the parameter is an entity or collection of entities or a context",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultiplesMapApiHandlers = new(
        id: "RCCMD012",
        title: "Only one MapApiHandlersAttribute should be used per project, and there is more than one",
        messageFormat: "Only one MapApiHandlersAttribute should be used per project, and there is more than one",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidMapApiHandlers = new(
        id: "RCCMD013",
        title: "Invalid use of MapApiHandlersAttribute",
        messageFormat: "Invalid use of MapApiHandlersAttribute: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IdNotFoundInReturnedCommand = new(
        id: "RCCMD014",
        title: "Id property not found for the object returned by the command",
        messageFormat: "Id property not found for the object returned by the command",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReturnedCommandTypeNotFound = new(
        id: "RCCMD015",
        title: "The type returned by the command was not found",
        messageFormat: "The type returned by the command was not found",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyNotFoundInReturnedCommand = new(
        id: "RCCMD016",
        title: "Property not found for the object returned by the command",
        messageFormat: "The property '{0}' was not found for the object returned by the command",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidFindType = new(
        id: "RCCMD017",
        title: "Invalid Find type",
        messageFormat: "Invalid use of MapFindAttribute: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WithDbContextCannotBeUsedWithWithUnitOfWork = new(
        id: "RCCMD018",
        title: "WithDbContextAttribute cannot be used with WithUnitOfWorkAttribute",
        messageFormat: "WithDbContextAttribute cannot be used with WithUnitOfWorkAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WithWorkContextCannotBeUsedWithWithUnitOfWork = new(
        id: "RCCMD019",
        title: "WithWorkContextAttribute cannot be used with WithUnitOfWorkAttribute",
        messageFormat: "WithWorkContextAttribute cannot be used with WithUnitOfWorkAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WithWorkContextCannotBeUsedWithWithDbContext = new(
        id: "RCCMD020",
        title: "WithWorkContextAttribute cannot be used with WithDbContextAttribute",
        messageFormat: "WithWorkContextAttribute cannot be used with WithDbContextAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);


    public static readonly DiagnosticDescriptor InvalidMapFindUsage = new(
        id: "RCCMD021",
        title: "Invalid use of the MapFind attribute",
        messageFormat: "Invalid use of MapFindAttribute: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidMapSearchUsage = new(
        id: "RCCMD022",
        title: "Invalid use of the MapSearch attribute",
        messageFormat: "Invalid use of MapSearchAttribute: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidWithFilterUsage = new(
        id: "RCCMD023",
        title: "Invalid use of the WithFilter attribute",
        messageFormat: "Invalid use of WithFilterAttribute: {0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RetryOnConcurrencyRequiresWorkContext = new(
        id: "RCCMD024",
        title: "WithRetryOnConcurrencyAttribute requires WithWorkContextAttribute",
        messageFormat: "WithRetryOnConcurrencyAttribute is only supported together with WithWorkContextAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RetryOnConcurrencyInvalidMaxAttempts = new(
        id: "RCCMD025",
        title: "Invalid maximum number of attempts for WithRetryOnConcurrencyAttribute",
        messageFormat: "The maximum number of attempts for WithRetryOnConcurrencyAttribute must be greater than zero",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleCommandMethods = new(
        id: "RCCMD026",
        title: "A command type must declare only one command method",
        messageFormat: "The command type '{0}' declares more than one method with CommandAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingMapAttributes = new(
        id: "RCCMD027",
        title: "A command type must declare only one map attribute",
        messageFormat: "The command type '{0}' declares conflicting Map attributes",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidMapArguments = new(
        id: "RCCMD028",
        title: "Invalid map attribute arguments",
        messageFormat: "The {0} attribute requires a route pattern and an endpoint name",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReservedIdentifier = new(
        id: "RCCMD029",
        title: "A command parameter uses a name reserved by generated code",
        messageFormat: "The parameter '{0}' collides with an identifier emitted by generated code in the same scope; rename it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateEndpointName = new(
        id: "RCCMD030",
        title: "Duplicate endpoint name",
        messageFormat: "The endpoint name '{0}' is used by more than one mapped endpoint; endpoint names must be unique",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EditEntityRouteParameterNotResolved = new(
        id: "RCCMD031",
        title: "The route parameter for the edited entity id cannot be resolved",
        messageFormat: "Cannot resolve the route parameter for the edited entity id in the route '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EditEntityRouteParameterIncompatible = new(
        id: "RCCMD032",
        title: "The route parameter is not compatible with the edited entity id",
        messageFormat: "The route parameter '{0}' is not compatible with the edited entity id: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingBindingSources = new(
        id: "RCCMD033",
        title: "The parameter declares more than one binding source",
        messageFormat: "The parameter '{0}' declares more than one binding source attribute; use a single binding source",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AsParametersNotSupported = new(
        id: "RCCMD034",
        title: "AsParameters is not supported on external parameters",
        messageFormat: "The AsParametersAttribute is not supported on the parameter '{0}'; bind each value individually",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RouteParameterNotInTemplate = new(
        id: "RCCMD035",
        title: "The bound route parameter does not exist in the endpoint route",
        messageFormat: "The route parameter '{0}' bound by the parameter '{1}' does not exist in the endpoint route template",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ImplicitBodyNotAllowed = new(
        id: "RCCMD036",
        title: "GET/DELETE endpoints cannot infer a request body",
        messageFormat: "The command '{0}' is mapped to {1} and has body properties; GET/DELETE endpoints cannot infer a request body",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleBodySources = new(
        id: "RCCMD037",
        title: "The endpoint has more than one request body source",
        messageFormat: "The parameter '{0}' binds the request body, which conflicts with another body source on the endpoint (the command body or another FromBody/FromForm parameter)",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly IReadOnlyDictionary<string, DiagnosticDescriptor> Descriptors =
        new[]
        {
            InvalidCommandType,
            InvalidReturnType,
            HasProblemsMethodNotFound,
            HasProblemsMethodDoesNotReturnBool,
            HasProblemsMethodDoesNotHaveOutParameterProblems,
            EntityTypeParameterDoesNotHaveIdProperty,
            ProduceNewEntityRequiresWithUnitOfWork,
            ProduceNewEntityMustReturnResultWithValue,
            CancellationTokenParameterMustBeAsync,
            EditEntityRequiresWithUnitOfWork,
            EditEntityRequiresFirstParameter,
            ParameterCannotBeMarkedWithParameter,
            MultiplesMapApiHandlers,
            InvalidMapApiHandlers,
            IdNotFoundInReturnedCommand,
            ReturnedCommandTypeNotFound,
            PropertyNotFoundInReturnedCommand,
            InvalidFindType,
            WithDbContextCannotBeUsedWithWithUnitOfWork,
            WithWorkContextCannotBeUsedWithWithUnitOfWork,
            WithWorkContextCannotBeUsedWithWithDbContext,
            InvalidMapFindUsage,
            InvalidMapSearchUsage,
            InvalidWithFilterUsage,
            RetryOnConcurrencyRequiresWorkContext,
            RetryOnConcurrencyInvalidMaxAttempts,
            MultipleCommandMethods,
            ConflictingMapAttributes,
            InvalidMapArguments,
            ReservedIdentifier,
            DuplicateEndpointName,
            EditEntityRouteParameterNotResolved,
            EditEntityRouteParameterIncompatible,
            ConflictingBindingSources,
            AsParametersNotSupported,
            RouteParameterNotInTemplate,
            ImplicitBodyNotAllowed,
            MultipleBodySources,
        }
        .ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);

    /// <summary>
    /// The canonical catalog accessor: maps a diagnostic id back to its registered
    /// <see cref="DiagnosticDescriptor"/>. Throws for an id that is not in the single catalog, so an id can
    /// never reach the output without a descriptor (which would surface as a generator failure).
    /// </summary>
    internal static DiagnosticDescriptor Get(string id) =>
        Descriptors.TryGetValue(id, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown SmartCommands diagnostic id.");

    /// <summary>All diagnostic ids registered in the single catalog.</summary>
    internal static IReadOnlyCollection<string> CatalogIds => (IReadOnlyCollection<string>)Descriptors.Keys;
}
