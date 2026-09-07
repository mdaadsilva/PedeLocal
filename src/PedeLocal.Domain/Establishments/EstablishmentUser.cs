namespace PedeLocal.Domain.Establishments;

/// <summary>Links a staff User to an Establishment with a staff-level role (admin/manager/attendant/stock operator).</summary>
public class EstablishmentUser
{
    public Guid EstablishmentId { get; set; }
    public Guid UserId { get; set; }
    public string StaffRole { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
