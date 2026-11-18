var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
var tiempoSesionMinutos = builder.Configuration.GetValue<int?>("Session:IdleTimeoutMinutes") ?? 30;
if (tiempoSesionMinutos <= 0)
{
    throw new InvalidOperationException("Session:IdleTimeoutMinutes debe ser mayor que cero.");
}

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(tiempoSesionMinutos);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddHttpClient<EasyMenu.Web.Services.EasyMenuApiClient>((services, client) =>
{
    var apiBaseUrl = builder.Configuration["ApiBaseUrl"]
        ?? throw new InvalidOperationException("Falta configurar ApiBaseUrl.");
    client.BaseAddress = new Uri($"{apiBaseUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(90);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
