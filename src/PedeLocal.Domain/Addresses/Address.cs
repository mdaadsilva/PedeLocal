using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Addresses;

public enum AddressType
{
    Urban,
    Rural
}

public class Address : BaseEntity
{
    public Guid CityId { get; set; }
    public Guid? UserId { get; set; }

    public AddressType Type { get; set; }
    public string? Label { get; set; }

    // Urban
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? District { get; set; }
    public string? Complement { get; set; }
    public string? ZipCode { get; set; }

    // Common / rural
    public string? Reference { get; set; }

    // Rural
    public string? PropertyName { get; set; }
    public string? Road { get; set; }
    public string? Kilometer { get; set; }

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? GooglePlaceId { get; set; }
}
