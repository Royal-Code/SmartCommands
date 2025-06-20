
namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

public partial class CriarProdutoResponse(Guid id, string nome)
{
    public Guid Id { get; } = id;

    public string Nome { get; } = nome;
}
