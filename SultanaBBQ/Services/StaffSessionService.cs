namespace SultanaBBQ.Services;

public sealed class StaffSessionService
{
    public string AccessCode { get; set; } = string.Empty;

    public bool HasAccessCode => !string.IsNullOrWhiteSpace(AccessCode);
}
