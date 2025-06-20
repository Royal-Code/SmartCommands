
#nullable disable
#pragma warning disable

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace RoyalCode.SmartCommands.Demo.Commands.Lojas;

public partial class CriarLoja
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Nome), nameof(Endereco))]
    internal protected void WasValidated() { }
}
