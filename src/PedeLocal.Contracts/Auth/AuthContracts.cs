namespace PedeLocal.Contracts.Auth;

public record RegisterCustomerRequest(string Name, string Email, string Phone, string Password);

public record RegisterDriverRequest(
    string Name,
    string Cpf,
    string Phone,
    string Email,
    string Password,
    Guid CityId,
    string VehicleType,
    string PixKeyType,
    string PixKey);

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);

public record AuthTokensResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc);

public record RegisterCustomerResponse(Guid UserId, string Name, string Email);

public record RegisterDriverResponse(Guid UserId, Guid DriverId, string OnboardingStatus);
