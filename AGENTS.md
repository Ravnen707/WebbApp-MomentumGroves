
# AI Directions for MomentumGroves

## Mandatory Project Rules

These instructions are mandatory and apply whenever an AI agent reads, creates, modifies, refactors, moves or removes code or files within the MomentumGroves project.

These are project requirements, not suggestions or optional preferences.

The AI MUST follow these instructions throughout every task, regardless of its size or complexity. Existing code that does not follow these rules is not permission to introduce additional violations.

Explicit instructions provided by the project owner for a specific task take precedence over these general project rules.

---

## 1. Architecture and Separation of Responsibilities

Follow ASP.NET Core MVC and maintain a clear separation of responsibilities throughout the application.

- Controllers MUST only handle HTTP requests, input handling, service calls and HTTP responses.
- Controllers MUST NOT contain business logic, database queries or unnecessary data processing.
- Services MUST contain business logic and application-specific operations.
- Models MUST represent application data, domain objects, DTOs and records.
- Repositories MUST handle data access and persistence operations.
- Views MUST handle presentation.
- Helpers MUST contain reusable utility functionality, such as conversions and formatting.
- Database-specific implementation details MUST NOT leak into controllers or views.

Do not introduce business logic into a controller simply because the implementation is small.

Do not place model, DTO or record declarations inside service classes or controller classes.

Every responsibility MUST be placed in its appropriate architectural layer.

Maintain dependency injection and use constructor injection where appropriate.

---

## 2. File and Folder Organization

Every file MUST be placed in a folder that reflects its responsibility.

Backend application code belongs under `src`.

Follow the project's existing folder structure, including:

- `src/Controllers`
- `src/Service`
- `src/Models`
- `src/Repositories`
- `src/Data`
- `src/Helper`

Repositories MUST NOT be placed inside the Data folder.

Models, records and DTOs MUST have their own appropriately named files.

Use an existing folder when its responsibility matches the file being created. Create a clearly named folder when no appropriate folder exists.

If a file being worked on is incorrectly placed, move it to the appropriate folder and update all affected namespaces, imports and references.

Do not reorganize unrelated files as part of an unrelated task.

Do not create unnecessary folders, duplicate folder structures or generic dumping grounds for unrelated functionality.

File names MUST clearly represent their contents and responsibilities.

---

## 3. C# Formatting and Curly Braces

Every `if`, `else if`, `else`, `for`, `foreach`, `while` and `do` statement MUST use explicit curly braces, even when the body contains only one statement.

Opening and closing braces MUST appear on their own lines.

Statements inside a block MUST be properly indented.

Correct:

```csharp
if (status.RequiresLocalSetup)
{
    return View(status);
}
```

Incorrect:

```csharp
if (status.RequiresLocalSetup)
    return View(status);
```

Incorrect:

```csharp
if (status.RequiresLocalSetup) return View(status);
```

Methods that return a value MUST use a block body with an explicit `return` statement.

Correct:

```csharp
public bool IsAvailable()
{
    return _service.IsAvailable();
}
```

Incorrect:

```csharp
public bool IsAvailable() => _service.IsAvailable();
```

Do not use expression-bodied methods as a shortcut for normal method implementations.

These formatting requirements also apply to test projects, helper classes, repositories and newly generated code.

---

## 4. XML Documentation

Every newly created C# method and constructor MUST include XML documentation.

This requirement applies to:

- Public methods.
- Private methods.
- Protected methods.
- Internal methods.
- Constructors.
- Controller actions.
- Service methods.
- Repository methods.
- Helper methods.
- Asynchronous methods.
- Test methods.

Every method MUST include a meaningful `<summary>` describing its purpose.

Every method parameter MUST have a corresponding `<param>` documentation entry.

Every method that returns a value MUST have a meaningful `<returns>` documentation entry.

Generic type parameters MUST be documented using `<typeparam>` where applicable.

Use `<exception>` documentation where relevant to the method's intended behaviour.

Example of a constructor:

```csharp
/// <summary>
/// Initializes the administration controller.
/// </summary>
/// <param name="updates">The service responsible for managing FRIDA updates.</param>
public AdminController(FridaUpdateService updates)
{
    _updates = updates;
}
```

Example of a method:

```csharp
/// <summary>
/// Retrieves a recipe by its unique identifier.
/// </summary>
/// <param name="id">The unique identifier of the recipe.</param>
/// <returns>The matching recipe, or null if no recipe exists.</returns>
public Recipe? GetRecipe(int id)
{
    return _repository.GetRecipe(id);
}
```

Example of a private method:

```csharp
/// <summary>
/// Determines whether the supplied recipe name is valid.
/// </summary>
/// <param name="name">The recipe name to validate.</param>
/// <returns>True when the name contains a valid value; otherwise, false.</returns>
private bool IsValidName(string name)
{
    return !string.IsNullOrWhiteSpace(name);
}
```

XML documentation MUST describe what the method actually does.

Do not generate empty documentation entries, meaningless summaries or placeholder comments.

When modifying an existing method, update its documentation if its behaviour, parameters or return value changes.

If an existing method being modified has no XML documentation, add it.

XML documentation is mandatory even when the method appears self-explanatory.

---

## 5. Code Readability and Formatting

Code MUST be written for readability, maintainability and future development.

Use conventional C# formatting and avoid unnecessary vertical expansion.

Keep short declarations, assignments, method calls and return statements on a single line when they remain readable.

Correct:

```csharp
return new DrinkSearchResult(true, Combine(local, cached));
```

Incorrect when unnecessary:

```csharp
return new DrinkSearchResult(
    true,
    Combine(
        local,
        cached
    )
);
```

Correct:

```csharp
const string defaultProvider = "SQLite";
```

Do not introduce unnecessary line breaks simply to make code appear more structured.

Long expressions and method calls MAY be split across multiple lines when doing so genuinely improves readability.

Additional requirements:

- Use meaningful variable, method and class names.
- Avoid unnecessary abbreviations.
- Avoid deeply nested logic when a readable alternative exists.
- Prefer clear control flow.
- Avoid unnecessary temporary variables.
- Avoid unnecessary abstractions.
- Avoid magic numbers and unexplained hardcoded values.
- Keep related operations together.
- Follow existing naming conventions.
- Maintain consistent formatting across the project.

Do not sacrifice readability for shorter code.

Do not sacrifice readability for unnecessary architectural complexity.

---

## 6. Method and Class Responsibilities

Methods MUST have a clearly defined responsibility.

Classes MUST have a clearly defined purpose.

Do not create excessively large methods that combine unrelated operations.

Extract reusable or independently testable logic into appropriately named methods when necessary.

Do not extract every individual statement into a separate method. Method extraction MUST provide a meaningful improvement in readability, reuse or testability.

Avoid creating unnecessary wrapper methods that only introduce additional layers of indirection.

Do not create interfaces, factories, managers or abstraction layers without a clear architectural purpose.

Do not introduce design patterns solely for the sake of using design patterns.

Prefer the simplest implementation that satisfies the requirements while remaining maintainable, testable and scalable.

---

## 7. Models, DTOs and Records

Models, DTOs and records MUST be placed in the appropriate Models folder or an appropriately named subfolder.

Do not declare models or records inside:

- Controllers.
- Services.
- Repositories.
- Helper classes.
- Unrelated implementation files.

Use meaningful names that describe the purpose of each model.

Separate request models, response models and domain models when they represent different responsibilities.

Do not expose internal persistence models unnecessarily through public endpoints.

Keep conversion and mapping logic in appropriate helper or mapping components when it is reusable.

Do not duplicate existing models without a valid reason.

---

## 8. Views, Razor and Modal Windows

Views MUST remain focused on presentation.

Do not place business logic, database access or complex data processing inside `.cshtml` files.

Use strongly typed view models where appropriate.

Modal windows and popup dialogs MUST be placed in dedicated partial view files.

Modal markup MUST NOT be embedded directly inside the main page view.

Use a dedicated `Modal` folder within the relevant view structure.

Example:

```text
Views/
└── Training/
    ├── Index.cshtml
    └── Modal/
        ├── _CreateExerciseModal.cshtml
        ├── _EditExerciseModal.cshtml
        └── _DeleteExerciseModal.cshtml
```

The main view MUST reference the relevant partial views.

Keep presentation components reusable when appropriate.

Use meaningful and stable HTML identifiers for elements that are accessed by JavaScript or automated browser tests.

Do not unnecessarily rename existing HTML IDs, classes or selectors, as they may be used by existing functionality and Playwright tests.

---

## 9. JavaScript Structure

JavaScript MUST be organized according to its functional responsibility.

Do not place unrelated functionality inside a single large JavaScript file.

Use dedicated JavaScript files for individual application features, such as:

- `food-diary.js`
- `recipes.js`
- `weight-tracking.js`
- `training.js`
- `admin.js`

Follow the existing JavaScript organization before introducing new files.

Do not turn `app.js` into a collection of unrelated feature implementations.

Avoid embedding large JavaScript implementations directly inside Razor views.

Reusable JavaScript functionality MUST be placed in an appropriate shared file when reuse is justified.

Additional requirements:

- Use meaningful function and variable names.
- Avoid unnecessary global variables.
- Avoid duplicated event handlers.
- Avoid duplicated application logic.
- Handle asynchronous operations correctly.
- Handle expected errors appropriately.
- Keep DOM manipulation separate from unrelated processing logic.
- Preserve existing event handling and selectors when modifying functionality.

Do not introduce additional JavaScript frameworks or dependencies unless required and explicitly approved.

---

## 10. CSS Structure and Styling

CSS MUST be organized according to the functionality it styles.

Use dedicated stylesheets for independent features where appropriate.

For example:

- `cheatdays.css`
- `sickdays.css`
- `training.css`
- `recipes.css`

Do not place all feature-specific styling inside a single global stylesheet.

Shared styling belongs in the appropriate shared stylesheet.

Avoid inline styling when the same result can be achieved through an appropriately named CSS class.

Do not duplicate existing CSS rules unnecessarily.

Preserve the application's existing visual identity, layout and design conventions.

Do not redesign unrelated components when implementing a functional change.

New UI elements MUST support the application's existing responsive layouts.

---

## 11. Database and Data Access

Database access MUST be handled through the appropriate repositories and persistence components.

Do not execute database queries directly from controllers or views.

Use parameterized queries and appropriate database access mechanisms.

Never construct SQL queries by concatenating untrusted user input.

Preserve existing database provider boundaries.

MomentumGroves supports standalone operation and optional cloud-backed operation.

Changes to data access MUST preserve the intended behaviour of both modes.

- Standalone operation MUST NOT require Supabase.
- Cloud operation MUST NOT silently fall back to local storage when a cloud operation fails.
- Database-specific behaviour MUST remain within the appropriate persistence implementation.
- Existing user data MUST be preserved.
- Database migrations MUST be deliberate and documented.

Do not delete, overwrite, reset or migrate existing application data without explicit authorization.

Do not execute destructive database operations against a live database without explicit authorization.

Do not introduce unnecessary database queries.

Consider transaction boundaries, concurrency and data consistency when modifying persistence functionality.

---

## 12. Error Handling and Validation

Expected failure scenarios MUST be handled deliberately.

Validate external input at the appropriate application boundary.

Business rules MUST be enforced within the appropriate service or domain layer.

Do not rely exclusively on frontend validation.

Do not silently swallow exceptions.

Incorrect:

```csharp
try
{
    await _service.SaveAsync();
}
catch
{
}
```

Errors MUST be handled or propagated appropriately.

Provide meaningful error messages without exposing credentials, sensitive information or unnecessary internal implementation details.

Use appropriate HTTP status codes for controller and API responses.

Do not hide underlying defects by introducing arbitrary fallback behaviour.

Fix the underlying cause of a problem rather than suppressing its symptoms.

---

## 13. Performance and Optimization

Performance MUST be considered when implementing or modifying functionality.

Avoid:

- Unnecessary database queries.
- Repeated processing of the same data.
- Unnecessary network requests.
- Loading large amounts of data without a valid reason.
- Blocking asynchronous operations.
- Unnecessary allocations.
- Repeated conversions and calculations.
- Inefficient loops where a clearer and more efficient alternative exists.

Use asynchronous operations appropriately for database, network and file access.

Do not introduce `.Result` or `.Wait()` as shortcuts for asynchronous execution.

Do not introduce parallel processing without considering thread safety, shared state and resource usage.

Do not introduce caching without considering invalidation and data consistency.

Optimization MUST NOT come at the expense of correctness, maintainability or readability.

Do not claim that an implementation is faster without measurements or a clear technical basis.

---

## 14. Testing Philosophy

Testing is a core part of MomentumGroves development, not an optional final step.

New functionality MUST be designed with testability in mind.

Consider the following scenarios when developing or modifying functionality:

- Expected behaviour.
- Invalid input.
- Boundary values.
- Empty collections.
- Null values where applicable.
- Failure scenarios.
- Duplicate operations.
- Repeated operations.
- State changes.
- Data consistency.
- Relevant concurrency scenarios.
- Regression scenarios.
- Integration boundaries.

Do not only test the happy path.

The objective is meaningful behavioural coverage, not simply increasing the number of tests or achieving a particular coverage percentage.

A passing test suite does not guarantee that the application is free of defects.

Tests MUST verify actual behaviour and MUST NOT be written solely to make the test suite pass.

---

## 15. NUnit Testing

NUnit is the primary testing framework for MomentumGroves.

Follow the existing NUnit test project structure.

Tests MUST be placed in the appropriate test project and organized according to the functionality being tested.

Test names MUST clearly communicate the scenario and expected behaviour.

Use parameterized tests when multiple input scenarios verify the same behaviour.

Use appropriate NUnit features, including:

- `[Test]`
- `[TestCase]`
- `[TestCaseSource]`
- `[SetUp]`
- `[TearDown]`

Use the Arrange, Act and Assert pattern where appropriate.

Tests MUST be independent and repeatable.

Do not create tests that depend on another test being executed first.

Do not depend on the execution order of the test suite.

Use test helpers, fixtures and reusable setup components when they provide meaningful improvements in readability and maintainability.

Do not introduce unnecessary duplication across test classes.

Do not mock the functionality that the test is intended to verify.

Do not remove or weaken an existing assertion simply because a test fails.

Do not introduce another testing framework into an existing NUnit test project without explicit authorization.

XML documentation and C# formatting requirements also apply to all test methods and test helper methods.

---

## 16. Playwright Testing

Playwright is used for automated browser testing and verification of complete application flows.

Playwright tests MUST verify meaningful user-facing behaviour.

Use the existing Playwright and NUnit integration.

Keep browser tests readable and maintainable.

Reusable navigation, interaction and verification logic SHOULD be placed in appropriately named test helper classes or page objects when reuse is justified.

Do not place every interaction, selector and repeated operation directly inside individual test methods.

Use stable selectors, meaningful HTML IDs and appropriate Playwright locators.

Prefer Playwright's built-in waiting mechanisms and assertions.

Correct:

```csharp
await Expect(Page.Locator("#dashboard")).ToBeVisibleAsync();
```

Avoid arbitrary delays:

```csharp
await Task.Delay(5000);
```

Fixed delays MUST NOT be used as a substitute for waiting for the expected application state.

Tests MUST verify the outcome of an interaction, not merely that an interaction was performed.

Test data MUST be isolated from real user data.

Tests that create data MUST have an appropriate cleanup strategy.

Cleanup MUST identify the exact test-created data and MUST NOT delete unrelated application data.

Automated browser tests MUST NOT modify the production Supabase database.

Use an isolated test environment and appropriate test configuration.

---

## 17. Test Ownership and Learning

The project owner actively uses MomentumGroves to develop and improve testing skills.

Do not automatically generate a comprehensive test suite for every implementation unless requested.

When asked to provide a test foundation, skeleton or example:

- Create the requested structure.
- Provide a limited number of meaningful example tests.
- Keep the examples understandable and reusable.
- Leave room for the project owner to implement additional tests independently.

Do not turn a request for a small testing example into hundreds of generated tests.

When modifying existing functionality, identify relevant existing tests and update them where necessary.

If a change introduces new behaviour that is not covered by tests, identify the missing coverage in the task summary.

The project owner determines when a comprehensive expansion of the test suite is required.

---

## 18. Dependencies and Packages

Do not introduce new NuGet packages, NPM packages or external dependencies without a valid technical reason.

Check whether the required functionality can be implemented using existing dependencies or built-in framework functionality.

Do not introduce a package merely to replace a small and understandable implementation.

Preserve compatibility with the project's target framework and supported environments.

Do not perform unrelated package upgrades.

Do not introduce breaking dependency changes without explicit authorization.

If a dependency has a known security vulnerability, report it and explain the proposed resolution.

Do not claim that dependencies are secure without performing an appropriate verification.

---

## 19. Security and Sensitive Information

Never hardcode credentials or secrets into source code.

This includes:

- Database passwords.
- API keys.
- Supabase secret keys.
- Authentication tokens.
- Connection strings containing credentials.
- Mobile synchronization keys.
- Personal access tokens.

Use the project's existing configuration mechanisms, environment variables or .NET User Secrets.

Never commit sensitive information to Git.

Do not expose server-side credentials to frontend JavaScript, Razor views or mobile applications.

Preserve existing authentication and authorization boundaries.

Do not weaken Row Level Security, database permissions or access controls to make an implementation easier.

Do not disable certificate validation or other security mechanisms to resolve connectivity problems.

Do not remove existing authentication mechanisms or synchronization keys without explicit authorization.

The public repository MUST remain safe for other developers to clone and use.

Private cloud configuration MUST NOT be required for standalone operation.

---

## 20. Existing Functionality and Scope of Changes

Only implement changes relevant to the requested task.

Existing functionality MUST be preserved unless the project owner explicitly requests a behavioural change.

Do not remove working functionality because it appears unnecessary.

Do not replace an existing implementation with a different approach solely because it is preferred by the AI.

Do not perform unrelated refactoring, redesign or architectural changes.

Do not rename public methods, endpoints, models, HTML identifiers or configuration keys without considering their existing references.

When changing shared functionality, inspect all relevant consumers before making the change.

If a requested implementation conflicts with existing functionality, explain the conflict before making destructive or incompatible changes.

Do not assume that an existing feature is unused simply because its usage is not immediately obvious.

---

## 21. Git and Commit Messages

Do not create Git commits or push changes unless explicitly requested.

When asked to create a commit message, provide a meaningful and explanatory commit message.

Commit messages MUST explain the purpose of the change and its relevant implementation details.

Do not provide short, generic or non-explanatory commit messages.

Incorrect:

```text
Fix stuff
```

Incorrect:

```text
Update tests
```

Correct:

```text
Improve recipe validation and automated test coverage

- Move recipe validation into the appropriate service.
- Add parameterized NUnit tests for invalid and boundary inputs.
- Preserve existing recipe creation and editing behaviour.
- Update XML documentation for affected service methods.
```

Commit messages MUST accurately reflect the changes that were actually implemented.

Do not claim that functionality was implemented, tested or verified when it was not.

Do not modify Git history, force-push, reset branches or discard existing changes without explicit authorization.

---

## 22. Documentation and Comments

Keep project documentation consistent with the actual implementation.

Update relevant README files and setup documentation when changes affect:

- Installation.
- Configuration.
- Dependencies.
- Database setup.
- Authentication.
- Application behaviour.
- Testing.
- Deployment.

Do not add documentation for functionality that does not exist.

Use inline comments when they explain a meaningful decision, complex behaviour or important constraint.

Do not add unnecessary comments that merely repeat what an individual line of code does.

This does not override the mandatory XML documentation requirements.

All new methods and constructors MUST still have XML documentation.

Write documentation and code comments in English unless explicitly instructed otherwise.

---

## 23. Verification Before Completing a Task

Before considering a task complete, the AI MUST inspect its changes against these project rules.

Verify the following where applicable:

- Files are located in the correct folders.
- Architectural responsibilities are correctly separated.
- Controllers do not contain business logic.
- New methods and constructors have XML documentation.
- All required control-flow blocks use explicit curly braces.
- Methods use appropriate block bodies and return statements.
- Code formatting is consistent and readable.
- No unnecessary duplication has been introduced.
- Existing functionality has been preserved.
- Relevant references and namespaces have been updated.
- Relevant tests have been reviewed.
- The project builds successfully.
- Relevant automated tests pass.
- No credentials or sensitive information have been introduced.
- No unrelated files have been modified.

Run relevant build and test commands when the environment permits.

If verification cannot be performed, explicitly state what could not be verified and why.

Do not report a successful build or test run unless it was actually performed successfully.

Do not hide warnings, errors or unresolved problems.

---

## 24. Task Completion and Reporting

After implementing a task, provide a clear summary containing:

1. What was implemented.
2. Which relevant files were created, modified, moved or removed.
3. Important architectural or implementation decisions.
4. Which tests and build commands were executed.
5. The actual results of those checks.
6. Any remaining warnings, limitations or unresolved problems.
7. Any additional manual steps required from the project owner.

Do not describe incomplete work as complete.

Do not make assumptions about requirements that materially change the intended functionality.

When an important requirement is unclear, request clarification before implementing an incompatible solution.

The project owner retains control over architectural decisions, functionality, dependencies, data, deployment and Git operations.

---

## 25. Playwright Tests — Strict Owner-Only Restriction

Playwright testing is exclusively reserved for the project owner to learn, practice and independently develop automated browser tests.

Coding agents MUST NOT:

- Create new Playwright tests.
- Modify or refactor existing Playwright tests.
- Delete, rename or move Playwright test files.
- Execute or attempt to execute Playwright tests.
- Modify Playwright test helpers, fixtures, page objects or configuration.
- Automatically fix failing Playwright tests.
- Generate additional Playwright test coverage.
- Modify Playwright assertions, selectors or test scenarios.
- Run combined test commands that include Playwright tests.

If an application change potentially affects existing Playwright tests, the coding agent MUST report the affected functionality to the project owner without modifying or executing the tests.

The project owner is solely responsible for maintaining, implementing, debugging and executing Playwright tests.

Coding agents MAY explain Playwright concepts and provide educational guidance when explicitly requested, but MUST NOT apply changes to the Playwright test project.

This restriction takes precedence over all previous testing, refactoring, verification and task completion requirements in this document, including Sections 14, 16, 17 and 23.

NUnit unit tests and service-level tests that do not involve Playwright remain unaffected by this restriction.

This is a mandatory restriction, not a recommendation. The coding agent MUST NOT bypass it, even when doing so would resolve a failing test or complete a verification process.


---

## 26. Line Length and Unnecessary Line Wrapping

The coding agent MUST respect the project's configured right margin (hard wrap guide).

Code MUST remain on a single line when the entire statement or declaration fits within the configured right margin and remains readable.

The coding agent MUST NOT introduce unnecessary line breaks or vertically expand code simply because a statement contains multiple parameters, arguments or expressions.

This requirement applies to:

- Class declarations.
- Primary constructors.
- Constructor declarations.
- Method signatures.
- Method calls.
- Parameter and argument lists.
- Variable and constant declarations.
- Return statements.
- Attributes.
- Conditional expressions.
- LINQ expressions.

### Correct — when within the right margin

```csharp
public sealed class DatabaseInitializationController(DatabaseInitializationService initialization, ILogger<DatabaseInitializationController> logger) : Controller
{
}
```

### Incorrect — when the declaration fits on a single line

```csharp
public sealed class DatabaseInitializationController(
    DatabaseInitializationService initialization,
    ILogger<DatabaseInitializationController> logger) : Controller
{
}
```

### Correct

```csharp
return new DrinkSearchResult(true, Combine(local, cached));
```

### Incorrect

```csharp
return new DrinkSearchResult(
    true,
    Combine(local, cached)
);
```

### Mandatory Formatting Requirements

- ALWAYS use the available horizontal space before introducing line breaks.
- ONLY wrap a statement or declaration when it exceeds the configured right margin or when required by another mandatory formatting rule.
- NEVER place each parameter or argument on a separate line when the complete declaration or call fits within the right margin.
- NEVER shorten meaningful variable, method or class names merely to avoid exceeding the right margin.
- NEVER vertically expand existing code without a valid formatting requirement.
- NEVER change the project's right margin or formatting configuration without explicit authorization.
- Inspect existing `.editorconfig` and Rider formatting settings to determine the configured line length.

When a line exceeds the configured right margin, split it at appropriate syntactical boundaries and maintain consistent indentation.

The objective is to maximize readable horizontal space while avoiding unnecessary vertical expansion.

This requirement reinforces Section 5 and MUST be followed throughout the entire project, including production code and test code.

The mandatory curly brace, explicit return and XML documentation requirements remain unchanged. This rule does not authorize single-line control-flow blocks or expression-bodied methods.

This is a mandatory formatting requirement, not a stylistic suggestion.



## Final Requirement

Every change MUST contribute to maintaining or improving the following qualities:

- Readability.
- Maintainability.
- Reliability.
- Testability.
- Performance.
- Scalability.
- Security.
- Consistency.

These qualities apply equally to production code, test code, configuration, documentation and project structure.

The AI MUST follow these project rules throughout every task.

Do not treat these instructions as optional recommendations.
