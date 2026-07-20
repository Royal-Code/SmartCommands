# Defeito latente: `MapCreatedRoute` com `Result` e rota estática gera código que não compila

**Registrado em:** 2026-07-18, durante a validação da revisão da Fase 1 do plano
`plan-smartcommands-accepted-tryfindby`. Fora do escopo daquele plano; a Fase 3 dele deve
considerar esta correção ao compartilhar ou espelhar o emissor para `MapAcceptedRoute`.

**Status:** RESOLVIDO em 2026-07-19, junto com a Fase 3. `GenerateCreatedMatchInvoke`
(`MapInformation.cs`) passou a receber o `ReturnModel` e, quando `returnModel.ValueType is null`
(comando sem valor, rota necessariamente estática), emite a rota como **string literal**
(`result.CreatedMatch("rota-estatica")`) em vez de lambda. A mesma lógica de rota é compartilhada
com o emissor do `MapAcceptedRoute` via os helpers `BuildStaticRouteLiteral`/`BuildRouteInterpolationBody`.
Regressão coberta pelo teste `MapCreatedRoute_estatica_sem_valor_emite_string_literal_e_compila`
(compila o código gerado com `AssertOutputCompiles`).

## Descrição

A validação do `MapCreatedRoute` permite um pattern sem placeholders e sem propriedades
declaradas em um comando que retorna `Result` puro: `ValidateCreatedRoute` retorna antes de
exigir `valueReturnType` quando `propertiesNames.Length == 0`
(`RoyalCode.SmartCommands.Generators/Generators/CommandHandlerGenerator.cs`, ~linhas 2031-2038).

Porém a emissão gera sempre um lambda como primeiro argumento:

```csharp
result.CreatedMatch(v => $"rota-estatica")
```

(`RoyalCode.SmartCommands.Generators/Generators/MapInformation.cs`, `GenerateCreatedMatchInvoke`,
~linha 562.)

Para `Result` puro, a única sobrecarga disponível no SmartProblems ApiResults é:

```csharp
CreatedMatch(this Result result, string createdPath)
```

(`RoyalCode.SmartProblems.ApiResults/HttpResults/HttpResultsExtensions.cs`, ~linha 142; os
construtores de `CreatedMatch` também não aceitam delegate.)

Um lambda não é conversível para `string`, portanto o código gerado para
`[MapCreatedRoute("rota-fixa")]` em comando que retorna `Result` **não compila** (CS1660/CS1503
no código gerado). O caso não possui nenhum uso na Demo nem cobertura de teste — todos os usos
atuais têm placeholder (`{id}`) e comando com valor de sucesso.

## Correção recomendada

No emissor de `MapCreatedRoute` (e no futuro `MapAcceptedRoute`, que deve nascer correto):

- `Result` + rota estática (sem placeholders): emitir `string` literal/interpolada simples;
- `Result<T>`: pode emitir `Func<T, string>` como hoje;
- `Result` + placeholders: já coberto pela validação (RCCMD050) — manter.

## Testes necessários

- Geração e compilação para `Result` + `MapCreatedRoute` com rota estática (com e sem `MapGroup`);
- Execução HTTP confirmando 201 com `Location` fixa;
- Caso negativo existente (`Result` + placeholder → RCCMD050) preservado;
- Os mesmos três casos para `MapAcceptedRoute` quando implementado.
