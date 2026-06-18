namespace AssetMgmt.Domain.Entities;

public class Department
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public Guid? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public User? Manager { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
