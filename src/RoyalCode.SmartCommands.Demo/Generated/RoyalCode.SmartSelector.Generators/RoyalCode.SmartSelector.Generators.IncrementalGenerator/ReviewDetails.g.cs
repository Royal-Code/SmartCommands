using RoyalCode.SmartCommands.Tests.Models;
using System.Linq.Expressions;

namespace RoyalCode.SmartCommands.Demo.Commands.Movies;

public partial class ReviewDetails
{
    private static Func<Review, ReviewDetails> selectReviewFunc;

    public static Expression<Func<Review, ReviewDetails>> SelectReviewExpression { get; } = a => new ReviewDetails
    {
        Id = a.Id,
        Content = a.Content,
        Rating = a.Rating,
        AuthorUsername = a.Author.Username,
        CreatedAt = a.CreatedAt
    };

    public static ReviewDetails From(Review review) => (selectReviewFunc ??= SelectReviewExpression.Compile())(review);
}
