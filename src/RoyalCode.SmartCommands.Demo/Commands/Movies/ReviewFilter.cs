using RoyalCode.SmartCommands.Tests.Models;
using RoyalCode.SmartSearch;

namespace RoyalCode.SmartCommands.Demo.Commands.Movies;

[MapGroup("movies")]
[MapSearch("/{movieId}/reviews", "Listagem paginada de produtos")]
[SearchReference<Review, ReviewDetails>]
public class ReviewFilter
{
    // atribuição direta da rota para a propriedade quando set é internal e bater com o nome de um parâmetro da rota
    // [MapRouteParam("movieId")] -> uma opção é usar esse atributo
    public int MovieId { get; internal set; }

    public string? Content { get; set; }

    public int? Rating { get; set; }

    //[WithSearchAction]
    // Com esse atributo, seria executado automaticamente este método.
    // Pode receber de parâmetros o que for necessário, inclusive serviços injetados.
    // o criteria seria do endpoint de search,
    // parametros que tem nome igual ao da rota são atribuídos automaticamente
    // os outros parâmetros são injetados pelo container de DI.
    internal void Configure(int movieId, ICriteria<Review> criteria)
    {
        MovieId = movieId;
    }
}
