using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using PeptideDataHomogenizer;
using PeptideDataHomogenizer.Components;
using PeptideDataHomogenizer.Components.Account;
using PeptideDataHomogenizer.Data;
using PeptideDataHomogenizer.Services;
using PeptideDataHomogenizer.State;
using PeptideDataHomogenizer.Tools;
using PeptideDataHomogenizer.Tools.ElsevierTools;
using PeptideDataHomogenizer.Tools.HtmlTools;
using PeptideDataHomogenizer.Tools.PubMedSearch;
using PeptideDataHomogenizer.Tools.RegexExtractors;
using PeptideDataHomogenizer.Tools.WileyTools;
using System.IO;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Shared Services:
builder.Services.AddTransient<ArticleExtractorFromHtml>();
builder.Services.AddTransient<ImagesExtractorFromHtml>();
builder.Services.AddScoped<IEUtilitiesService, EUtilitiesService>();
builder.Services.AddScoped<IPubMedAPIConsumer, PubMedAPIConsumer>();
builder.Services.AddScoped<IPageFetcher, PageFetcher>();
// Update the registration of IElsevierArticleFetcher to use a factory method
builder.Services.AddScoped<IElsevierArticleFetcher, ElsevierArticleFetcher>(provider =>
    new ElsevierArticleFetcher(provider.GetRequiredService<HttpClient>(),
        provider.GetRequiredService<IWebHostEnvironment>(),
        builder.Configuration["EnvironmentVariables:ElsevierAPIKey"]
    ));
builder.Services.AddScoped<IFullArticleDownloader, FullArticleDownloader>();
builder.Services.AddScoped<PDBRecordsExtractor>();
builder.Services.AddScoped<LLMSimulationLengthExtractor>();
builder.Services.AddScoped<PDBContextDataExtractor>();
builder.Services.AddScoped<ArticleBrowserState>();

//Services
builder.Services.AddTransient<OrganizationService>();
builder.Services.AddTransient<UserProjectService>();
builder.Services.AddTransient<ArticleContentService>();
builder.Services.AddTransient<ArticleModerationService>();
builder.Services.AddTransient<ArticleService>();
builder.Services.AddTransient<ProjectService>();
builder.Services.AddTransient<UserService>();
builder.Services.AddTransient<ProteinDataService>();
builder.Services.AddTransient<UserOrganizationService>();
builder.Services.AddTransient<ArticlePerProjectService>();
builder.Services.AddTransient<ProteinDataPerProjectService>();
builder.Services.AddTransient<JournalsService>();
builder.Services.AddTransient<PublishersService>();

builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddTransient<DatabaseDataHandler>(provider =>
    new DatabaseDataHandler(provider.GetRequiredService<ApplicationDbContext>()));

builder.Services.AddScoped<WileyArticleDownloader>(provider =>
   new WileyArticleDownloader(
       builder.Configuration["EnvironmentVariables:WileyAPIKey"],
       provider.GetRequiredService<ArticleExtractorFromHtml>()
   ));

builder.Services.AddScoped<EncryptionHelper>(provider => 
    new EncryptionHelper(Encoding.UTF8.GetBytes(builder.Configuration["EnvironmentVariables:EncryptionKey"])));

builder.Services.AddScoped<ContextCookieManager>();
builder.Services.AddScoped<ProjectCookieManager>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddHttpClient();


// Use SQLite database in the Data folder
var sqlitePath = Path.Combine(builder.Environment.ContentRootPath, "Data", "Application.db");
var connectionString = $"Data Source={sqlitePath}";

// Ensure the directory exists
var dataDirectory = Path.GetDirectoryName(sqlitePath);
if (!Directory.Exists(dataDirectory))
{
    Directory.CreateDirectory(dataDirectory);
}



////sqlite databse
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

//use sqlserver database from connection string
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//    options.UseSqlServer(connectionString, sqlOptions =>
//    {
//        sqlOptions.MigrationsAssembly("PeptideDataHomogenizer");
//        sqlOptions.EnableRetryOnFailure();
//    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Replace both identity configurations with this single one:
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();


builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddScoped<CookieEvents>();
builder.Services.ConfigureApplicationCookie(opt =>
        opt.EventsType = typeof(CookieEvents)
    );


builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireRole("SuperAdmin"));
    options.AddPolicy("IsAdmin", policy =>
        policy.RequireRole("Admin", "SuperAdmin"));
});

var app = builder.Build();

app.UseCors(options => options.AllowAnyOrigin());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

using (var serviceScope = app.Services.GetService<IServiceScopeFactory>().CreateScope())
{
    var context = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    context.Database.Migrate();
}

//ensure the following roles are created: "User", "Admin", "SuperAdmin"
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var roles = new[] { "User", "Admin", "SuperAdmin" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
}

//ensure the following user is created: "nicola.santolini@live.com" with password "Password123!"
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var email = "fakeuser@admin.com";
    var password = "Password123!";
    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            PhoneNumberConfirmed = true, 
            EmailConfirmed = true 
        };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            // Assign the "SuperAdmin" role to the user
            await userManager.AddToRoleAsync(user, "SuperAdmin");
        }
    }
    else
    {
        // If the user already exists, ensure they are in the "SuperAdmin" role
        if (!await userManager.IsInRoleAsync(user, "SuperAdmin"))
        {
            await userManager.AddToRoleAsync(user, "SuperAdmin");
        }
    }
}

using (var scope = app.Services.CreateScope())
{
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await databaseInitializer.InitializeDatabase();
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();