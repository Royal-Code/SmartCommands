using RoyalCode.SmartCommands.Demo.Domain;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

public sealed record ProdutoEstoqueDetalhes(Guid ProdutoId, int Disponivel, int Reservado, int Version)
{
	public static ProdutoEstoqueDetalhes Empty(Guid produtoId) => new(produtoId, 0, 0, 0);

	public static ProdutoEstoqueDetalhes From(ProdutoEstoque estoque)
	{
		return new ProdutoEstoqueDetalhes(estoque.ProdutoId, estoque.Disponivel, estoque.Reservado, estoque.Version);
	}
}
