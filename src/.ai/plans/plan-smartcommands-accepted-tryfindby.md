# Plan: `Accepted` e busca por chave alternativa/composta (`smartcommands-accepted-tryfindby`)

## Status: EM EXECUÇÃO - Fase 1 concluída; Q1-Q6 fechadas (DF10-DF16)

## Progresso

`█░░░░░` **17%** - 1 de 6 fases concluídas

| Fase | Estado |
|---|---|
| Fase 1 - Baseline, viabilidade e decisões de contrato | Concluída em 2026-07-18; evidências no Resultado da Fase 1 |
| Fase 2 - Resultados HTTP `Accepted` no SmartProblems | Pronta para iniciar (Fase 1 concluída); escopo ampliado pela DF16 |
| Fase 3 - `Accepted` no SmartCommands e no generator | Bloqueada pela Fase 2; dependência da Fase 10 do plano principal já satisfeita |
| Fase 4 - Contrato runtime e adapters de `TryFindBy` | Pronta para iniciar (Fase 1 e DFs 10-16 fechadas); exige antes a extensão no EnterprisePatterns (DF13-DF14) |
| Fase 5 - Mapeamento `TryFindBy` no generator | Bloqueada pela Fase 4 |
| Fase 6 - Integração, documentação e preparação de rollout | Bloqueada pelas Fases 2-5 |

> **Manutenção deste plano:** marque uma tarefa com `- [x]` somente depois de verificar seu critério e registrar
> a evidência em `Resultado da Fase`. Ao concluir uma fase, atualize estado, barra, matriz de rastreabilidade e riscos.
> Este plano coordena mais de um repositório; antes de editar cada um, leia o `AGENTS.md` aplicável e preserve seus
> contratos de build, testes, versionamento e publicação.

---

## Contexto

### Fontes verificadas

- `.docs/templates/template-ai-implementation-plan.md` — define a estrutura e a rastreabilidade obrigatórias deste plano.
- `.ai/plans/plan-smartcommands-correcoes-melhorias.md` — DF23 limita a Fase 10 a filtros, `Ok`/`Created`/`NoContent` e tags, diferindo `Accepted` e `TryFindBy` para este plano.
- `.docs/reviews/review-questao-4-plan-smartcommands-correcoes-melhorias-v1.md` — registra o direcionamento humano: 202 é útil para processamento assíncrono; `MapAcceptedRoute` deve ser análogo a `MapCreatedRoute`; não impor um tipo rígido de retorno; `OperationReference` exige desenho próprio; `TryFindBy` deve ser componentizado antes da integração HTTP.
- `RoyalCode.SmartCommands/MapFindAttribute.cs`, `IRepositoryAccessor.cs` e `RoyalCode.SmartCommands.Generators/Generators/FindInformation.cs` — `MapFind` atual busca exclusivamente por `Id<TEntity,TId>`, projeta para o tipo decorado e retorna `Ok`/`NotFound`.
- `RoyalCode.SmartCommands/MapCreatedRouteAttribute.cs` e `RoyalCode.SmartCommands.Generators/Generators/MapInformation.cs` — referência para placeholders nomeados, composição da `Location`, resposta e metadata.
- `../../SmartProblems/src/RoyalCode.SmartProblems.ApiResults/HttpResults/CreatedMatch'0.cs`, `CreatedMatch'1.cs` e `NoContentMatch.cs` — padrões atuais de union HTTP para `Result`/`Result<T>` e problemas.
- `../../SmartProblems/src/RoyalCode.SmartProblems.EntityFramework/SmartProblemsEFExtensions.TryFind.cs` — já possui `TryFindByAsync` por expressão, seletor de propriedade e valor, com semântica `FirstOrDefaultAsync` e problema `NotFound`.
- `../../SmartProblems/src/RoyalCode.SmartProblems.EntityFramework/FindCriteria.cs` — composição EF de múltiplos critérios com AND e mensagem rica; carrega `IQueryable<TEntity>` e, portanto, não é contrato portável para o núcleo do SmartCommands.
- `../../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Repositories.Abstractions/DataServices.cs` e `RoyalCode.WorkContext.Abstractions/WorkContextExtensions.cs` — `IRepository<TEntity>`/`IWorkContext` já oferecem busca por expressão e por seletor de propriedade, além da busca por ID.
- `RoyalCode.SmartCommands.WorkContext/Adapters/RepositoryAdapter.cs` — o adapter atual expõe ao SmartCommands somente as duas buscas por ID, apesar das capacidades adicionais do repositório subjacente.
- `../../Searches/src/RoyalCode.SmartSearch.Abstractions/ICriteria.cs` — oferece filtros, projeção e materialização rica, mas adicionaria uma abstração e dependência maior do que a busca simples por igualdade exige.
- [RFC 9110, status 202](https://www.rfc-editor.org/rfc/rfc9110.html#name-202-accepted) — 202 informa que o processamento foi aceito, mas ainda não concluído; a representação deve descrever o estado e apontar para monitoramento quando aplicável, sem tornar um formato específico obrigatório.

### Estado atual do código (verificado em 2026-07-17)

- Não existe `AcceptedMatch`/`AcceptedMatch<T>` no SmartProblems ApiResults.
- Não existe `HttpResultStatus.Accepted` nem `MapAcceptedRoute` no SmartCommands; a Fase 10 do plano principal já estabilizou `HttpResultStatus` com `Ok`, `Created` e `NoContent`.
- `CreatedMatch` possui variantes com e sem valor, inclui `Location` e delega problemas para `MatchErrorResult`; é a referência estrutural mais próxima de `Accepted`.
- O comando pode retornar `Result` ou `Result<T>`; 202 não exige que `T` seja um identificador, URL ou modelo de operação específico.
- `TryFindByAsync` já está implementado e testado na borda EF do SmartProblems para um predicado, um seletor de propriedade e critérios compostos.
- O WorkContext já consegue buscar entidade por expressão/seletor através de `IRepository<TEntity>`, mas o `IRepositoryAccessor<TEntity>` do SmartCommands não expõe essas formas.
- A projeção de `MapFind` por ID existe nos adapters. Não foi localizada uma operação portável equivalente que combine predicado/chave alternativa com projeção `TEntity -> TDto` em todos os adapters.
- `FindCriteria<TEntity>` do SmartProblems é útil na implementação EF, mas não deve vazar para `RoyalCode.SmartCommands`, pois contém consulta EF/IQueryable.
- `ICriteria<TEntity>` do SmartSearch cobre cenários muito mais amplos; sua adoção para uma busca de igualdade simples não está justificada no estado atual.

### Lacunas, conflitos e restrições

- O contrato declarativo de uma chave alternativa/composta ainda precisa casar propriedades da entidade, parâmetros da rota e tipos sem voltar a mapeamento posicional frágil.
- Preservar a projeção de `MapFind` pode exigir uma capacidade nova nos adapters ou nas abstrações de repositório; retornar somente a entidade reduziria completude e poderia expor o domínio por HTTP.
- `FirstOrDefault` tolera dados duplicados; `SingleOrDefault` detecta violação de unicidade, mas exige uma categoria de erro e comportamento comum entre providers.
- `Accepted` atravessa SmartProblems e SmartCommands. O consumo continuará por pacote pinado, logo cada etapa externa exige release explícita antes de atualizar o próximo repositório.
- O plano não autoriza publicação, mudança de versão, commit, push ou CI/CD. Esses gates serão registrados, mas só executados mediante pedido do mantenedor.
- Validações gerais de sintaxe/precedência de rota pertencem ao ASP.NET Core; o generator valida apenas os nomes e vínculos que ele próprio interpreta.

### Superfícies impactadas a mapear

| Superfície | Repositório/projeto | Impacto esperado |
|---|---|---|
| Unions HTTP | SmartProblems / `RoyalCode.SmartProblems.ApiResults` | `AcceptedMatch` e `AcceptedMatch<T>`, metadata 202, corpo e `Location` opcionais |
| Core de resultados | SmartProblems / `RoyalCode.SmartProblems` | extração reutilizável de critérios (DF14) e resultado projetado com identidade da entidade (DFs 15-16) |
| Contratos públicos HTTP | SmartCommands / runtime | `HttpResultStatus.Accepted` e `MapAcceptedRouteAttribute` |
| Runtime de persistência | SmartCommands / runtime | eventual extensão de `IRepositoryAccessor<TEntity>` para chave alternativa/composta |
| Adapter EF | SmartCommands.EntityFramework | execução/projeção de busca por critérios sem vazar EF para o núcleo |
| Adapter WorkContext | SmartCommands.WorkContext e EnterprisePatterns | reaproveitamento de `IRepository<TEntity>` e extensão mínima para projeção por predicado |
| Generator/analyzer | SmartCommands.Generators | leitura semântica, modelos incrementais, diagnósticos, emissão e OpenAPI |
| Demo e testes | SmartCommands.Demo, Demo.Tests e testes do generator | processamento assíncrono, consulta por chave simples/composta e casos negativos |
| Documentação/pacotes | repositórios envolvidos | XML docs, guias, analyzer releases, notas de quebra e ordem de releases |

---

## Objetivo

Adicionar suporte coerente a respostas HTTP 202 e a consultas de uma entidade por chave alternativa ou composta,
sem impor um modelo de operação ao domínio, sem acoplar o núcleo a EF/SmartSearch e sem duplicar capacidades já
existentes. A entrega deve preservar `Result`/`Result<T>`, problemas padronizados, projeção segura, cancelamento,
metadata OpenAPI, incrementalidade do generator e paridade entre os adapters EF e WorkContext.

---

## Fora de escopo

- Definir um contrato universal de job, fila, polling, callback, webhook ou estado de operação.
- Introduzir `OperationReference` neste ciclo; ele permanece candidato independente depois de existir caso de uso concreto em mais de um pacote.
- Form/file/upload/streaming assistido, conforme DF23 do plano principal.
- Tornar SmartSearch dependência obrigatória do SmartCommands sem evidência da Fase 1 e nova decisão humana.
- Criar parser geral de rotas ou revalidar regras já pertencentes ao ASP.NET Core.
- Publicar pacotes, alterar versões, workflows, secrets, branches, commits ou tags sem autorização explícita.
- Preservar uma API substituída por aliases ou `[Obsolete]` caso a investigação ainda encontre uma breaking change necessária.

---

## Perguntas ao humano

- **Q1 — Declaração de `TryFindBy`:** como o usuário deve relacionar propriedades da entidade aos valores de entrada?
  - **Opções:**
    - **A) Propriedades nomeadas no map (recomendada):** `[MapFindBy<TEntity>(route, name, nameof(TEntity.Prop1), ...)]`; cada propriedade casa, sem diferenciar caixa, com um placeholder de mesmo nome e o generator deriva o tipo. Alias exige uma futura API específica, não pares posicionais de strings.
    - **B) Tipo de filtro dedicado:** `[MapFindBy<TEntity, TFilter>(route, name)]`; o endpoint recebe `TFilter` por binding e uma abstração adicional transforma o filtro em critérios. É mais flexível, mas se aproxima de `MapSearch`/SmartSearch e amplia muito o contrato.
  - **Impacto se não decidir:** não é possível congelar o atributo, os snapshots nem a assinatura gerada da Fase 5.
  - **Status:** Respondida em 2026-07-18: opção A. Registrada na DF10.

- **Q2 — Projeção da busca alternativa:** o primeiro release precisa manter a mesma proteção de DTO do `MapFind`?
  - **Opções:**
    - **A) Projeção obrigatória (recomendada):** o tipo decorado continua sendo `TDto`; criar a menor capacidade portável de projeção por critérios nos adapters/abstrações, sem materializar a entidade antes da projeção quando o provider suportar tradução.
    - **B) Entidade somente no primeiro release:** expor apenas `FindResult<TEntity>` e adiar DTO. Reduz implementação, mas deixa `MapFindBy` funcionalmente inferior e facilita exposição acidental do domínio.
  - **Impacto se não decidir:** define se EnterprisePatterns/SmartSelector precisam evoluir e muda a superfície pública de `IRepositoryAccessor<TEntity>`.
  - **Status:** Respondida em 2026-07-18: opção A. Registrada na DF11.

- **Q3 — Semântica de unicidade:** como tratar mais de uma entidade para uma chave declarada como alternativa/composta?
  - **Opções:**
    - **A) `FirstOrDefault` (recomendada):** manter a semântica já existente em SmartProblems e WorkContext; unicidade é invariante do modelo/banco, e o pacote retorna a primeira correspondência.
    - **B) `SingleOrDefault`:** detectar duplicidade, mas definir um novo problema/erro consistente e pagar o custo/comportamento do provider em todos os adapters.
  - **Impacto se não decidir:** impede fechar o contrato runtime, os testes de duplicidade e a documentação operacional.
  - **Status:** Respondida em 2026-07-18: opção A. Registrada na DF12.

- **Q4 — Dono da projeção por predicado no EnterprisePatterns:** a sonda da Fase 1 confirmou que `IFinder<TEntity>` tem entidade-por-predicado e DTO-por-ID, mas não DTO-por-predicado.
  - **Opções:** A) ampliar o próprio `IFinder<TEntity>` (consistente com o padrão atual; breaking para implementações externas de `IRepository<TEntity>`); B) interface nova separada herdada por `IRepository<TEntity>` (mesma quebra, abstração a mais).
  - **Status:** Respondida em 2026-07-18: opção A. Registrada na DF13.

- **Q5 — Superfície do novo método de projeção:** somente a variante explícita com `IReadOnlyList<FindCriterion>` ou também uma sobrecarga de conveniência sem critérios?
  - **Opções:** A) só a explícita; B) explícita + conveniência com problema gerado por análise best-effort da expressão (paridade com `TryFindByAsync` do SmartProblems EF).
  - **Status:** Respondida em 2026-07-18: opção B. Registrada na DF14.

- **Q6 — Nome usado no problema `NotFound` da projeção:** a sonda mostrou que `FindResult<TDto>` nomeia o DTO na mensagem, consistente com o `MapFind` por ID atual.
  - **Opções:** A) manter nome do DTO (paridade com o comportamento atual); B) usar o nome da entidade (semanticamente correto; a variante por predicado é implementável só no EnterprisePatterns, gerando o `Problem` via `FindResult<TEntity>.Problem(criteria)` e embrulhando em `FindResult<TDto>`).
  - **Sub-decisão (alcance):** mudar só o caminho novo ou alinhar também o `MapFind` por ID (`FindResult<TDto,TId>`), que exige adição no SmartProblems.
  - **Status:** Respondida em 2026-07-18: opção B, com alinhamento do caminho por ID também. Registrada nas DF15-DF16.

---

## Decisões fechadas

- **DF1 — 202 não impõe modelo de sucesso:** command handlers continuam retornando `Result` ou `Result<T>` segundo o caso de uso; o suporte HTTP não exige `OperationReference`, ID ou URL como `T`. Fonte: direcionamento humano da Q4 e semântica do RFC 9110.
- **DF2 — `Accepted` com ou sem `Location`:** `WithResultStatus(HttpResultStatus.Accepted)` seleciona 202 sem exigir rota; `MapAcceptedRoute` é opcional, implica 202 e monta `Location` por placeholders nomeados, de modo análogo ao `MapCreatedRoute`. Fonte: direcionamento humano.
- **DF3 — Matches pertencem ao SmartProblems:** criar `AcceptedMatch` e `AcceptedMatch<T>` no ApiResults, preservando `MatchErrorResult`, metadata 202 e corpo somente quando houver valor. SmartCommands consome a release correspondente; não duplica a union no código gerado. Fonte: arquitetura atual dos pacotes e direcionamento humano.
- **DF4 — `OperationReference` diferido:** a possível integração com um modelo de operação será avaliada em plano próprio; `Accepted` não cria dependência dele. Fonte: direcionamento humano.
- **DF5 — Composição antes de abstração nova:** `TryFindBy` deve reutilizar SmartProblems/EF e `IRepository<TEntity>`/WorkContext; SmartSearch só entra se a Fase 1 demonstrar uma lacuna que essas capacidades não resolvem e houver decisão humana explícita. Fonte: direcionamento humano e inspeção do código.
- **DF6 — Critérios portáveis no núcleo:** `FindCriteria<TEntity>` não aparece em contratos do SmartCommands, pois contém `IQueryable<TEntity>`/EF. Expressões e descritores de critérios portáveis podem ser usados; execução específica fica nos adapters. Fonte: inspeção do código.
- **DF7 — Rotas na fronteira correta:** o generator valida apenas correspondência, existência, duplicidade, tipo e acessibilidade das propriedades/placeholders que interpreta; sintaxe geral e seleção do endpoint permanecem no ASP.NET Core. Fonte: DF22 do plano principal.
- **DF8 — Cancelamento e problemas preservados:** toda operação async recebe `CancellationToken`; cancelamento não vira 202/404/500 padronizado, e falhas de `Result`/`Result<T>` nunca são descartadas por seleção de status. Fonte: invariantes do repositório.
- **DF9 — Mudanças públicas documentadas diretamente:** qualquer quebra aprovada é aplicada sem shim `[Obsolete]`, com XML docs, testes e nota de migração. Fonte: política do mantenedor no plano principal.
- **DF10 — `MapFindBy` declara propriedades nomeadas:** adotar `[MapFindBy<TEntity>(route, name, nameof(TEntity.Prop1), ...)]`; cada propriedade direta da entidade casa, sem diferenciar caixa, com um placeholder de mesmo nome, e o generator deriva semanticamente o tipo do parâmetro. Alias explícito fica diferido; não usar pares posicionais de strings nem tipo de filtro dedicado neste ciclo. Fonte: resposta humana à Q1 em 2026-07-18.
- **DF11 — Projeção para DTO é obrigatória:** o tipo decorado por `MapFindBy` permanece `TDto`; EF e WorkContext devem projetar no provider, sem materializar primeiro a entidade nem expor o domínio por HTTP. A lacuna de projeção por predicado será fechada no EnterprisePatterns antes de o SmartCommands consumir a nova capacidade; SmartSelector não recebe uma feature específica de `TryFindBy`. Fonte: resposta humana à Q2 em 2026-07-18 e inspeção das capacidades existentes.
- **DF12 — Busca usa `FirstOrDefault`:** manter a semântica atual de SmartProblems e WorkContext. Unicidade é invariante do modelo/banco; ocorrências duplicadas não criam uma nova categoria de problema neste ciclo, e os testes devem documentar que, sem ordenação explícita, o contrato não promete qual duplicata será retornada. Fonte: resposta humana à Q3 em 2026-07-18.
- **DF13 — Projeção por predicado entra em `IFinder<TEntity>`:** os novos métodos são adicionados ao próprio `IFinder<TEntity>` no `RoyalCode.Repositories.Abstractions`, seguindo o padrão de todas as buscas existentes. É breaking para implementações externas de `IRepository<TEntity>` e deve ser tratado explicitamente no rollout do pacote de persistência (DF9: sem shim `[Obsolete]`). Fonte: resposta humana à Q4 em 2026-07-18.
- **DF14 — Assinatura explícita + conveniência:** a superfície nova é `Task<FindResult<TDto>> FindAsync<TDto>(Expression<Func<TEntity, bool>> filter, IReadOnlyList<FindCriterion> criteria, CancellationToken ct = default) where TDto : class`, mais a sobrecarga sem `criteria` cujo problema `NotFound` é gerado por análise best-effort da expressão (mesma semântica do `TryFindByAsync` do SmartProblems EF). `FindCriterion` pertence ao core do SmartProblems, já dependência do Repositories.Abstractions — nenhuma dependência nova. O generator do SmartCommands consome somente a variante explícita. A análise best-effort da expressão é exposta pelo core do SmartProblems como extração reutilizável de critérios (ex.: `ExtractCriteria<TEntity>(Expression<Func<TEntity, bool>>) -> FindCriterion[]`), preservando o contrato tudo-ou-nada atual (folha não reconhecida degrada tudo, sem lançar); o SmartProblems EF passa a delegar para ela e o EnterprisePatterns a consome — a lógica hoje privada no pacote EF não é duplicada nem devolve `FindResult` pré-categorizado. Fonte: resposta humana à Q5 em 2026-07-18, sonda D1-D4 da Fase 1 e revisão humana da Fase 1 em 2026-07-18.
- **DF15 — `NotFound` da projeção nomeia a entidade:** a busca por predicado com projeção nomeia a entidade, não o DTO. A mecânica inicialmente registrada (embrulhar um `Problem` pronto em `FindResult<TDto>`) foi descartada na revisão da Fase 1: `HasInvalidParameter` devolve `this.problem ?? ...` e entregaria o problema armazenado com a categoria errada. O resultado projetado preserva um descritor do alvo da busca — display name e nome técnico da entidade mais critérios normalizados (`ByName` resolvido previamente contra a entidade, ou informação suficiente para consultar `DisplayNames`) — e gera `NotFound` e `InvalidParameter` lazily com as categorias corretas. Factory candidata: `FindResult<TDto>.ProjectedFrom<TEntity>(TDto? value, IReadOnlyList<FindCriterion> criteria)`. Exige adição no core do SmartProblems (Fase 2), consumida pelo EnterprisePatterns. Fonte: resposta humana à Q6 em 2026-07-18, sonda D4 e revisão humana da Fase 1 em 2026-07-18.
- **DF16 — Caminho por ID alinhado à entidade:** o `TryFindAsync<TEntity, TDto, TId>` do EnterprisePatterns (usado pelo `MapFind` por ID) também passa a nomear a entidade. Como `FindResult<TDto, TId>` gera `NotFound` e `HasInvalidParameter` lazily com `typeof(TDto)` e recebe `parameterName` somente na chamada, um `Problem` pré-construído não basta: o resultado preserva a identidade da entidade original (o descritor da DF15, com o ID no lugar dos critérios) para gerar as duas categorias, os detalhes e as extension data `entity`/`id` corretamente. Factory candidata: `FindResult<TDto, TId>.ProjectedFrom<TEntity>(TDto? value, TId id)`. Entra na mesma release do SmartProblems que o `AcceptedMatch`, com a mudança de mensagem registrada na nota de comportamento. Testes mínimos: `NotFound`, `HasInvalidParameter`, `ToResult`, extension data, `[DisplayName]` na entidade, resultado encontrado sem mudança comportamental e o quirk atual de `FindResult<TEntity>(Problem)`. Fonte: resposta humana à sub-decisão da Q6 em 2026-07-18 e revisão humana da Fase 1 em 2026-07-18.

---

## Histórico de decisões

- Q4 do plano principal comparou uma opção HTTP mínima com um pacote ampliado. O mantenedor selecionou a opção A para concluir filtros/status/tags sem misturar casos ainda imaturos.
  - **Conclusão:** `Accepted` e `TryFindBy` foram explicitamente movidos para este plano; form/file/stream permaneceram fora.
- Foi proposta integração imediata de 202 com um tipo `OperationReference`.
  - **Conclusão:** SUPERSEDED pela DF1/DF4; 202 permanece agnóstico ao modelo, e a referência de operação poderá ser uma feature consumidora futura.
- Foi considerada a adoção direta de SmartSearch para busca alternativa/composta.
  - **Conclusão:** SUPERSEDED parcialmente pela DF5; primeiro serão compostas as capacidades mais simples já existentes, mantendo SmartSearch como hipótese condicionada a evidência.
- Q1 comparou propriedades nomeadas no map com um tipo de filtro dedicado.
  - **Conclusão:** selecionada a opção A; propriedades e placeholders casam por nome conforme DF10.
- Q2 comparou projeção obrigatória com exposição inicial da entidade.
  - **Conclusão:** selecionada a opção A; DTO é obrigatório e a projeção ocorre no provider conforme DF11.
- Q3 comparou `FirstOrDefault` com `SingleOrDefault`.
  - **Conclusão:** selecionada a opção A; manter `FirstOrDefault` e tratar unicidade no modelo/banco conforme DF12.
- Q4 comparou ampliar `IFinder<TEntity>` com criar interface separada para a projeção por predicado.
  - **Conclusão:** selecionada a opção A; a capacidade entra no `IFinder<TEntity>` conforme DF13.
- Q5 comparou expor apenas a variante explícita com adicionar a sobrecarga de conveniência sem critérios.
  - **Conclusão:** selecionada a opção B; ambas as sobrecargas existem conforme DF14, e o generator usa a explícita.
- Q6 comparou nomear o DTO ou a entidade no problema `NotFound`, com sub-decisão sobre alinhar o caminho por ID.
  - **Conclusão:** selecionada a opção B com alinhamento do por ID; DF15 cobre o caminho novo e DF16 amplia o escopo da release do SmartProblems.
- A revisão humana da entrega da Fase 1 (2026-07-18) confirmou cinco achados sobre o plano e o código, validados contra os fontes.
  - **Conclusão:** a matriz de `Accepted` distingue rota estática de placeholders para `Result`; a análise best-effort vira extração de critérios no core do SmartProblems (DF14 emendada); DF15/DF16 passam a exigir descritor da entidade com categorias lazy, no lugar de `Problem` pré-construído (mecânica anterior SUPERSEDED); a validação de constraints fica na fronteira da DF7; o defeito latente do `MapCreatedRoute` com `Result` e rota estática (emissor gera lambda sem sobrecarga correspondente) foi registrado à parte em `.docs/reviews/issue-mapcreatedroute-result-rota-estatica-2026-07-18.md`.

---

## Design alvo

### Contratos e bordas

- `HttpResultStatus` recebe `Accepted` estendendo o enum e a leitura semântica estabilizados pela Fase 10 do plano principal.
- `MapAcceptedRouteAttribute` recebe pattern e nomes de propriedades do valor de sucesso, usando placeholders nomeados e as mesmas regras seguras do `MapCreatedRoute`.
- `WithResultStatus(Accepted)` e `MapAcceptedRoute` são combinações compatíveis; `MapAcceptedRoute` com `Ok`, `Created` ou `NoContent` é diagnosticado e não gera endpoint.
- `Result` produz 202 sem corpo; `Result<T>` produz 202 com `T`. Quando houver `MapAcceptedRoute`, `Location` é adicionado sem alterar o corpo determinado pelo retorno.
- A API de `TryFindBy` usa propriedades nomeadas conforme DF10. Ela não aceita property names soltos sem validação semântica nem pares posicionais frágeis.
- O tipo exposto pela Minimal API é DTO conforme DF11; entity tracking e detalhes de provider não atravessam a borda HTTP.

### Modelo, dados e persistência

- A busca simples usa igualdade entre valor de entrada e propriedade direta da entidade; composição usa AND e preserva a ordem declarada para produzir o problema `NotFound`.
- O generator deriva tipos das propriedades da entidade e verifica conversibilidade dos parâmetros antes de emitir código.
- O núcleo descreve a intenção com contratos portáveis. EF pode usar `TryFindByAsync`/`FindCriteria`; WorkContext delega a `IRepository<TEntity>`.
- A ausência confirmada de projeção portátil por predicado será fechada pela menor extensão necessária no EnterprisePatterns, com testes dos providers, antes de alterar `IRepositoryAccessor<TEntity>`.
- Não materializar coleção para escolher um item; executar operação single-result diretamente no provider com `CancellationToken`.

### Arquitetura alvo

```text
Command/DTO decorado
   ├── WithResultStatus(Accepted) ───────────────┐
   ├── MapAcceptedRoute (opcional / Location) ──┼─> generator ─> AcceptedMatch[/<T>]
   └── MapFindBy + propriedades nomeadas ───────┘                 │
                                                                  ├─> SmartProblems.ApiResults
MapFindBy ─> IRepositoryAccessor<TEntity> ─────────────────────────┤
             ├── EntityFramework adapter ─> TryFindBy/projeção    │
             └── WorkContext adapter ─> IRepository/projeção ─────┘
```

- Não criar um generator paralelo: os novos modelos participam das mesmas agregações, regras de nome, diagnósticos e emissão compartilhada dos maps atuais.
- Cada repositório continua dono de sua abstração: SmartProblems das unions/problemas; EnterprisePatterns de repositório/WorkContext; SmartCommands da orquestração e geração.

### Segurança, concorrência e confiabilidade

- 202 só é emitido quando o handler conclui com `Result` de sucesso; aceitar a solicitação não converte exceção, cancelamento ou problema em sucesso.
- `Location` deve ser construída por valores escapados/formatados pela infraestrutura apropriada; não concatenar entrada não codificada em headers.
- A busca respeita filtros de segurança/tenant já aplicados pelo provider ou pelo contexto; o generator não contorna query filters.
- Chaves alternativas declaradas devem ser documentadas como únicas conforme DF12; o banco continua sendo a autoridade para a constraint.
- Nenhuma mensagem de provider/exceção é exposta no problema `NotFound` ou na resposta 202.

### Compatibilidade, migração e rollout

- Ordem prevista de pacotes: SmartProblems ApiResults -> EnterprisePatterns com projeção por predicado -> SmartCommands runtime/generator/adapters. A dependência da Fase 10 do plano principal já está satisfeita.
- Toda atualização de `PackageReference` ocorre somente após a versão correspondente estar publicada e confirmada pelo mantenedor.
- Validar os pacotes SmartCommands em net8/net9/net10 e o generator em netstandard2.0; testes/Demo permanecem em net10.0 conforme o repositório.
- Não publicar durante a execução técnica deste plano. A Fase 6 produz a lista de versões/dependências e aguarda autorização para cada release.

---

## Ordem de execução

1. **Fase 1:** medir as capacidades existentes, criar sondas e validar as assinaturas decorrentes das DFs 10-12 sem mudar contratos.
2. **Fase 2:** adicionar e validar `AcceptedMatch` no repositório dono da union HTTP, junto com a extração de critérios (DF14) e o resultado projetado (DFs 15-16) no core.
3. **Fase 3:** integrar 202 ao enum, atributo, generator, Demo e OpenAPI após a Fase 10 principal.
4. **Fase 4:** implementar a menor abstração portável de busca alternativa/composta e paridade dos adapters.
5. **Fase 5:** expor a busca por atributo e geração, com diagnósticos e testes incrementais/HTTP.
6. **Fase 6:** executar matriz cruzada, atualizar documentação e preparar a ordem de rollout sem publicar.

---

## Fase 1 - Baseline, viabilidade e decisões de contrato

**Depende de:** DF1-DF12; acesso somente leitura aos repositórios SmartProblems, EnterprisePatterns, Searches e SmartCommands.

**Escopo:** inventário de APIs, protótipos descartáveis/testes de caracterização, matriz de casos e validação das assinaturas decorrentes das DFs 10-12.

**O que/como:** comprovar quais capacidades de expressão, projeção, composição e metadata já existem em cada provider. Não alterar API pública antes de comparar as assinaturas geradas e o custo entre EF e WorkContext.

**Tarefas:**

- [x] Registrar commits/status, versões de pacotes e baselines de build/test dos repositórios potencialmente impactados.
- [x] Montar matriz de `Accepted` para `Result`/`Result<T>`, com/sem `Location`, sucesso/problema/cancelamento, resposta HTTP e OpenAPI esperados.
- [x] Prototipar a declaração da DF10 somente em teste/rascunho e validar legibilidade, chave composta, tipos nullable, constraints de rota e diagnósticos possíveis.
- [x] Verificar projeção por predicado nos adapters EF/WorkContext e especificar precisamente a menor extensão necessária no EnterprisePatterns para cumprir a DF11.
- [x] Caracterizar a semântica e o custo de `FirstOrDefault` nos providers relevantes, incluindo a ausência de garantia sobre qual registro duplicado é retornado, conforme DF12.
- [x] Confirmar se SmartSearch oferece benefício necessário além de filtro/seleção simples; registrar evidência antes de propor dependência.
- [x] Obter resposta humana para Q1-Q3 e convertê-las nas DFs 10-12 antes de iniciar as Fases 4-5.

**Critérios de aceite:** matriz reproduzível e assinaturas candidatas registradas; nenhuma API pública alterada; DFs 10-12 validadas por sondas; dono de cada mudança entre repositórios identificado; nenhuma dependência em SmartSearch introduzida por conveniência.

**Testes:** baselines dos projetos envolvidos; sondas de compilação para atributos/assinaturas; consultas SQLite para simples/composta/duplicada/projeção/cancelamento; inspeção de expression trees sem benchmark prematuro.

### Resultado da Fase 1

**Concluída em 2026-07-18.** Nenhuma API pública foi alterada. Evidência: inspeção de código, builds/testes de baseline e uma sonda executável descartável (console net10.0 + SQLite in-memory, em scratchpad fora dos repositórios) que referencia o projeto local `RoyalCode.SmartProblems.EntityFramework`. Respostas humanas de Q4-Q6 convertidas nas DFs 13-16.

**Baselines registrados (2026-07-18):**

| Repositório | Branch @ commit | Build (Release) | Testes |
|---|---|---|---|
| SmartCommands | `main` @ `59af7d0`, limpo | `SmartCommands.sln`: 0 erros; 3 warnings NU5104 pré-existentes (dependências preview pinadas) | `RoyalCode.SmartCommands.Tests` 333/333; `Demo.Tests` 83/83 |
| SmartProblems | `main` @ `81207e4`, limpo | `SmartProblems.sln`: 0 erros; 8 warnings xUnit1031 pré-existentes (só em testes) | 410/410 |
| EnterprisePatterns | `releases/unit-of-work` @ `4f5ad89`, limpo | Solução completa falha (pré-existente, sem relação com o plano): `RoyalCode.Commands.Tests` referencia projetos `Commands` removidos (4 erros CS0246/CS0234). A cadeia relevante (`RoyalCode.Persistence.Tests` + Repositories/WorkContext EF) compila com 0 erros e 0 warnings | `Persistence.Tests` 21/21 |
| Searches | `feature/operator-expression-customization` @ `feaf89a`; 2 docs modificados | não construído (somente leitura; nenhuma mudança prevista) | — |

Versões pinadas no `src/Directory.Build.props` do SmartCommands: SmartProblems/SmartValidations `1.0.0-preview-7.0`, WorkContext `0.9.0`, SmartSearch `0.11.0`, SmartSelector `0.5.2`, Extensions.SourceGenerator `0.4.0`. EnterprisePatterns publica `PersistVer 0.9.0` e consome `ProbVer 1.0.0-preview-7.0`.

**Matriz de `Accepted` (guia das Fases 2-3):**

| Retorno | `MapAcceptedRoute` | Sucesso | Problema | Cancelamento | OpenAPI esperado |
|---|---|---|---|---|---|
| `Result` | ausente | 202 sem corpo, sem `Location` | `MatchErrorResult` (status do problema) | `OperationCanceledException` propaga; nunca vira 202 | 202 sem content-type + metadata de problemas |
| `Result` | presente (rota estática) | 202 sem corpo, `Location` fixa — `Result` não tem valor de sucesso, logo o pattern não pode conter placeholders; placeholder com `Result` gera diagnóstico (mesma regra do RCCMD050 atual) | idem | idem | idem |
| `Result<T>` | ausente | 202 com corpo JSON `T` | idem | idem | 202 `application/json` tipado `T` + problemas |
| `Result<T>` | presente | 202 com corpo `T` e `Location` montada por placeholders das propriedades de `T` | idem | idem | idem |

Assinaturas candidatas (validadas contra a estrutura de `CreatedMatch'0/'1` e `NoContentMatch`): `AcceptedMatch(Result result, string? location = null)`, `AcceptedMatch<T>(Result<T> result, string? location = null)`, com conversões implícitas de `Accepted`/`Accepted<T>`/`MatchErrorResult`/`Problem`/`Problems` e `PopulateMetadata` com `ResponseTypeMetadata(202)` + `MatchErrorResult.PopulateMetadata`. Nota: diferentemente de `CreatedMatch`, a `Location` é opcional.

**Sonda DF10 (compilação):** o atributo rascunho `MapFindByAttribute<TEntity>(route, name, params string[] propertiesNames)` compila e lê bem nos três cenários: chave simples (`nameof(Product.Sku)` + `{sku}`), chave composta com constraint (`{region}/{code:int}` + `nameof(Store.Region)`, `nameof(Store.Code)`) e propriedade nullable (`Product.Ean` `string?`). Diagnósticos candidatos identificados para a Fase 5: placeholder sem propriedade correspondente, propriedade não declarada/duplicada/inacessível/não direta e nulabilidade que afete a própria emissão. O generator deriva o tipo do parâmetro da propriedade da entidade e valida somente os vínculos que ele próprio interpreta; a semântica das constraints de rota (`:int`, customizadas, defaults) permanece sob autoridade do ASP.NET Core, conforme DF7 — diferentemente de `MapCreatedRoute`/`MapAcceptedRoute`, cujos patterns são templates de `Location` e por isso restringem placeholders ao formato simples.

**Projeção por predicado (DF11):** confirmado que nenhum adapter possui a capacidade: `IRepositoryAccessor<TEntity>` do SmartCommands expõe apenas as duas buscas por ID; `IFinder<TEntity>` do EnterprisePatterns tem entidade-por-predicado, entidade-por-propriedade e DTO-por-ID (`SelectDtoById` + `EFExtensions.TryFindAsync<TEntity,TDto,TId>`), mas não DTO-por-predicado. A menor extensão foi especificada e validada pela sonda (seções D e E): `Where(filter).Select(selector).FirstOrDefaultAsync(ct)` executa em consulta única, com SELECT contendo apenas as colunas do DTO, zero entidades rastreadas (`ChangeTracker` vazio) e query filter global preservado (linha de outro tenant não encontrada). O contrato definitivo está nas DFs 13-15; o seletor `TEntity -> TDto` vem do `ISelectorFactory` já usado por `SelectDtoById`, que deve ser generalizado (base `SelectDto<TEntity,TDto>`) sem feature nova no SmartSelector.

**`FirstOrDefault` e duplicidade (DF12):** com duas linhas de mesmo `Sku`, `TryFindByAsync` retorna a primeira correspondência sem erro; o SQL gerado não contém `ORDER BY`, comprovando que qual duplicata retorna é indefinido pelo contrato. `FindCriteria` composta aplica AND, preserva a ordem declarada na mensagem (`with Region 'leste', Code '99'`) e aceita valor de propriedade nullable. Cancelamento: token cancelado propaga `OperationCanceledException` tanto no `TryFindByAsync` quanto na projeção candidata.

**Achado da sonda (origem das Q6/DF15-DF16):** `FindResult<TDto>.Problem(criteria)` e `FindResult<TDto,TId>.NotFound` nomeiam o tipo genérico — o DTO — na mensagem (`The record of 'ProductDetails' with Sku 'SKU-X' was not found`). Decisão humana: nomear a entidade nos dois caminhos (DF15 para o predicado, DF16 para o por ID, com adição no SmartProblems).

**SmartSearch (DF5):** evidência registrada contra a adoção: `ICriteria<TEntity>` (Abstractions) é um pipeline completo de busca — filter-objects com atributos (`CriterionAttribute`), ordenação, paginação, `ISearchManager` — e não produz `FindResult`/problema `NotFound`. Para igualdade por chave alternativa, as capacidades compostas de SmartProblems EF + `IFinder<TEntity>` bastam; SmartSearch adicionaria dependência e cerimônia sem contribuir a semântica de problema. Nenhuma dependência introduzida.

**Donos identificados por mudança:** `AcceptedMatch`/`AcceptedMatch<T>`, extração de critérios (DF14) e resultado projetado (DFs 15-16) → SmartProblems (mesma release); `IFinder<TEntity>` + implementação EF + generalização de `SelectDtoById` → EnterprisePatterns (breaking de `PersistVer`, rollout documentado); atributo, `IRepositoryAccessor<TEntity>`, adapters e generator → SmartCommands. SmartSelector e Searches: nenhuma mudança para este plano.

**Revisão da Fase 1 (2026-07-18):** a entrega foi revisada por humano e os cinco achados foram validados contra os fontes: (1) a matriz de `Accepted` foi corrigida para distinguir rota estática de placeholders em `Result`, e a validação foi confirmada como já existente no `ValidateCreatedRoute` (RCCMD050); durante a validação foi confirmado um defeito latente pré-existente — o emissor de `MapCreatedRoute` gera sempre lambda, mas `Result` puro só tem sobrecarga `string`, então `Result` + rota estática produz código que não compila — registrado à parte em `.docs/reviews/issue-mapcreatedroute-result-rota-estatica-2026-07-18.md`; (2) a análise best-effort é livre de EF e foi promovida a extração de critérios no core (DF14 emendada); (3) `NotFound`/`HasInvalidParameter` lazy com `typeof(TDto)` e o quirk `this.problem ?? ...` invalidaram a mecânica de `Problem` pré-construído — DFs 15-16 reescritas para o descritor da entidade com factories `ProjectedFrom<TEntity>`; (4) o diagnóstico candidato de constraint foi reescrito para a fronteira da DF7; (5) dependências e critérios das Fases 2-5 e globais foram alinhados às DFs 10-16.

---

## Fase 2 - Resultados HTTP `Accepted` no SmartProblems

**Depende de:** Fase 1 e disponibilidade para coordenar uma release do SmartProblems.

**Escopo:** `RoyalCode.SmartProblems.ApiResults`, testes, XML docs e documentação do SmartProblems.

**O que/como:** implementar unions 202 seguindo `CreatedMatch`/`NoContentMatch`, mas sem exigir `Location` nem formato específico do valor de sucesso.

**Tarefas:**

- [ ] Criar `AcceptedMatch` para `Result`, `Accepted`, `MatchErrorResult`, `Problem` e `Problems`, com `Location` opcional.
- [ ] Criar `AcceptedMatch<T>` para `Result<T>`/`Accepted<T>`, preservando corpo `T`, problemas e conversão segura para a variante não genérica quando necessária.
- [ ] Declarar metadata 202 correta com e sem `T`, sem content type para a variante sem corpo e com JSON para `T`.
- [ ] Validar argumentos públicos (`Location`, `IResult`, delegates se existirem) e documentar todas as APIs públicas com XML docs.
- [ ] Criar testes diretos de execução HTTP e metadata para sucesso, problema, `Location` ausente/presente e tipo genérico.
- [ ] Expor no core a extração reutilizável de critérios de expressão (DF14), preservando o contrato tudo-ou-nada, com o SmartProblems EF delegando para ela sem mudança de comportamento.
- [ ] Implementar o descritor/factories de resultado projetado das DFs 15-16 (`ProjectedFrom<TEntity>`), preservando a identidade da entidade nas categorias `NotFound` e `InvalidParameter`, com os testes mínimos da DF16 (incluindo o quirk atual de `FindResult<TEntity>(Problem)`) e nota de comportamento na mesma release.
- [ ] Atualizar documentação/notas do SmartProblems e preparar o gate de pacote sem publicar.

**Critérios de aceite:** matches executam 202 corretamente, não perdem problemas, não obrigam corpo/Location, anunciam metadata exata e passam em todos os TFMs suportados pelo SmartProblems; a extração de critérios (DF14) e o resultado projetado com identidade da entidade (DFs 15-16) integram a mesma release, com o comportamento do EF preservado; API pública e pacote estão prontos para revisão de release.

**Testes:** projeto de testes ApiResults/HTTP do SmartProblems; build/test `SmartProblems.sln -c Release`; pack e consumer-smoke locais se autorizados pelo fluxo do repositório.

### Resultado da Fase 2

*a preencher*

---

## Fase 3 - `Accepted` no SmartCommands e no generator

**Depende de:** Fases 1-2; Fase 10 do `plan-smartcommands-correcoes-melhorias.md` concluída; pacote SmartProblems com `AcceptedMatch` liberado e atualizado mediante confirmação do mantenedor.

**Escopo:** runtime, generator, diagnostics, Demo, Demo.Tests e documentação SmartCommands.

**O que/como:** estender o mecanismo de status já estabilizado, sem caminho especial paralelo e sem impor modelo de operação.

**Tarefas:**

- [ ] Adicionar `Accepted` a `HttpResultStatus` e cobrir igualdade/invalidação dos snapshots que carregam o enum.
- [ ] Criar `MapAcceptedRouteAttribute` com placeholders nomeados e validações equivalentes às invariantes próprias de `MapCreatedRoute`.
- [ ] Definir e testar os três casos de rota do `MapAcceptedRoute`: `Result` com rota estática (válido, emite `string`), `Result` com placeholders (diagnóstico) e `Result<T>` com placeholders das propriedades de `T`; coordenar com a correção do emissor de `MapCreatedRoute` registrada à parte, ao compartilhar ou espelhar a emissão.
- [ ] Emitir `AcceptedMatch`/`AcceptedMatch<T>` conforme retorno e `Location`, preservando inferência quando nenhum atributo explícito for usado.
- [ ] Diagnosticar conflitos com outros status/routes, propriedades ausentes/inacessíveis/incompatíveis e valores de enum desconhecidos sem gerar fonte parcial.
- [ ] Produzir `.Produces(202)`/metadata tipada coerente e manter a documentação de problemas.
- [ ] Criar Demo de comando enfileirado sem corpo e com DTO de acompanhamento, ambos com problemas e rota opcional.
- [ ] Atualizar XML docs, README, `.docs/commands.md`, catálogo RCCMD e AnalyzerReleases.

**Critérios de aceite:** 202 funciona para `Result` e `Result<T>` com/sem `Location`; conflitos produzem RCCMD localizado sem `CS8785`; runtime e OpenAPI concordam; nenhum tipo de operação é obrigatório; maps/status anteriores não regressam.

**Testes:** generator positivo/negativo e incremental; snapshots; WebApplicationFactory para status/body/header/problems/cancelamento; JSON OpenAPI; build/test padrão da solução.

### Resultado da Fase 3

*a preencher*

---

## Fase 4 - Contrato runtime e adapters de `TryFindBy`

**Depende de:** Fase 1 e DFs 10-16.

**Escopo:** contratos SmartCommands, adapters EF/WorkContext e a menor extensão necessária no EnterprisePatterns. SmartSelector não recebe funcionalidade específica de `TryFindBy`.

**O que/como:** expor uma única intenção portável de busca por critérios e implementar paridade real, reutilizando operações existentes. Evitar `IQueryable`, `DbContext`, `FindCriteria` ou SmartSearch no contrato do núcleo.

**Tarefas:**

- [ ] Definir assinatura(s) de `IRepositoryAccessor<TEntity>` conforme DFs 10-16, com XML docs, nullable correto e `CancellationToken` obrigatório.
- [ ] Representar múltiplos critérios e dados do `NotFound` sem reflection/string parsing em runtime quando o generator já conhece símbolos.
- [ ] Implementar adapter EF sobre `TryFindByAsync`/composição/projeção existente, preservando query filters e execução assíncrona no provider.
- [ ] Implementar adapter WorkContext sobre `IRepository<TEntity>`; a projeção por predicado é adicionada antes no EnterprisePatterns conforme DFs 13-15 (incluindo o alinhamento por ID da DF16), e a release é consumida sem fallback silencioso para materialização rastreada.
- [ ] Garantir paridade de simples/composta, null, conversões, not found, duplicidade conforme DF12, cancelamento e projeção conforme DF11.
- [ ] Documentar breaking changes e atualizar todos os fakes/implementações de `IRepositoryAccessor<TEntity>` na solução.

**Critérios de aceite:** núcleo permanece provider-agnostic; EF e WorkContext observam o mesmo contrato e resultado; não há N+1/materialização de coleção; projeção não expõe entidade conforme DF11; cancelamento e problemas são idênticos; APIs públicas possuem XML docs e validação.

**Testes:** unitários de contrato/fakes; SQLite real para EF; repositório fake e testes do WorkContext; SQL/log de consulta para comprovar single query/projeção; testes de cancelamento e duplicidade.

### Resultado da Fase 4

*a preencher*

---

## Fase 5 - Mapeamento `TryFindBy` no generator

**Depende de:** Fase 4 e DFs 10-16.

**Escopo:** atributos, leitura semântica, modelos incrementais, diagnósticos, emissão, Demo e testes HTTP/OpenAPI.

**O que/como:** gerar a assinatura do delegate a partir de propriedades e placeholders resolvidos semanticamente, reutilizando o pipeline comum de `MapFind` e as convenções de hint name/metadata.

**Tarefas:**

- [ ] Criar o atributo definido na DF10 com XML docs e exemplos simples/compostos.
- [ ] Resolver entidade, propriedades, placeholders, tipos, nulabilidade e acessibilidade via símbolos/TypedConstants; congelar snapshots symbol-free/equatáveis.
- [ ] Diagnosticar propriedade/placeholder ausente, duplicado, incompatível, não direto ou ambíguo; entrada inválida não chega a DI/emissão.
- [ ] Emitir chamada ao contrato da Fase 4, retorno DTO conforme DF11, `Ok`/`NotFound`, metadata e filtros/tags comuns.
- [ ] Preservar ordem determinística, nomes reservados e hint names da DF20; adicionar testes tracked de cache e invalidação seletiva.
- [ ] Criar casos Demo: SKU simples, chave composta e not found rico; testar rota/query conforme o contrato escolhido.
- [ ] Atualizar catálogo RCCMD, AnalyzerReleases, README e `.docs/commands.md`.

**Critérios de aceite:** simples e composta geram código compilável e determinístico; o tipo do delegate coincide com a propriedade da entidade; problemas listam critérios corretos; OpenAPI documenta parâmetros/respostas; toda entrada inválida conhecida produz diagnóstico, nunca `CS8785`; `MapFind` por ID permanece inalterado.

**Testes:** igualdade/incrementalidade; generator snapshots/diagnostics; compilação da saída; WebApplicationFactory e OpenAPI para sucesso/not found/conversão/constraints; build/test padrão.

### Resultado da Fase 5

*a preencher*

---

## Fase 6 - Integração, documentação e preparação de rollout

**Depende de:** Fases 2-5 concluídas e pacotes locais/versões candidatas disponíveis.

**Escopo:** matriz cruzada, todos os TFMs, pacotes locais, consumer-smoke, documentação e plano de releases.

**O que/como:** validar a cadeia como consumidor real por `.nupkg`, revisar API/generated diff e registrar a ordem necessária sem publicar automaticamente.

**Tarefas:**

- [ ] Executar build/test limpo de cada repositório alterado e registrar warnings contra seus baselines.
- [ ] Empacotar localmente SmartProblems, EnterprisePatterns e SmartCommands; consumir os `.nupkg` em projetos mínimos net8/net9/net10.
- [ ] Testar no mesmo consumer `Accepted`, busca simples/composta, EF, WorkContext, OpenAPI e coexistência com maps antigos.
- [ ] Revisar API pública e fontes geradas; documentar migração de qualquer assinatura quebrada e dependências mínimas por pacote.
- [ ] Atualizar todos os resultados de fase, matriz, riscos, diferidos e referências com evidência executada.
- [ ] Preparar ordem/versões/notas de release; aguardar autorização explícita para versionar, publicar ou atualizar dependentes remotos.

**Critérios de aceite:** suites verdes; nenhum warning novo; consumers reais compilam e executam nos TFMs anunciados; runtime/OpenAPI concordam; documentação apresenta casos positivos, limitações e migração; nenhuma publicação foi feita sem autorização.

**Testes:** builds/testes Release dos repositórios; pack local; consumer-smoke net8/net9/net10; testes HTTP/OpenAPI; inspeção de warnings, API diff e generated diff.

### Resultado da Fase 6

*a preencher*

---

## Matriz de rastreabilidade

| Objetivo | Fase(s) | Decisão/pergunta | Critério de aceite | Testes |
|---|---|---|---|---|
| 202 sem modelo rígido | 1-3, 6 | DF1-DF4 | `Result`/`Result<T>`, corpo e `Location` opcionais, problemas preservados | ApiResults + HTTP + OpenAPI |
| Rota de acompanhamento | 1-3 | DF2, DF7 | placeholders próprios validados; header correto; sem parser geral | generator diagnostics + HTTP |
| Busca alternativa/composta portável | 1, 4-6 | DF5-DF8, DF10-DF14 | contrato sem EF/SmartSearch; paridade EF/WorkContext | SQLite + fakes + consumer-smoke |
| Projeção segura | 1, 4-6 | DF11, DF13-DF16 | DTO projetado pelo provider, sem materialização prévia da entidade; `NotFound` nomeia a entidade | SQL/projeção + HTTP |
| Generator robusto/incremental | 3, 5-6 | DF7-DF9 | modelos symbol-free, diagnósticos, sem fonte parcial/CS8785 | tracked steps + snapshots + compile |
| Rollout coordenado | 2-6 | DF3, DF9 | pacotes ordenados e consumíveis sem publicação implícita | pack + smoke por TFM |

---

## Invariantes a preservar

1. `Accepted` representa somente sucesso já aceito; problemas, exceções e cancelamento nunca viram 202.
2. `Result<T>` mantém `T` no corpo de 202; `Result` não inventa corpo; `Location` é opcional em ambos.
3. Nenhum modelo de job/operação se torna dependência obrigatória do SmartCommands.
4. O núcleo não referencia EF, `IQueryable`, `FindCriteria<TEntity>` nem SmartSearch para busca simples.
5. Busca simples e composta produz o mesmo resultado semântico nos adapters EF e WorkContext.
6. Toda operação async propaga o `CancellationToken` e não converte `OperationCanceledException` em problema comum.
7. Entradas inválidas geram RCCMD localizado e nenhuma fonte parcial, `CS8785` ou `AD0001`.
8. Modelos incrementais são imutáveis/equatáveis/symbol-free; emissão é determinística e usa hint names padronizados.
9. Não expor entidade por HTTP por acidente nem materializar coleções para obter um único registro.
10. Rotas são validadas apenas nas invariantes interpretadas pelo SmartCommands; ASP.NET Core continua autoridade geral.
11. Toda API pública alterada possui XML docs, testes e nota de migração direta.
12. Commit, push, versão, publicação e CI/CD exigem autorização explícita.

---

## Critérios globais de conclusão

- DFs 10-16 implementadas conforme as respostas de Q1-Q6 e a revisão da Fase 1; nenhuma escolha arquitetural ficou implícita.
- `AcceptedMatch`/`AcceptedMatch<T>`, status e rota opcional funcionam com runtime e OpenAPI coerentes.
- `TryFindBy` simples/composto possui contrato portável, paridade EF/WorkContext e projeção conforme DF11.
- Generator cobre entradas válidas/inválidas, incrementalidade e colisões sem crash nem fonte parcial.
- Problemas `NotFound`, cancelamento, query filters e constraints de segurança são preservados.
- SmartSearch e `OperationReference` permanecem fora, salvo nova decisão humana sustentada por evidência.
- Builds/testes relevantes passam sem warning novo e pacotes locais são consumidos nos TFMs anunciados.
- Documentação, XML docs, catálogo de diagnósticos e notas de rollout refletem o comportamento final.
- Nenhum pacote ou repositório remoto foi alterado além da autorização concedida durante a execução.

---

## Riscos

| Risco | Gatilho | Impacto | Mitigação | Estado |
|---|---|---|---|---|
| 202 sugerir processamento concluído | endpoint usa `Accepted` para operação síncrona finalizada | contrato HTTP enganoso | documentação/cenário de fila e escolha explícita por atributo | Aberto |
| `Location` insegura ou inválida | concatenação direta de valores de rota | header incorreto/injeção | placeholders nomeados, formatação/escape e testes de valores especiais | Aberto |
| Pacotes dessincronizados | SmartCommands consome match/adapter ainda não publicado | restore/build quebrado | ordem de rollout e PackageReference pinado; aguardar confirmação humana | Aberto |
| Abstração de busca vazar EF | `FindCriteria`/`IQueryable` entra no núcleo | acoplamento e adapter WorkContext artificial | DF6 e revisão de API na Fase 4 | Aberto |
| Projeção causar materialização rastreada | adapter busca entidade e mapeia em memória | custo, tracking e exposição de dados | DF11, SQL/projeção testada e evolução no dono correto | Aberto; sonda da Fase 1 comprovou o caminho sem tracking e com SELECT restrito ao DTO |
| Chave duplicada passar silenciosamente | DF12 e banco sem unique constraint | resultado possivelmente não determinístico | documentar invariante, recomendar constraint e teste de comportamento | Aceito por decisão; mitigar por documentação e constraint |
| SmartSearch ser adotado por conveniência | tipo de filtro parece reutilizável | dependências/ciclo e escopo excessivos | DF5; exigir evidência e decisão nova | Fechado na Fase 1: evidência registrada, nenhuma dependência introduzida |
| Generator regredir incrementalidade | novo modelo carrega símbolo/array mutável | cache incorreto e retenção | snapshots/EquatableArray e tracked-step tests | Aberto |
| Escopo atravessar muitos repositórios | projeção requer mudança em EnterprisePatterns | atraso e releases encadeadas | Fase 1 identifica menor dono; fases/gates separados | Mitigado: donos por mudança registrados no Resultado da Fase 1 (DF13-DF16); DF16 amplia a release do SmartProblems |

---

## Diferidos e backlog

- `OperationReference` padronizado, polling, retry-after, callbacks e links de operação — plano próprio após casos reais em mais de um consumidor.
- Cancelamento de operação já aceita e idempotency keys — feature HTTP/infra separada.
- Alias explícito entre placeholder e propriedade de chave alternativa — reavaliar após uso da convenção definida na DF10.
- Operadores além de igualdade, OR, ranges, includes e hints — usar `MapSearch`/SmartSearch; não ampliar `TryFindBy` silenciosamente.
- Streaming/form/file/upload — uso direto de Minimal API, conforme DF23.
- Detecção automática de unique index do EF — não fazer do modelo EF uma exigência do generator.

---

## Referências

- `.ai/plans/plan-smartcommands-correcoes-melhorias.md`
- `.docs/reviews/review-questao-4-plan-smartcommands-correcoes-melhorias-v1.md`
- `.docs/commands.md`
- `RoyalCode.SmartCommands/MapFindAttribute.cs`
- `RoyalCode.SmartCommands/IRepositoryAccessor.cs`
- `RoyalCode.SmartCommands.Generators/Generators/FindInformation.cs`
- `../../SmartProblems/src/RoyalCode.SmartProblems.ApiResults/HttpResults/CreatedMatch'0.cs`
- `../../SmartProblems/src/RoyalCode.SmartProblems.ApiResults/HttpResults/CreatedMatch'1.cs`
- `../../SmartProblems/src/RoyalCode.SmartProblems.EntityFramework/SmartProblemsEFExtensions.TryFind.cs`
- `../../SmartProblems/src/RoyalCode.SmartProblems.EntityFramework/FindCriteria.cs`
- `../../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.Repositories.Abstractions/DataServices.cs`
- `../../EnterprisePatterns/RoyalCode.EnterprisePatterns/RoyalCode.WorkContext.Abstractions/WorkContextExtensions.cs`
- `../../Searches/src/RoyalCode.SmartSearch.Abstractions/ICriteria.cs`
- [RFC 9110 - 202 Accepted](https://www.rfc-editor.org/rfc/rfc9110.html#name-202-accepted)

---

## Comandos de manutenção para a IA executora

- Antes de cada fase, leia decisões, perguntas, dependências, invariantes, riscos e o `AGENTS.md` de cada repositório tocado.
- Antes do primeiro edit, verifique `git status --short` e o diff dos arquivos; preserve trabalho do usuário e limite o patch ao escopo.
- Não marque pergunta como fechada por inferência: registre a resposta humana e converta-a em DF.
- Não introduza referência entre projetos/repos sem registrar dono, versão mínima, ordem de release e consumer-smoke.
- Marque tarefa `- [x]` somente após executar a verificação correspondente; registre comandos, contagens, warnings e limitações no resultado da fase.
- Ao concluir fase, atualize `Status`, `Progresso`, tabela de fases, `Matriz de rastreabilidade` e riscos.
- Se uma fase descobrir necessidade de SmartSearch, `OperationReference` ou outro contrato fora do escopo, pare, registre nova pergunta e solicite decisão antes de implementar.
- Não faça commit, push, tag, mudança de versão, publicação ou alteração de CI/CD sem pedido explícito.
