using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Models;

public class Review : Entity<int>
{
    public Review(
        Movie movie,
        string content,
        int rating,
        User author)
    {
        Movie = movie;
        Content = content;
        Rating = rating;
        Author = author;
        CreatedAt = DateTime.UtcNow;
        Comments = new List<Comment>();
    }

    public virtual Movie Movie { get; set; }
    public string Content { get; set; }
    public int Rating { get; set; } // Nota entre 1 e 5, por exemplo
    public virtual User Author { get; set; } // Resenha feita por um usuário
    public DateTime CreatedAt { get; set; }
    public virtual ICollection<Comment> Comments { get; set; }
}
