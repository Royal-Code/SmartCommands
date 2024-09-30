using RoyalCode.Entities;

namespace Coreum.NewCommands.Tests.Models;

public class Director : Entity<int>
{
    public Director(string name)
    {
        Name = name;
        Movies = [];
    }

    public Director(string name, string? biography, DateTime? birthdate, string? photo)
    {
        Name = name;
        Biography = biography;
        Birthdate = birthdate;
        Photo = photo;
        Movies = [];
    }

    public string Name { get; set; }
    public string? Biography { get; set; }
    public DateTime? Birthdate { get; set; }
    public string? Photo { get; set; }

    // Navegação para filmes
    public virtual ICollection<Movie> Movies { get; set; }
}
