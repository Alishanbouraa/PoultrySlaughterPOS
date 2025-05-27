using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;
using PoultrySlaughterPOS.Services;
using Serilog;
using System.IO;
using System.Windows;

namespace PoultrySlaughterPOS
{
    public partial class App : Application
    {
        private IHost? _host;

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                // Configure Serilog
                Log.Logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File("logs/pos-log-.txt", rollingInterval: RollingInterval.Day)
                    .CreateLogger();

                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Configure host with dependency injection
                _host = Host.CreateDefaultBuilder()
                    .UseSerilog()
                    .ConfigureServices((context, services) =>
                    {
                        ConfigureServices(services, configuration);
                    })
                    .Build();

                // Start the host
                await _host.StartAsync();

                // Initialize database
                using (var scope = _host.Services.CreateScope())
                {
                    var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
                    await dbInitService.InitializeAsync();
                }

                // Create and show main window through DI
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();

                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup failed");
                MessageBox.Show($"فشل في تشغيل البرنامج:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add configuration
            services.AddSingleton(configuration);

            // Add Entity Framework with connection pooling
            services.AddDbContext<PoultryDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.EnableSensitiveDataLogging(true); // Only for development
                options.LogTo(Console.WriteLine, LogLevel.Information);
            }, ServiceLifetime.Scoped);

            // Add DbContext Factory for concurrent operations
            services.AddDbContextFactory<PoultryDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });

            // Add services
            services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();

            // Add main window as singleton (only one instance needed)
            services.AddSingleton<MainWindow>();

            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddSerilog();
            });
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }

            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}