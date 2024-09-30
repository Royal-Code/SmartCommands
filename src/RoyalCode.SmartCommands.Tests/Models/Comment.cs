using RoyalCode.Entities;

namespace Coreum.NewCommands.Tests.Models;

public class Comment : Entity<int>
{
    public Comment(
        string content,
        User author,
        Movie? movie = null,
        Review? review = null)
    {
        Content = content;
        Author = author;
        Movie = movie;
        Review = review;
        CreatedAt = DateTime.UtcNow;
    }

    public string Content { get; set; }
    public virtual User Author { get; set; } // Comentário feito por um usuário
    public DateTime CreatedAt { get; set; }

    public virtual Movie? Movie { get; set; } // Comentário relacionado a um filme
    public virtual Review? Review { get; set; } // Ou comentário relacionado a uma resenha
}
