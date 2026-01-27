
# Documentação do Módulo de Segurança

Este documento fornece uma visão geral do módulo de segurança do RoyalCode SmartCommands, 
detalhando suas funcionalidades, configurações e melhores práticas para garantir a proteção dos dados e operações.

## Funcionalidades Principais

- **Criação de usuários**: Permite o registro de novos usuários com validação de dados e políticas de senha.
- **Validação de credenciais**: Implementa mecanismos robustos para autenticação de usuários.
- **Gerenciamento de permissões**: Define e controla o acesso a recursos com base em papéis e permissões atribuídas.

## Escopo do Módulo e Entidades

O módulo de segurança é responsável por autenticação e autorização de acesso, e pela manutenção das entidades de segurança:

- **Usuário**: entidade que representa uma pessoa com credenciais e estado de acesso.
  - Atributos principais: `Id`, `Nome`, `Email`, `SenhaHash`, `Salt`, `Ativo`, `DataCriacao`, `DataAtualizacao`.
  - Regras:
    - `Email` único e válido.
    - `Ativo` controla acesso; usuários inativos não podem autenticar.
    - Senhas não são armazenadas em texto puro, apenas `SenhaHash` com `Salt` e algoritmo de hashing configurado.

- **Perfil**: conjunto de permissões que pode ser associado a usuários.
  - Atributos principais: `Id`, `Nome`, `Descricao`, `Ativo`, `Permissoes[]`.
  - Regras:
    - Nome único.
    - Perfis podem ser ativados/inativados; perfis inativos não concedem acesso.

- **Permissão**: capacidade/grant de acesso a uma funcionalidade ou recurso.
  - Atributos principais: `Id`, `Codigo`, `Descricao`, `Ativo`.
  - Regras:
    - `Codigo` único (ex.: `USUARIOS.CRIAR`, `USUARIOS.VER`, `PERFIS.ATRIBUIR`).
    - Permissões inativas não devem ser consideradas em avaliação de acesso.

Relacionamentos:
- Usuário 1..N Perfis (usuário pode ter múltiplos perfis).
- Perfil N..N Permissões.
- Usuário pode ter permissões diretas adicionais (opcional, para cenários de exceção).

## Requisitos Funcionais e Domínio

- Gerenciar usuários
  - Criar novos usuários
  - Atualizar informações de usuários
  - Inativar usuários
  - Reativar usuários
  - Alterar senha do próprio usuário
  - Resetar senha (administrador)

- Gerenciar perfis
  - Criar, atualizar e excluir perfis de usuário
  - Atribuir permissões a perfis
  - Ativar/Inativar perfis

- Gerenciar permissões
  - Criar, atualizar e excluir permissões
  - Ativar/Inativar permissões
  - Auditar uso de permissões (opcional para demonstração)

## Casos de Uso

### Criação de Usuário

- **Descrição**: Permite a criação de um novo usuário no sistema.
- Parâmetros:
  - Nome
  - Email
  - Senha

### Validação de Credenciais

- **Descrição**: Verifica se as credenciais fornecidas correspondem a um usuário registrado.
- Parâmetros:
  - Email
  - Senha

### Gerenciamento de Permissões

- **Descrição**: Atribui ou revoga permissões a usuários com base em seus papéis.
- Parâmetros:
  - ID do Usuário
  - Lista de Permissões

### Cadastro de Perfis

- **Descrição**: Permite o cadastro de perfis de usuário com diferentes níveis de acesso.
  - CRUD completo para perfis.

### Cadastro de Permissões

- **Descrição**: Permite o cadastro de permissões específicas que podem ser atribuídas a perfis ou usuários.
  - CRUD completo para permissões.

### Alteração de Senha

- **Descrição**: Permite ao usuário alterar sua própria senha mediante confirmação da senha atual.
- Parâmetros:
  - Senha atual
  - Nova senha
  - Confirmação de nova senha

### Reset de Senha (Administrativo)

- **Descrição**: Permite ao administrador redefinir a senha de um usuário, exigindo renovação no próximo login.
- Parâmetros:
  - ID do Usuário
  - Nova senha temporária (ou geração automática)

### Validação de Acesso

- **Descrição**: Avalia se um usuário possui acesso a uma funcionalidade com base em perfis e permissões.
- Parâmetros:
  - ID do Usuário
  - Código da Permissão
  - Contexto (opcional) para escopos específicos

## Serviços

- **Serviço de Autenticação**: Gerencia o processo de login e validação de usuários.
- **Serviço de Autorização**: Controla o acesso a recursos com base em permissões.
- **Serviço de Senhas**: Implementa políticas de senha, como Hashing, expiração e validação de senha.