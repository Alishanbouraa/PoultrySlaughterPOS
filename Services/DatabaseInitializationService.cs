using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PoultrySlaughterPOS.Data;

namespace PoultrySlaughterPOS.Services
{
    public interface IDatabaseInitializationService
    {
        Task InitializeAsync();
        Task<bool> TestConnectionAsync();
    }

    public class DatabaseInitializationService : IDatabaseInitializationService
    {
        private readonly IDbContextFactory<PoultryDbContext> _contextFactory;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(IDbContextFactory<PoultryDbContext> contextFactory, ILogger<DatabaseInitializationService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Starting database initialization...");

                // Test connection first
                if (await TestConnectionAsync())
                {
                    _logger.LogInformation("Database connection successful.");

                    using var context = await _contextFactory.CreateDbContextAsync();

                    // Create database if it doesn't exist
                    await context.Database.EnsureCreatedAsync();
                    _logger.LogInformation("Database created/verified successfully.");

                    // Apply any pending migrations
                    var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                    if (pendingMigrations.Any())
                    {
                        _logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations...");
                        await context.Database.MigrateAsync();
                        _logger.LogInformation("Migrations applied successfully.");
                    }

                    // Verify tables exist
                    await VerifyTablesAsync();

                    _logger.LogInformation("Database initialization completed successfully.");
                }
                else
                {
                    _logger.LogError("Database connection failed. Please check your connection string.");
                    throw new InvalidOperationException("Cannot connect to database. Please verify SQL Server LocalDB is installed and running.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing database connection...");
                using var context = await _contextFactory.CreateDbContextAsync();
                await context.Database.OpenConnectionAsync();
                await context.Database.CloseConnectionAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed: {Message}", ex.Message);
                return false;
            }
        }

        private async Task VerifyTablesAsync()
        {
            try
            {
                using var context = await _contextFactory.CreateDbContextAsync();

                // Check if tables exist by querying each DbSet
                var trucksCount = await context.Trucks.CountAsync();
                var customersCount = await context.Customers.CountAsync();
                var truckLoadsCount = await context.TruckLoads.CountAsync();
                var invoicesCount = await context.Invoices.CountAsync();
                var paymentsCount = await context.Payments.CountAsync();
                var reconciliationsCount = await context.DailyReconciliations.CountAsync();
                var auditLogsCount = await context.AuditLogs.CountAsync();

                _logger.LogInformation("Database verification completed:");
                _logger.LogInformation("- Trucks: {TrucksCount} records", trucksCount);
                _logger.LogInformation("- Customers: {CustomersCount} records", customersCount);
                _logger.LogInformation("- Truck Loads: {TruckLoadsCount} records", truckLoadsCount);
                _logger.LogInformation("- Invoices: {InvoicesCount} records", invoicesCount);
                _logger.LogInformation("- Payments: {PaymentsCount} records", paymentsCount);
                _logger.LogInformation("- Daily Reconciliations: {ReconciliationsCount} records", reconciliationsCount);
                _logger.LogInformation("- Audit Logs: {AuditLogsCount} records", auditLogsCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Table verification failed: {Message}", ex.Message);
                throw;
            }
        }
    }
}