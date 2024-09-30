using RoyalCode.Entities;

namespace Coreum.NewCommands.Tests.Models;

public class User : Entity<int>
{
    public string Username { get; set; }
    public string Email { get; set; }
    public virtual ICollection<Review> Reviews { get; set; }
    public virtual ICollection<Comment> Comments { get; set; }
}
