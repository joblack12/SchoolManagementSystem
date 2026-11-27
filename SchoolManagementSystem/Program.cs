using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Repositories;
using Syncfusion.Licensing;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SchoolManagementSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });

            if (OperatingSystem.IsWindows())
            {
                builder.Host.UseWindowsService(options =>
                {
                    options.ServiceName = "SchoolManagementService";
                });
            }

            // Use C:\ProgramData\SchoolManagementSystem on Windows; keep ContentRootPath on Linux/Docker
            string baseDataPath = OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SchoolManagementSystem")
                : builder.Environment.ContentRootPath;

            string keysPath = Path.Combine(baseDataPath, "keys");
            string logsPath = Path.Combine(baseDataPath, "Logs");
            string uploadsPath = Path.Combine(baseDataPath, "uploads");

            Directory.CreateDirectory(keysPath);
            Directory.CreateDirectory(logsPath);
            Directory.CreateDirectory(uploadsPath);

            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName("SchoolManagementSystem");

            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddFile(Path.Combine(logsPath, "SchoolManagementSystem-{Date}.txt"));

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Identity/Account/Login";
                options.LogoutPath = "/Identity/Account/Logout";
                options.AccessDeniedPath = "/Identity/Account/AccessDenied";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

            builder.Services.AddDbContext<SchoolDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlServerOptionsAction: sqlOptions =>
                    {
                        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                        sqlOptions.CommandTimeout(120);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    }));

            builder.Services.AddIdentity<User, IdentityRole>(cfg =>
            {
                cfg.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;

                cfg.SignIn.RequireConfirmedEmail = false;
                cfg.User.RequireUniqueEmail = true;

                cfg.Password.RequireDigit = false;
                cfg.Password.RequiredUniqueChars = 0;
                cfg.Password.RequireUppercase = false;
                cfg.Password.RequireLowercase = false;
                cfg.Password.RequireNonAlphanumeric = false;
                cfg.Password.RequiredLength = 6;
            })
                .AddDefaultTokenProviders()
                            .AddEntityFrameworkStores<SchoolDbContext>();



            builder.Services.AddAuthentication()
                .AddJwtBearer(cfg =>
                {
                    cfg.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = builder.Configuration["Tokens:Issuer"],
                        ValidAudience = builder.Configuration["Tokens:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Tokens:Key"] ?? throw new InvalidOperationException("JWT Key is missing.")))
                    };
                });

            builder.Services.AddScoped<IAlertRepository, AlertRepository>();
            builder.Services.AddScoped<ICourseRepository, CourseRepository>();
            builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
            builder.Services.AddScoped<IGradeRepository, GradeRepository>();
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
            builder.Services.AddScoped<ISchoolClassRepository, SchoolClassRepository>();
            builder.Services.AddScoped<IStudentRepository, StudentRepository>();
            builder.Services.AddScoped<ISubjectRepository, SubjectRepository>();
            builder.Services.AddScoped<ITeacherRepository, TeacherRepository>();
            builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            builder.Services.AddScoped<IUserHelper, UserHelper>();

            if (string.IsNullOrWhiteSpace(builder.Configuration["Mail:Password"]) || builder.Configuration["Mail:Password"] == "your-16-char-app-password")
            {
                builder.Services.AddTransient<IMailHelper, DevelopmentMailHelper>();
            }
            else
            {
                builder.Services.AddTransient<IMailHelper, MailHelper>();
            }


            builder.Services.AddScoped<IConverterHelper, ConverterHelper>();
            builder.Services.AddScoped<IBlobHelper, LocalFileHelper>();
            builder.Services.AddTransient<SeedDb>();

            SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NDaF5cWWtCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWH9ec3RTRWhfWUx3XUY=");

            var app = builder.Build();

            var forwardedOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto
            };

            forwardedOptions.KnownIPNetworks.Clear();
            forwardedOptions.KnownProxies.Clear();

            _ = Task.Run(async () =>
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                var stoppingToken = app.Lifetime.ApplicationStopping;

                int retryCount = 0;
                int maxRetries = 12;
                bool databaseReady = false;

                while (!databaseReady && retryCount < maxRetries && !stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        retryCount++;

                        // Create a fresh scope on every attempt so DbContext state is never stale
                        using var scope = app.Services.CreateScope();
                        var services = scope.ServiceProvider;
                        var context = services.GetRequiredService<SchoolDbContext>();

                        logger.LogInformation("Attempting to connect and migrate database (Attempt {Retry} of {Max})...", retryCount, maxRetries);

                        if ((await context.Database.GetPendingMigrationsAsync(stoppingToken)).Any())
                        {
                            await context.Database.MigrateAsync(stoppingToken);
                        }

                        var seeder = services.GetRequiredService<SeedDb>();
                        await seeder.SeedAsync();

                        databaseReady = true;
                        logger.LogInformation("Database migration and seeding completed successfully.");
                    }
                    catch (OperationCanceledException)
                    {
                        // Service is shutting down; exit cleanly
                        break;
                    }
                    catch (SqlException ex)
                    {
                        logger.LogWarning("SQL Server is not ready yet (Error {Number}): {Message}. Waiting 10 seconds before retry {Retry}/{Max}...",
                            ex.Number, ex.Message, retryCount, maxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An unexpected error occurred while migrating or seeding the database (Attempt {Retry}/{Max}).", retryCount, maxRetries);
                        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    }
                }

                if (!databaseReady && !stoppingToken.IsCancellationRequested)
                {
                    logger.LogCritical("Database failed to initialize after {Max} attempts.", maxRetries);
                }
            });


            if (!app.Environment.IsDevelopment())
            {
                app.UseStatusCodePagesWithReExecute("/Error/{0}");
            }

            app.UseStaticFiles();

            // On Windows, serve uploaded photos/documents from C:\ProgramData\SchoolManagementSystem\uploads
            if (OperatingSystem.IsWindows())
            {
                app.UseStaticFiles(new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(uploadsPath),
                    RequestPath = "/uploads"
                });
            }

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.MapRazorPages();

            await app.RunAsync();
        }
    }
}
