
#nullable disable
#pragma warning disable

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RoyalCode.SmartCommands.Demo.Commands;

public partial class CriarProduto
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Nome))]
    internal protected void WasValidated() { }
}
