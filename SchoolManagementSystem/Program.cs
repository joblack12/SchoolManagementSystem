using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SchoolManagementSystem.Data;
using SchoolManagementSystem.Data.Entities;
using SchoolManagementSystem.Helpers;
using SchoolManagementSystem.Repositories;
using System.Text;
using Syncfusion.Licensing;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Hosting;
using System.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using SymmetricSecurityKey = Microsoft.IdentityModel.Tokens.SymmetricSecurityKey;
using Microsoft.Data.SqlClient;

namespace SchoolManagementSystem
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Logging Configuration
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddFile("Logs/SchoolManagementSystem-{Date}.txt");

            // 2. Add controllers and views (MVC support)
            builder.Services.AddControllersWithViews();

            // 3. Database Context Configuration
            builder.Services.AddDbContext<SchoolDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                    sqlServerOptionsAction: sqlOptions =>
                    {
                        // 💡 Enables automatic retries during container startup delays
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                    }));

            // In Identity configuration (for local development you can set to false)
            builder.Services.AddIdentity<User, IdentityRole>(cfg =>
            {
                cfg.Tokens.AuthenticatorTokenProvider = TokenOptions.DefaultAuthenticatorProvider;
                cfg.SignIn.RequireConfirmedEmail = false; // <-- set false for dev
                cfg.User.RequireUniqueEmail = true;

                // Password settings
                cfg.Password.RequireDigit = false;
                cfg.Password.RequiredUniqueChars = 0;
                cfg.Password.RequireUppercase = false;
                cfg.Password.RequireLowercase = false;
                cfg.Password.RequireNonAlphanumeric = false;
                cfg.Password.RequiredLength = 6;
            })
            .AddDefaultTokenProviders()
            .AddEntityFrameworkStores<SchoolDbContext>();

            // 5. Authentication & JWT Configuration
            builder.Services.AddAuthentication()
                .AddCookie()
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

            // 6. Application Repositories
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

            // Generic Repository
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            // 7. Core Helpers & Infrastructure 
            builder.Services.AddScoped<IUserHelper, UserHelper>();

            // Replace the existing AddTransient registration for IMailHelper with environment-aware registration:
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddTransient<IMailHelper, DevelopmentMailHelper>();
            }
            else
            {
                builder.Services.AddTransient<IMailHelper, MailHelper>();
            }

            builder.Services.AddScoped<IConverterHelper, ConverterHelper>();

            // 8. Local Storage Integration (Replaced Azure Blob Storage)
            builder.Services.AddScoped<IBlobHelper, LocalFileHelper>();

            // 9. Database Seeder Registration
            builder.Services.AddTransient<SeedDb>();

            // 10. Syncfusion License Registration
            SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NDaF5cWWtCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWH9ec3RTRWhfWUx3XUY=");

            var app = builder.Build();

            // 11. Seed Database & Roles Execution
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();

                // 💡 Add resilience to handle the SQL Server initialization delay
                int retryCount = 0;
                int maxRetries = 6;
                bool databaseReady = false;

                while (!databaseReady && retryCount < maxRetries)
                {
                    try
                    {
                        retryCount++;
                        var context = services.GetRequiredService<SchoolDbContext>();

                        logger.LogInformation("Attempting to connect and migrate database (Attempt {Retry} of {Max})...", retryCount, maxRetries);

                        // Automatically applies migrations if missing
                        if (context.Database.GetPendingMigrations().Any())
                        {
                            context.Database.Migrate();
                        }

                        // Trigger your seeding logic safely once connection is verified
                        var seeder = services.GetRequiredService<SeedDb>(); // Adjust to match your exact DI structure
                        await seeder.SeedAsync();

                        databaseReady = true;
                        logger.LogInformation("Database migration and seeding completed successfully.");
                    }
                    catch (SqlException ex) when (ex.Number == -2 || ex.Message.Contains("Timeout"))
                    {
                        logger.LogWarning("SQL Server is still booting up. Waiting 10 seconds before retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
                        // Wait before retrying to allow transient issues to resolve
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }
            }

            app.UseStatusCodePagesWithReExecute("/Error/{0}");

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Authentication must run before Authorization
            app.UseAuthentication();
            app.UseAuthorization();

            // 13. Route Mapping configurations
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // 💡 Automatically apply database migrations on application startup
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<SchoolDbContext>();

                    // This will automatically create the database and apply all migrations safely
                    if (context.Database.GetPendingMigrations().Any())
                    {
                        context.Database.Migrate();
                    }
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
            }

            await app.RunAsync();
        }
    }
}