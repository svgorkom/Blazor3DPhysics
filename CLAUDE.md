# CLAUDE.md — AI Assistant Contribution Guide (Blazor)

**Purpose**  
This document defines the rules for AI-assisted changes to this Blazor codebase. It ensures **high confidence**, **consistency**, **traceability**, and **zero hallucinations** across code, tests, and docs.

---

## 1) Scope & Goals

- **Scope:** All code, tests, docs, configurations, scripts, and infrastructure in this repository.
- **Goals:**
  1. Keep contributions **reliable, compilable, and testable**.
  2. Enforce a **consistent style** and **mandatory documentation** for public APIs.
  3. Ensure **docs are updated with each behavioral change**.
  4. Avoid hallucinations via **verification protocol** and **confidence levels**.

---

## 2) TL;DR for AI Assistants

- ✅ **Only submit compilable, tested code** that passes `dotnet build`, `dotnet test`, and `dotnet format`.
- ✅ **Document all public APIs** with XML docs (`<summary>`, `<param>`, `<returns>`, `<exception>`).
- ✅ **Update relevant `.md` docs** for any change in behavior, public surface, or user flows.
- ✅ **Declare a confidence level** (`High | Medium | Low`) with evidence (commands run, files changed, tests added).
- ❌ **No invented APIs, flags, or behavior.** If unsure, state **“Unknown”** and propose a minimal, verified alternative.
- ❌ **No hidden TODOs.** Use `// TODO(AI-<date>): <action>` and open an issue if not resolved in the PR.

---

## 3) Project Overview (fill in)

- **Product / Domain:** _[What the app does, who uses it]_
- **Blazor Hosting Model:** _Server | WebAssembly | Hybrid_
- **Auth Model:** _[e.g., OpenID Connect / Azure AD / Cookie]_
- **Data Layer:** _[EF Core? Dapper? REST/gRPC?]_
- **Key Packages:** _[list with versions]_
- **Runtime Target:** **.NET 8 (LTS)** unless specified otherwise.

> Keep this section up to date when the architecture or dependencies change.

---

## 4) Solution Layout

```
repo-root/
  src/
    App/                  # Blazor client or server app
    App.Components/       # Reusable components (optional)
    App.Domain/           # Entities, value objects, domain services
    App.Application/      # Use-cases, DTOs, validators
    App.Infrastructure/   # Data access, external integrations
  tests/
    App.Tests/            # Unit tests
    App.ComponentTests/   # bUnit tests for components (optional)
    App.E2E/              # Playwright E2E (optional)
  docs/
    architecture.md
    user-guide.md
    changelog.md
    …
  build/
    Directory.Build.props
    Directory.Build.targets
  .editorconfig
  CLAUDE.md (this file)
```

---

## 5) Code Style & Quality Gates

### 5.1 EditorConfig (required)
Place at repo root:

```ini
# .editorconfig
root = true

[*.cs]
charset = utf-8-bom
indent_style = space
indent_size = 4
insert_final_newline = true
end_of_line = lf

# C# style
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_namespace_declarations = file_scoped:suggestion
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_event = false:suggestion

# Nullable + warnings as errors will be enforced in props
```

### 5.2 Enforce analyzers and warnings-as-errors

Create `build/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- Optional additional analyzers -->
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.507" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

### 5.3 Formatting

All PRs must pass:

```bash
dotnet build
dotnet format --verify-no-changes
dotnet test
```

---

## 6) Documentation & Docstrings (Mandatory)

### 6.1 XML docs required for public APIs

- Every `public` type/member must include XML docs:
  - `<summary>` — _one sentence, present tense_
  - `<param>` for each parameter
  - `<returns>` if non-void
  - `<exception>` for each thrown exception
  - `<remarks>` for non-obvious behavior/side effects

Enable `GenerateDocumentationFile` in projects where public APIs exist:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn> <!-- Allow missing docs warnings ONLY where justified -->
</PropertyGroup>
```

> Do **not** broadly suppress CS1591 in public libraries unless there’s a documented exception.

### 6.2 Example (service class)

```csharp
/// <summary>Issues customer invoices and persists their status.</summary>
public interface IInvoiceService
{
    /// <summary>Generates an invoice for a given order.</summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The created invoice identifier.</returns>
    /// <exception cref="OrderNotFoundException">Thrown when the order does not exist.</exception>
    Task<Guid> GenerateAsync(Guid orderId, CancellationToken cancellationToken = default);
}
```

### 6.3 Blazor components documentation

- Document **public** component parameters (`[Parameter]`) and callbacks.
- Prefer **code-behind** partial classes for complex logic:

**`OrderSummary.razor`**
```razor
@inherits OrderSummaryBase
<div class="order-summary">
    <h3>Order @OrderId</h3>
    <button @onclick="OnRefreshClicked">Refresh</button>
</div>
```

**`OrderSummary.razor.cs`**
```csharp
public partial class OrderSummaryBase : ComponentBase
{
    /// <summary>The order identifier to render.</summary>
    [Parameter] public Guid OrderId { get; set; }

    /// <summary>Raised when a refresh is requested by the user.</summary>
    [Parameter] public EventCallback<Guid> RefreshRequested { get; set; }

    protected async Task OnRefreshClicked()
        => await RefreshRequested.InvokeAsync(OrderId);
}
```

---

## 7) Blazor Conventions

- **Naming:** Components `PascalCase` (`OrderSummary.razor`), event callbacks end with `Changed` or reflect action (`RefreshRequested`).
- **Parameters:** Use `[Parameter]` and `[CascadingParameter]` explicitly; avoid mutable parameters; prefer `EventCallback<T>`.
- **Code-behind:** Use `.razor.cs` for non-trivial logic; keep `.razor` mostly declarative.
- **CSS isolation:** Prefer `Component.razor.css`.
- **Routing:** Declare with `@page "/path"` in page components. Keep route params typed (`@page "/orders/{OrderId:guid}"`).
- **State:** Minimize global state. Prefer DI and cascading values; consider a store pattern (e.g., Fluxor) if complexity grows.
- **JS Interop:** Use `IJSRuntime` via typed wrappers/facades; keep JS minimal and isolated; handle exceptions and `CancellationToken`.
- **Forms & Validation:** Use `EditForm` with `DataAnnotationsValidator`; put validation attributes in the shared DTOs/ViewModels.
- **Auth:** Use `AuthorizeView` and `@attribute [Authorize]` where needed; guard server endpoints independently of UI.
- **Performance:** Use `Virtualize` for long lists; override `ShouldRender` when beneficial; prefer `RenderFragment` composition.

---

## 8) Error Handling, Logging, and Nullability

- **Nullability:** `nullable enable` is required (see props). Avoid `!` (`null-forgiving`) unless justified in comments.
- **Exceptions:** Throw specific exceptions; document via `<exception/>`. Don’t swallow exceptions; log with context.
- **Logging:** Use `ILogger<T>`; log at appropriate levels (`Warning`/`Error` with correlation IDs if available).
- **Async/Cancellation:** Propagate `CancellationToken`; never `async void` except event handlers.

---

## 9) Testing Policy

- **Unit tests:** Mandatory for services, helpers, and business logic.  
- **Component tests (bUnit):** For important components (rendering, parameters, and events).  
- **E2E (Playwright):** For critical flows (auth, checkout, etc.) if enabled.  
- **Coverage target:** _[Set target, e.g., 80% for core projects]_  
- **Command:** `dotnet test --configuration Release`

Add at least one test per bug fix and one per new feature branch.

---

## 10) Git Workflow & Commit Conventions

- **Branching:** `feature/<topic>`, `bugfix/<id>`, `chore/<task>`.
- **Conventional Commits:**  
  - `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `perf:`, `build:`, `ci:`, `chore:`  
  - Example: `feat: add OrderSummary component with refresh callback`
- **One topic per PR**; keep PRs small and focused.

---

## 11) Documentation Maintenance (every change)

Update relevant docs when:
- Public API changes
- Behavior or UX changes
- Config/env variables change
- Dependencies/versions change

**Mandatory files to consider:**
- `docs/architecture.md`
- `docs/user-guide.md`
- `docs/changelog.md` (append under “Unreleased”)
- Component/state diagrams if impacted

---

## 12) PR Checklist (Paste into PR template)

- [ ] Code builds locally: `dotnet build`
- [ ] Code formatted: `dotnet format` (no diffs)
- [ ] Tests added/updated and pass: `dotnet test`
- [ ] Public APIs documented (XML)
- [ ] Blazor components follow conventions (parameters, callbacks, routing)
- [ ] Relevant docs updated (`.md` in `/docs` or root)
- [ ] Breaking changes documented in `docs/changelog.md`
- [ ] **AI:** Declared confidence level with evidence (see next section)

---

## 13) AI Usage & “No Hallucinations” Protocol

### 13.1 Confidence Levels

- **High:**  
  - Build + tests pass locally;  
  - Changes are minimal and grounded in existing code;  
  - New APIs are fully documented and tested.
- **Medium:**  
  - Build passes;  
  - Limited uncertainty (clearly flagged as `TODO(AI-YYYY-MM-DD)` with rationale).
- **Low:**  
  - Significant unknowns;  
  - Provide a design note and request human review before implementation.

> PRs should target **High** confidence. If Medium/Low, explain why and propose the smallest safe change plus tests.

### 13.2 Required PR Evidence (AI-authored changes)

Include a “Verification” section in the PR body:

```
## Verification
- Build: ✅ dotnet build
- Format: ✅ dotnet format --verify-no-changes
- Tests: ✅ dotnet test (Results: X passed / 0 failed)
- Manual: ✅ Click-tested page /orders (SSR), auth flow unchanged
- Docs: ✅ Updated docs/architecture.md and docs/changelog.md
- Confidence: **High**
```

### 13.3 What to do when unsure

- Do **not** invent framework APIs, CLI flags, or configuration values.
- Write a minimal, compilable alternative; clearly mark unknowns:
  `// TODO(AI-YYYY-MM-DD): Confirm correct event pattern for ...`
- Open a linked issue if resolution is out of scope of the PR.

---

## 14) Automation (CI) Examples

> Adjust to your CI system (GitHub Actions/Azure DevOps/etc.).

**GitHub Actions: `.github/workflows/ci.yml`**
```yaml
name: CI

on:
  pull_request:
    branches: [ main ]
  push:
    branches: [ main ]

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore
      - name: Format Check
        run: dotnet format --verify-no-changes
      - name: Test
        run: dotnet test --configuration Release --no-build --collect "XPlat Code Coverage"
  docs-guard:
    runs-on: ubuntu-latest
    if: ${{ github.event_name == 'pull_request' }}
    steps:
      - uses: actions/checkout@v4
      - name: Require docs when src changes
        run: |
          CHANGED_SRC=$(git diff --name-only origin/${{ github.base_ref }}... | grep -E '^src/' || true)
          CHANGED_DOCS=$(git diff --name-only origin/${{ github.base_ref }}... | grep -E '(^docs/|\.md$)' || true)
          if [ -n "$CHANGED_SRC" ] && [ -z "$CHANGED_DOCS" ]; then
            echo "Source changed but no docs updated (.md). Please update docs."
            exit 1
          fi
```

---

## 15) Security & Secrets

- **No secrets in repo.** Use `dotnet user-secrets` (dev) and secure key vaults (prod).
- Sanitize logs; never log PII or credentials.
- Review external script/CDN usage for integrity and CSP compliance.

---

## 16) Example Patterns

### 16.1 Dependency Injection

```csharp
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
// Prefer interface-first; keep concrete types internal where possible.
```

### 16.2 JS Interop Wrapper

```csharp
public interface IClipboardJs
{
    Task WriteTextAsync(string text, CancellationToken ct = default);
}

internal sealed class ClipboardJs(IJSRuntime js) : IClipboardJs
{
    public async Task WriteTextAsync(string text, CancellationToken ct = default)
        => await js.InvokeVoidAsync("navigator.clipboard.writeText", ct, text);
}
```

### 16.3 Component Event Pattern

```csharp
[Parameter] public EventCallback<Guid> SelectedChanged { get; set; }

private async Task SelectAsync(Guid id) => await SelectedChanged.InvokeAsync(id);
```

---

## 17) Editor/Tooling Recommendations

- **VS / VS Code**: Enable format-on-save; install C# Dev Kit; enable Roslyn analyzers.
- Consider adding `.vscode/extensions.json` and `.vscode/settings.json` to standardize workspace behavior.

---

## 18) Governance & Exceptions

- Any exception to these rules must be stated in the PR with rationale and time-bounded follow-up.
- This file is **living documentation**—update it as the project evolves.

---

### Appendices

**A. Conventional Commit Examples**
- `feat: add invoice generation use-case`
- `fix: prevent double submission in OrderSummary`
- `docs: describe SSR auth flow`
- `test: add bUnit tests for search component`
- `refactor: extract price calculation to domain service`

**B. Useful Commands**
```bash
dotnet new install Microsoft.Azure.Templates   # Example, if used
dotnet workload list
dotnet watch run
```
