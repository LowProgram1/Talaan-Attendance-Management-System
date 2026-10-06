using AMIS.API;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is required");
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(connection));
builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(o => {
    o.Password.RequiredLength = 12;
    o.Password.RequireDigit = true;
    o.Password.RequireLowercase = true;
    o.Password.RequireUppercase = true;
    o.Password.RequireNonAlphanumeric = true;
    o.User.RequireUniqueEmail = true;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(o => {
    o.Cookie.Name = "amis.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization(o => { o.AddPolicy("Administrator", p => p.RequireRole("Administrator")); o.AddPolicy("Staff", p => p.RequireRole("Administrator", "Teacher")); });
builder.Services.AddScoped<EmailService>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
var frontendOrigins = new[] { builder.Configuration["FRONTEND_URL"] ?? "http://localhost:3000", builder.Configuration["PUBLIC_FRONTEND_URL"] }
    .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.TrimEnd('/')).Distinct().ToArray();
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p.WithOrigins(frontendOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("frontend");
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)) {
        var origin = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && !frontendOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    foreach (var role in new[] { "Administrator", "Teacher", "Guardian" }) if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var adminEmail = app.Configuration["ADMIN_EMAIL"];
    var adminPassword = app.Configuration["ADMIN_PASSWORD"];
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword) && await users.FindByEmailAsync(adminEmail) is null) {
        var admin = new AppUser { UserName = adminEmail, Email = adminEmail, FullName = "Administrator", EmailConfirmed = true };
        var result = await users.CreateAsync(admin, adminPassword);
        if (!result.Succeeded) throw new InvalidOperationException("Initial admin password does not meet policy: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(admin, "Administrator");
    }
}
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapAuth();
app.MapAcademics();
app.MapAttendance();
app.MapSchool();
app.MapImport();
app.Run();
