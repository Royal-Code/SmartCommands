using System.Diagnostics.CodeAnalysis;
using Coreum.NewCommands.Tests.Models;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using RoyalCode.SmartProblems;
using RuleSet = RoyalCode.SmartValidations.RuleSet;

namespace Coreum.NewCommands.Tests.Generators;

public class GenerateFindEntitiesTests
{
    [Theory]
    [InlineData(CreateMovieCode.Command, CreateMovieCode.Interface, CreateMovieCode.Handler)]
    [InlineData(CreateMovieWithGenreCode.Command, CreateMovieWithGenreCode.Interface, CreateMovieWithGenreCode.Handler)]
    [InlineData(CreateMovieFullCode.Command, CreateMovieFullCode.Interface, CreateMovieFullCode.Handler)]
    public void FindEntitiesTests(string commandCode, string interfaceCode, string handlerCode)
    {
        Util.Compile(commandCode, out var output, out var diagnostics);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var generatedInterface = output.SyntaxTrees.Skip(1).FirstOrDefault()?.ToString();
        generatedInterface.Should().Be(interfaceCode);

        var generatedHandler = output.SyntaxTrees.Skip(2).FirstOrDefault()?.ToString();
        generatedHandler.Should().Be(handlerCode);
    }
}

public class CreateMovie
{
    public string? Title { get; set; }

    public int Year { get; set; }

    public int Runtime { get; set; }

    public int DirectorId { get; set; }

    [MemberNotNullWhen(false, nameof(Title))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateMovie>()
            .NotEmpty(Title)
            .NotEmpty(Year)
            .NotEmpty(Runtime)
            .NotEmpty(DirectorId)
            .HasProblems(out problems);
    }

    [Command, WithUnitOfWork<CineDbContext>, WithValidateModel]
    public Movie Execute(CineDbContext db, Director director)
    {
        var movie = new Movie(Title!, Year, Runtime, director);
        db.Movies.Add(movie);
        return movie;
    }
}

public interface ICreateMovieHandler
{
    public Task<Result<Movie>> HandleAsync(CreateMovie command, CancellationToken ct);
}

public class CreateMovieHandler : ICreateMovieHandler
{
    private readonly IUnitOfWorkAccessor<CineDbContext> accessor;

    public CreateMovieHandler(IUnitOfWorkAccessor<CineDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Movie>> HandleAsync(CreateMovie command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var directorEntry = await this.accessor.FindEntityAsync<Director, int>(command.DirectorId, ct);
        if (directorEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var director = directorEntry.Entity;

        var commandResult = command.Execute(this.accessor.Context, director);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class CreateMovieCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.FindEntities;

public class CreateMovie
{
    public string? Title { get; set; }

    public int Year { get; set; }

    public int Runtime { get; set; }

    public int DirectorId { get; set; }

    [MemberNotNullWhen(false, nameof(Title))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateMovie>()
            .NotEmpty(Title)
            .NotEmpty(Year)
            .NotEmpty(Runtime)
            .NotEmpty(DirectorId)
            .HasProblems(out problems);
    }

    [Command, WithUnitOfWork<CineDbContext>, WithValidateModel]
    public Movie Execute(CineDbContext db, Director director)
    {
        var movie = new Movie(Title!, Year, Runtime, director);
        db.Movies.Add(movie);
        return movie;
    }
}
""";

    public const string Interface =
"""
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.FindEntities;

public interface ICreateMovieHandler
{
    public Task<Result<Movie>> HandleAsync(CreateMovie command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.FindEntities;
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.FindEntities.Internals;

public class CreateMovieHandler : ICreateMovieHandler
{
    private readonly IUnitOfWorkAccessor<CineDbContext> accessor;

    public CreateMovieHandler(IUnitOfWorkAccessor<CineDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Movie>> HandleAsync(CreateMovie command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var directorEntry = await this.accessor.FindEntityAsync<Director, int>(command.DirectorId, ct);
        if (directorEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var director = directorEntry.Entity;

        var commandResult = command.Execute(this.accessor.Context, director);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}

public class CreateMovieWithGenre
{
    public string? Title { get; set; }

    public int Year { get; set; }

    public int Runtime { get; set; }

    public int DirectorId { get; set; }

    public int? GenreId { get; set; }

    [MemberNotNullWhen(false, nameof(Title))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateMovie>()
            .NotEmpty(Title)
            .NotEmpty(Year)
            .NotEmpty(Runtime)
            .NotEmpty(DirectorId)
            .HasProblems(out problems);
    }

    [Command, WithUnitOfWork<CineDbContext>, WithValidateModel]
    public Movie Execute(CineDbContext db, Director director, Genre? genre)
    {
        var movie = new Movie(Title!, Year, Runtime, director);

        if (genre is not null)
            movie.Genres.Add(genre);

        db.Movies.Add(movie);
        return movie;
    }
}

public interface ICreateMovieWithGenreHandler
{
    public Task<Result<Movie>> HandleAsync(CreateMovieWithGenre command, CancellationToken ct);
}

public class CreateMovieWithGenreHandler : ICreateMovieWithGenreHandler
{
    private readonly IUnitOfWorkAccessor<CineDbContext> accessor;

    public CreateMovieWithGenreHandler(IUnitOfWorkAccessor<CineDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Movie>> HandleAsync(CreateMovieWithGenre command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var directorEntry = await this.accessor.FindEntityAsync<Director, int>(command.DirectorId, ct);
        if (directorEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var director = directorEntry.Entity;

        Genre? genre = null;
        if (command.GenreId is not null)
        {
            var genreEntry = await this.accessor.FindEntityAsync<Genre, int>(command.GenreId.Value, ct);
            if (genreEntry.NotFound(out notFoundProblems))
                return notFoundProblems;
            genre = genreEntry.Entity;
        }

        var commandResult = command.Execute(this.accessor.Context, director, genre);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class CreateMovieWithGenreCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;

namespace Coreum.NewCommands.Tests.FindEntities;

public class CreateMovieWithGenre
{
    public string? Title { get; set; }

    public int Year { get; set; }

    public int Runtime { get; set; }

    public int DirectorId { get; set; }

    public int? GenreId { get; set; }

    [MemberNotNullWhen(false, nameof(Title))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateMovie>()
            .NotEmpty(Title)
            .NotEmpty(Year)
            .NotEmpty(Runtime)
            .NotEmpty(DirectorId)
            .HasProblems(out problems);
    }

    [Command, WithUnitOfWork<CineDbContext>, WithValidateModel]
    public Movie Execute(CineDbContext db, Director director, Genre? genre)
    {
        var movie = new Movie(Title!, Year, Runtime, director);

        if (genre is not null)
            movie.Genres.Add(genre);

        db.Movies.Add(movie);
        return movie;
    }
}
""";

    public const string Interface =
"""
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.FindEntities;

public interface ICreateMovieWithGenreHandler
{
    public Task<Result<Movie>> HandleAsync(CreateMovieWithGenre command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.FindEntities;
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.FindEntities.Internals;

public class CreateMovieWithGenreHandler : ICreateMovieWithGenreHandler
{
    private readonly IUnitOfWorkAccessor<CineDbContext> accessor;

    public CreateMovieWithGenreHandler(IUnitOfWorkAccessor<CineDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Movie>> HandleAsync(CreateMovieWithGenre command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var directorEntry = await this.accessor.FindEntityAsync<Director, int>(command.DirectorId, ct);
        if (directorEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var director = directorEntry.Entity;

        Genre? genre = null;
        if (command.GenreId is not null)
        {
            var genreEntry = await this.accessor.FindEntityAsync<Genre, int>(command.GenreId.Value, ct);
            if (genreEntry.NotFound(out notFoundProblems))
                return notFoundProblems;
            genre = genreEntry.Entity;
        }

        var commandResult = command.Execute(this.accessor.Context, director, genre);

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}

public class CreateMovieFull
{
    public string? Title { get; set; }

    public int Year { get; set; }

    public int Runtime { get; set; }

    public int DirectorId { get; set; }

    public string? Plot { get; set; }

    public string? PosterUri { get; set; }

    public ICollection<int> Genres { get; set; }

    public ICollection<int> ActorsIds { get; set; }

    [MemberNotNullWhen(false, nameof(Title))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateMovie>()
            .NotEmpty(Title)
            .NotEmpty(Year)
            .NotEmpty(Runtime)
            .NotEmpty(DirectorId)
            .HasProblems(out problems);
    }

    [Command, WithUnitOfWork<CineDbContext>, WithValidateModel]
    public Movie Execute(CineDbContext db, Director director, IEnumerable<Genre> genres, Actor[] actors)
    {
        var movie = new Movie(Title!, Year, Runtime, director, Plot, PosterUri)
        {
            Genres = genres.ToList(),
            Actors = actors.ToList()
        };
        db.Movies.Add(movie);
        return movie;
    }
}

public interface ICreateMovieFullHandler
{
    public Task<Result<Movie>> HandleAsync(CreateMovieFull command, CancellationToken ct);
}

public class CreateMovieFullHandler : ICreateMovieFullHandler
{
    private readonly IUnitOfWorkAccessor<CineDbContext> accessor;

    public CreateMovieFullHandler(IUnitOfWorkAccessor<CineDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Movie>> HandleAsync(CreateMovieFull command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var directorEntry = await this.accessor.FindEntityAsync<Director, int>(command.DirectorId, ct);
        if (directorEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var director = directorEntry.Entity;

        List<Genre> genres = [];
        foreach (var genreId in command.Genres)
        {
            var genreEntry = await this.accessor.FindEntityAsync<Genre, int>(genreId, ct);
            if (genreEntry.NotFound(out notFoundProblems))
                return notFoundProblems;
            genres.Add(genreEntry.Entity);
        }

        List<Actor> actors = [];
        foreach (var actorId in command.ActorsIds)
        {
            var actorEntry = await this.accessor.FindEntityAsync<Actor, int>(actorId, ct);
            if (actorEntry.NotFound(out notFoundProblems))
                return notFoundProblems;
            actors.Add(actorEntry.Entity);
        }

        var commandResult = command.Execute(this.accessor.Context, director, genres, actors.ToArray());

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

public static class CreateMovieFullCode
{
    public const string Command =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;
using RoyalCode.SmartValidations;
using System.Collections.Generic;

namespace Coreum.NewCommands.Tests.FindEntities;

public class CreateMovieFull
{
    public string? Title { get; set; }

    public int Year { get; set; }

    public int Runtime { get; set; }

    public int DirectorId { get; set; }

    public string? Plot { get; set; }

    public string? PosterUri { get; set; }

    public ICollection<int> Genres { get; set; }

    public ICollection<int> ActorsIds { get; set; }

    [MemberNotNullWhen(false, nameof(Title))]
    public bool HasProblems([NotNullWhen(true)] out Problems? problems)
    {
        return RuleSet.For<CreateMovie>()
            .NotEmpty(Title)
            .NotEmpty(Year)
            .NotEmpty(Runtime)
            .NotEmpty(DirectorId)
            .HasProblems(out problems);
    }

    [Command, WithUnitOfWork<CineDbContext>, WithValidateModel]
    public Movie Execute(CineDbContext db, Director director, ICollection<Genre> genres, Actor[] actors)
    {
        var movie = new Movie(Title!, Year, Runtime, director, Plot, PosterUri)
        {
            Genres = genres.ToList(),
            Actors = actors.ToList()
        };
        db.Movies.Add(movie);
        return movie;
    }
}
""";

    public const string Interface =
"""
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.FindEntities;

public interface ICreateMovieFullHandler
{
    public Task<Result<Movie>> HandleAsync(CreateMovieFull command, CancellationToken ct);
}

""";

    public const string Handler =
"""
using Coreum.NewCommands;
using Coreum.NewCommands.Tests.FindEntities;
using Coreum.NewCommands.Tests.Models;
using RoyalCode.SmartProblems;

namespace Coreum.NewCommands.Tests.FindEntities.Internals;

public class CreateMovieFullHandler : ICreateMovieFullHandler
{
    private readonly IUnitOfWorkAccessor<CineDbContext> accessor;

    public CreateMovieFullHandler(IUnitOfWorkAccessor<CineDbContext> accessor)
    {
        this.accessor = accessor;
    }

    public async Task<Result<Movie>> HandleAsync(CreateMovieFull command, CancellationToken ct)
    {
        if (command.HasProblems(out var validationProblems))
            return validationProblems;

        await this.accessor.BeginAsync(ct);

        Problems? notFoundProblems;

        var directorEntry = await this.accessor.FindEntityAsync<Director, int>(command.DirectorId, ct);
        if (directorEntry.NotFound(out notFoundProblems))
            return notFoundProblems;
        var director = directorEntry.Entity;

        List<Genre> genres = [];
        foreach (var genreId in command.Genres)
        {
            var genreEntry = await this.accessor.FindEntityAsync<Genre, int>(genreId, ct);
            if (genreEntry.NotFound(out notFoundProblems))
                return notFoundProblems;
            genres.Add(genreEntry.Entity);
        }

        List<Actor> actors = [];
        foreach (var actorId in command.ActorsIds)
        {
            var actorEntry = await this.accessor.FindEntityAsync<Actor, int>(actorId, ct);
            if (actorEntry.NotFound(out notFoundProblems))
                return notFoundProblems;
            actors.Add(actorEntry.Entity);
        }

        var commandResult = command.Execute(this.accessor.Context, director, genres, actors.ToArray());

        return await this.accessor.CompleteAsync(ct).MapAsync(commandResult);
    }
}

""";
}