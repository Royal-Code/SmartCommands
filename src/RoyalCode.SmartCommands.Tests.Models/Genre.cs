using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Models;

public class Genre : Entity<int>
{
    public Genre(string name)
    {
        Name = name;
        Movies = [];
    }

    public string Name { get; set; }

    // Navegação para filmes
    public virtual ICollection<Movie> Movies { get; set; }
}