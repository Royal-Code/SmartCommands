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
        if (Errors is null)
            return other.Errors is null;

        if (other.Errors is null)
            return false;

        return Errors.SequenceEqual(other.Errors);
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
        if (Errors is null)
            return other.Errors is null;

        if (other.Errors is null)
            return false;

        return Errors.SequenceEqual(other.Errors);
    }
}
