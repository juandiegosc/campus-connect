namespace BuildingBlocks.Application.Correlation;

public interface ICorrelationContext
{
    string CorrelationId { get; }
}
