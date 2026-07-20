using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartSelector;

namespace RoyalCode.SmartCommands.Demo.Commands.Pedidos;

#nullable disable // poco

/// <summary>
/// Vitrine da Fase 5 (DF10): <c>MapFindBy</c> com chave composta — busca o <see cref="PedidoItem"/> pela
/// combinação (<c>PedidoId</c>, <c>ProdutoSku</c>), projetando no DTO. Os dois placeholders da rota casam
/// (sem diferenciar caixa) com as propriedades declaradas, e a busca combina os critérios com <c>AND</c>,
/// na ordem declarada. O <c>404</c> nomeia a entidade e lista os dois critérios.
///
/// <para>AutoSelect/AutoProperties geram a projeção (PedidoItem -&gt; ItemPedidoPorSku) no provider.</para>
/// </summary>
[MapGroup("pedidos")]
[MapFindBy<PedidoItem>("{pedidoId:guid}/itens/{produtoSku}", "item-do-pedido-por-sku",
    nameof(PedidoItem.PedidoId), nameof(PedidoItem.ProdutoSku))]
[WithDescription("Get an order item by order id and product SKU (composite key)")]
[WithTags("Pedidos")]
[AutoSelect<PedidoItem>, AutoProperties]
public partial class ItemPedidoPorSku
{
}
