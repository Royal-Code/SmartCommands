using RoyalCode.SmartCommands.Tests.Models;

namespace RoyalCode.SmartCommands.Demo.Commands.Movies;

public static class ReviewDetails_Extensions
{
    public static IQueryable<ReviewDetails> SelectReviewDetails(this IQueryable<Review> query)
    {
        return query.Select(ReviewDetails.SelectReviewExpression);
    }

    public static IEnumerable<ReviewDetails> SelectReviewDetails(this IEnumerable<Review> enumerable)
    {
        return enumerable.Select(ReviewDetails.From);
    }

    public static ReviewDetails ToReviewDetails(this Review review) => ReviewDetails.From(review);
}
