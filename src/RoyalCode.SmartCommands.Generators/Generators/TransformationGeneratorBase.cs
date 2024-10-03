using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

public abstract class TransformationGeneratorBase : ITransformationGenerator
{
    protected List<Diagnostic>? Errors { get; set; }

    public void Generate(SourceProductionContext spc)
    {
        bool hasErrors = Errors is not null && Errors.Count > 0;
        if (hasErrors)
            Errors!.ForEach(spc.ReportDiagnostic);

        Generate(spc, hasErrors);
    }

    protected abstract void Generate(SourceProductionContext spc, bool hasErrors);

    protected void AddError(Diagnostic error)
    {
        Errors ??= [];
        Errors.Add(error);
    }

    protected bool EqualErrors(TransformationGeneratorBase other)
    {
        return Errors?.SequenceEqual(other.Errors) ?? other is null;
    }
}

public abstract class TransformationGeneratorBase<TModel> : ITransformationGenerator<TModel>
{
    protected List<Diagnostic>? Errors { get; set; }

    public void Generate(SourceProductionContext spc, IEnumerable<TModel> models)
    {
        bool hasErrors = Errors is not null && Errors.Count > 0;
        if (hasErrors)
            Errors!.ForEach(spc.ReportDiagnostic);

        Generate(spc, models, hasErrors);
    }

    protected abstract void Generate(SourceProductionContext spc, IEnumerable<TModel> models, bool hasErrors);

    protected void AddError(Diagnostic error)
    {
        Errors ??= [];
        Errors.Add(error);
    }

    protected bool EqualErrors(TransformationGeneratorBase<TModel> other)
    {
        return Errors?.SequenceEqual(other.Errors) ?? other is null;
    }
}
