using Gazify.Core.Mapping;
using Gazify.Helpers;
using Gazify.Seeds;
using Gazify.Settings;
using Gazify.Tasks;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Reflection;
using UoN.ExpressiveAnnotations.NetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 8;

    options.User.RequireUniqueEmail = true;
});

builder.Services.AddDataProtection().SetApplicationName(nameof(Gazify));

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddTransient<IEmailBodyBuilder, EmailBodyBuilder>();

builder.Services.AddControllersWithViews();

builder.Services.AddAutoMapper(Assembly.GetAssembly(typeof(MappingProfile)));
builder.Services.Configure<MailSettings>(builder.Configuration.GetSection(nameof(MailSettings)));
builder.Services.AddExpressiveAnnotations();
try
{
    builder.Services.AddHangfire(x => x.UseSqlServerStorage(connectionString));
    builder.Services.AddHangfireServer();
}
catch(Exception ex)
{
    Console.Write(ex);
}
builder.Services.Configure<AuthorizationOptions>(options => options.AddPolicy("Adminsonly", policy =>
{
    policy.RequireAuthenticatedUser();
    policy.RequireRole(AppRoles.Admin);
}));

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseMigrationsEndPoint();
//}
//else
//{
//    app.UseExceptionHandler("/Home/Error");
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}

app.UseMigrationsEndPoint();
//app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();

using var scope = scopeFactory.CreateScope();

var roleManger = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
var userManger = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

await DefaultRoles.SeedAsync(roleManger);
await DefaultUsers.SeedAdminUserAsync(userManger);

//hangfire
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    DashboardTitle = "Gazify Dashboard",
    IsReadOnlyFunc = (DashboardContext context) => true,
    Authorization = new IDashboardAuthorizationFilter[]
    {
        new HangfireAuthorizationFilter("Adminsonly")
    }
});

var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var emailBodyBuilder = scope.ServiceProvider.GetRequiredService<IEmailBodyBuilder>();
var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

var hangfireTasks = new HnagfireTasks(dbContext, emailBodyBuilder, emailSender);

RecurringJob.AddOrUpdate(() => hangfireTasks.PrepareExpirationAlert(), "0 14 * * *");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();


app.Run();
