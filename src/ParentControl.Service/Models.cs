namespace ParentControl.Service;

public sealed class ParentOptions
{
    public string ChildUser { get; set; } = "";
    public string[] AllowedNetworks { get; set; } = ["100.64.0.0/10", "127.0.0.0/8", "::1/128"];
    public int WarningSeconds { get; set; } = 30;
}

public sealed record ControlState(bool Locked, DateTimeOffset? LockedUntil, string Reason,
    DateTimeOffset UpdatedAt, DateTimeOffset? LastLockAt, long Revision);
public sealed record StatusDto(string ComputerName, bool Locked, DateTimeOffset? LockedUntil,
    string Reason, DateTimeOffset UpdatedAt, DateTimeOffset? LastLockAt, long Revision, long TodayActiveMinutes);
public sealed record LoginRequest(string UserName, string Password);
public sealed record LockRequest(int? Minutes, DateTimeOffset? Until, string? Reason);
public sealed record ScheduleRule(int DayOfWeek, string Start, string End, bool Locked, string Mode);
