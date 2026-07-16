using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using RoyalCode.Extensions.SourceGenerator.Descriptors;
using RoyalCode.SmartCommands.Generators.Generators;
using Xunit;

namespace RoyalCode.SmartCommands.Tests.Characterization;

/// <summary>
/// <para>
///     Testes de caracterização (Fase 1) para as falhas de igualdade e de coleção nula verificadas
///     nos modelos do pipeline do generator. Cada teste afirma o comportamento-alvo (correto) e, por
///     isso, FALHA no baseline exatamente pelo bug que documenta. As Fases 2/4 devem torná-los verdes.
/// </para>
/// <para>
///     Referências no plano: "Falha concreta de igualdade" e "Falhas concretas nos demais modelos".
/// </para>
/// </summary>
[Trait("Category", "Characterization")]
public class ModelEqualityCharacterizationTests
{
    // Bug: MapApiHandlersInformation.Equals(MapApiHandlersInformation) ignora WithOpenApi.
    // Alvo: dois hosts iguais no tipo mas com WithOpenApi diferente não podem ser iguais,
    // pois isso invalidaria o cache incremental ao alternar WithOpenApi.
    [Fact]
    public void MapApiHandlersInformation_TypedEquality_ShouldConsiderWithOpenApi()
    {
        var classType = new TypeDescriptor("Extensions", ["Tests.Scenarios"]);

        var withOpenApi = new MapApiHandlersInformation(classType, withOpenApi: true, diagnostics: null);
        var withoutOpenApi = new MapApiHandlersInformation(classType, withOpenApi: false, diagnostics: null);

        Assert.NotEqual(withOpenApi, withoutOpenApi);
    }

    // Bug: MapApiHandlersInformation.Equals(object) testa "obj is AddHandlersServicesInformation",
    // logo dois hosts idênticos nunca são iguais via Equals(object).
    [Fact]
    public void MapApiHandlersInformation_ObjectEquality_ShouldMatchSameType()
    {
        var classType = new TypeDescriptor("Extensions", ["Tests.Scenarios"]);

        var a = new MapApiHandlersInformation(classType, withOpenApi: true, diagnostics: null);
        var b = new MapApiHandlersInformation(classType, withOpenApi: true, diagnostics: null);

        Assert.True(a.Equals((object)b), "Equals(object) deveria comparar contra MapApiHandlersInformation.");
    }

    // Bug: FindInformation.Equals ignora IdType (e o GetHashCode também).
    // Alvo: mudar apenas o tipo do Id deve invalidar a igualdade.
    [Fact]
    public void FindInformation_TypedEquality_ShouldConsiderIdType()
    {
        var entityType = new TypeDescriptor("Product", ["Tests.Scenarios"]);
        var modelType = new TypeDescriptor("ProductDetails", ["Tests.Scenarios"]);
        var intId = new TypeDescriptor("int", ["System"]);
        var longId = new TypeDescriptor("long", ["System"]);

        var withIntId = new FindInformation(
            entityType, intId, modelType,
            endpointRoutePattern: "\"{id}\"", endpointName: "\"find-product\"",
            description: null, summary: null, authorizationPolicies: [], groupName: "products");

        var withLongId = new FindInformation(
            entityType, longId, modelType,
            endpointRoutePattern: "\"{id}\"", endpointName: "\"find-product\"",
            description: null, summary: null, authorizationPolicies: [], groupName: "products");

        Assert.NotEqual(withIntId, withLongId);
    }

    // Bug: MapCreatedInformation compara PropertiesNames por referência (string[].Equals).
    // Alvo: mesmo conteúdo deve ser igual, independentemente da instância do array.
    [Fact]
    public void MapCreatedInformation_Equality_ShouldCompareByContent()
    {
        var a = new MapCreatedInformation("{id}", ["Id"]);
        var b = new MapCreatedInformation("{id}", ["Id"]);

        Assert.Equal(a, b);
    }

    // Bug: MapResponseValuesInformation compara PropertiesNames por referência (IList.Equals).
    // Alvo: duas listas de mesmo conteúdo (aqui, ambas vazias) devem ser iguais.
    [Fact]
    public void MapResponseValuesInformation_Equality_ShouldCompareByContent()
    {
        var a = new MapResponseValuesInformation(new List<PropertyDescriptor>());
        var b = new MapResponseValuesInformation(new List<PropertyDescriptor>());

        Assert.Equal(a, b);
    }

    // Bug: MapInformation compara AuthorizationPolicies por conteúdo em Equals (SequenceEqual),
    // mas por identidade de referência em GetHashCode (AuthorizationPolicies?.GetHashCode()).
    // Isso viola o contrato "objetos iguais têm hash iguais" e quebra o cache incremental.
    //
    // Observação (Fase 1): ao contrário do previsto no plano, políticas nulas NÃO lançam exceção,
    // porque o SequenceEqual em escopo (da base de geração) tolera null; o defeito real é a
    // inconsistência de hash sobre a coleção com identidade por referência.
    [Fact]
    public void MapInformation_EqualByContent_ShouldHaveEqualHashCode()
    {
        var a = new MapInformation
        {
            HttpMethod = "Post",
            RoutePattern = "\"/\"",
            EndpointName = "\"create\"",
            GroupName = "products",
            AuthorizationPolicies = ["admin"],
        };

        var b = new MapInformation
        {
            HttpMethod = "Post",
            RoutePattern = "\"/\"",
            EndpointName = "\"create\"",
            GroupName = "products",
            AuthorizationPolicies = ["admin"],
        };

        // Pré-condição: por conteúdo, os dois são iguais.
        Assert.True(a.Equals(b), "MapInformation com mesmo conteúdo deveria ser igual.");

        // Alvo: hash consistente com a igualdade por conteúdo.
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
