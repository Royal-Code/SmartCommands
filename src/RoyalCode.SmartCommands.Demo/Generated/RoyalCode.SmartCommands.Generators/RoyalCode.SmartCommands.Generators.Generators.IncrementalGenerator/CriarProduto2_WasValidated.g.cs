
#nullable disable
#pragma warning disable

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

public partial class CriarProduto2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Nome))]
    internal protected void WasValidated() { }
}
