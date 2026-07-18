# Revisão: erros de compilação após atualização das libs RoyalCode (2026-07-13)

> **Errata — 2026-07-18:** a hipótese de `async void` apresentada originalmente na §4 foi rejeitada após
> inspeção das APIs. Os extension methods continuam retornando `Task`; parâmetros `Action<>` existentes não
> tornam o extension method `async void`. A quebra confirmada foi a mudança das assinaturas assíncronas com
> `TParam`: o delegate passou a receber `CancellationToken`, e o token da extensão passou a ser o último
> argumento. A correção válida usa lambdas `static` com `TParam` e token explícitos. As afirmações originais
> sobre fire-and-forget não devem ser usadas como diagnóstico atual.

## 1. Contexto

O `Directory.Build.props` atualizou as dependências RoyalCode:

| Pacote | De | Para |
|---|---|---|
| RoyalCode.SmartProblems | 1.0.0-preview-6.0 | 1.0.0-preview-7.0 |
| RoyalCode.SmartValidations | 1.0.0-preview-4.0 | 1.0.0-preview-7.0 |
| RoyalCode.WorkContext | 0.8.13 | 0.9.0 |
| RoyalCode.SmartSearch | 0.10.5 | 0.11.0 |
| RoyalCode.SmartSelector | 0.4.0 | 0.5.0 |
| ExSrcGen | 0.1.13 | 0.2.0 |

Todos os projetos de biblioteca da solução compilam. Os erros ocorrem apenas em
`RoyalCode.SmartCommands.Demo` (código gerado) e `RoyalCode.SmartCommands.Tests` (cenários).

## 2. Erros de compilação

Todos são `CS1593` (delegado com número errado de argumentos), no mesmo padrão de código:

| Arquivo | Linha |
|---|---|
| Demo/Generated/.../DesativarProdutoHandler.g.cs | 37 |
| Demo/Generated/.../ReativarProdutoHandler.g.cs | 37 |
| Tests/Scenarios/Ds/DoSomethingSyncWithResult.cs | 53 |
| Tests/Scenarios/Ds/DoSomethingSyncWithResultSome.cs | 53 |
| Tests/Scenarios/Es/CreateSomeSyncWithResult.cs | 49 |
| Tests/Scenarios/Fs/EditSomeSync.cs | 56 |
| Tests/Scenarios/Gs/DoWithTwoParameters.cs | 42 |

Código que falha (forma antiga, emitida pelo gerador e replicada nos cenários):

```csharp
return await command.Execute(produto)
    .ContinueAsync(this.accessor, async (a) => await a.CompleteAsync(ct));
```

## 3. Causa raiz: breaking change no SmartProblems preview-7.0

Nas sobrecargas `Async` com `TParam` (`ContinueAsync`, `MapAsync`, `CollectAsync`, `MatchAsync`),
quando o delegate retorna `Task`/`Task<T>`, ele agora **recebe `CancellationToken` como último
parâmetro**, e o `CancellationToken` do método fica **por último** na chamada
(regra §5 de `problems.ai-rules.md` e seção "Regra para sobrecargas Async com TParam" de `problems.md`).

Comparação das sobrecargas (XML doc dos pacotes NuGet):

```
// preview-6.0 (removida)
Result.ContinueAsync<TParam>(TParam param, Func<TParam, Task> action)

// preview-7.0 (nova forma)
Result.ContinueAsync<TParam>(TParam param, Func<TParam, CancellationToken, Task> action, CancellationToken ct)
```

O mesmo vale para `Result<T>` (`Func<T, TParam, CancellationToken, Task>`) e para as extensões
sobre `Task<Result>` / `Task<Result<T>>` / `FindResult<>`.

A sobrecarga `MapAsync(Task<Result>, T valor)` usada em `CompleteAsync(ct).MapAsync(commandResult)`
**continua existindo** — esse padrão não precisa mudar.

## 4. Hipótese rejeitada: ligação com `Action<>` (`async void`)

Na versão inicial desta revisão, as ocorrências que ainda compilavam foram classificadas como `async void`.
Essa interpretação estava errada. O fato verificável era a existência de chamadas na forma antiga e de sete
erros `CS1593`; a correção deveria alinhar todas as chamadas ao novo contrato, mas não havia evidência de que
os extension methods tivessem se tornado fire-and-forget.

- Quando o receptor é `Result`/`Result<T>` (método de instância, comando síncrono sem decorators),
  não há sobrecarga compatível → **erro CS1593** (os 7 casos acima).
- As ocorrências restantes também precisavam ser migradas para a assinatura nova, mesmo quando outra
  sobrecarga ainda permitia compilação. Isso é uma questão de contrato e consistência, não prova de
  `async void` no extension method.

Arquivos gerados no Demo que ainda continham a forma antiga (9 arquivos, 11 ocorrências):
`CriarPedidoHandler`, `CriarProduto2Handler`, `CancelarPedidoHandler`, `ReservarEstoqueHandler`,
`RegistrarEstoqueInicialHandler`, `LiberarReservaEstoqueHandler`, `AdicionarEntradaEstoqueHandler`
(+ os 2 que dão erro). O mesmo acontece nos cenários assíncronos/decorated dos testes
(`DoSomethingAsync*`, `DoSomethingWithDecorators*`, `CreateSomeAsyncWithResult`,
`CreateSomeWithDecorators*WithResult`).

> Conclusão corrigida: a mudança deve ser aplicada na fonte do **gerador** e os artefatos/cenários devem ser
> regenerados para usar uniformemente a assinatura nova. A alegação de quebra silenciosa em runtime foi
> retirada.

## 5. O que precisa ser atualizado

### 5.1 Gerador (fonte da verdade)

[`RoyalCode.SmartCommands.Generators/Commands/CompleteUnitOfWorkCommand.cs`](../../RoyalCode.SmartCommands.Generators/Commands/CompleteUnitOfWorkCommand.cs)
tem dois pontos de emissão:

- Linha ~90 (`produceNewEntity`): `async (e, a) => await a.AddEntityAsync(e, ct)`
- Linha ~105 (completar UoW): `async ({lambdaParam}) => await a.CompleteAsync(ct)` com
  `lambdaParam` = `"a"` ou `"_, a"`

Nova emissão (decidido em 2026-07-13: lambdas `static async`, como recomendam as docs do
SmartProblems — sem captura, sem alocação de closure):

```csharp
// comando retorna Result:
return await command.Do(db)
    .ContinueAsync(this.accessor, static async (a, token) => await a.CompleteAsync(token), ct);

// comando retorna Result<T>:
return await command.Do(db)
    .ContinueAsync(this.accessor, static async (_, a, token) => await a.CompleteAsync(token), ct);

// produceNewEntity (WithProduces / nova entidade):
return await command.Do(db)
    .ContinueAsync(this.accessor, static async (e, a, token) => await a.AddEntityAsync(e, token), ct)
    .ContinueAsync(this.accessor, static async (_, a, token) => await a.CompleteAsync(token), ct);
```

Observações:
- O parâmetro do delegate não pode se chamar `ct` (conflitaria com o `ct` do método externo);
  sugiro `token`.
- Dentro do delegate, usar `token` (e não o `ct` externo) — obrigatório para o lambda ser `static`.
- O `MapAsync(commandResult)` (linha ~81) não muda.

### 5.2 Cenários de teste (código compilado + strings esperadas)

Cada arquivo de cenário contém o handler escrito à mão (compila contra as libs reais) **e** as
constantes string com o código esperado do gerador — ambos precisam da mesma alteração:

- `Scenarios/Ds/` (8 arquivos, 2 ocorrências cada): `DoSomethingSyncWithResult`,
  `DoSomethingSyncWithResultSome`, `DoSomethingAsyncWithResult`, `DoSomethingAsyncWithResultSome`,
  `DoSomethingWithDecoratorsSyncWithResult`, `DoSomethingWithDecoratorsSyncWithResultSome`,
  `DoSomethingWithDecoratorsAsyncWithResult`, `DoSomethingWithDecoratorsAsyncWithResultSome`
- `Scenarios/Es/` (4 arquivos, 4 ocorrências cada — AddEntityAsync + CompleteAsync):
  `CreateSomeSyncWithResult`, `CreateSomeAsyncWithResult`,
  `CreateSomeWithDecoratorsSyncWithResult`, `CreateSomeWithDecoratorsAsyncWithResult`
- `Scenarios/Fs/EditSomeSync.cs` (2 ocorrências)
- `Scenarios/Gs/DoWithTwoParameters.cs` (2 ocorrências)

### 5.3 Strings esperadas em testes do gerador

- `Tests/Generators/AddServicesTests.cs` (1 ocorrência, linha ~213)
- `Tests/Generators/RetryOnConcurrencyTests.cs` (3 ocorrências, linhas ~135/194/259)

### 5.4 Código gerado do Demo

Os arquivos em `Demo/Generated/**` são re-emitidos no build (EmitCompilerGeneratedFiles);
após corrigir o gerador, basta recompilar e commitar os `.g.cs` atualizados.
Os novos arquivos do SmartSelector 0.5.0 que apareceram no `git status`
(`ProdutoEstoqueDetalhes.*`, `ReviewDetails.*`) são saída normal da nova versão do generator —
apenas commitar.

### 5.5 Validação

1. `dotnet build SmartCommands.sln` sem erros.
2. `dotnet test` — os testes de snapshot/string do gerador confirmarão a nova emissão.
3. Testes de integração do Demo (SoftDeleteTests, concorrência etc.) validam que o fluxo assíncrono aguarda
   o commit real do UoW com a assinatura nova.

## 6. Avaliação das demais libs (docs em `.docs/references/`)

| Lib | Situação |
|---|---|
| **SmartProblems preview-7.0** | Única com breaking change que afeta a solução (§3). `problems.ai-rules.md` §2.5 e §6 documentam a regra `(param, delegate, ct)`. |
| **SmartValidations preview-7.0** | `RuleSet`/`Rules.Set` usados nos cenários e no Demo compilam sem alteração. A doc recomenda `Rules.Set<T>()` como forma preferida (uso atual com `RuleSet.For<T>()` continua suportado). |
| **WorkContext 0.9.0** | Sem impacto de compilação. `IUnitOfWorkAccessor`/`CompleteAsync` são contratos desta própria solução (`RoyalCode.SmartCommands`); os adapters (`UnitOfWorkAccessor`, `DbContextAccessor`) compilam contra 0.9.0. |
| **SmartSearch 0.11.0** | Sem impacto de compilação; API de criteria usada no Demo permanece compatível. |
| **SmartSelector 0.5.0** | Sem impacto; gerou dois pares novos de arquivos `.g.cs` no Demo (commitar). |
| **ExSrcGen 0.2.0** | Sem impacto de compilação no projeto Generators. |

## 7. Melhorias opcionais (não bloqueantes)

1. **Lambdas `static` no código gerado** — decidido: será adotado já na correção (§5.1).
2. **CS8669 nos arquivos gerados** — o build emite dezenas de warnings de anotação nullable em
   código gerado. Importante: o comentário `// <auto-generated/>` sozinho **não resolve** —
   ele é o que faz o compilador desligar o contexto nullable do arquivo, e é justamente isso
   que dispara o CS8669 quando há anotações `?`. O padrão canônico para source generators é
   emitir ambos no cabeçalho de cada arquivo:

   ```csharp
   // <auto-generated/>
   #nullable enable
   ```

   O marcador `<auto-generated/>` faz analyzers e style rules ignorarem o arquivo (e o atributo
   `[GeneratedCode("...", "...")]` nos tipos ajuda analyzers adicionais); o `#nullable enable`
   apenas declara o contexto de anotações — não obriga a anotar nada — e é a correção que o
   próprio CS8669 pede. Fica para uma rodada separada, pois altera todos os snapshots de teste.
3. **Na própria lib SmartProblems** (mantida por você): manter as sobrecargas e XML docs inequívocos para
   que consumidores migrem diretamente para as formas com `TParam` e `CancellationToken`. A hipótese
   original de captura silenciosa como `async void` foi rejeitada pela errata deste documento.

## 8. Decisões tomadas e status

Decisões (2026-07-13):

1. **Estilo do lambda gerado**: `static async (a, ct) => ...` — o parâmetro do token chama-se
   `ct`, sombreando o `ct` do método externo (permitido em lambdas `static`, que não capturam).
2. **CS8669**: será tratado em rodada separada, com o cabeçalho `// <auto-generated/>` +
   `#nullable enable` (ver §7.2 — o comentário sozinho não elimina o warning).
3. **SmartProblems**: sem ação na lib — não há erro nela; a mudança de assinatura é intencional,
   e a ligação com `Action<>` é consequência natural da resolução de sobrecarga.

### Status da implementação (2026-07-13): concluída ✔

- `CompleteUnitOfWorkCommand.cs`: dois pontos de emissão atualizados para
  `static async (..., ct) => ...` + `ct` como último argumento.
- 16 arquivos de teste atualizados (40 ocorrências): 14 cenários (código compilado + strings
  esperadas) + `AddServicesTests.cs` + `RetryOnConcurrencyTests.cs`.
- Demo regenerado no build: 9 arquivos `.g.cs` reescritos com o padrão novo.
- `dotnet build SmartCommands.sln`: sem erros.
- `dotnet test`: 153/153 aprovados (87 Tests + 66 Demo.Tests).
