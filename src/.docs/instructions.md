# Copilot Instructions

## General Guidelines
- Follow coding standards for consistency and maintainability.
- Use XML documentation to describe domain functionality for classes, methods, properties, services, and interfaces.
- For current architecture guidelines, use `.docs/feature-slice-architecture.md`; use
  `.docs/legacy-architecture.md` only when maintaining or migrating older solutions.

## Code Style
- Value objects implement `IValidable` using `RuleSet` in `HasProblems`.
- Class member order should be: private fields, constructors, properties, methods.
- Entities require a protected parameterless constructor for deserialization. Not sealed. Use #nullable disable/restore for that constructor.
- Validations must not throw exceptions; business methods should return `Result`/`Result<T>` (SmartProblems).
- Use SmartValidations `RuleSet` for validation processes (`.docs/references/validations.md`).
- Use `Problems`, `Problem`, `Result`, and `Result<T>` from SmartProblems for error handling and reporting
  (`.docs/references/problems.md`).
- Use SmartSelector to create Details and Summary DTOs (`.docs/references/selector.md`).
- Use SmartSearch for filtering and querying data (`.docs/references/smartsearch.md`).
- Use `IWorkContext` for data access operations (`.docs/references/workcontext.md`).
- Create entities with the RoyalCode domain abstractions (`.docs/references/domain.md`).
- Create commands and configure generated Minimal APIs using SmartCommands
  (`.docs/references/smart-commands.md`).
