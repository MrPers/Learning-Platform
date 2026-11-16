# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

.NET 10 SDK; solution file is `ELearningPlatform.slnx`. `global.json` selects the **Microsoft.Testing.Platform** runner, so `dotnet test` needs `--solution` (not a bare path argument).

```bash
dotnet build ELearningPlatform.slnx
dotnet run --project ELearningPlatform.API            # https://localhost:7250, docs at /scalar/v1, Hangfire at /hangfire (Development only)
dotnet test --solution ELearningPlatform.slnx
dotnet test --project tests/ELearningPlatform.Application.Tests
dotnet test --project tests/ELearningPlatform.API.Tests -- --filter-method "*LoginTests.*"   # xUnit v3 filters go after `--`
```

Test projects are xUnit v3 executables (`OutputType=Exe`); they can also be run directly with `dotnet run --project tests/<name>`.

EF migrations (startup project is the API, migrations live in Infrastructure):

```bash
dotnet ef migrations add <Name> --project ELearningPlatform.Infrastructure --startup-project ELearningPlatform.API --output-dir Persistence/Migrations
```

There is no linter config, Dockerfile or CI.

## Configuration and secrets

- The app **refuses to start** while required settings are empty: every settings class is bound with `ValidateDataAnnotations().ValidateOnStart()` (`Infrastructure/DependencyInjection.cs`).
- Secrets go in `ELearningPlatform.API/appsettings.Secrets.json` (git-ignored; template is `appsettings.Secrets.example.json`). `Program.cs` inserts it right after the last `appsettings*.json` source, so env vars (`JWT__SecretKey`) and CLI args still override it.
- On startup `InitializeDatabaseAsync` runs `MigrateAsync`, seeds roles + admin, and in Development also seeds dev teachers/students. Tests skip this with `Database:SkipInitialization` where needed.
- Stripe test keys (`Stripe:SecretKey`, `PublishableKey`, `WebhookSecret`) are required at startup. Locally: `stripe listen --forward-to https://localhost:7250/api/payments/webhooks/stripe` prints the webhook secret.
- Storage is SQLite for both the app (`elearning.db`) and Hangfire (`hangfire.db`); `Hangfire:Storage=InMemory` switches Hangfire to memory (used by tests).

## Architecture

Clean Architecture; dependency direction: `API → Application/Infrastructure → Domain/Shared`. Application defines interfaces, Infrastructure implements them.

- **No repository / unit-of-work layer** (despite what the README says). Services in `Application/Services/*` use `IAppDbContext` directly; it resolves to the same scoped `AppDbContext` that Identity's `UserManager` uses, so they share one change tracker. Application references EF Core (and `Microsoft.AspNetCore.App`) deliberately.
- **Mapping is explicit** EF projections in `Application/Mapping/Mappings.cs`, no AutoMapper. Keep projections translatable to SQL.
- **Result pattern**: services return `Result` / `Result<T>` with an `Error` (code + `ErrorType`) instead of throwing. Controllers convert via `ToActionResult(this)` (`API/Extensions/ResultExtensions.cs`), which maps `ErrorType` → HTTP status and emits RFC 9457 problem details with a `code` extension. Unhandled exceptions go through `GlobalExceptionHandler`.
- **Validation**: FluentValidation validators live in `Application/Validators` and are run by the API's `FluentValidationFilter` (MVC filter), not by FluentValidation's deprecated auto-validation. New DTOs need a validator registered by assembly scan (automatic).
- **Auth**: `AddIdentityCore` (no cookies) + JWT bearer. Claims are not inbound-mapped (`MapInboundClaims=false`; name claim is `sub`, role claim is `JwtTokenService.RoleClaim`). Refresh tokens are stored hashed and delivered via cookie (`RefreshTokenCookie`). Auth is split into `AuthService`, `RegistrationService`, `PasswordService`, `ExternalLoginService` (Google) and `AuthTokenIssuer`; 2FA via email/SMS. Roles: Admin, Teacher, Student, with policies in `Shared/Constants/PoliciesConstants`.
- **Rate limiting**: the `auth` policy (per-IP fixed window, `RateLimiting:AuthPermitLimit`) is applied to anonymous auth endpoints.
- **Background work**: email/SMS are sent via Hangfire jobs (`NotificationService`, `AppEmailService`); HTML email templates in `Infrastructure/EmailTemplates` are copied to output.
- **Uploads**: `MediaService` writes under wwwroot; `Application/Common/Files` validates extension, size limits (`Uploads` settings) and magic-number signatures.
- **Payments (FinTech core)** live in `Application/Services/Payments`:
  - Double-entry ledger: `LedgerTransaction` + `LedgerEntry` (debit/credit, `BalanceAfter`). Every transaction balances, balance = credits − debits, so the sum over all wallets is always 0 (`GET api/admin/finance/reconciliation` checks this). System wallets: `PlatformRevenue`, `ExternalClearing` (Stripe money in/out, goes negative), `PayoutHold`. Only `Ledger.Post`/`ReverseAsync` change balances.
  - Ledger and `AuditLogs` are **append-only**: `AppDbContext` throws on update/delete and the `PaymentsLedger` migration adds SQLite triggers. Refunds and rejected payouts are reversal transactions (`ReversalOfId`), never deletes.
  - Every money operation runs in `FinanceTransaction.ExecuteAsync` (one DB transaction, `BEGIN IMMEDIATE` on SQLite, retry on `DbUpdateConcurrencyException` against `Wallet.RowVersion`). Don't call `SaveChangesAsync` inside the operation; the runner saves and commits.
  - Money is `decimal(18,2)` + 3-letter currency (single platform currency, `Payments:Currency`). SQLite stores decimals as TEXT, so **sum/compare decimals in memory**, not in SQL.
  - Stripe: `IPaymentGateway` (outgoing, substituted in tests) and `IPaymentWebhookParser` (signature check, real in tests with `ApiFactory.StripeWebhookSecret`). Top-ups are credited only by the `payment_intent.succeeded` webhook; webhook events are deduplicated in `ProcessedWebhookEvents`.
  - Money POST endpoints carry `[Idempotent]` (requires `Idempotency-Key`, stores the response per user+key) and `[EnableRateLimiting(RateLimitPolicies.Payments)]` (per user). Never log amounts, client secrets or keys.
  - `IAuditable` entities are audited by `AuditSaveChangesInterceptor` (who/what/when/old/new) in the same SaveChanges.
- Fuzzy course/video search uses FuzzySharp (`Application/Common/FuzzySearch.cs`); list endpoints use `PaginatedResult` with absolute links built via `ControllerExtensions.CurrentUrl`.

## Tests

Three projects in `tests/`, all xUnit v3 + NSubstitute:

- `Application.Tests`: service tests run against the **real EF model on in-memory SQLite** (`TestInfrastructure/ServiceTestContext`), not the EF InMemory provider. Application/Infrastructure expose internals to their test projects via `InternalsVisibleTo`.
- `Infrastructure.Tests`: migrations, seeding, DI validation, mail/SMS/JWT/Google services.
- `API.Tests`: `ApiFactory` (a `WebApplicationFactory<Program>`) boots the real API with a temp SQLite DB and web root per factory, in-memory Hangfire, rate limit raised, and fakes/substitutes for email, SMS, mail and Google validation. Provide any new required setting in `ApiFactory.Settings`, otherwise startup validation fails every API test.
