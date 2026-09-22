namespace PedeLocal.Domain.Drivers;

/// <summary>The CNH (Carteira Nacional de Habilitação) data a driver submits for admin review.
/// There is no automatic validation against Senatran or any other official registry here — this is
/// purely what the driver declares, backed by an optional photo, for a human SUPER_ADMIN to review
/// and decide on via <see cref="Driver.OnboardingStatus"/>. Keyed directly on DriverId (1:1, same
/// pattern as <see cref="Deliveries.DeliveryConfirmation"/>) rather than through a separate Id,
/// since a driver has exactly one current license record.</summary>
public class DriverLicense
{
    public Guid DriverId { get; set; }

    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>CNH category as declared by the driver (e.g. "A", "B", "AB") — free text because the
    /// valid category combinations are a Detran/Senatran concern, not something this system
    /// validates or enforces.</summary>
    public string Category { get; set; } = string.Empty;

    public DateOnly ExpiresAt { get; set; }

    /// <summary>EAR — Exercício de Atividade Remunerada — required for drivers who use a motorcycle
    /// to work professionally in Brazil. Not applicable to every vehicle type, hence nullable.</summary>
    public bool? HasEar { get; set; }

    /// <summary>URL returned by <c>POST /api/uploads/image</c> — this entity never receives or
    /// stores the file itself, only the reference.</summary>
    public string? DocumentImageUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
