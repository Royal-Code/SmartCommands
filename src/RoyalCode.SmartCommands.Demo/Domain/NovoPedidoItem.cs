namespace RoyalCode.SmartCommands.Demo.Domain;

public sealed record NovoPedidoItem(
	Guid ProdutoId,
	string ProdutoNome,
	string ProdutoSku,
	int Quantidade,
	decimal PrecoUnitario);
