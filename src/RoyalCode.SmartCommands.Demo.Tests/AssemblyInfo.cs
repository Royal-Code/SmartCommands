using Xunit;

// Contorno de gap da lib: RoyalCode.SmartSearch.Linq.Sortings.OrderByProvider inicializa o handler de
// ordenacao default (ex.: (Produto, Id)) num dicionario estatico que NAO e thread-safe. Sob execucao
// paralela, dois testes que disparam buscas para a mesma entidade pela primeira vez colidem com
// "An item with the same key has already been added". Enquanto o gap nao e corrigido na SmartSearch,
// os testes de integracao da demo (host real) rodam de forma serial. Ver "Registro de gaps" no plano.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
