using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
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
using System.IO;

var builder = WebApplication.CreateBuilder(args);

try
{
    var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
    Directory.CreateDirectory(dataProtectionPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
        .SetApplicationName("NewsApp2");
}
catch
{
    builder.Services.AddDataProtection()
        .SetApplicationName("NewsApp2");
}

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new DateOnlyModelBinderProvider());
    options.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());
})
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));

builder.Services.AddDbContext<AppDbContext>(x =>
    x.UseSqlServer(builder.Configuration.GetConnectionString("DbCon"), sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), null);
        sqlOptions.CommandTimeout(10);
    })
);

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
    options.ExpireTimeSpan = TimeSpan.FromDays(180);
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
        policy.RequireClaim("SiteState", "true");
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






//appsettings.jason ����� ��� ��������� ������ �������� �������� �� 
builder.Services.AddSingleton(
    builder.Configuration.GetSection("MailSettings").Get<MailSettings>()     ?? new MailSettings()
);

builder.Services.AddScoped<IEmailSender, EmailSender>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddScoped<BarcodeScanService>();
builder.Services.AddScoped<InventoryService>();

//-----------------------------------------
var app = builder.Build();
//-----------------------------------------

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed at startup. App will continue but database may be outdated.");
    }

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

}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseStatusCodePagesWithReExecute("/Error/{0}");
}
else
    app.UseDeveloperExceptionPage(); //���� ����� ���������� ������ �������


app.UseHttpsRedirection();
app.UseStaticFiles();

var defaultCulture = new CultureInfo("ar-IQ");
defaultCulture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
defaultCulture.DateTimeFormat.DateSeparator = "/";
defaultCulture.DateTimeFormat.Calendar = new GregorianCalendar();
defaultCulture.NumberFormat.DigitSubstitution = DigitShapes.None;

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
