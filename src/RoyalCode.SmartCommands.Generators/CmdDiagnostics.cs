using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Generators;

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
        title: "The ProduceNewEntityAttribte requires WithUnitOfWorkAttribute",
        messageFormat: "The ProduceNewEntityAttribte requires WithUnitOfWorkAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ProduceNewEntityMustReturnResultWithValue = new(
        id: "RCCMD007",
        title: "The ProduceNewEntityAttribte must return a Result with value",
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
        title: "The EditEntityAttribte requires WithUnitOfWorkAttribute",
        messageFormat: "The EditEntityAttribte requires WithUnitOfWorkAttribute",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EditEntityRequiresFirstParameter = new(
        id: "RCCMD010",
        title: "When EditEntityAttribte is used, the first parameter must be of the same type as the entity entered in the attribute",
        messageFormat: "When EditEntityAttribte is used, the first parameter must be of the same type as the entity entered in the attribute",
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
}
