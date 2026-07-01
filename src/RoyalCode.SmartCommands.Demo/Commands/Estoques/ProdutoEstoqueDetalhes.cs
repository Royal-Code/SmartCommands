using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartSelector;

namespace RoyalCode.SmartCommands.Demo.Commands.Estoques;

#nullable disable // POCO

[MapGroup("produtos")]
[MapFind("{id:guid}/estoque", "Get product stock details"), EntityReference<ProdutoEstoque, Guid>]
[AutoSelect<ProdutoEstoque>]
public partial class ProdutoEstoqueDetalhes
{
	public Guid ProdutoId { get; set; }

	public int Disponivel { get; set; }

	public int Reservado { get; set; }

	public int Version { get; set; }
}
