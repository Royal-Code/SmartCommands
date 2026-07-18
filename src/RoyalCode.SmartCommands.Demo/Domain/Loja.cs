using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Demo.Domain;

/// <summary>
/// Ente proprio da demo (nao reutiliza o Loja de Tests.Models).
/// </summary>
public class Loja : Entity<int>
{
    public Loja(string nome, string endereco)
    {
        Nome = nome;
        Endereco = endereco;
    }

#nullable disable
    /// <summary>
    /// Construtor de materializacao do Entity Framework.
    /// </summary>
    protected Loja() { }
#nullable enable

    public string Nome { get; private set; }

    public string Endereco { get; private set; }

    /// <summary>
    /// Exclusao logica: o DELETE da API desativa a loja em vez de remover o registro.
    /// </summary>
    public bool Ativa { get; private set; } = true;

    public void Desativar() => Ativa = false;

    public void Renomear(string novoNome) => Nome = novoNome;

    public override string ToString() => $"{Nome} - {Endereco}";
}
