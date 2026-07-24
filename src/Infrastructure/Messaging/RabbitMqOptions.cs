namespace KartCategoryService.Infrastructure.Messaging;

/// <summary>Binds the "RabbitMq" configuration section - message-bus-manifest.json's exchange shape.</summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    /// <summary>message-bus-manifest.json: `category.exchange`, topic, durable.</summary>
    public string Exchange { get; set; } = "category.exchange";
}
