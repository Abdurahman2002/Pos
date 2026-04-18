using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Localization.Routing;
using NewsApp2.Classes;
using NewsApp2.Classes.Authorization;
using NewsApp2.Classes.Middlewares;
using NewsApp2.Classes.Helpers;
using NewsApp2.Models;
using NewsApp2.Models.Entities;
using NewsApp2.Models.Interfaces;
using NewsApp2.Models.Repositories;
using NewsApp2.Models.UnitOfWork;
using NewsApp2.Models.Services;
using System.Security.Claims;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new DateOnlyModelBinderProvider());
})
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));

builder.Services.AddDbContext<AppDbContext>
    (x => x.UseSqlServer(builder.Configuration.GetConnectionString("DbCon")));

//----------------------------------------------------------------------------
//IdentityUser  �� ���� AspNetUsers
//IdentityRole �� ���� AspNetRoles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 3; // ����� ������ 3 ����
    options.Password.RequiredUniqueChars = 1; // ��� ���� ���� ��� �����
    options.Password.RequireNonAlphanumeric = false; // ��� ������� ������ ����� ������ ��� ��� ���� ��� �����
    options.Password.RequireDigit = true; //��� �� ����� ��� ���
    options.Password.RequireLowercase = false; //�� ���� ���� �����
    options.Password.RequireUppercase = false; //�� ���� ���� �����
    options.Lockout.MaxFailedAccessAttempts = 5; // ����� ������ ��� 5 ������� ���� �����
    options.Lockout.DefaultLockoutTimeSpan =
             TimeSpan.FromMinutes(15); //����� ������ ���� ���� ��� ����� 
    options.SignIn.RequireConfirmedAccount = true; // ����� ������ ����� ����� ������
    options.User.RequireUniqueEmail = true; // ��� ����� �������� ��� ������� ���� �������� ���� �� �����
})
       .AddEntityFrameworkStores<AppDbContext>() //������ �������� ������ ��� ������� ������ ����������� Identity ���
       .AddDefaultTokenProviders(); //������ ���� ������ ��������� ������ ���� ����� ����� ���� ������ ������ ������ ���������� �������� ����� �������� �������� .

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(360);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Error/403";
});

builder.Services.AddScoped<IAuthorizationHandler, ApprovedUserHandler>();


//����� ��� ���� ������� �� ������� ������� ��� �������//DataProtectorTokenProvider//�� ���� ������ ���� ��� ������� ������ ���� ������
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromMinutes(180); // ������ ������ 60 �����
    //options.TokenLifespan = TimeSpan.FromDays(1));
});

//����� security stamp validation ������ �� �������� �������
// ������ ��� SecurityStamp
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(15); //���� ���� ���� ��������//SecurityStamp// ���� ������ �� 15 ����� ���� ����� ��
});



//---------------Use the policy for Authorization Checks--------------------------
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CreatePolicy",
        policy => policy.RequireClaim("Create", "true"));
    
    options.AddPolicy("EditPolicy", 
        policy => policy.RequireClaim("Edit", "true"));

    options.AddPolicy("DeletePolicy", 
        policy => policy.RequireClaim("Delete", "true"));

    options.AddPolicy("EditUserPolicy", 
        policy => policy.RequireClaim("EditUser", "true"));


    options.AddPolicy("SiteStatePolicy", 
        policy => policy.RequireClaim("SiteState", "true"));

    options.AddPolicy("AdminOrProgPolicy",
        policy => policy.RequireRole("Prog", "Admin", "SalesManager")); // ��� ����� ������� ����� ������� �� ������ �� ���������� �������


    options.AddPolicy("ProgOrAdminOrEmployeePolicy", policy =>
    {
        policy.RequireClaim(ClaimTypes.Role, "Admin", "Employee", "Prog"); // ��� ����� ������� ����� ������� �� ������ �� ���������� �������
    });

    options.AddPolicy("SettingsPolicy", policy =>
    {
        policy.RequireClaim("SiteState", "EditUser"); // �����
    });


    options.AddPolicy("EditUserPolicy1", policy =>
        policy.RequireAssertion(p =>
        p.User.IsInRole("Prog") || (p.User.IsInRole("Admin")
        && p.User.HasClaim(claim => claim.Type == "Edit" 
        && claim.Value == "true")))
    );

    options.AddPolicy("ApprovedUserPolicy", policy =>
        policy.Requirements.Add(new ApprovedUserRequirement()));

        options.AddPolicy("InventoryCreatePolicy", policy =>
            policy.RequireAssertion(p =>
                p.User.HasClaim("InventoryCreate", "true") ||
                p.User.IsInRole("Cashier") ||
                p.User.IsInRole("SalesManager") ||
                p.User.IsInRole("SalesOfficer") ||
                p.User.IsInRole("Prog") ||
                p.User.IsInRole("Admin"))
        );

        options.AddPolicy("InventoryEditPolicy", policy =>
            policy.RequireAssertion(p =>
                p.User.HasClaim("InventoryEdit", "true") ||
                p.User.IsInRole("Cashier") ||
                p.User.IsInRole("SalesManager") ||
                p.User.IsInRole("SalesOfficer") ||
                p.User.IsInRole("Prog") ||
                p.User.IsInRole("Admin"))
        );

        options.AddPolicy("InventoryDeletePolicy", policy =>
            policy.RequireAssertion(p =>
                p.User.HasClaim("InventoryDelete", "true") ||
                p.User.IsInRole("SalesManager") ||
                p.User.IsInRole("Prog") ||
                p.User.IsInRole("Admin"))
        );

        options.AddPolicy("InventoryApprovePolicy", policy =>
            policy.RequireAssertion(p =>
                p.User.HasClaim("InventoryApprove", "true") ||
                p.User.IsInRole("SalesManager") ||
                p.User.IsInRole("Prog") ||
                p.User.IsInRole("Admin"))
        );

});
//------------------------------------------------






builder.Services.AddAutoMapper(typeof(MappingProfile));


//appsettings.jason ����� ��� ��������� ������ �������� �������� �� 
builder.Services.AddSingleton(
    builder.Configuration.GetSection("MailSettings").Get<MailSettings>()     ?? new MailSettings()
);

builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<BarcodeScanService>();

//-----------------------------------------
var app = builder.Build();
//-----------------------------------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    db.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'dbo.PosShifts', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PosShifts](
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_PosShifts_Id] DEFAULT NEWID() CONSTRAINT [PK_PosShifts] PRIMARY KEY,
        [OpenedByUserId] NVARCHAR(450) NOT NULL,
        [OpenedByUserName] NVARCHAR(200) NULL,
        [OpeningCashLyd] DECIMAL(18,2) NOT NULL,
        [OpenedAtUtc] DATETIME2 NOT NULL,
        [ClosedAtUtc] DATETIME2 NULL,
        [ClosingCashLyd] DECIMAL(18,2) NULL,
        [Status] NVARCHAR(20) NOT NULL,
        [Note] NVARCHAR(500) NULL,
        [Created] DATETIME2 NOT NULL,
        [Modified] DATETIME2 NULL
    );

    CREATE INDEX [IX_PosShifts_OpenedByUserId_Status] ON [dbo].[PosShifts]([OpenedByUserId], [Status]);
END

IF COL_LENGTH(N'dbo.SalesInvoices', N'PosShiftId') IS NULL
BEGIN
    ALTER TABLE [dbo].[SalesInvoices] ADD [PosShiftId] UNIQUEIDENTIFIER NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_SalesInvoices_PosShiftId'
      AND object_id = OBJECT_ID(N'dbo.SalesInvoices')
)
BEGIN
    CREATE INDEX [IX_SalesInvoices_PosShiftId] ON [dbo].[SalesInvoices]([PosShiftId]);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'FK_SalesInvoices_PosShifts_PosShiftId'
)
BEGIN
    ALTER TABLE [dbo].[SalesInvoices]
    ADD CONSTRAINT [FK_SalesInvoices_PosShifts_PosShiftId]
        FOREIGN KEY ([PosShiftId]) REFERENCES [dbo].[PosShifts]([Id])
        ON DELETE SET NULL;
END

IF COL_LENGTH(N'dbo.Items', N'DefaultSalePriceLyd') IS NULL
BEGIN
    ALTER TABLE [dbo].[Items] ADD [DefaultSalePriceLyd] DECIMAL(18,2) NULL;
END

IF COL_LENGTH(N'dbo.InventorySettings', N'MaxCashierDiscountPercent') IS NULL
BEGIN
    ALTER TABLE [dbo].[InventorySettings] ADD [MaxCashierDiscountPercent] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_InventorySettings_MaxCashierDiscountPercent] DEFAULT (10);
END

IF COL_LENGTH(N'dbo.SalesInvoices', N'PaymentMethod') IS NULL
BEGIN
    ALTER TABLE [dbo].[SalesInvoices] ADD [PaymentMethod] NVARCHAR(20) NOT NULL CONSTRAINT [DF_SalesInvoices_PaymentMethod] DEFAULT (N'Cash');
END

IF COL_LENGTH(N'dbo.SalesInvoices', N'PaymentMethod') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [dbo].[SalesInvoices]
          SET [PaymentMethod] = N''Cash''
          WHERE [PaymentMethod] IS NULL OR LTRIM(RTRIM([PaymentMethod])) = N'''';');
END

DECLARE @DailySalesCustomerSeedId UNIQUEIDENTIFIER = '7e2efb6c-0cb2-430f-92af-6e0ad720f105';
DECLARE @DailySalesCustomerId UNIQUEIDENTIFIER;

SELECT TOP (1) @DailySalesCustomerId = [Id]
FROM [dbo].[Customers]
WHERE [Id] = @DailySalesCustomerSeedId OR [Name] = N'مبيعات يومية'
ORDER BY CASE WHEN [Id] = @DailySalesCustomerSeedId THEN 0 ELSE 1 END;

IF @DailySalesCustomerId IS NULL
BEGIN
    SET @DailySalesCustomerId = @DailySalesCustomerSeedId;

    INSERT INTO [dbo].[Customers] ([Id], [Name], [Phone], [Note], [Created], [Modified])
    VALUES (@DailySalesCustomerId, N'مبيعات يومية', NULL, N'عميل افتراضي لمبيعات الكاش اليومية', SYSUTCDATETIME(), NULL);
END

IF COL_LENGTH(N'dbo.SalesInvoices', N'CustomerId') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalesInvoices', N'PaymentMethod') IS NOT NULL
BEGIN
        UPDATE [dbo].[SalesInvoices]
        SET [CustomerId] = @DailySalesCustomerId
        WHERE ([CustomerId] IS NULL OR [CustomerId] = CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER))
            AND ISNULL(LTRIM(RTRIM([PaymentMethod])), N'Cash') <> N'Credit';
END

IF OBJECT_ID(N'dbo.CustomerReceipts', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CustomerReceipts](
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_CustomerReceipts_Id] DEFAULT NEWID() CONSTRAINT [PK_CustomerReceipts] PRIMARY KEY,
        [CustomerId] UNIQUEIDENTIFIER NOT NULL,
        [PosShiftId] UNIQUEIDENTIFIER NULL,
        [ReceiptDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [Note] nvarchar(500) NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [CreatedByUserName] nvarchar(256) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL
    );

    CREATE INDEX [IX_CustomerReceipts_CustomerId_ReceiptDate] ON [dbo].[CustomerReceipts]([CustomerId], [ReceiptDate]);
    CREATE INDEX [IX_CustomerReceipts_PosShiftId] ON [dbo].[CustomerReceipts]([PosShiftId]);

    ALTER TABLE [dbo].[CustomerReceipts]
    ADD CONSTRAINT [FK_CustomerReceipts_Customers_CustomerId]
        FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customers]([Id])
        ON DELETE NO ACTION;

    ALTER TABLE [dbo].[CustomerReceipts]
    ADD CONSTRAINT [FK_CustomerReceipts_PosShifts_PosShiftId]
        FOREIGN KEY ([PosShiftId]) REFERENCES [dbo].[PosShifts]([Id])
        ON DELETE SET NULL;
END

IF COL_LENGTH(N'dbo.CustomerReceipts', N'PosShiftId') IS NULL
BEGIN
    ALTER TABLE [dbo].[CustomerReceipts] ADD [PosShiftId] UNIQUEIDENTIFIER NULL;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_CustomerReceipts_PosShiftId'
      AND [object_id] = OBJECT_ID(N'dbo.CustomerReceipts'))
BEGIN
    CREATE INDEX [IX_CustomerReceipts_PosShiftId] ON [dbo].[CustomerReceipts]([PosShiftId]);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE [name] = N'FK_CustomerReceipts_PosShifts_PosShiftId')
BEGIN
    ALTER TABLE [dbo].[CustomerReceipts]
    ADD CONSTRAINT [FK_CustomerReceipts_PosShifts_PosShiftId]
        FOREIGN KEY ([PosShiftId]) REFERENCES [dbo].[PosShifts]([Id])
        ON DELETE SET NULL;
END

IF OBJECT_ID(N'dbo.ExpenseEntries', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ExpenseEntries](
        [Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_ExpenseEntries_Id] DEFAULT NEWID() CONSTRAINT [PK_ExpenseEntries] PRIMARY KEY,
        [ExpenseDate] date NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [ExpenseKind] nvarchar(20) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [EmployeeId] uniqueidentifier NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [ReferenceNo] nvarchar(50) NULL,
        [Note] nvarchar(500) NULL,
        [CreatedByUserId] nvarchar(450) NULL,
        [CreatedByUserName] nvarchar(256) NULL,
        [Created] datetime2 NOT NULL,
        [Modified] datetime2 NULL
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_ExpenseEntries_ExpenseDate_ExpenseKind'
      AND [object_id] = OBJECT_ID(N'dbo.ExpenseEntries'))
BEGIN
    CREATE INDEX [IX_ExpenseEntries_ExpenseDate_ExpenseKind] ON [dbo].[ExpenseEntries]([ExpenseDate], [ExpenseKind]);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE [name] = N'IX_ExpenseEntries_EmployeeId_ExpenseDate'
      AND [object_id] = OBJECT_ID(N'dbo.ExpenseEntries'))
BEGIN
    CREATE INDEX [IX_ExpenseEntries_EmployeeId_ExpenseDate] ON [dbo].[ExpenseEntries]([EmployeeId], [ExpenseDate]);
END

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE [name] = N'FK_ExpenseEntries_Employees_EmployeeId')
BEGIN
    ALTER TABLE [dbo].[ExpenseEntries]
    ADD CONSTRAINT [FK_ExpenseEntries_Employees_EmployeeId]
        FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees]([Id])
        ON DELETE NO ACTION;
END

IF COL_LENGTH(N'dbo.ExpenseEntries', N'ExpenseKind') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [dbo].[ExpenseEntries]
          SET [ExpenseKind] = N''General''
          WHERE [ExpenseKind] IS NULL OR LTRIM(RTRIM([ExpenseKind])) = N'''';');
END

IF COL_LENGTH(N'dbo.ExpenseEntries', N'PaymentMethod') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [dbo].[ExpenseEntries]
          SET [PaymentMethod] = N''Cash''
          WHERE [PaymentMethod] IS NULL OR LTRIM(RTRIM([PaymentMethod])) = N'''';');
END

IF COL_LENGTH(N'dbo.ExpenseEntries', N'Category') IS NOT NULL
BEGIN
    EXEC(N'UPDATE [dbo].[ExpenseEntries]
          SET [Category] = N''مصروف عام''
          WHERE [Category] IS NULL OR LTRIM(RTRIM([Category])) = N'''';');
END

");

    var requiredRoles = new[]
    {
        "Prog",
        "Admin",
        "Employee",
        "EmployeePending",
        "Cashier",
        "SalesManager",
        "SalesOfficer"
    };

    foreach (var roleName in requiredRoles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var legacyRoleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["InventoryAdmin"] = "SalesManager",
        ["InventoryClerk"] = "SalesOfficer",
        ["InventoryViewer"] = "SalesOfficer",
        ["EmployeeRequest"] = "EmployeePending"
    };

    foreach (var mapping in legacyRoleMap)
    {
        var oldRole = mapping.Key;
        var newRole = mapping.Value;

        if (!await roleManager.RoleExistsAsync(oldRole))
            continue;

        var usersInLegacyRole = await userManager.GetUsersInRoleAsync(oldRole);
        foreach (var user in usersInLegacyRole)
        {
            if (!await userManager.IsInRoleAsync(user, newRole))
                await userManager.AddToRoleAsync(user, newRole);

            await userManager.RemoveFromRoleAsync(user, oldRole);
        }
    }
}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
}
else if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage(); //���� ����� ���������� ������ �������


app.UseHttpsRedirection();
app.UseStaticFiles();

var defaultCulture = new CultureInfo("ar-IQ");
defaultCulture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
defaultCulture.DateTimeFormat.DateSeparator = "/";

var supportedCultures = new[]
{
    defaultCulture,
    new CultureInfo("en-GB")
};

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(defaultCulture.Name)
    .AddSupportedCultures(supportedCultures.Select(c => c.Name).ToArray())
    .AddSupportedUICultures(supportedCultures.Select(c => c.Name).ToArray());

localizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new QueryStringRequestCultureProvider(),
    new CookieRequestCultureProvider(),
    new AcceptLanguageHeaderRequestCultureProvider(),
    new RouteDataRequestCultureProvider()
};

app.UseRequestLocalization(localizationOptions);

app.UseRouting();

app.UseAuthentication();//Who are you//������ �� ���� �������� ����� ����� ������ ������ ����� ��������� ��������
app.UseAuthorization();//What you allowed to do//������ �� ������ ��������

app.UseMiddleware<LegacyInventoryGuardMiddleware>();

//-------��� �� ���� ��� UseAuthentication/UseAuthorization------------------------
app.UseMiddleware<SiteStateMiddleware>();
//---------------------------------------------------------------------

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=StockBalances}/{action=Index}/{id?}");


app.Run();
