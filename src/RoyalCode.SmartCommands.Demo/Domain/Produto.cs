using RoyalCode.Entities;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Domain;

/// <summary>
/// Agregado de catalogo da demo. Ente proprio da demo (nao reutiliza o Produto de Tests.Models).
/// </summary>
public class Produto : Entity<Guid>
{
    public Produto(string nome, string sku, decimal preco)
    {
        // Invariantes do agregado (defesa em profundidade / fail-fast). A validacao amigavel de entrada
        // (400/409) continua na feature via HasProblems; estas guardas so disparam se um comando futuro
        // tentar criar um Produto invalido sem passar pela validacao.
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        if (preco <= 0)
            throw new ArgumentOutOfRangeException(nameof(preco), preco, "O preco deve ser maior que zero.");

        Nome = nome;
        Sku = sku;
        Preco = preco;
        Ativo = true;
        CriadoEm = DateTimeOffset.UtcNow;
    }

#nullable disable
    /// <summary>
    /// Construtor de materializacao do Entity Framework.
    /// </summary>
    protected Produto() { }
#nullable enable

    public string Nome { get; private set; }

    public string Sku { get; private set; }

    public decimal Preco { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>
    /// Navegacao de leitura para o estoque do produto (relacao 1:1, dependente <see cref="ProdutoEstoque"/>).
    /// Existe para o lado de consulta (busca por disponibilidade em <c>ProdutoFiltro</c> via caminho aninhado
    /// <c>Estoque.Disponivel</c>); nao participa das invariantes de escrita do catalogo, que seguem sem conhecer
    /// o estoque.
    /// </summary>
    public ProdutoEstoque? Estoque { get; private set; }

    /// <summary>
    /// Edita nome e preco preservando o SKU (identidade do produto no catalogo).
    /// </summary>
    public void Editar(string nome, decimal preco)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (preco <= 0)
            throw new ArgumentOutOfRangeException(nameof(preco), preco, "O preco deve ser maior que zero.");

        Nome = nome;
        Preco = preco;
    }

    /// <summary>
    /// Desativa o produto (soft delete). Transicao nao idempotente: desativar um produto ja inativo e
    /// tratado como estado invalido (409), conforme decisao da Fase 5.
    /// </summary>
    public Result Desativar()
    {
        if (!Ativo)
            return Problems.InvalidState(
                "O produto ja esta inativo.",
                typeId: "demo.produto.ja_inativo");

        Ativo = false;
        return Result.Ok();
    }

    /// <summary>
    /// Reativa o produto. Transicao nao idempotente: reativar um produto ja ativo e estado invalido (409).
    /// </summary>
    public Result Reativar()
    {
        if (Ativo)
            return Problems.InvalidState(
                "O produto ja esta ativo.",
                typeId: "demo.produto.ja_ativo");

        Ativo = true;
        return Result.Ok();
    }

    public override string ToString() => $"{Sku} - {Nome}";
}
