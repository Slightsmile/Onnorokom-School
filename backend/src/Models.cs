namespace Backend;

public record ClassDto(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);

public record CreateClassRequest(string Name, int TotalSeats);

public record CreateClassResponse(string Id, string Name, int TotalSeats);

public record HoldDto(string HoldId, string ClassId, string? ClassName, string StudentId, string Status, DateTime ExpiresAt);

public record ApiError(string Error, string Message);
