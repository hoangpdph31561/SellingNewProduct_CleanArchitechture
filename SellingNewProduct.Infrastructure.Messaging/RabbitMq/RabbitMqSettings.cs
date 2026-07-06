namespace SellingNewProduct.Infrastructure.Messaging.RabbitMq;

/// <summary>Connection settings for the RabbitMQ command/work broker.</summary>
public sealed class RabbitMqSettings
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    /// <summary>How many times a worker retries a command before it is dead-lettered.</summary>
    public int MaxRetries { get; set; } = 3;
}
