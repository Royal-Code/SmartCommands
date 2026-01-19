using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Demo.Seguranca.Domain;

/// <summary>
/// Tipo de bloqueio que pode ser aplicado a usuários.
/// </summary>
public class TipoBloqueio : Entity<Guid>
{
    // Construtores
    /// <summary>
    /// Cria um tipo de bloqueio com nome e descrição.
    /// </summary>
    public TipoBloqueio(string nome, string descricao)
    {
        Id = Guid.CreateVersion7();
        Nome = nome?.Trim() ?? string.Empty;
        Descricao = descricao?.Trim() ?? string.Empty;
        Ativo = true;
    }

#nullable disable
    /// <summary>
    /// Construtor protegido sem parâmetros para deserialização.
    /// </summary>
    protected TipoBloqueio() { }
#nullable enable

    // Propriedades
    /// <summary>
    /// Nome do tipo de bloqueio.
    /// </summary>
    public string Nome { get; private set; }

    /// <summary>
    /// Descrição do tipo de bloqueio.
    /// </summary>
    public string Descricao { get; private set; }

    /// <summary>
    /// Se o tipo de bloqueio está ativo.
    /// </summary>
    public bool Ativo { get; private set; }
}
