using AssetMgmt.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssetMgmt.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AssetModel> AssetModels => Set<AssetModel>();
    public DbSet<AssetInstance> AssetInstances => Set<AssetInstance>();
    public DbSet<AllocationRequest> AllocationRequests => Set<AllocationRequest>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<HandoverDocument> HandoverDocuments => Set<HandoverDocument>();
    public DbSet<DepreciationPolicy> DepreciationPolicies => Set<DepreciationPolicy>();
    public DbSet<DepreciationLedger> DepreciationLedger => Set<DepreciationLedger>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
