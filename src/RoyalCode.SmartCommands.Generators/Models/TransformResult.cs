using Microsoft.CodeAnalysis;

namespace Coreum.NewCommands.Generators.Models;

#pragma warning disable S4035 // IEquatable
#pragma warning disable S2328 // GetHashCode

public readonly struct TransformResult<TGenerator> : IEquatable<TransformResult<TGenerator>>
{

    public static implicit operator TransformResult<TGenerator>(TGenerator generator) => new(generator);

    public static implicit operator TransformResult<TGenerator>(Diagnostic diagnostic) => new(diagnostic);

    public TransformResult(TGenerator generator) : this()
    {
        Generator = generator;
    }

    public TransformResult(Diagnostic? diagnostics) : this()
    {
        Diagnostics = diagnostics;
    }

    public readonly TGenerator? Generator { get; }

    public readonly Diagnostic? Diagnostics { get; }

    public bool Equals(TransformResult<TGenerator> other)
    {
        if (Generator is not null)
            return other.Generator is not null 
                   && Generator.Equals(other.Generator);

        if (Diagnostics is null) 
            return other is { Generator: null, Diagnostics: null };
        
        return other.Diagnostics is not null 
               && Diagnostics.Equals(other.Diagnostics);
    }

    public override bool Equals(object? obj)
    {
        return obj is TransformResult<TGenerator> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = Generator?.GetHashCode() ?? 0;
        hash = hash * 31 + (Diagnostics?.GetHashCode() ?? 0);
        return hash;
    }
}
