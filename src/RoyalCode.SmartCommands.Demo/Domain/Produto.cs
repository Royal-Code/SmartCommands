using RoyalCode.Entities;
using RoyalCode.SmartProblems;

namespace RoyalCode.SmartCommands.Demo.Domain;

/// <summary>
/// <para>
///     Agregado de catalogo da demo. Ente proprio da demo (nao reutiliza o Produto de Tests.Models).
/// </para>
/// <para>
///     Implementa <see cref="IActiveState"/> e <see cref="IHasCode{TCode}"/> de <c>RoyalCode.Entities</c> de forma
///     explicita (sem herdar <c>Entity&lt;Guid, string&gt;</c>): o vocabulario publico do dominio (<see cref="Ativo"/>,
///     <see cref="Sku"/>) e mantido intacto para nao quebrar DTOs/filtros/testes que ja usam esses nomes, enquanto os
///     contratos das libs ficam disponiveis para codigo generico que dependa deles (ex.: <c>IActiveState.IsActive</c>).
/// </para>
/// </summary>
public class Produto : Entity<Guid>, IActiveState, IHasCode<string>
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
        Version = 1;
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
    /// Token de concorrencia otimista (ver <c>WithRetryOnConcurrency</c> nos comandos que editam este agregado).
    /// Incrementado a cada mutacao de estado por <see cref="Touch"/>.
    /// </summary>
    public int Version { get; private set; }

    /// <inheritdoc cref="IActiveState.IsActive"/>
    bool IActiveState.IsActive => Ativo;

    /// <inheritdoc cref="IHasCode{TCode}.Code"/>
    string IHasCode<string>.Code => Sku;

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
        Touch();
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
        Touch();
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
        Touch();
        return Result.Ok();
    }

    private void Touch() => Version++;

    public override string ToString() => $"{Sku} - {Nome}";
}
