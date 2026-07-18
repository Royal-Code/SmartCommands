Minha conclusão: para executar o plano com as opções recomendadas `Q1=A`, `Q2=A` e `Q3=A`, serão necessárias mudanças em `SmartProblems` e `EnterprisePatterns`. O `SmartSelector` não precisa mudar para viabilizar o `TryFindBy`, mas deve mudar para adotar os novos hint names. A infraestrutura comum de nomes deve ficar no `RoyalCode.Extensions.SourceGenerator`.

Nenhum arquivo foi alterado nesta análise.

## 1. Decisões que ainda bloqueiam o plano

O [plano](/C:/git/RoyalCode/SmartCommands/src/.ai/plans/plan-smartcommands-accepted-tryfindby.md:99) ainda mantém Q1–Q3 abertas, embora as evidências agora sustentem bem:

- Q1: opção A — propriedades nomeadas no atributo.
- Q2: opção A — projeção obrigatória para DTO.
- Q3: opção A — `FirstOrDefault`.

Há também uma pequena inconsistência: o cabeçalho diz “Fase 1 pronta”, mas a fase continua pendente, com tarefas desmarcadas e decisões abertas.

Antes da implementação, eu fecharia formalmente essas três DFs.

### Revisão

As questões foram respondidas, o plano atualizado, e problemas resolvidos.

## 2. SmartProblems

### Mudança obrigatória: `AcceptedMatch`

O projeto `RoyalCode.SmartProblems.ApiResults` precisa receber:

- `AcceptedMatch`
- `AcceptedMatch<T>`

Eles devem seguir a estrutura de [CreatedMatch](/C:/git/RoyalCode/SmartProblems/src/RoyalCode.SmartProblems.ApiResults/HttpResults/CreatedMatch'0.cs), mas com:

- HTTP 202;
- `Location` opcional;
- sem corpo para `Result`;
- corpo `T` para `Result<T>`;
- preservação de `MatchErrorResult`;
- metadata 202 correta;
- conversões de `Accepted`, `Accepted<T>`, `Problem` e `Problems`.

Assinaturas centrais sugeridas:

```csharp
public AcceptedMatch(Result result, string? location = null);

public AcceptedMatch(
    Accepted result);

public AcceptedMatch<T>(
    Result<T> result,
    string? location = null);
```

Também é útil uma variante com `Func<T, string?>` caso a `Location` seja calculada a partir do valor de sucesso, embora o SmartCommands possa entregar a URL já construída.

Testes necessários:

- sucesso sem corpo e sem `Location`;
- sucesso sem corpo e com `Location`;
- `Result<T>` preservando JSON;
- problema nunca convertido em 202;
- execução HTTP real;
- metadata/OpenAPI;
- argumentos nulos;
- net8, net9 e net10.

### `TryFindBy`: nenhuma mudança obrigatória no SmartProblems

O `RoyalCode.SmartProblems.EntityFramework` já possui:

- busca por predicado;
- propriedade e valor;
- critérios compostos com AND;
- problema rico de `NotFound`;
- cancelamento;
- semântica `FirstOrDefaultAsync`.

Portanto, eu não colocaria projeção ou `ISelectorFactory` no SmartProblems. O pacote deve continuar responsável pelo resultado/problema e pelas extensões EF genéricas, não pela seleção de DTO usada pelos repositórios.

## 3. EnterprisePatterns

Esta mudança passa a ser obrigatória se Q2 for realmente a opção A.

O [IFinder<TEntity>](/C:/git/RoyalCode/EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Repositories.Abstractions/DataServices.cs:92) já oferece:

- entidade por predicado;
- entidade por propriedade;
- DTO por ID.

O que falta é exatamente:

> DTO projetado por predicado/chave alternativa.

### Contrato mínimo necessário

Uma assinatura candidata é:

```csharp
Task<FindResult<TDto>> FindAsync<TDto>(
    Expression<Func<TEntity, bool>> filter,
    IReadOnlyList<FindCriterion> criteria,
    CancellationToken ct = default)
    where TDto : class;
```

O predicado representa a consulta, enquanto `criteria` preserva os dados ricos do `NotFound` sem analisar novamente a árvore de expressão em runtime.

Pode existir também uma sobrecarga mais geral sem critérios explícitos:

```csharp
Task<FindResult<TDto>> FindAsync<TDto>(
    Expression<Func<TEntity, bool>> filter,
    CancellationToken ct = default)
    where TDto : class;
```

Mas o SmartCommands deve consumir a variante explícita, pois o generator já conhece as propriedades e valores.

### Implementação EF

A implementação em `RoyalCode.Repositories.EntityFramework` deve executar aproximadamente:

```csharp
var dto = await set
    .Where(filter)
    .Select(selector)
    .FirstOrDefaultAsync(ct);
```

Sem:

- materializar a entidade;
- tracking desnecessário;
- carregar coleção;
- compilar a expressão;
- realizar duas consultas.

O repositório já possui a obtenção do seletor em [SelectDtoById](/C:/git/RoyalCode/EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Repositories.EntityFramework/SelectDtoById.cs:12). Eu generalizaria essa responsabilidade:

```text
SelectDto<TEntity, TDto>
├── GetSelector(DbContext)
├── Select(IQueryable<TEntity>)
└── SelectWhere(IQueryable<TEntity>, predicate)
```

`SelectDtoById` poderia delegar para essa base. Isso evita que uma funcionalidade genérica de projeção continue presa a uma classe chamada “ById”.

### Testes necessários

- chave simples;
- chave composta;
- valores nullable;
- `NotFound` com critérios na ordem declarada;
- duplicidade usando `FirstOrDefault`;
- cancelamento;
- query filter/tenant preservado;
- somente uma consulta;
- SQL selecionando apenas campos do DTO;
- nenhuma entidade rastreada;
- seletor ausente produzindo erro claro;
- implementação fake do `IRepository<TEntity>` atualizada.

Essa adição ao `IFinder<TEntity>` é uma mudança pública e quebra implementações externas de `IRepository<TEntity>`. Deve ser tratada explicitamente no rollout do pacote de persistência.

## 4. SmartSelector em relação ao `TryFindBy`

Não encontrei necessidade de uma feature nova no SmartSelector para a busca alternativa.

Ele já gera as expressões usadas pela infraestrutura de projeção, e o ecossistema já possui `ISelectorFactory`. O que falta está na operação do repositório:

```text
predicado + seletor + FirstOrDefaultAsync
```

Assim:

- não criar `[TryFindBy]` no SmartSelector;
- não fazer o SmartSelector conhecer repositórios;
- não criar integração direta SmartSelector → SmartCommands;
- apenas garantir que a projeção gerada continue traduzível pelo EF.

## 5. Nomes dos arquivos do SmartSelector

A mudança é válida e resolve o mesmo problema já corrigido no SmartCommands.

Hoje [GeneratedSourceConventions](/C:/git/RoyalCode/SmartSelector/src/RoyalCode.SmartSelector.Generators/Generators/GeneratedSourceConventions.cs:191) gera nomes como:

```text
RoyalCode.SmartSelector.Demo.Details.Library.BookDetails.AutoProperties.g.cs
```

O novo padrão deveria gerar:

```text
BookDetails_AutoProperties.ABCDEFGH.g.cs
BookDetails_AutoSelect.XYZ23456.g.cs
BookDetails_Extensions.KLMN7654.g.cs
AddressDetails_AutoDetails.QWERT234.g.cs
```

Propriedades do formato:

- nome legível na frente;
- categoria preservada;
- hash como sufixo;
- exatamente oito caracteres Base32;
- primeiros 40 bits de SHA-256;
- identidade completa usada no hash;
- nome legível limitado;
- comprimento físico previsível;
- resultado determinístico.

A identidade deve incluir, no mínimo:

```text
namespace
+ tipos contenedores
+ metadata name do tipo
+ nome do tipo gerado
+ categoria do artefato
```

Uma forma segura de migrar é usar o nome completo atual como identidade do hash. Assim, a semântica atual de unicidade é preservada, apenas a representação física muda.

## 6. Onde deve ficar o algoritmo de hint name

Como agora existem pelo menos dois generators RoyalCode precisando da mesma convenção, eu não copiaria o [HintName do SmartCommands](/C:/git/RoyalCode/SmartCommands/src/RoyalCode.SmartCommands.Generators/Generators/HintName.cs:13) para o SmartSelector.

O algoritmo deve ser movido para:

```text
RoyalCode.Extensions.SourceGenerator
```

Com uma API genérica, por exemplo:

```csharp
public static class GeneratedHintName
{
    public static string Create(
        string identity,
        string readableName);
}
```

Responsabilidades da implementação compartilhada:

- validação;
- sanitização;
- limite do trecho legível;
- SHA-256;
- codificação Base32;
- sufixo `.g.cs`.

Cada generator continua responsável por construir sua identidade e seu nome legível.

Ordem de rollout sugerida:

1. `RoyalCode.Extensions.SourceGenerator`
   - adicionar `GeneratedHintName`;
   - testes determinísticos e de colisão;
   - liberar versão aditiva.

2. `SmartSelector`
   - substituir as duas sobrecargas de `GeneratedSourceConventions.FileName`;
   - atualizar testes e artefatos gerados;
   - liberar nova versão.

3. `SmartCommands`
   - migrar seu helper privado para a implementação compartilhada;
   - comprovar que os nomes permanecem idênticos aos atuais.

## 7. Cobertura necessária para os nomes

O SmartSelector já testa DTOs homônimos em namespaces e tipos contenedores diferentes em [GenerationHardeningTests](/C:/git/RoyalCode/SmartSelector/src/RoyalCode.SmartSelector.Tests/Tests/GenerationHardeningTests.cs:6). Esses testes precisam ser adaptados e ampliados com:

- mesmo tipo em namespaces diferentes;
- tipos aninhados homônimos;
- categorias diferentes para o mesmo DTO;
- nome extremamente longo;
- caracteres sanitizados;
- namespace global;
- determinismo entre execuções;
- hash com regex `[A-Z2-7]{8}`;
- limite máximo do arquivo;
- alteração de namespace mudando somente o hash;
- mesma identidade produzindo exatamente o mesmo nome.

Existem cerca de 74 referências de testes aos hint names antigos. Eu evitaria codificar hashes em todos eles. O utilitário de testes pode localizar o artefato por:

```text
tipo + categoria
```

Os valores exatos do hash devem ser verificados apenas nos testes específicos de `GeneratedHintName`.

## Resumo do impacto entre repositórios

| Repositório | Necessidade |
|---|---|
| SmartProblems | Obrigatório para `AcceptedMatch` e `AcceptedMatch<T>` |
| EnterprisePatterns | Obrigatório se Q2=A, para projeção por predicado |
| SmartSelector | Nenhuma feature para `TryFindBy`; obrigatório para melhorar hint names |
| Extensions.SourceGenerator | Recomendado como dono do algoritmo compartilhado de hint name |
| SmartSearch/Searches | Nenhuma mudança necessária |
| SmartCommands | Consumirá as releases anteriores e implementará atributo, adapters e generator |

A ordem geral mais segura fica:

```text
Extensions.SourceGenerator → SmartSelector
SmartProblems → EnterprisePatterns → SmartCommands
```