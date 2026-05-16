namespace SultanaBBQ.Shared;

public sealed record DiningTableDto(
    Guid Id,
    string Name,
    int Capacity,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record DiningTableRequest(
    string Name,
    int Capacity,
    bool IsActive);

public sealed record TableStatusDto(
    Guid Id,
    string Name,
    int Capacity,
    bool IsActive,
    string Status,
    Guid? OpenOrderId,
    Guid? ReservationId,
    string? ReservationName,
    string? ReservationTime,
    decimal OpenOrderTotal);

public sealed record TableOrderDto(
    Guid Id,
    Guid TableId,
    string TableName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    decimal Total,
    IReadOnlyList<TableOrderItemDto> Items,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? OrderNote = null);

public sealed record TableOrderItemDto(
    Guid Id,
    Guid OrderId,
    string Category,
    string Name,
    decimal UnitPrice,
    int Quantity,
    string? Note,
    decimal LineTotal);

public sealed record AddOrderItemRequest(
    string Category,
    string Name,
    decimal UnitPrice,
    int Quantity,
    string? Note);

public sealed record UpdateOrderItemRequest(
    int Quantity,
    string? Note);

public sealed record ReceiptDto(
    Guid OrderId,
    string TableName,
    DateTimeOffset CreatedAt,
    DateTimeOffset CompletedAt,
    decimal Total,
    IReadOnlyList<TableOrderItemDto> Items);

public sealed record CustomerTableOrderRequest(
    string TableNumber,
    string? CustomerName,
    string? CustomerPhone,
    string? Note,
    IReadOnlyList<CustomerTableOrderItemRequest> Items);

public sealed record CustomerTableOrderItemRequest(
    string Category,
    string Name,
    decimal UnitPrice,
    int Quantity);

public sealed record StaffMessageResponse(string Message);
