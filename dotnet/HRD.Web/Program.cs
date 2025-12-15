using HRD.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Load secrets.json
var secretsPath = Path.Combine(Directory.GetCurrentDirectory(), "../../secrets.json");
if (File.Exists(secretsPath))
{
    builder.Configuration.AddJsonFile(secretsPath, optional: true, reloadOnChange: true);
}
else
{
    // Try to find it if we are deeper or in bin? 
    // Usually in dev root is fine.
    // Let's assume user dev setup: /Users/kowalski/work/hrd-api/secrets.json
    builder.Configuration.AddJsonFile("/Users/kowalski/work/hrd-api/secrets.json", optional: true, reloadOnChange: true);
}

builder.Services.AddScoped<HRD.Api.HRDApi>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var login = config["login"];
    var password = config["password"];
    var apiKeyHex = config["apiKeyHex"];

    if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(apiKeyHex))
    {
        // Fallback or throw?
        // Let's just create unauthenticated one or throw
        throw new Exception("Missing API Credentials in configuration");
    }

    return HRD.Api.HRDApi.Create(apiKeyHex, login, password);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
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

app.Run();
