using RoyalCode.Entities;

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

    public void Desativar() => Ativo = false;

    public void Reativar() => Ativo = true;

    public override string ToString() => $"{Sku} - {Nome}";
}
