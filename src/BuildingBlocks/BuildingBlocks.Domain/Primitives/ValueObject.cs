namespace BuildingBlocks.Domain.Primitives;

public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(c => c.GetHashCode())
            .Aggregate(HashCode.Combine);
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left is not null && right is not null && left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !(left == right);
}
