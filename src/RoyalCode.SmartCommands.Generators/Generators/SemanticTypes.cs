using Microsoft.CodeAnalysis;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// <para>
///     Criação de <see cref="TypeDescriptor"/> a partir de símbolos para emissão: o nome é renderizado no
///     formato mínimo do C# (keywords como <c>string</c>/<c>int</c>, genéricos com nomes curtos, anotação de
///     nulabilidade preservada), garantindo que aliases e nomes qualificados no código do usuário produzam o
///     mesmo texto emitido que a forma simples.
/// </para>
/// </summary>
internal static class SemanticTypes
{
    private static readonly SymbolDisplayFormat NameFormat =
        SymbolDisplayFormat.MinimallyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    internal static TypeDescriptor CreateDescriptor(ITypeSymbol symbol)
    {
        var name = symbol.ToDisplayString(NameFormat);
        var isNullable = symbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

        return new TypeDescriptor(
            name,
            symbol.GetNamespaces().ToArray(),
            symbol,
            isNullable,
            symbol.NullableAnnotation);
    }
}
