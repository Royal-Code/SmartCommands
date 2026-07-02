# Plano: Cenarios de uso integrados na API Demo

## Objetivo

Evoluir `RoyalCode.SmartCommands.Demo` para conter exemplos de uso mais realistas, com API funcional e testes de integracao cobrindo o comportamento de ponta a ponta.

Cada fase deve entregar:

- um exemplo de dominio pequeno, mas plausivel;
- endpoints gerados por `SmartCommands`;
- persistencia via `WorkContext`;
- erros de negocio expostos com `SmartProblems`;
- validacoes com `SmartValidations`;
- buscas com `SmartSearch` quando aplicavel;
- testes em `RoyalCode.SmartCommands.Demo.Tests` usando host real e SQLite in-memory;
- notas sobre gaps encontrados nas libs RoyalCode.

O objetivo nao e criar uma aplicacao comercial completa. O objetivo e usar a demo como laboratorio vivo para provar integracao, documentar padroes de uso e revelar lacunas de design.

## Status

**Fases 1 (Catalogo), 2 (Estoque e reserva), 3 (Pedido simples), 4 (Busca avancada) e 5 (Soft delete e desativacao)
CONCLUIDAS. Fase 6 pendente.** A demo ganhou dominio proprio (pasta `Domain/`: `Produto`, `ProdutoEstoque`, `Pedido`,
`Loja`, `DemoDbContext`), substituindo os entes de Tests.Models (decisao de base F2), com catalogo rico (SKU obrigatorio
e unico, preco > 0, editar preservando SKU), fluxo de estoque com saldo disponivel/reservado, token `Version` e retry de
concorrencia, e fluxo de pedido com criacao atomica, reserva de estoque, cancelamento e consulta/listagem. A Fase 4
entregou filtros compostos (nome parcial, faixa de preco, disponibilidade em estoque), paginacao e **ordenacao**
(nome/preco asc-desc, orderby invalido -> 400); isso exigiu **corrigir o SmartSearch** (bug de projecao DTO que
descartava a ordenacao sob paginacao) — liberado como **0.10.5** (ver Resultado da Fase 4 e Registro de gaps). A Fase 5
adicionou soft delete: transicoes de estado nao idempotentes (`409`), listagem publica que esconde inativos por padrao e
busca administrativa via `?incluirInativos=true`. Detalhes nos "Resultados" das fases 1, 2, 3, 4 e 5.

Este plano e um **living plan**: cada fase concluida recebe status/notas, e o registro de gaps reflete o que foi achado
e onde foi resolvido.

## Resultados ja incorporados

Gaps ja descobertos pela demo e resolvidos nas libs (fecham o loop do laboratorio). Testes: `RoyalCode.SmartCommands.Demo.Tests` (execucao real, SQLite in-memory) + snapshots do gerador em `RoyalCode.SmartCommands.Tests`.

- **Concorrencia otimista com retry** [Feature] — `[WithRetryOnConcurrency]` + primitiva `IUnitOfWork.RetryOnConcurrencyAsync` no `RoyalCode.SmartCommands.WorkContext`; laco rollback + `CleanUp` entre tentativas; politica por `RetryOnConcurrencyOptions` (bind via `BindConfiguration`). Coberto por `DemoApiConcurrencyRetryTests` e `RetryOnConcurrencyTests`.
- **Retry com valor (`Result<T>` / `ProduceNewEntity`)** [Feature] — overload generico `IUnitOfWork.RetryOnConcurrencyAsync<T>` + ramo no gerador que emite `RetryOnConcurrencyAsync<T>(...)` colocando o encadeamento produtor de valor (`Execute -> AddEntityAsync -> CompleteAsync`) dentro da lambda. Habilita retry em comandos que devolvem valor no mesmo endpoint (ex.: `CriarPedido` reserva estoque e devolve o pedido criado sob retry). Coberto por `ConcurrencyRetryTests` (primitiva) e `PedidoTests.CriarPedido_ComConflito*` (execucao real).
- **Problem factory por operation key** [Feature] — `IConcurrencyRetryProblemFactory` + delegates/provider + fallback por options, para customizar o problema quando o retry esgota. Coberto por `ConcurrencyRetryProblemFactoryTests` e cenarios de retry da demo.
- **Body-ness dos comandos** [Design] — comando sem forma de corpo (sem propriedades settaveis nem parametros de construtor publicos) nao entra como parametro do endpoint; e instanciado via `new`, e a requisicao pode ser enviada sem body. Coberto pelos snapshots em `Scenarios/Hs/Tests.cs`.
- **Guard de body ausente** [Bug] — comando com corpo cujo body nao chega vira `400 InvalidParameter` (SmartProblems), em vez de `NullReferenceException` (500). Coberto por `DemoApiErrorContractTests`.

## Decisoes iniciais

- Manter cenarios pequenos e focados, para cada fase caber em uma revisao clara.
- Preferir regras de dominio reais a exemplos artificiais.
- Testar sempre pelo endpoint HTTP quando o objetivo for validar geracao de codigo e contrato publico.
- Usar testes unitarios apenas para regras de dominio puras ou gaps muito especificos.
- Registrar gaps em comentarios no plano ou em arquivo de review quando exigirem decisao de design.
- Evitar dependencias externas alem das libs RoyalCode ja usadas pela demo, salvo quando a fase justificar explicitamente.
- Preservar a demo como exemplo legivel: nomes, rotas e comandos devem ensinar o uso das libs.
- **Modelos de dominio proprios da demo.** Nao reutilizar `RoyalCode.SmartCommands.Tests.Models` (`Produto`, `Some`): esse projeto e compartilhado com os testes de snapshot do gerador, e enriquecer `Produto` (SKU, preco, unicidade) quebraria esses testes. A demo passa a ter seus proprios entes.
- **Sem lib `Domain` dedicada.** Nao existe `RoyalCode.Domain` no repo; as invariantes de agregado usam `RoyalCode.Entities` e `RoyalCode.Repositories` como dependencia indireta. Remover das fases a mencao condicional "se a lib estiver disponivel".

## Estrutura esperada por fase

Para cada fase:

1. Modelar ou ajustar o dominio.
2. Criar comandos e endpoints.
3. Criar buscas ou detalhes quando fizer sentido.
4. Mapear problemas esperados.
5. Criar testes de sucesso, erro e persistencia.
6. Avaliar gaps de `SmartCommands`, `WorkContext`, `SmartProblems`, `SmartValidations`, `SmartSearch` e do modelo de dominio local da demo.

## Fase 1 - Catalogo com regras de dominio

### Objetivo

Transformar o exemplo de produto em um catalogo minimo com regras de dominio reais.

### Funcionalidades

- Criar produto com:
  - nome obrigatorio;
  - SKU obrigatorio;
  - SKU unico;
  - preco maior que zero;
  - status inicial ativo.
- Editar nome e preco.
- Impedir SKU duplicado.
- Obter detalhes do produto.
- Listar produtos ativos.

### Integracoes exercitadas

- `SmartCommands` para comandos de criacao e edicao.
- `SmartValidations` para validacoes simples de entrada.
- `SmartProblems` para erros de SKU duplicado e preco invalido.
- `WorkContext` para persistencia e unidade de trabalho.
- `SmartSearch` para listagem basica por nome, SKU e status.
- `Entities`/`Repositories` para invariantes no agregado (ver Decisoes iniciais; nao ha lib `Domain` dedicada).

### Testes

- Criar produto valido retorna `201` e persiste dados.
- Criar produto com nome vazio retorna `400`.
- Criar produto com preco zero ou negativo retorna problema esperado.
- Criar produto com SKU duplicado retorna conflito/estado invalido.
- Editar produto altera nome/preco e preserva SKU.
- Buscar/listar retorna apenas produtos esperados.

### Gaps a observar

- Como expressar validacao assincrona de unicidade com `SmartCommands`.
- Melhor padrao para converter erro de dominio em `Problem`.
- Se a API gerada ajuda ou atrapalha comandos com regras alem de `HasProblems`.

### Resultado — CONCLUIDA

Entregue com dominio proprio da demo (decisao de base F2). Nova pasta `Domain/` com `Produto` (rico: `Nome`, `Sku`,
`Preco`, `Ativo`, com `Editar`/`Desativar`), `Loja` e `DemoDbContext` (substitui o `CineDbContext` de Tests.Models;
indice unico de SKU). Os comandos/consultas de produto foram migrados e enriquecidos: `CriarProduto2` (validacao
`NotEmpty`/`GreaterThan` + unicidade de SKU por consulta assincrona, devolvendo `409` com `typeId`
`demo.produto.sku_duplicado`), `EditarProduto` (nome + preco, preservando SKU), `ProdutoDetalhes`/`ProdutoFiltro`
(SKU + preco; filtro por nome/SKU/status). `ProgramExtensions` passou a usar `AddWorkContext<DemoDbContext>()`.

**Invariantes no agregado:** o construtor de `Produto` e `Editar` guardam as invariantes (nome/SKU nao vazios,
preco > 0) com fail-fast — defesa em profundidade caso um comando futuro crie/edite `Produto` sem passar pela
validacao amigavel da feature (que continua no comando via `HasProblems`).

Testes: `CatalogoTests` (8 cenarios da Fase 1: criar valido, nome vazio, SKU vazio, preco invalido, SKU duplicado,
editar preservando SKU, listar por SKU/status) + os 17 testes de integracao existentes migrados — **25/25 verdes**;
suite do gerador **83/83**.

**Limitacao conhecida — corrida de SKU (F2).** O `AnyAsync` cobre o caso comum com `409` amigavel; o indice unico do
banco garante que nenhum SKU duplicado seja persistido. Porem, sob criacao **concorrente**, o perdedor cai na violacao
de constraint e hoje vira `500` (o `WorkContext.SaveAsync` converte `DbUpdateException` em `Problems.InternalError`), e
nao no mesmo `409`. Mapear violacao de constraint do provider para um `Problem` de dominio e um gap de stack
(WorkContext/SmartProblems), nao especifico da demo — nao resolvido aqui.

**Exceção temporaria — Tests.Models (F3).** `Produto`/`Loja` foram migrados (o ponto principal da Fase 1), mas os
exemplos de `Movies` (`ReviewDetails`/`ReviewFilter`) ainda usam `Movie`/`Review` de `RoyalCode.SmartCommands.Tests.Models`,
e o `.csproj` mantem a referencia por causa deles. Esses exemplos nao estao ligados ao app (nao ha grupo/rota de Movies
em `ConfigurePipeline`). Migrar ou remover o dominio de `Movies` fica para uma fase futura; ate la, referencia mantida
como excecao.

**Gap descoberto** [Bug] — `RoyalCode.SmartSearch.Linq.Sortings.OrderByProvider.GetDefaultHandler` inicializa o
handler de ordenacao default (ex.: `(Produto, Id)`) num dicionario estatico **nao thread-safe**: sob execucao paralela,
dois testes que disparam a primeira busca da mesma entidade colidem com *"An item with the same key has already been
added"*. Contorno: os testes de integracao da demo rodam **serialmente** (`DisableTestParallelization` em
`AssemblyInfo.cs`). Correcao pertence a `SmartSearch` (outro repo) — registrado abaixo.

## Fase 2 - Estoque e reserva

### Objetivo

Criar um fluxo pequeno de estoque para exercitar transacao, concorrencia e regras de negocio com impacto real.

### Funcionalidades

- Registrar saldo inicial de estoque para um produto.
- Adicionar entrada de estoque.
- Reservar quantidade.
- Liberar reserva.
- Impedir reserva acima do disponivel.
- Consultar saldo disponivel e reservado.

### Integracoes exercitadas

- Entidades/agregados locais da demo para regra de saldo e reserva.
- `WorkContext` para transacao e persistencia de mutacoes.
- `SmartCommands` para comandos de entrada, reserva e liberacao.
- `SmartProblems` para estoque insuficiente e produto inexistente.
- `WithRetryOnConcurrency` para conflito de atualizacao do estoque.
- SQLite in-memory nos testes para evitar falso positivo do EF InMemory.

### Testes

- Entrada de estoque aumenta saldo disponivel.
- Reserva reduz disponivel e aumenta reservado.
- Liberacao reduz reservado e aumenta disponivel.
- Reserva acima do disponivel retorna problema.
- Reserva de produto inexistente retorna `404`.
- Conflito transitorio no save tenta novamente e conclui.
- Conflito persistente retorna problema configurado pela operation key.

### Decisao tomada

Usar `Version` inteiro no agregado `ProdutoEstoque`, configurado como concurrency token no EF. O dominio incrementa a
versao em cada mutacao de saldo/reserva. Em SQLite isso evita depender de `rowversion` e deixa o exemplo portavel.

### Gaps a observar

- Suporte mais natural a concurrency token/`Version` nos exemplos.
- Limites do retry quando ha multiplas entidades alteradas.
- Como documentar side effects que nao podem ficar dentro do retry.

### Resultado - CONCLUIDA

Entregue com `ProdutoEstoque` como agregado proprio da demo, contendo `Disponivel`, `Reservado` e `Version`.
`DemoDbContext` mapeia `Version` como concurrency token e relaciona estoque 1:1 com `Produto`.

Comandos/endpoints gerados em `/produtos/{id}/estoque`: registrar saldo inicial, adicionar entrada, reservar e liberar
reserva. Todos usam `EditEntity<Produto, Guid>` para reaproveitar o `404` de produto inexistente, `WithValidateModel`
para quantidade positiva e `WithWorkContext` para transacao. Os comandos que alteram estoque existente usam
`[WithRetryOnConcurrency]` para conflitos de atualizacao; `RegistrarEstoqueInicial` nao usa retry porque insere uma
entidade nova, e a corrida real nesse endpoint e violacao de indice unico (`ProdutoId`), nao conflito de `Version`.
As regras do agregado `ProdutoEstoque` retornam `Result`/`Result<ProdutoEstoque>`, evitando exception como fluxo de
negocio e eliminando a duplicacao "checar antes, executar depois" nos comandos.

A consulta `GET /produtos/{id}/estoque` e gerada por `MapFind` + `AutoSelect` sobre `ProdutoEstoqueDetalhes`. Como
`ProdutoEstoque.Id == Produto.Id`, a rota continua usando o id do produto; se o estoque ainda nao foi registrado, o
recurso `/estoque` retorna `404` em vez de saldo zerado sintetico. O DTO expoe `Version` de forma intencionalmente
didatica para demonstrar o token de concorrencia; em API de produto real, preferir contrato explicito de versao ou
ETag/`If-Match`.

`ReservarEstoque` usa operation key `demo.estoques.reservar` com problem configurado para retry esgotado
("O estoque foi alterado por outro processo."). Estoque insuficiente e estoque nao registrado retornam `409` via
`SmartProblems`.

Testes: `EstoqueTests` cobre 11 cenarios apos revisao posterior da Fase 3 (entrada, reserva, liberacao, consulta sem
estoque registrado, estoque insuficiente, estoque nao registrado em entrada/reserva, reserva insuficiente na liberacao,
produto inexistente, conflito transitorio e conflito persistente com problem da operation key). Suite atual:
`RoyalCode.SmartCommands.Tests` **83/83** + demo **47/47**.

**Defesa de dominio:** `ProdutoEstoque.RegistrarInicial` mantem guarda para `produto == null`, embora esse caminho nao
seja alcancavel pelo endpoint HTTP porque `EditEntity<Produto, Guid>` retorna `404` antes. A guarda protege a fabrica
caso o dominio seja usado diretamente por outro comando.

**Gap descoberto** [Feature/Design] - `[WithRetryOnConcurrency]` no caminho atual funciona bem com `Result`, mas nao com
comando mutacional retornando `Result<T>` dentro da primitiva de retry. A fase ficou intencionalmente como comandos
mutacionais `Result` + consulta GET para ler o estado. Se a lib quiser suportar "muta e retorna DTO" com retry, falta
um overload generico de `RetryOnConcurrencyAsync` e ajuste no gerador.

## Fase 3 - Pedido simples

### Objetivo

Construir um fluxo de pedido que combine catalogo e estoque, exercitando orquestracao de regras e persistencia atomica.

> **Fatiar para revisao clara** (principio "pequeno e focado"): 3a — criar pedido + reservar estoque; 3b — cancelar pedido + liberar reservas; 3c — consulta/listagem. As funcionalidades abaixo cobrem os tres cortes; executar e revisar por corte.

### Funcionalidades

- Criar pedido com itens.
- Validar produto existente e ativo.
- Validar quantidade maior que zero.
- Reservar estoque durante a criacao do pedido.
- Calcular total do pedido a partir do preco atual.
- Consultar detalhes do pedido.
- Cancelar pedido e liberar reservas.

### Integracoes exercitadas

- `SmartCommands` para comando com lista de itens.
- `SmartValidations` para validacao estrutural do request.
- Entidades/agregados locais da demo para regras do pedido.
- `WorkContext` para transacao envolvendo pedido e estoque.
- `SmartProblems` para produto inativo, estoque insuficiente e pedido inexistente.
- `SmartSearch` para listagem de pedidos por status.

### Testes

- Criar pedido valido retorna `201`, cria pedido e reserva estoque.
- Criar pedido com item invalido retorna `400`.
- Criar pedido com produto inativo retorna problema de negocio.
- Criar pedido sem estoque suficiente retorna problema e nao persiste pedido parcial.
- Cancelar pedido libera estoque reservado.
- Buscar detalhes retorna itens e total calculado.

### Gaps a observar

- Ergonomia de comandos com colecoes complexas.
- Padrao para orquestrar multiplos agregados sem esconder regra em handler gerado.
- Necessidade de decorators especificos para carregamento de contexto.

### Resultado - CONCLUIDA

Entregue com agregado `Pedido` e itens (`PedidoItem`) no dominio proprio da demo. O pedido guarda snapshot de produto
(`ProdutoNome`, `ProdutoSku`, `PrecoUnitario`) e calcula `Total` a partir do preco atual no momento da criacao. O status
inicial e `Aberto`; cancelamento muda para `Cancelado`.

Comandos/endpoints gerados em `/pedidos`: criar pedido, cancelar pedido, consultar detalhes e listar por status. A
criacao valida lista de itens, produto existente/ativo e estoque registrado; reserva estoque e cria o pedido na mesma
unidade de trabalho, **sob retry de concorrencia** (`WithRetryOnConcurrency` + overload generico
`RetryOnConcurrencyAsync<Pedido>` — ver "Resultados ja incorporados"): sob conflito no estoque, o corpo reexecuta
(recarrega e reserva de novo) em vez de vazar `500`. Produto inexistente informado no body e tratado como `400 InvalidParameter` (referencia invalida);
produto inativo, estoque nao registrado e estoque insuficiente continuam como `409 InvalidState`. As regras de estoque
seguem retornando `Result`, sem exception como fluxo de negocio. O cancelamento usa
`WithRetryOnConcurrency(Operation = "demo.pedidos.cancelar")`, carrega os itens do pedido, cancela e libera as reservas
na mesma transacao.

A consulta de detalhes usa `MapFind` com DTO projetado por `SelectExpression`; a listagem usa `SmartSearch` por
`PedidoFiltro.Status`.

Testes: `PedidoTests` cobre 11 cenarios (criar valido, item invalido, produto inexistente no body, produto inativo,
estoque nao registrado, estoque insuficiente sem pedido parcial, cancelar liberando estoque, cancelar ja cancelado,
pedido inexistente, retry transitorio no cancelamento e listagem por status). `DemoApiEndpointTests` tambem valida as
rotas geradas de pedidos. Suite da demo:
**47/47 verdes**.

**Observacao de design:** a criacao de pedido orquestra dois agregados (`ProdutoEstoque` e `Pedido`) dentro do comando.
Para este tamanho de exemplo ficou legivel; se fases futuras repetirem esse padrao com mais carregamentos, pode valer
testar um decorator/helper de contexto para carregar agregados relacionados sem inflar o comando.

## Fase 4 - Busca avancada de produtos

### Objetivo

Expandir os exemplos de busca para cobrir filtros, ordenacao, paginacao e erros de query em um caso de catalogo.

### Funcionalidades

- Buscar produtos por:
  - nome parcial;
  - SKU;
  - faixa de preco;
  - status;
  - disponibilidade em estoque.
- Ordenar por nome, preco e data de criacao.
- Paginar resultados.
- Retornar shape estavel para busca.

### Integracoes exercitadas

- `SmartSearch` com criterios compostos.
- `SmartCommands` para endpoint gerado de search.
- `WorkContext`/EF para traducao de query.
- `SmartProblems` para query invalida.
- OpenAPI gerado quando aplicavel.

### Testes

- Filtro por nome retorna produtos esperados.
- Filtro por faixa de preco respeita limites.
- Ordenacao por preco ascendente/descendente funciona.
- Paginacao retorna total/itens conforme contrato atual.
- Query invalida retorna problema esperado.
- Busca por disponibilidade considera estoque reservado.

### Gaps a observar

- Clareza do contrato HTTP para filtros complexos.
- Como expor erros de parsing/ordenacao de forma padronizada.
- Qualidade do OpenAPI gerado para endpoints de search.

### Resultado - CONCLUIDA

Reaproveitou o endpoint de busca ja existente (`GET /produtos`), enriquecendo `ProdutoFiltro` em vez de criar um
segundo endpoint. Filtros entregues, todos via pipeline padrao do SmartSearch (sem gerador de expressao low-level):

- **nome parcial** — `string Nome` (operador `Like`/`Contains` por convencao);
- **SKU** — `string Sku` (convencao) e **status** — `bool? Ativo` (ja existiam da Fase 1);
- **faixa de preco** — `PrecoMinimo`/`PrecoMaximo` (`decimal?`) com dois `[Criterion]` sobre o mesmo alvo `Produto.Preco`
  (`GreaterThanOrEqual`/`LessThanOrEqual`); nulos ignorados (`IgnoreIfIsEmpty`), faixa aberta em qualquer ponta;
- **disponibilidade em estoque** — `EstoqueDisponivelMinimo` (`int?`) com `[Criterion("Estoque.Disponivel", GreaterThanOrEqual)]`
  via **caminho aninhado** na nova navegacao de leitura `Produto.Estoque`. Como `Disponivel` ja desconta o reservado,
  um produto totalmente reservado (`Disponivel == 0`) ou sem estoque registrado (`Estoque == null`, LEFT JOIN nulo) nao
  satisfaz o criterio — atende "considera estoque reservado".

Mudancas de dominio: `Produto` ganhou `CriadoEm` (`DateTimeOffset`, no construtor) e a navegacao 1:1 de leitura
`Estoque` (inverso configurado no `DemoDbContext` via `WithOne(p => p.Estoque)`); `ProdutoDetalhes` ganhou `CriadoEm` no
shape. A navegacao existe so para o lado de consulta (busca por disponibilidade) e nao participa das invariantes de
escrita do catalogo.

**Paginacao entregue** — `?itemsPerPage=N&page=P` bind via `[AsParameters] SearchOptions`; o resultado carrega
`count` (total), `itemsPerPage`, `taken`, `items` (contrato `IResultList`). Resultado vazio devolve **204 NoContent**
(comportamento do `Performer`), nao 200 com lista vazia — os testes tratam isso.

**Ordenacao HTTP — entregue apos corrigir o SmartSearch (0.10.5).** A investigacao mostrou que o gap **nao** era binding
nem o engine: era um bug de projecao no `SmartSearch.EntityFramework` — `CriteriaQuery.Select<TDto>` nao propagava o
estado de ordenacao ja aplicado, entao, com paginacao (`take > 0`), `CheckSorting` reaplicava a ordenacao default (`Id`)
sobre a projecao, sobrescrevendo o `?orderby`. So quebrava com **projecao DTO + paginacao juntas** (por isso `AsSearch`
e buscas sem paginacao sempre funcionaram). Corrigido no repo Searches e liberado como **0.10.5**; junto foram:

- `orderby` invalido agora devolve **400 InvalidParameter** (a `OrderByNotSupportedException` passou a derivar de
  `OrderByException`, capturada pelo `Performer`), em vez de 500;
- `IResultList.Pages` passou a usar `Ceiling` (antes `Floor`);
- `OrderByProvider` (cache de handlers) agora e thread-safe (`ConcurrentDictionary`) — fecha tambem o gap de
  thread-safety da Fase 1.

Consumo na demo: `RoyalCode.WorkContext.EntityFramework` 0.8.13 ja depende de `SmartSearch.* 0.10.5`, entao a demo
recebe o fix **transitivamente**, sem referencias diretas.

Ordenacao suportada: **nome** e **preco** (asc/desc via `?orderby=Preco` / `?orderby=Preco-desc`). Limitacao de provider:
`?orderby=CriadoEm` ainda falha no **SQLite** (nao ordena `DateTimeOffset` em `ORDER BY`) — nao e bug do SmartSearch;
`CriadoEm` continua no shape e como campo, mas nao e chave de ordenacao valida sob SQLite.

Testes: `BuscaProdutosTests` cobre nome parcial, faixa de preco (dentro e fora dos limites -> 204), paginacao (total +
itens por pagina), disponibilidade considerando reserva, **ordenacao por preco (asc/desc) e por nome**, e **orderby
invalido -> 400**. Suite da demo: **63/63 verdes**; gerador **83/83**. No repo Searches, regressao coberta por
`DtoProjectionSortingTests` e `SearchContractFixesTests` (**171/171**).

## Fase 5 - Soft delete e desativacao

### Objetivo

Modelar remocao logica/desativacao para exercitar filtros padrao, comandos de estado e buscas administrativas.

### Funcionalidades

- Desativar produto.
- Reativar produto.
- Impedir criacao de pedido com produto desativado.
- Listagem publica deve esconder inativos por padrao.
- Busca administrativa pode incluir inativos via parametro explicito.
- Detalhes de produto inativo devem seguir regra definida pela API.

### Integracoes exercitadas

- Entidades/agregados locais da demo para transicoes ativo/inativo.
- `SmartCommands` para comandos de desativacao/reativacao.
- `SmartProblems` para estado invalido.
- `SmartSearch` para filtro padrao e filtro administrativo.
- `WorkContext` para persistencia do estado.

### Testes

- Desativar produto ativo retorna `200`.
- Desativar produto ja inativo retorna problema de estado invalido ou sucesso idempotente, conforme decisao da fase.
- Reativar produto retorna `200`.
- Listagem publica nao retorna produto inativo.
- Busca administrativa retorna produto inativo quando solicitado.
- Pedido com produto inativo falha sem reservar estoque.

### Decisao tomada

Desativar um produto ja inativo (e reativar um ja ativo) e **erro de estado invalido (409)**, nao idempotente
(decidido pelo autor). A regra fica no agregado (`Produto.Desativar`/`Reativar` retornam `Result` com `InvalidState`),
e os comandos apenas propagam o `Result`.

### Gaps a observar

- Suporte a filtros globais ou criterios padrao em `SmartSearch`/`WorkContext`.
- Como diferenciar busca publica e administrativa sem duplicar muito codigo.
- Padrao para comandos idempotentes.

### Resultado - CONCLUIDA

Transicoes de estado com regra no agregado: `Produto.Desativar()`/`Reativar()` passaram a retornar `Result` e devolvem
`409 InvalidState` (`demo.produto.ja_inativo` / `demo.produto.ja_ativo`) quando o produto ja esta no estado alvo. Os
comandos `DesativarProduto` (ajustado para retornar `Result`) e o novo `ReativarProduto` (`PATCH /produtos/{id}/reativar`,
bodyless) so propagam o `Result` do agregado; ambos usam `EditEntity<Produto, Guid>` (404 se nao existe) + `WithWorkContext`
+ `WithRetryOnConcurrency`.

**Busca publica x administrativa sem duplicar (decisao tomada).** Um unico endpoint `/produtos` com
`ProdutoFiltro`: a listagem publica **esconde inativos por padrao** e a administrativa os inclui via
`?incluirInativos=true`. Implementado com um metodo `[WithFilter] AplicarVisibilidade` que, quando o chamador nao pediu
`IncluirInativos` e nao filtrou `Ativo` explicitamente, forca `Ativo = true` (reaproveitando o criterio `Ativo`
existente). Funciona porque `FilterBy` so guarda o objeto de filtro e os valores sao lidos na execucao (`Prepare`),
depois do `[WithFilter]` rodar — nao houve necessidade de "filtro global" na lib. Quem quer apenas inativos usa
`?ativo=false`.

**Detalhes de produto inativo (decisao tomada):** o find por id (`GET /produtos/{id}`) continua acessivel e retorna
`200` com `ativo:false` — soft delete esconde da **listagem**, nao do acesso direto.

**Pedido com produto inativo:** ja coberto desde a Fase 3 (`CriarPedido` valida `produto.Ativo` -> `409` inativo antes
de reservar; `PedidoTests.CriarPedido_ComProdutoInativo_RetornaProblemaDeNegocio` assegura que o estoque nao muda).

Testes: `SoftDeleteTests` cobre 8 cenarios (desativar ativo -> 200 + `ativo:false`; desativar ja inativo -> 409; reativar
inativo -> 200; reativar ja ativo -> 409; detalhes de inativo -> 200; listagem publica esconde inativo; busca admin com
`incluirInativos=true` inclui inativo; `ativo=false` retorna somente inativos). Suite da demo: **62/62 verdes**; gerador
**83/83**.

## Fase 6 - Fluxo de aprovacao e publicacao

### Objetivo

Criar um fluxo de estado com transicoes controladas, bom para demonstrar regras de dominio e problemas de negocio previsiveis.

### Funcionalidades

- Produto pode nascer como rascunho.
- Enviar produto para revisao.
- Aprovar produto.
- Rejeitar produto com motivo.
- Publicar produto aprovado.
- Impedir transicoes invalidas.
- Consultar historico minimo de transicoes, se isso nao aumentar demais a fase.

### Integracoes exercitadas

- Entidades/agregados locais da demo para maquina de estados simples.
- `SmartCommands` para comandos por transicao.
- `SmartProblems` para transicao invalida.
- `SmartValidations` para campos obrigatorios, como motivo de rejeicao.
- `WorkContext` para persistencia das transicoes.
- `SmartSearch` para filtro por estado de publicacao.

### Testes

- Rascunho pode ir para revisao.
- Produto em revisao pode ser aprovado.
- Produto aprovado pode ser publicado.
- Produto rascunho nao pode ser publicado direto.
- Rejeicao exige motivo.
- Historico registra transicoes quando implementado.
- Busca por estado retorna produtos corretos.

### Gaps a observar

- Padrao para comandos de transicao de estado.
- Melhor forma de retornar problema com estado atual e transicao tentada.
- Se `SmartCommands` precisa de suporte mais ergonomico para comandos sem body ou com body minimo.

## Verificacao geral

Ao final de cada fase:

- `dotnet test src\RoyalCode.SmartCommands.Demo.Tests\RoyalCode.SmartCommands.Demo.Tests.csproj`
- `dotnet test src\SmartCommands.sln`

Quando a fase alterar generator ou WorkContext compartilhado, adicionar tambem testes unitarios/snapshot no projeto `RoyalCode.SmartCommands.Tests`, se aplicavel.

### Isolamento de testes

Hoje cada teste cria sua propria `DemoApiFactory` com conexao SQLite in-memory propria e chama `EnsureDeleted`/`EnsureCreated` (isolamento por teste, ok no momento). Antes de crescer fixtures/dados nas fases, consolidar e documentar a estrategia (seed compartilhado vs. reset por teste) para evitar acoplamento entre cenarios.

### OpenAPI

`WithOpenApi` aparece como gap de qualidade (Fase 4), mas ainda nao ha estrategia de teste do **documento** gerado — os testes atuais validam apenas rotas via `EndpointDataSource`. Quando a Fase 4 chegar, definir como assertar o schema/spec, nao so as rotas.

## Registro de gaps

Cada gap encontrado deve ser classificado como:

- **Bug**: comportamento atual quebra o contrato esperado.
- **Design**: a API funciona, mas o uso fica pouco claro ou verboso.
- **Feature**: a necessidade e valida, mas ainda nao existe suporte na lib.
- **Docs**: o comportamento existe, mas precisa de exemplo ou documentacao.

O registro pode ficar neste plano enquanto a fase estiver ativa. Se o gap crescer ou exigir decisao, mover para um arquivo especifico em `.docs` ou plano proprio.

### Resolvidos no SmartSearch 0.10.5 (loop fechado pela demo)

Gaps descobertos pela demo (Fases 1 e 4) e corrigidos no repo Searches; regressao coberta por `DtoProjectionSortingTests`
e `SearchContractFixesTests` (SmartSearch **171/171**), e o comportamento HTTP por `BuscaProdutosTests` na demo.

- **[Bug] Ordenacao HTTP (`?orderby`) descartada sob projecao + paginacao.** Raiz: `CriteriaQuery.Select<TDto>` nao
  propagava `appliedSorting`; com paginacao, `CheckSorting` reaplicava a ordenacao default (`Id`) sobre a projecao. Fix:
  propagar o estado de ordenacao para a query projetada. (So quebrava com **projecao DTO + paginacao**; `AsSearch` e
  buscas sem paginacao sempre funcionaram — o que despistou o diagnostico inicial.)
- **[Bug] `orderby` invalido devolvia 500 em vez de 400.** `OrderByNotSupportedException` passou a derivar de
  `OrderByException` (Abstractions), entao os `catch (OrderByException)` do `Performer` a traduzem para
  `400 InvalidParameter`.
- **[Bug] `SmartSearch.OrderByProvider` nao thread-safe (Fase 1).** O cache estatico de handlers passou de `Dictionary`
  para `ConcurrentDictionary` (runtime idempotente; registro mantem checagem de duplicado via `TryAdd`). Fecha o gap que
  era contornado com `DisableTestParallelization`.
- **[Bug menor] `IResultList.Pages` usava divisao inteira.** `CountPages` passou de `Math.Floor` para `Math.Ceiling`
  (`count=3`, `itemsPerPage=2` -> `2` paginas).

Consumo na demo: `RoyalCode.WorkContext.EntityFramework` 0.8.13 depende de `SmartSearch.* 0.10.5`, entao a demo recebe o
fix transitivamente (sem referencias diretas).

### Gaps abertos

- **[Design] Ordenacao case-sensitive e `DateTimeOffset` no SQLite (Fase 4).** `?orderby=preco` (minusculo) -> erro (o
  `DefaultOrderByGenerator` resolve a propriedade de forma case-sensitive); `?orderby=CriadoEm` -> erro no SQLite
  (`DateTimeOffset` nao e ordenavel em `ORDER BY` — limitacao de provider, nao bug do SmartSearch). A case-sensitivity e
  um contrato pouco amigavel que poderia ser suavizado na lib; a de `DateTimeOffset` e do provider.

O gap de **retry com `Result<T>` / `ProduceNewEntity`** (antes aqui) foi **resolvido** — ver "Resultados ja
incorporados" (overload generico `RetryOnConcurrencyAsync<T>` + ramo no gerador; `CriarPedido` agora reserva estoque e
cria o pedido sob retry).

## Fora de escopo

- Transformar a demo em produto completo.
- Criar autenticacao/autorizacao real.
- Cobrir todos os atributos do `SmartCommands` em matriz completa.
- Substituir testes unitarios existentes dos generators.
- Criar banco externo para testes.

## Ordem recomendada

1. Catalogo com regras de dominio.
2. Estoque e reserva.
3. Pedido simples.
4. Busca avancada de produtos.
5. Soft delete e desativacao.
6. Fluxo de aprovacao e publicacao.

Essa ordem maximiza reaproveitamento: catalogo prepara produto, estoque usa produto, pedido usa catalogo e estoque, busca amplia leitura, soft delete ajusta regras transversais, e aprovacao consolida comandos de estado.
