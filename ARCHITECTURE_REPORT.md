# NewsApp2 Architecture Report

Date: 2026-01-17

This document summarizes the existing architecture, conventions, and patterns of the NewsApp2 ASP.NET Core MVC application. It is strictly descriptive (Stage 1) and does not propose any changes.

## Overview
- ASP.NET Core MVC application targeting .NET 9 (bin/net9.0 present).
- Uses ASP.NET Core Identity, EF Core with SQL Server, DI, custom middleware, AutoMapper.
- Follows a generic Repository + UnitOfWork pattern; controllers orchestrate data operations directly (no separate service layer).

## Project Structure
Top-level folders and notable files:
- Program: [Program.cs](Program.cs)
- Configuration: [appsettings.json](appsettings.json), [appsettings.Development.json](appsettings.Development.json)
- Data:
  - DbContext: [Models/AppDbContext.cs](Models/AppDbContext.cs)
  - Seeding & model config: [Models/ModelBuilderExtensions.cs](Models/ModelBuilderExtensions.cs)
  - Entities: [Models/Entities](Models/Entities)
- Identity:
  - User entity: [Models/Entities/ApplicationUser.cs](Models/Entities/ApplicationUser.cs)
  - Static claims: [Models/Entities/StaticClaims.cs](Models/Entities/StaticClaims.cs)
- Data Access:
  - Interfaces: [Models/Interfaces/IGRepository.cs](Models/Interfaces/IGRepository.cs), [Models/Interfaces/IUnitOfWork.cs](Models/Interfaces/IUnitOfWork.cs)
  - Repository: [Models/Repositories/GRepository.cs](Models/Repositories/GRepository.cs)
  - UnitOfWork: [Models/UnitOfWork/UnitOfWork.cs](Models/UnitOfWork/UnitOfWork.cs)
- Controllers: [Controllers](Controllers)
  - Examples: [Controllers/NewsController.cs](Controllers/NewsController.cs), [Controllers/SectionsController.cs](Controllers/SectionsController.cs), Identity-related controllers: [Controllers/AccountController.cs](Controllers/AccountController.cs), [Controllers/UsersController.cs](Controllers/UsersController.cs), [Controllers/RolesController.cs](Controllers/RolesController.cs)
  - Error handling: [Controllers/ErrorController.cs](Controllers/ErrorController.cs)
- UI:
  - Views: [Views](Views)
  - Layout switching: [Classes/ViewLayoutAttribute.cs](Classes/ViewLayoutAttribute.cs), [Views/_ViewStart.cshtml](Views/_ViewStart.cshtml), [Views/_ViewImports.cshtml](Views/_ViewImports.cshtml)
  - ViewComponents: [ViewComponents](ViewComponents)
- Middleware: [Classes/Middlewares/SiteStateMiddleware.cs](Classes/Middlewares/SiteStateMiddleware.cs)
- Mapping: [Classes/MappingProfile.cs](Classes/MappingProfile.cs)
- Email: [Models/Repositories/EmailSender.cs](Models/Repositories/EmailSender.cs), config in `MailSettings` section of appsettings.

## Startup & Middleware Pipeline
Defined in [Program.cs](Program.cs):
- DI registrations:
  - `AddControllersWithViews()`
  - UnitOfWork generic: `AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>))`
  - DbContext: `AddDbContext<AppDbContext>(x => x.UseSqlServer(Configuration["ConnectionStrings:DbCon"]))`
  - Identity: `AddIdentity<ApplicationUser, IdentityRole>(options => ...)`
    - Stores: `.AddEntityFrameworkStores<AppDbContext>()`
    - Tokens: `.AddDefaultTokenProviders()`
  - AutoMapper: `AddAutoMapper(typeof(MappingProfile))`
  - Settings: `AddSingleton(builder.Configuration.GetSection("MailSettings").Get<MailSettings>() ?? new MailSettings())`
  - Email sender: `AddScoped<IEmailSender, EmailSender>()`
- Authorization policies:
  - `CreatePolicy`, `EditPolicy`, `DeletePolicy`, `EditUserPolicy`, `SiteStatePolicy`, `UserProfilePolicy`, `AdminOrProgPolicy`, `ProgOrAdminOrEmployeePolicy`, `SettingsPolicy`, `EditUserPolicy1` (assertion-based)
- Pipeline:
  - In non-development: `UseExceptionHandler("/Error")`, `UseStatusCodePagesWithReExecute("/Error/{0}")`
  - In development: `UseDeveloperExceptionPage()`
  - Common: `UseHttpsRedirection()`, `UseStaticFiles()`, `UseRouting()`, `UseAuthentication()`, `UseAuthorization()`
  - Custom middleware: `UseMiddleware<SiteStateMiddleware>()` (after auth)
  - Routing: default route `{controller=Home}/{action=Index}/{id?}`

## Identity & Authorization
- User type: `ApplicationUser : IdentityUser` ([Models/Entities/ApplicationUser.cs](Models/Entities/ApplicationUser.cs)) with extra fields: `Age`, `LastAccessTime`, timestamps, `Approval`, navigation to `UserProfile` and `Employee`.
- DbContext inheritance: `AppDbContext : IdentityDbContext<IdentityUser>` ([Models/AppDbContext.cs](Models/AppDbContext.cs)). Identity is registered for `ApplicationUser` and `IdentityRole` in DI.
- Password & lockout settings configured in `AddIdentity` options.
- Security stamp validation interval: 15 minutes.
- Email confirmation required: `RequireConfirmedAccount = true`; unique email: `RequireUniqueEmail = true`.
- Policies (claim/role-based):
  - Role-based: `AdminOrProgPolicy` (roles `Prog`, `Admin`), `ProgOrAdminOrEmployeePolicy` (via `ClaimTypes.Role` with values `Admin`, `Employee`, `Prog`).
  - Claim-based: `Create`, `Edit`, `Delete`, `EditUser`, `SiteState`, `UserProfile` all must be `true`.
  - Assertion-based: `EditUserPolicy1`: allow `Prog` OR (`Admin` with `Edit=true`).
- Static claims catalog: [Models/Entities/StaticClaims.cs](Models/Entities/StaticClaims.cs). Used to ensure lead developer has all required claims at login ([Controllers/AccountController.cs](Controllers/AccountController.cs)).
- Roles management, user management flows in [Controllers/RolesController.cs](Controllers/RolesController.cs) and [Controllers/UsersController.cs](Controllers/UsersController.cs).
- Seeding: `ModelBuilderExtensions.Seed()` populates `SiteInfo`, `Contact`, `SiteState`, creates role `Prog`, seeds a `Programmer@Gmail.com` user and assigns it to `Prog` ([Models/ModelBuilderExtensions.cs](Models/ModelBuilderExtensions.cs)).

## Data Access Pattern
- Generic Repository (`IGRepository<T>` + `GRepository<T>`) provides:
  - `Insert`, `Update` (attach + set `Modified` state), `Delete`
  - `GetAll(tracking?)`, `GetWhere(filter, tracking?)`, `GetByIdAsync`, `Include(params expressions)` returning `IQueryable<T>` (AsNoTracking by default)
- UnitOfWork (`IUnitOfWork<T>` + `UnitOfWork<T>`) encapsulates repository and `SaveAsync()`.
- Controllers typically inject `IUnitOfWork<TEntity>` and in some cases `AppDbContext` for simple existence checks.
- No explicit service layer; business operations are performed inside controllers with repository/UoW.

## EF Core & DbContext
- DbContext: [Models/AppDbContext.cs](Models/AppDbContext.cs)
  - DbSets: `Contact`, `Sections`, `News`, `SiteInfo`, `SiteState`, `UserProfiles`, `Employees`.
  - Keys default to GUID with SQL `NEWID()`.
  - Relationships:
    - `ApplicationUser` 1:1 `UserProfile` with FK `UserProfile.UserId` (cascade delete).
    - `ApplicationUser` 1:1 `Employee` with FK `Employee.UserId` (cascade delete).
  - Seeding via `modelBuilder.Seed()`.

## Validation
- DataAnnotations across Entities and ViewModels:
  - Examples: `News.Title` `[StringLength(1000, MinimumLength=3)]`, `News.Details` `[Required]`, `News.SectionId` `[Required]`.
  - `Section.Name` uses `[MaxLength]`, `[MinLength]`, and client/server remote validation via `[Remote(action: "NameExists", controller: "Sections")]`.
  - Identity ViewModels: `[EmailAddress]`, `[DataType(DataType.Password)]`, `[Range]`, `[Required]` with error messages.
  - `[ValidateNever]` used on navigation properties to skip validation (`ApplicationUser.UserProfile`, etc.).
- No FluentValidation observed; validation is DataAnnotations-based plus Remote checks.

## Mapping
- AutoMapper configured via [Classes/MappingProfile.cs](Classes/MappingProfile.cs) and added in DI.
- Examples include mapping `SiteVM -> SiteInfo`, `SiteVM -> Contact` via custom `ITypeConverter`, plus demonstrations of mapping config with `ContactVM`.
- Controllers primarily pass entities directly to views; view models are used in Identity and some settings-related flows.

## Error & Exception Handling
- Pipeline-level:
  - `UseExceptionHandler("/Error")` routes to [Controllers/ErrorController.cs](Controllers/ErrorController.cs#L20) `Error()` to show exception details (path + message).
  - `UseStatusCodePagesWithReExecute("/Error/{0}")` routes status codes to `HttpStatusCodeHandler` for friendly messages (404 shows NotFound view).
- Controller-level: try/catch blocks around data operations set `ViewBag.ErrorTitle`/`ViewBag.ErrorMessage` and return `Error`/`NotFound` views.

## Custom Middleware
- [Classes/Middlewares/SiteStateMiddleware.cs](Classes/Middlewares/SiteStateMiddleware.cs):
  - Reads `SiteState` (cached for 360 minutes) via `IUnitOfWork<SiteState>`.
  - If site is closed and user is not `Prog`, redirects to `/Home/Closing` except for `/account/login` and `/home/closing` paths.
  - Executes after `UseAuthentication/UseAuthorization`.

## Views Organization
- Layout selection per controller via `[ViewLayout("_LayoutDashboard")]` or `[ViewLayout("_Layout")]`. Actual layout chosen in [Views/_ViewStart.cshtml](Views/_ViewStart.cshtml) using `ViewData["Layout"]` fallback to `_Layout`.
- Views organized per controller folder under [Views](Views) (e.g., `News`, `Sections`, `Account`, `Users`, `Roles`, `Shared`).
- `_ViewImports.cshtml` brings in namespaces and TagHelpers, and injects `IAuthorizationService`.
- ViewComponents for role/claims counts (`UserRolesViewComponent`, `UserClaimsViewComponent`) and `NewsCountViewComponent`.

## Conventions & Patterns
- Controllers:
  - Use attributes: `[Authorize(Roles=...)]` or `[Authorize(Policy=...)]`, `[ViewLayout(...)]`.
  - CRUD actions use typical signatures: `Index`, `Details`, `Create` (GET/POST), `Edit` (GET/POST), `Delete` (GET/POST `DeleteConfirmed`).
  - Model binding with `[Bind(...)]` and `[ValidateAntiForgeryToken]` for POST.
  - Use `ViewData`/`ViewBag` for passing select lists and status messages.
  - File upload handled via base controller utility (`UploadFile`, `DeleteOldFile`, `CheckImgExtension`).
- Entities:
  - Inherit from `BaseEntity` (GUID `Id`, UTC `Created`, nullable `Modified`, local time helpers).
  - Use DataAnnotations for validation, `NotMapped` for transient fields (e.g., file upload `IFormFile`).
- Repository/UoW:
  - AsNoTracking by default; `Include(...)` helper for eager loading.
  - `Update` uses attach + set `EntityState.Modified`.
- Identity:
  - Email confirmation enforced; roles/claims govern admin capabilities.
  - Seeding for `Prog` role and programmer user.
- Views:
  - Layout set via attribute; `_ViewStart` resolves per-request.

## Reference Files (to emulate for new models)
When adding a new model, mimic these reference implementations:
- Controller (CRUD pattern + authorization + layout):
  - [Controllers/NewsController.cs](Controllers/NewsController.cs)
  - [Controllers/SectionsController.cs](Controllers/SectionsController.cs)
- Data Access:
  - DI registration (already generic): `IUnitOfWork<TEntity>` is injectable; use `Repository` methods (GetAll/GetWhere/Include/Insert/Update/Delete, SaveAsync).
  - Repository: [Models/Repositories/GRepository.cs](Models/Repositories/GRepository.cs)
  - UnitOfWork: [Models/UnitOfWork/UnitOfWork.cs](Models/UnitOfWork/UnitOfWork.cs)
- Entity base & validation:
  - [Models/Entities/BaseEntity.cs](Models/Entities/BaseEntity.cs)
  - Example entity patterns: [Models/Entities/News.cs](Models/Entities/News.cs), [Models/Entities/Section.cs](Models/Entities/Section.cs)
- Views & layout:
  - Attribute: [Classes/ViewLayoutAttribute.cs](Classes/ViewLayoutAttribute.cs)
  - `_ViewStart` and imports: [Views/_ViewStart.cshtml](Views/_ViewStart.cshtml), [Views/_ViewImports.cshtml](Views/_ViewImports.cshtml)
- Identity flows (if needed for access control):
  - [Controllers/UsersController.cs](Controllers/UsersController.cs), [Controllers/RolesController.cs](Controllers/RolesController.cs), [Controllers/AccountController.cs](Controllers/AccountController.cs)
- Error handling pattern:
  - [Controllers/ErrorController.cs](Controllers/ErrorController.cs)
- Mapping (if using view models):
  - [Classes/MappingProfile.cs](Classes/MappingProfile.cs)

## Notable Extension Methods & DI Registrations
- `ModelBuilderExtensions.Seed()` (EF Core model seeding).
- DI registrations are done directly in [Program.cs](Program.cs); no custom service-registration extension methods are used for modules.

---
End of Stage 1. This report captures the current architecture without proposing any transformation.
