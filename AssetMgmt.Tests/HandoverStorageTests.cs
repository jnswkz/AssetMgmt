using AssetMgmt.Application.Handover;
using AssetMgmt.Domain.Entities;
using AssetMgmt.Domain.Enums;
using AssetMgmt.Infrastructure.Persistence;
using AssetMgmt.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace AssetMgmt.Tests;

public class HandoverStorageTests
{
    [Fact]
    public async Task LegacyPublicFile_IsCopiedToPrivateStorage_AndOriginalIsKept()
    {
        var temp = CreateTemp();
        try
        {
            var webRoot = Path.Combine(temp, "wwwroot");
            var privateRoot = Path.Combine(temp, "private", "handovers");
            Directory.CreateDirectory(Path.Combine(webRoot, "handovers"));
            var source = Path.Combine(webRoot, "handovers", "BB-2026-0001.pdf");
            await File.WriteAllTextAsync(source, "pdf");
            await using var db = CreateDb();
            var doc = Document("/handovers/BB-2026-0001.pdf");
            db.HandoverDocuments.Add(doc);
            await db.SaveChangesAsync();
            var service = CreateService(db, temp, webRoot, privateRoot);

            await service.MigrateLegacyFilesAsync(default);

            Assert.Equal($"{doc.Id:N}.pdf", doc.FilePath);
            Assert.True(File.Exists(Path.Combine(privateRoot, doc.FilePath)));
            Assert.True(File.Exists(source));
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    [Fact]
    public async Task StorageKey_PathTraversal_IsRejected()
    {
        var temp = CreateTemp();
        try
        {
            var webRoot = Path.Combine(temp, "wwwroot");
            var privateRoot = Path.Combine(temp, "private", "handovers");
            Directory.CreateDirectory(webRoot);
            await using var db = CreateDb();
            var request = RequestGraph();
            db.Add(request.Request);
            db.Add(request.Allocation!);
            db.HandoverDocuments.Add(Document("../secret.pdf", request.Allocation!));
            await db.SaveChangesAsync();
            var service = CreateService(db, temp, webRoot, privateRoot);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GetFileForRequestAsync(request.Request.Id, default));
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static HandoverDocumentService CreateService(
        AppDbContext db, string contentRoot, string webRoot, string privateRoot) => new(
            db,
            new TestEnvironment(contentRoot, webRoot),
            Options.Create(new HandoverStorageOptions { HandoverRoot = privateRoot }));

    private static HandoverDocument Document(string path, Allocation? allocation = null) => new()
    {
        Id = Guid.NewGuid(),
        DocumentNumber = $"BB-{Guid.NewGuid():N}",
        AllocationId = allocation?.Id ?? Guid.NewGuid(),
        Allocation = allocation!,
        FilePath = path,
        GeneratedBy = Guid.NewGuid()
    };

    private static (AllocationRequest Request, Allocation? Allocation) RequestGraph()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = "u", NormalizedUserName = "U",
            Email = "u@example.test", NormalizedEmail = "U@EXAMPLE.TEST", PasswordHash = "x",
            EmployeeCode = "U1", FullName = "User"
        };
        var model = new AssetModel { Id = Guid.NewGuid(), Name = "Laptop", Category = AssetCategory.Laptop };
        var asset = new AssetInstance
        {
            Id = Guid.NewGuid(), AssetCode = "A1", Serial = "S1", Model = model
        };
        var request = new AllocationRequest
        {
            Id = Guid.NewGuid(), Requester = user, RequesterId = user.Id,
            AssetInstance = asset, AssetInstanceId = asset.Id, IdempotencyKey = "k",
            HandoverDueAt = DateTime.UtcNow
        };
        var allocation = new Allocation
        {
            Id = Guid.NewGuid(), AllocationRequest = request, AllocationRequestId = request.Id,
            User = user, UserId = user.Id, AssetInstance = asset, AssetInstanceId = asset.Id,
            EventType = AllocationEventType.Allocated, StartDate = DateTime.UtcNow,
            CreatedBy = user.Id
        };
        return (request, allocation);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static string CreateTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), $"asset-mgmt-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestEnvironment(string contentRoot, string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AssetMgmt.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
