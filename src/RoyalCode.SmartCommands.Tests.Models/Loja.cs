using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Models;

public class Loja : Entity<int>
{
    public Loja(string nome, string endereco)
    {
        Nome = nome;
        Endereco = endereco;
    }

#nullable disable
    /// <summary>
    /// Deserialization constructor for Entity Framework.
    /// </summary>
    public Loja() { }
#nullable enable

    public string Nome { get; set; }
    
    public string Endereco { get; set; }

    public virtual ICollection<Produto> Produtos { get; set; } = [];

    public override string ToString()
    {
        return $"{Nome} - {Endereco}";
    }
}
