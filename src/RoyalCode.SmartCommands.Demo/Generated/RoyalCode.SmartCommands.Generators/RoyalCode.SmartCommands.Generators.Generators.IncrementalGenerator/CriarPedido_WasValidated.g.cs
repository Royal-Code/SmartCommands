
#nullable disable
#pragma warning disable

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

public partial class CriarPedido
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Itens))]
    internal protected void WasValidated() { }
}
