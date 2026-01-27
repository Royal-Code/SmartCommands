# Plano de Continuidade do Módulo de Segurança

Este plano detalha as etapas para concluir a implementação do módulo de segurança (usuários, perfis e permissões), alinhado às diretrizes do projeto demo RoyalCode SmartCommands.

## Etapas

1. Revisar estado atual
- Levantar APIs existentes, comandos e mapeamentos via `MapApiHandlersAttribute`.
- Conferir configuração de `IWorkContext` (`SegurancaConfigureWorkContext`).

2. Mapear lacunas vs requisitos
- Comparar documentação de negócio com código atual e listar itens faltantes.

3. Definir entidades
- `Usuario` (`Entity`): `Id`, `Nome`, `Email`, `SenhaHash`, `Salt`, `Ativo`, timestamps; métodos ativar/inativar, trocar senha.
- `Perfil` (`Entity`): `Id`, `Nome`, `Descricao`, `Ativo`, `Permissoes[]`.
- `Permissao` (`Entity`): `Id`, `Codigo`, `Descricao`, `Ativo`.
- Construtor protegido sem parâmetros; `RuleSet` para validação.

4. Contratos de comandos
- Usuários: `CriarUsuario`, `AtualizarUsuario`, `InativarUsuario`, `ReativarUsuario`, `AlterarSenha`, `ResetarSenha`.
- Perfis: `CriarPerfil`, `AtualizarPerfil`, `ExcluirPerfil`, `AtribuirPermissoesAoPerfil`.
- Permissões: `CriarPermissao`, `AtualizarPermissao`, `ExcluirPermissao`.
- `ValidarAcesso`: agrega permissões e decide.

5. Serviços de senha
- `IPasswordService`: gerar `Salt`, `HashPassword`, `VerifyPassword`.
- Políticas: mínimo, complexidade, histórico opcional, expiração, lockout.

6. Serviço de autorização
- `IAuthorizationService`: somar permissões ativas de perfis e diretas e retornar decisão.

7. Handlers de usuários
- Implementar handlers com `IWorkContext`, `Result/SmartProblems`.

8. Handlers de perfis
- CRUD e atribuição de permissões.

9. Handlers de permissões
- CRUD.

10. Configurar APIs mínimas
- Mapear endpoints de usuários, perfis e permissões.
- Rotas para alteração/reset de senha e validação de acesso.

11. Validações SmartValidation
- `RuleSet` para todos os comandos e entidades; sem exceções.

12. Integração com `IWorkContext`
- Repositórios para leitura/escrita conforme `.docs/workcontext.md`.

13. DTOs (SmartSelection)
- `Summary` e `Details` para `Usuario`, `Perfil`, `Permissao`.

14. Filtros/Especificadores
- Filtros por `Ativo`, `Nome/Email`, `Codigo`, paginação.

15. Testes
- Unitários de serviços/validações.
- Integração de handlers/APIs mínimas.

16. Documentação técnica
- Atualizar `.docs/architecture.md`, `.docs/commands.md`, `.docs/validation.md`, `.docs/selector.md`, `.docs/search.md`, `.docs/workcontext.md`.

17. Dependências e targets
- Validar `Directory.Build.props` e `demo.targets`.

18. Build e correções
- Compilar, corrigir erros e validar endpoints principais.

## Considerações de Projeto
- Alvo das demos: `.NET 8/9/10` e `.NET Standard 2.0` conforme `Directory.Build.props`.
- Sem exceções para regras de negócio; sempre `Result`/`SmartProblems`.
- Ordem de membros: campos privados, construtores, propriedades, métodos.
- Entidades com construtor protegido sem parâmetros e `#nullable disable/restore`.
