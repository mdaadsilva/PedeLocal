namespace PedeLocal.Contracts.Cities;

public record CityResponse(Guid Id, string Name, string State, bool IsActive);

public record CreateCityRequest(string Name, string State);

public record UpdateCityRequest(string Name, string State);

public record UpdateCityStatusRequest(bool IsActive);
