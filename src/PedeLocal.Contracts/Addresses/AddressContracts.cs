namespace PedeLocal.Contracts.Addresses;

public record CreateAddressRequest(
    Guid CityId,
    string Type, // "Urban" | "Rural"
    string? Label,
    string? Street,
    string? Number,
    string? District,
    string? Complement,
    string? Reference,
    string? ZipCode,
    string? PropertyName,
    string? Road,
    string? Kilometer,
    decimal Latitude,
    decimal Longitude,
    string? GooglePlaceId);

public record UpdateAddressRequest(
    string Type,
    string? Label,
    string? Street,
    string? Number,
    string? District,
    string? Complement,
    string? Reference,
    string? ZipCode,
    string? PropertyName,
    string? Road,
    string? Kilometer,
    decimal Latitude,
    decimal Longitude,
    string? GooglePlaceId);

public record AddressResponse(
    Guid Id,
    Guid CityId,
    string Type,
    string? Label,
    string? Street,
    string? Number,
    string? District,
    string? Complement,
    string? Reference,
    string? ZipCode,
    string? PropertyName,
    string? Road,
    string? Kilometer,
    decimal Latitude,
    decimal Longitude,
    string? GooglePlaceId);
