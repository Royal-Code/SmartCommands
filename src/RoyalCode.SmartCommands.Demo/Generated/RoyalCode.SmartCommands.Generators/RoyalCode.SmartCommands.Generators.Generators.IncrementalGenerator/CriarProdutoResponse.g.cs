
namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

public partial class CriarProdutoResponse(System.Guid id, string nome)
{
    public System.Guid Id { get; } = id;

    public string Nome { get; } = nome;
}
