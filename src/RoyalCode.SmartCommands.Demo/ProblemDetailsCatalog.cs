using System.Net;
using RoyalCode.SmartProblems.Descriptions;

namespace RoyalCode.SmartCommands.Demo;

/// <summary>
/// <para>
///     Catalogo de <see cref="ProblemDetailsDescription"/> (RFC 9457) para os <c>typeId</c> customizados usados
///     pelo dominio da demo. As categorias (<c>InvalidState</c>, <c>InvalidParameter</c>, ...) ja definem o
///     status HTTP de cada resposta; o catalogo enriquece a conversao para <c>ProblemDetails</c> com um
///     <c>title</c>/<c>description</c> estaveis por tipo e alimenta a pagina de documentacao publicada em
///     <c>/.problems</c> (ver <see cref="ProgramExtensions"/>).
/// </para>
/// <para>
///     O <c>status</c> aqui e apenas documental (reflete o status real emitido pela categoria do <c>Problem</c>);
///     ele nao altera o status HTTP de uma resposta que ja usa categoria padrao.
/// </para>
/// </summary>
internal static class ProblemDetailsCatalog
{
    public static void Configure(RoyalCode.SmartProblems.Descriptions.ProblemDetailsOptions options)
    {
        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.produto.sku_duplicado",
            title: "SKU duplicado",
            description: "Ja existe um produto cadastrado com o SKU informado.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.produto.ja_inativo",
            title: "Produto ja inativo",
            description: "O produto informado ja esta desativado; desativar novamente e um estado invalido.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.produto.ja_ativo",
            title: "Produto ja ativo",
            description: "O produto informado ja esta ativo; reativar novamente e um estado invalido.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.concurrency_conflict",
            title: "Conflito de concorrencia no produto",
            description: "O produto foi alterado por outro processo enquanto a operacao estava em andamento; a retentativa configurada se esgotou.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.estoque.produto_obrigatorio",
            title: "Produto obrigatorio para registrar estoque",
            description: "Nao e possivel registrar o estoque inicial sem um produto valido.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.estoque.ja_registrado",
            title: "Estoque ja registrado",
            description: "O estoque inicial ja foi registrado para este produto.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.estoque.quantidade_invalida",
            title: "Quantidade de estoque invalida",
            description: "A quantidade informada para a operacao de estoque deve ser maior que zero.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.estoque.insuficiente",
            title: "Estoque insuficiente",
            description: "A quantidade disponivel em estoque e menor que a quantidade solicitada para reserva.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.estoque.reserva_insuficiente",
            title: "Reserva insuficiente",
            description: "A quantidade reservada em estoque e menor que a quantidade solicitada para liberacao.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.estoque.concurrency_conflict",
            title: "Conflito de concorrencia no estoque",
            description: "O estoque foi alterado por outro processo enquanto a operacao estava em andamento; a retentativa configurada se esgotou.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.sem_itens",
            title: "Pedido sem itens",
            description: "O pedido deve possuir ao menos um item.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.quantidade_invalida",
            title: "Quantidade de item do pedido invalida",
            description: "Todos os itens do pedido devem possuir quantidade maior que zero.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.produto_nao_encontrado",
            title: "Produto do pedido nao encontrado",
            description: "Um dos produtos informados nos itens do pedido nao existe no catalogo.",
            status: HttpStatusCode.BadRequest));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.produto_inativo",
            title: "Produto do pedido inativo",
            description: "Um dos produtos informados nos itens do pedido esta desativado no catalogo.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.estoque_nao_registrado",
            title: "Estoque do pedido nao registrado",
            description: "Um dos produtos informados nos itens do pedido ainda nao possui estoque inicial registrado.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.estoque_nao_encontrado",
            title: "Estoque do pedido nao encontrado",
            description: "O estoque de um dos itens do pedido nao foi encontrado ao tentar liberar a reserva no cancelamento.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.ja_cancelado",
            title: "Pedido ja cancelado",
            description: "O pedido informado ja esta cancelado; cancelar novamente e um estado invalido.",
            status: HttpStatusCode.Conflict));

        options.Descriptor.Add(new ProblemDetailsDescription(
            typeId: "demo.pedido.concurrency_conflict",
            title: "Conflito de concorrencia no pedido",
            description: "O pedido ou o estoque relacionado foi alterado por outro processo enquanto a operacao estava em andamento; a retentativa configurada se esgotou.",
            status: HttpStatusCode.Conflict));
    }
}
