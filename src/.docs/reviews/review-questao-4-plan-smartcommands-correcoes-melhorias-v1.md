Na minha visão, a diferença principal é esta:

- **Opção A** evolui a infraestrutura dos endpoints atuais e cria pontos de extensão.
- **Opção B** adiciona novos tipos de caso de uso HTTP e novas abstrações de consulta/transporte.

## Opção A — Extensibilidade mínima

Entrariam três grupos de funcionalidades.

### 1. Filtros por endpoint

Novo atributo repetível, conceitualmente:

```csharp
[WithEndpointFilter<AuditFilter>]
[WithEndpointFilter<IdempotencyFilter>]
[MapPost("/", "create-product")]
public class CreateProduct;
```

O generator produziria algo equivalente a:

```csharp
group.MapPost("/", CreateProductHandleAsync)
    .AddEndpointFilter<AuditFilter>()
    .AddEndpointFilter<IdempotencyFilter>();
```

Contrato esperado:

- aceita implementações de `IEndpointFilter`;
- filtros resolvidos por DI pelo ASP.NET Core;
- preserva a ordem declarada;
- funciona em Command, Find e Search;
- tipo abstrato, aberto, inacessível ou que não implemente `IEndpointFilter` gera diagnóstico;
- sem criar pipeline de filtros próprio no SmartCommands.

Isso permite idempotência, auditoria, tenant, feature flags e validações estritamente HTTP sem contaminar handlers.

---

Direcionamento Humano:

Essa é uma funcionalidade interessante.

---

### 2. Política explícita de resultado/status

Hoje parte da resposta é inferida:

- retorno com valor → 200;
- `MapCreatedRoute` → 201;
- Delete sem valor → 204;
- Delete com valor → 200.

A Opção A acrescentaria uma forma explícita de selecionar a resposta. Por exemplo, conceitualmente:

```csharp
[MapResult(MapResultKind.Ok)]
[MapResult(MapResultKind.NoContent)]
```

Ou propriedades nos próprios `Map*`. A forma pública ainda precisaria ser escolhida.

O conjunto da opção A seria:

- `Ok` — 200, com ou sem valor conforme o contrato definido;
- `Created` — 201, reutilizando `MapCreatedRoute`;
- `NoContent` — 204, exigindo retorno sem valor;
- política inferida atual continuaria sendo o default;
- combinações incompatíveis gerariam diagnóstico.

Exemplos de erros:

- `NoContent` em comando que retorna `Result<T>`;
- `Created` sem valor ou sem informações suficientes para `Location`;
- dois atributos de política;
- `MapCreatedRoute` combinado com política `Ok`.

Eu evitaria criar atributos independentes como `MapOk`, `MapCreated`, `MapNoContent` se um único contrato tipado puder representar todas as escolhas.

---

Direcionamento Humano:

Eu acho que um `[WithResultStatus(ResultStatus.Ok)]` seria um nomenclatura melhor. Que achas?
Também acho que `NoContent` em comando que retorna `Result<T>` deveria ser permitido.

---

### 3. Metadata completa e uniforme

A Fase 9 já corrigiu boa parte da metadata. Na Fase 10, a novidade seria tornar a metadata parte do modelo compartilhado da política de resposta:

- status de sucesso explícito;
- tipo do body de sucesso;
- ausência de body em 204;
- `Location` em 201;
- `ProblemDetails` por categoria;
- filtros e policies preservados;
- OpenAPI coerente com a política escolhida.

Eu também avaliaria incluir metadata simples que falta na superfície atual, principalmente tags:

```csharp
[WithTags("Products", "Catalog")]
```

Mas evitaria reproduzir toda a API de metadata do ASP.NET Core por atributos RoyalCode. O usuário ainda pode configurar o `RouteGroupBuilder` externamente.

---

Direcionamento Humano:

Não sei se vale a pena `WithTags`. Não é complicado adicionar mais isso, poderia ser feito.
Teria que avaliar o que os commands propões e padronizam e as customizações.
O objetivo é ter algo padronizado, feito de forma mais simples (menos arquivos e lugares para mexer), mais fácil de trabalhar, mais automátizado, mais rápido de desenvolver.
Altas customizações podem ficar com minimal API direto.
Seria ruim não ter uma funcionalidade que é muito usada e limitaria o uso dos commands.

---

### Resultado prático da Opção A

Ela não adiciona novos meios de localizar entidades nem novos formatos de upload. Ela melhora muito a extensibilidade dos endpoints existentes com impacto arquitetural relativamente controlado.

---

## Opção B — Pacote ampliado

A Opção B incluiria tudo da A e mais três famílias substancialmente maiores.

### 1. Resposta `Accepted` — 202

Nova política para operações aceitas, mas ainda não concluídas:

```csharp
[MapResult(MapResultKind.Accepted)]
```

Possíveis formas:

```csharp
Result
Result<OperationReference>
```

Com suporte opcional a `Location`:

```csharp
[MapAcceptedRoute("/operations/{id}", nameof(OperationReference.Id))]
```

Precisaria definir:

- 202 com ou sem body;
- como construir `Location`;
- diferença entre `Created` e `Accepted`;
- se qualquer comando pode usar 202 ou se deve retornar uma referência de operação;
- metadata OpenAPI correspondente.

É uma extensão pequena em código, mas exige um contrato público bem definido para não transformar `Accepted` apenas em “outro número de status”.

---

Direcionamento Humano:

É até útil ter algo assim.
Pense num comando que é valido e colocado em alguma fila, seja no banco ou em sistema de mensageria.
Depois o status do processamento pode ser consultado.
Acho que `MapAcceptedRoute` segue o fio do `MapCreatedRoute`, deveria ter algo similar.

Quanto ao tipo retornado, eu não acho que deveria forçar algo.
Na especificação do HTTP o STATUS CODE 202 não define algo rígido no retorno.
Então penso que aqui não seria necessário um retorno rígido.

Até poderia existir um `OperationReference` que quando usado tem uma integração melhor com o gerador de código e api's internas se existirem, mas precisaria pensar bem sobre isso, elaborar um design bem útil.

Também seria necessário criar um AcceptedMatch, algo no SmartProblems, precisa liberar uma nova versão eu acho.

---

### 2. Find por chave alternativa ou composta

Hoje `MapFind` está ligado ao `Id<TEntity,TId>`. A opção ampliada permitiria casos como:

```csharp
GET /products/by-sku/{sku}
GET /orders/{storeId}/{number}
```

Possíveis contratos:

```csharp
[MapFindBy<Product>(nameof(Product.Sku))]
```

ou:

```csharp
[MapFindBy<Order>(
    nameof(Order.StoreId),
    nameof(Order.Number))]
```

Isso exigiria muito mais que novos atributos:

- binding de uma ou várias chaves;
- resolução dos nomes dos parâmetros de rota;
- validação dos tipos;
- consulta por expressão ou contrato novo no accessor;
- garantia ou pressuposto de unicidade;
- retorno `NotFound`;
- possível resultado ambíguo quando a chave não é única;
- implementação nos adapters EF e WorkContext;
- suporte a projeção;
- diagnósticos para propriedade inexistente ou não consultável.

É a parte mais arquitetural da Opção B, porque amplia contratos de persistência, não apenas a camada HTTP.

---

Direcionamento Humano:

No `SmartProblems` há integração com EFCore.
Tem um TryFindBy lá.
Teria que avaliar se é possível usar aquilo, ou o `ICriteria` do SmartSearch.
Primeiro teria que avaliar a viabilidade de usar algo componentizado, depois poderia pensar como solucionar a arquitetura da funcionalidade.
Criar um novo método `FindEntityByAsync` no unit of work adapter seria simples, mas o resto precisa estar pronto para isso.

---

### 3. Formulários, arquivos e streams

Entraria suporte assistido para comandos multipart:

```csharp
[MapPost("/documents", "upload-document")]
public class UploadDocument
{
    public string Description { get; set; }

    public IFormFile File { get; set; }
}
```

Ou parâmetros externos:

```csharp
Result Execute(
    [WithParameter, FromForm] string description,
    [WithParameter, FromForm] IFormFile file);
```

O generator precisaria lidar com:

- `[FromForm]`;
- `IFormFile` e `IFormFileCollection`;
- `multipart/form-data`;
- arquivos opcionais e obrigatórios;
- limites de tamanho;
- cancelamento;
- OpenAPI de multipart;
- antiforgery do ASP.NET Core sem desativá-lo silenciosamente;
- evitar retenção indevida de `IFormFile` ou `Stream` após a requisição.

Streaming pode significar duas coisas diferentes e eu as separaria:

- **entrada em stream**: upload sem materializar todo o arquivo;
- **saída em stream**: download, exportação ou resposta incremental.

Saída ainda exigiria decisões sobre:

- `Stream`, `IAsyncEnumerable<T>` ou `PipeReader`;
- content type e nome de arquivo;
- status e headers;
- descarte do stream;
- cancelamento durante envio;
- representação OpenAPI.

---

Direcionamento Humano:

Este tipo de funcionalidade é muito específica, é melhor usar minimal api diretamente.

---

### Resultado prático da Opção B

Ela abre casos de uso realmente novos, mas aumenta bastante o escopo:

| Aspecto | Opção A | Opção B |
|---|---:|---:|
| Novas APIs públicas | poucas | várias |
| Mudança em persistência | não | provavelmente sim |
| Dependência de tipos ASP.NET | pequena | maior |
| Novos formatos HTTP | não | multipart/stream |
| Risco de design | moderado | alto |
| Casos de uso novos | filtros e respostas explícitas | operações assíncronas, chaves alternativas, upload/download |

## Minha recomendação

Eu escolheria a **Opção A** para a Fase 10.

Ela entrega uma base útil imediatamente: filtros e política explícita de resposta serão reutilizados por qualquer feature futura. Já a Opção B mistura três problemas independentes:

1. 202/Accepted;
2. consultas por chaves alternativas;
3. arquivos e streaming.

Depois da A, eu trataria cada um como uma fase ou plano separado. Entre os itens da B, `Accepted` é pequeno o bastante para eventualmente entrar junto, mas chave alternativa/composta e upload/stream merecem desenhos próprios.

---

Direcionamento Humano:

As três primeiras opções são mais simples e fáceis de fazer.

A do retorno 202 é bem útil, daria um pequeno trabalho de design e liberação do SmartProblems, mas é uma boa funcionalidade.

O TryFindBy é boa, mas acho que daria um longa implementação, teria que avaliar.

O de Streaming é desnecessária, isso fica para Minimal API.

Então penso que a opção A seria o melhor para este plano e as duas outras funcionalidades poderiam ser feitas em um novo plano.

---