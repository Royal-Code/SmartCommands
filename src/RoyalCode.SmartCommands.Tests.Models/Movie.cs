using RoyalCode.Entities;

namespace RoyalCode.SmartCommands.Tests.Models;

public class Movie : Entity<int>
{
    public Movie(
        string title,
        int year,
        int runtime,
        Director director,
        string? plot = null,
        string? posterUri = null)
    {
        Title = title;
        Year = year;
        Runtime = runtime;
        Director = director;
        
        Plot = plot;
        PosterUri = posterUri;

        Genres = [];
        Actors = [];
        Reviews = [];
        Comments = [];
    }

    public string Title { get; set; }
    
    public int Year { get; set; }
    
    public int Runtime { get; set; }

    public string? Plot { get; set; }
    
    public string? PosterUri { get; set; }

    public virtual ICollection<Genre> Genres { get; set; }

    public virtual ICollection<Actor> Actors { get; set; }

    public virtual Director Director { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } // Resenhas associadas ao filme

    public virtual ICollection<Comment> Comments { get; set; } // Comentários diretamente no filme
}
