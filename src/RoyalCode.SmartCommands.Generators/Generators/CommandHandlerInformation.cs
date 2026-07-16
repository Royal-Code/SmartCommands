using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;
using RoyalCode.SmartCommands.Generators.Models;

namespace RoyalCode.SmartCommands.Generators.Generators;


internal sealed class CommandHandlerInformation : TransformationGeneratorBase, IEquatable<CommandHandlerInformation>
{
    private readonly bool canGenerate;

    public CommandHandlerInformation()
    {
        canGenerate = true;
    }

    public CommandHandlerInformation(DiagnosticInfo diagnostic)
    {
        canGenerate = false;
        AddError(diagnostic);
    }


#nullable disable

    public TypeDescriptor ModelType { get; internal set; }
    public string Namespace => ModelType.Namespaces[0];
    public bool HasWithValidateModel { get; internal set; }
    public bool HasWithDecorators { get; internal set; }
    public string MethodName { get; internal set; }
    public bool MethodIsAsync { get; internal set; }
    public bool HandlerMustBeAsync { get; internal set; }
    public TypeDescriptor MethodReturnType { get; internal set; }
    public TypeDescriptor HandlerReturnType { get; internal set; }
    public ReturnModel ReturnModel { get; internal set; }
    public List<ParameterDescriptor> Parameters { get; internal set; }
    public string HandlerInterfaceName { get; internal set; }
    public string HandlerImplementationName { get; internal set; }
    public List<string> NotNullProperties { get; internal set; }
    public bool HasWithUnitOfWork { get; internal set; }
    public bool HasWithFindEntities { get; internal set; }
    public List<IdPropertyBoundToEntityParameter> IdPropertiesBindings { get; internal set; }
    public List<string> ProduceProblems { get; internal set; }

#nullable enable

    /// <summary>
    /// Atributos de binding capturados dos parâmetros <c>[WithParameter]</c> (DF3), por nome de parâmetro;
    /// aplicados somente ao delegate Minimal API, nunca à interface do handler.
    /// </summary>
    public Dictionary<string, RoyalCode.Extensions.SourceGenerator.Collections.EquatableArray<ParameterBindingModel>>? ParameterBindings { get; internal set; }

    /// <summary>Validações adicionais do comando (DF13), já ordenadas por <c>Order</c> + desempate determinístico.</summary>
    public List<CommandValidationInformation> Validators { get; internal set; } = [];

    public TypeDescriptor? ContextAccessorType { get; internal set; }
    public ContextAccessorModes ContextAccessorMode { get; internal set; }
    public TypeDescriptor? ProduceNewEntityType { get; internal set; }
    public EditTypeDescriptor? EditType { get; internal set; }
    public MapInformation? MapInformation { get; internal set; }

    /// <summary>
    /// Whether the command body must be wrapped in an optimistic-concurrency retry loop (opt-in via attribute).
    /// </summary>
    public bool HasRetryOnConcurrency { get; internal set; }

    /// <summary>
    /// The explicit maximum number of attempts taken from the attribute argument, or <c>null</c> to read it from the configured options.
    /// </summary>
    public int? RetryMaxAttempts { get; internal set; }

    /// <summary>
    /// Whether the command has a request-body shape (public settable properties or public constructor parameters).
    /// When <c>false</c>, the generated endpoint does not receive the command as a parameter and instantiates it via <c>new</c>.
    /// </summary>
    public bool HasBodyProperties { get; internal set; }

    /// <summary>
    /// The semantic operation key used to create a custom problem when the retry budget is exhausted.
    /// </summary>
    public string? RetryOperation { get; internal set; }

    protected override void Generate(SourceProductionContext spc, bool hasErrors)
    {
        // os erros já foram reportados pela base (Generate(spc)); aqui apenas bloqueia a emissão.
        if (!canGenerate || hasErrors)
            return;

        // cria interface do handler
        var interfaceGenerator = CommandHandlerGenerator.GenerateInterface(this);
        // obtém o método handler da interface
        var interfaceHandlerMethod = interfaceGenerator.Methods.Enumerate<MethodGenerator>().First();

        // cria classe que implementa o handler
        var implementationGenerator = CommandHandlerGenerator.GenerateImplementation(this, interfaceHandlerMethod);

        // cria classe partial do model se possível.
        var partialModelGenerator = CommandHandlerGenerator.GenerateWasValidated(this);

        GeneratedFileHeader.AddTo(interfaceGenerator);
        GeneratedFileHeader.AddTo(implementationGenerator);
        if (partialModelGenerator is not null)
            GeneratedFileHeader.AddTo(partialModelGenerator, suppressMemberNotNullWarning: true);

        // gera o código fonte
        interfaceGenerator.Generate(spc);
        implementationGenerator.Generate(spc);
        partialModelGenerator?.Generate(spc);
    }

    public void SetErrors(List<DiagnosticInfo>? diagnostics)
    {
        if (diagnostics is not null && diagnostics.Count > 0)
            Errors = diagnostics;
    }

    public bool Equals(CommandHandlerInformation? other)
    {
        return other is not null &&
               Equals(ModelType, other.ModelType) &&
               HasWithValidateModel == other.HasWithValidateModel &&
               HasWithDecorators == other.HasWithDecorators &&
               MethodName == other.MethodName &&
               MethodIsAsync == other.MethodIsAsync &&
               Equals(MethodReturnType, other.MethodReturnType) &&
               Equals(HandlerReturnType, other.HandlerReturnType) &&
               Parameters.SequenceEqual(other.Parameters) &&
               HandlerInterfaceName == other.HandlerInterfaceName &&
               HandlerImplementationName == other.HandlerImplementationName &&
               HandlerMustBeAsync == other.HandlerMustBeAsync &&
               NotNullProperties.SequenceEqual(other.NotNullProperties) &&
               HasWithUnitOfWork == other.HasWithUnitOfWork &&
               HasWithFindEntities == other.HasWithFindEntities &&
               Equals(ContextAccessorType, other.ContextAccessorType) &&
               ContextAccessorMode == other.ContextAccessorMode &&
               IdPropertiesBindings.SequenceEqual(other.IdPropertiesBindings) &&
               ProduceProblems.SequenceEqual(other.ProduceProblems) &&
               Equals(ProduceNewEntityType, other.ProduceNewEntityType) &&
               Equals(EditType, other.EditType) &&
               Equals(MapInformation, other.MapInformation) &&
               HasRetryOnConcurrency == other.HasRetryOnConcurrency &&
               RetryMaxAttempts == other.RetryMaxAttempts &&
               RetryOperation == other.RetryOperation &&
               HasBodyProperties == other.HasBodyProperties &&
               EqualErrors(other);
    }

    public override bool Equals(object? obj)
    {
        return obj is CommandHandlerInformation other && Equals(other);
    }

    public override int GetHashCode()
    {
        int hashCode = -737078483;
        hashCode = hashCode * -1521134295 + ModelType.GetHashCode();
        hashCode = hashCode * -1521134295 + HasWithValidateModel.GetHashCode();
        hashCode = hashCode * -1521134295 + HasWithDecorators.GetHashCode();
        hashCode = hashCode * -1521134295 + MethodName.GetHashCode();
        hashCode = hashCode * -1521134295 + MethodIsAsync.GetHashCode();
        hashCode = hashCode * -1521134295 + MethodReturnType.GetHashCode();
        hashCode = hashCode * -1521134295 + HandlerReturnType.GetHashCode();
        hashCode = hashCode * -1521134295 + Parameters.GetHashCode();
        hashCode = hashCode * -1521134295 + HandlerInterfaceName.GetHashCode();
        hashCode = hashCode * -1521134295 + HandlerImplementationName.GetHashCode();
        hashCode = hashCode * -1521134295 + HandlerMustBeAsync.GetHashCode();
        hashCode = hashCode * -1521134295 + NotNullProperties.GetHashCode();
        hashCode = hashCode * -1521134295 + IdPropertiesBindings.GetHashCode();
        hashCode = hashCode * -1521134295 + ProduceProblems.GetHashCode();
        hashCode = hashCode * -1521134295 + HasWithUnitOfWork.GetHashCode();
        hashCode = hashCode * -1521134295 + HasWithFindEntities.GetHashCode();
        hashCode = hashCode * -1521134295 + ContextAccessorType?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + ContextAccessorMode.GetHashCode();
        hashCode = hashCode * -1521134295 + ProduceNewEntityType?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + EditType?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + MapInformation?.GetHashCode() ?? 0;
        hashCode = hashCode * -1521134295 + HasRetryOnConcurrency.GetHashCode();
        hashCode = hashCode * -1521134295 + RetryMaxAttempts.GetHashCode();
        hashCode = hashCode * -1521134295 + (RetryOperation?.GetHashCode() ?? 0);
        hashCode = hashCode * -1521134295 + HasBodyProperties.GetHashCode();
        hashCode = hashCode * -1521134295 + Errors?.GetHashCode() ?? 0;
        return hashCode;
    }

    internal enum ContextAccessorModes
    {
        None,
        Specified,
        DbContext,
        WorkContext
    }
}
