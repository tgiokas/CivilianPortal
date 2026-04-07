namespace CitizenPortal.Application.Interfaces;

public interface IMessagePublisher
{
    Task PublishJsonAsync<T>(
        string route,                // e.g. topic, queue, etc
        string key,                  // partition key or correlation key
        T payload,
        IEnumerable<KeyValuePair<string, string>>? headers = null,
        CancellationToken cancellationToken = default);
}
