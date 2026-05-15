namespace SultanaBBQ.Shared;

public sealed record ReservationRequest(
    string Name,
    string Email,
    string Phone,
    int Guests,
    DateOnly Date,
    string Time,
    string? Notes);

public sealed record ReservationResponse(
    Guid Id,
    string Message);

public sealed record OwnerReservation(
    Guid Id,
    string Name,
    string Email,
    string Phone,
    int Guests,
    DateOnly Date,
    string Time,
    string? Notes,
    string Status,
    DateTimeOffset CreatedAt);
