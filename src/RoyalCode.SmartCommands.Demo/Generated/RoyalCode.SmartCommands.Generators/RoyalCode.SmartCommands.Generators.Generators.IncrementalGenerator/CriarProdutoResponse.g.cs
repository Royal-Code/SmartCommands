
namespace RoyalCode.SmartCommands.Demo.Commands;

public partial class CriarProdutoResponse(System.Guid id, string nome)
{
    System.Guid Id { get; } = id;

    string Nome { get; } = nome;
}
