using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartSelector;

namespace RoyalCode.SmartCommands.Demo.Commands.Movies;

#nullable disable // POCO 

[MapGroup("movies")]
[MapFind("{id:int}", "Get review details"), EntityReference<Review, int>]
[AutoSelect<Review>]
public partial class ReviewDetails
{
    public int Id { get; set; }
    public string Content { get; set; }
    public int Rating { get; set; }
    public string AuthorUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}