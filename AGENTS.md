# Agent Instructions

This repository contains a .NET/C# application.

## General behavior

- Inspect the existing implementation before making changes.
- Follow existing naming, architecture, and coding patterns.
- Make the smallest change necessary to satisfy the task.
- Do not introduce new NuGet packages unless necessary.
- Do not change public APIs unless required by the task.
- Do not modify unrelated files.
- Do not suppress compiler or analyzer warnings without explaining why.

## C# guidelines

- Follow the repository's .editorconfig.
- Respect nullable reference type annotations.
- Prefer clear, idiomatic modern C#.
- Prefer file-scoped namespaces where consistent with the project.
- Use async/await for asynchronous I/O.
- Do not use `.Result` or `.Wait()` on Tasks.
- Methods returning Task should normally use the Async suffix.
- Accept CancellationToken where an operation can reasonably be cancelled.
- Prefer dependency injection over service locator patterns.
- Prefer immutable data where practical.
- Avoid unnecessary abstractions and premature generalization.
- Assign the return value to a named variable before returning, so the
  value is self-documenting and easy to inspect in the debugger.
  Write `var result = Compute(); return result;` rather than
  `return Compute();`. Trivial returns (`return;`, constants, direct
  field/property reads, expression-bodied members) are exempt.

## Architecture

The solution follows these dependency rules:

Domain
    ↑
Application
    ↑
Infrastructure / API

- Domain must not reference Infrastructure.
- Domain must not depend on database, HTTP, or UI concerns.
- Application contains use cases and orchestration.
- Infrastructure implements external concerns such as persistence.
- API/controllers/endpoints should contain minimal business logic.

Before modifying architecture, inspect existing projects and dependencies.

## Testing

When changing behavior:

- Add or update tests.
- Follow the existing test framework and conventions.
- Test externally observable behavior rather than implementation details.
- Do not delete, skip, or weaken tests merely to make them pass.
- Prefer focused tests relevant to the change.

## Validation

After modifying C# code, run:

    dotnet build

Then run:

    dotnet test

If formatting is configured, also run:

    dotnet format --verify-no-changes

If a command fails:

1. Read the error.
2. Determine whether the failure was caused by your changes.
3. Fix problems caused by your changes.
4. Run the relevant command again.

Do not claim that a change works unless it has been built/tested,
or explicitly state that validation could not be performed.

## Git

- Whenever a coding task is finished, you are allowed to commit the work
  in sensible, separate commits.
- **Do not push to remotes without explicit permission from the user** —
  pushing happens only when the user asks for it.

## Before creating new code

Before creating a new:

- service
- interface
- repository
- DTO
- exception
- utility
- extension method

search the repository for an existing equivalent.

Prefer reusing or extending existing components over creating duplicates.