# Copilot Instructions

## General Guidelines
- Follow coding standards for consistency and maintainability.
- Use XML documentation to describe domain functionality for classes, methods, properties, services, and interfaces.

## Code Style
- Value objects implement `IValidable` using `RuleSet` in `HasProblems`.
- Class member order should be: private fields, constructors, properties, methods.
- Entities require a protected parameterless constructor for deserialization. Not sealed. Use #nullable disable/restore for that constructor.
- Validations must not throw exceptions; business methods should return `Result`/`Result<T>` (SmartProblems).
- Use `SmartValidation` `RuleSet` for validation processes.