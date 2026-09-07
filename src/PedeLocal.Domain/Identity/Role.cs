namespace PedeLocal.Domain.Identity;

public static class RoleNames
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Customer = "CUSTOMER";
    public const string Driver = "DRIVER";
    public const string EstablishmentAdmin = "ESTABLISHMENT_ADMIN";
    public const string EstablishmentManager = "ESTABLISHMENT_MANAGER";
    public const string Attendant = "ATTENDANT";
    public const string StockOperator = "STOCK_OPERATOR";

    public static readonly string[] All =
    {
        SuperAdmin, Customer, Driver, EstablishmentAdmin, EstablishmentManager, Attendant, StockOperator
    };
}

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
