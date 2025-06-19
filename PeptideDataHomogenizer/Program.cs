using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PeptideDataHomogenizer;
using PeptideDataHomogenizer.Components;
using PeptideDataHomogenizer.Components.Account;
using PeptideDataHomogenizer.Data;
using PeptideDataHomogenizer.State;
using PeptideDataHomogenizer.Tools;
using PeptideDataHomogenizer.Tools.ElsevierTools;
using PeptideDataHomogenizer.Tools.HtmlTools;
using PeptideDataHomogenizer.Tools.NotCurrentlyInUse;
using PeptideDataHomogenizer.Tools.PubMedSearch;
using PeptideDataHomogenizer.Tools.RegexExtractors;
using PeptideDataHomogenizer.Tools.WileyTools;

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
builder.Services.AddScoped<IEUtilitiesService, EUtilitiesService>();
builder.Services.AddScoped<IPubMedAPIConsumer, PubMedAPIConsumer>();
builder.Services.AddScoped<IPageFetcher, PageFetcher>();
builder.Services.AddScoped<IElsevierArticleFetcher, ElsevierArticleFetcher>();
builder.Services.AddScoped<IFullArticleDownloader, FullArticleDownloader>();
builder.Services.AddScoped<PythonRegexProteinDataExtractor>();
builder.Services.AddScoped<PDBRecordsExtractor>();
builder.Services.AddScoped<LLMSimulationLengthExtractor>();
builder.Services.AddScoped<PDBContextDataExtractor>();
builder.Services.AddScoped<ArticleBrowserState>();
builder.Services.AddTransient<DatabaseDataHandler>(provider =>
    new DatabaseDataHandler(provider.GetRequiredService<ApplicationDbContext>()));

builder.Services.AddScoped<WileyArticleDownloader>(provider =>
   new WileyArticleDownloader(
       "\"27687327-2ab6-489e-9efb-350133f42584\"",
       provider.GetRequiredService<ArticleExtractorFromHtml>()
   ));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});



// Add to services collection
builder.Services.AddHttpClient();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddScoped<CookieEvents>();
builder.Services.ConfigureApplicationCookie(opt =>
        opt.EventsType = typeof(CookieEvents)
    );

var app = builder.Build();


app.UseCors(options => options.AllowAnyOrigin());
// --- Start Python Flask API when the app starts ---

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

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();



app.Run();