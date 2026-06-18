namespace Academic.Domain.Students;

/// <summary>
/// Value object for student guardian contact information.
/// </summary>
public sealed class GuardianContact : IEquatable<GuardianContact>
{
    public string Name  { get; }   // required, ≤ 120 chars
    public string Email { get; }   // valid email format

    private GuardianContact(string name, string email)
    {
        Name  = name;
        Email = email;
    }

    /// <summary>
    /// Creates a GuardianContact from the given name and email.
    /// Returns (null, errorMessage) on validation failure.
    /// </summary>
    public static (GuardianContact? Result, string? Error) TryCreate(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name))
            return (null, "Guardian name is required.");

        if (name.Length > 120)
            return (null, "Guardian name must not exceed 120 characters.");

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
            return (null, "Guardian email must be a valid email address.");

        return (new GuardianContact(name.Trim(), email.Trim()), null);
    }

    public bool Equals(GuardianContact? other)
        => other is not null && Name == other.Name && Email == other.Email;

    public override bool Equals(object? obj)
        => obj is GuardianContact other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Name, Email);

    public static bool operator ==(GuardianContact? left, GuardianContact? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(GuardianContact? left, GuardianContact? right)
        => !(left == right);
}
