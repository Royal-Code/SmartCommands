using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Diagnostics;

namespace RoyalCode.SmartCommands.Generators.Generators;

internal abstract class TransformationGeneratorBase : ITransformationGenerator
{
    protected List<DiagnosticInfo>? Errors { get; set; }

    internal IReadOnlyList<DiagnosticInfo> Diagnostics => Errors ?? (IReadOnlyList<DiagnosticInfo>)Array.Empty<DiagnosticInfo>();

    public void Generate(SourceProductionContext spc)
    {
        bool hasErrors = Errors is not null && Errors.Count > 0;
        if (hasErrors)
            Errors!.ForEach(error => spc.ReportDiagnostic(error.ToDiagnostic(CmdDiagnostics.Get)));

        Generate(spc, hasErrors);
    }

    protected abstract void Generate(SourceProductionContext spc, bool hasErrors);

    protected void AddError(DiagnosticInfo error)
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

internal abstract class TransformationGeneratorBase<TModel> : ITransformationGenerator<TModel>
{
    protected List<DiagnosticInfo>? Errors { get; set; }

    internal IReadOnlyList<DiagnosticInfo> Diagnostics => Errors ?? (IReadOnlyList<DiagnosticInfo>)Array.Empty<DiagnosticInfo>();

    public void Generate(SourceProductionContext spc, IEnumerable<TModel> models)
    {
        bool hasErrors = Errors is not null && Errors.Count > 0;
        if (hasErrors)
            Errors!.ForEach(error => spc.ReportDiagnostic(error.ToDiagnostic(CmdDiagnostics.Get)));

        Generate(spc, models, hasErrors);
    }

    protected abstract void Generate(SourceProductionContext spc, IEnumerable<TModel> models, bool hasErrors);

    protected void AddError(DiagnosticInfo error)
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
