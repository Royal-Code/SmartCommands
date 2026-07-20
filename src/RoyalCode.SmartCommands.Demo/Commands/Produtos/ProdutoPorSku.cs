using RoyalCode.SmartCommands.Demo.Domain;
using RoyalCode.SmartSelector;

namespace RoyalCode.SmartCommands.Demo.Commands.Produtos;

#nullable disable // poco

/// <summary>
/// Vitrine da Fase 5 (DF10): <c>MapFindBy</c> busca o <see cref="Produto"/> por uma chave alternativa (o SKU)
/// e projeta no DTO. Ao contrário do <c>MapFind</c> (por ID), a busca usa uma propriedade nomeada; o
/// placeholder <c>{sku}</c> casa (sem diferenciar caixa) com <c>nameof(Produto.Sku)</c>. Quando não há
/// correspondência, o <c>404</c> nomeia a entidade e cita o critério da busca.
///
/// <para>AutoSelect/AutoProperties geram a projeção (Produto -&gt; ProdutoPorSku) no provider.</para>
/// </summary>
[MapGroup("produtos")]
[MapFindBy<Produto>("por-sku/{sku}", "produto-por-sku", nameof(Produto.Sku))]
[WithDescription("Get product details by its SKU (alternate key)")]
[WithTags("Produtos")]
[AutoSelect<Produto>, AutoProperties]
public partial class ProdutoPorSku
{
}
