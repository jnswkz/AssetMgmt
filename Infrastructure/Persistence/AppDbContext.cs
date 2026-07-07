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
    public DbSet<AssetDisposal> AssetDisposals => Set<AssetDisposal>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReturnObligation> ReturnObligations => Set<ReturnObligation>();
    public DbSet<InventoryScan> InventoryScans => Set<InventoryScan>();
    public DbSet<InventoryScanItem> InventoryScanItems => Set<InventoryScanItem>();
    public DbSet<AiAgentConversation> AiAgentConversations => Set<AiAgentConversation>();
    public DbSet<AiAgentMessage> AiAgentMessages => Set<AiAgentMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
