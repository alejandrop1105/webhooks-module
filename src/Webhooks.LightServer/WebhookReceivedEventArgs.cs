namespace Webhooks.LightServer;

/// <summary>
/// Event arguments for received webhooks.
/// </summary>
public class WebhookReceivedEventArgs : EventArgs
{
    /// <summary>
    /// The source identifier from the URL (e.g., 'woocommerce', 'stripe').
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// The raw JSON payload.
    /// </summary>
    public string Payload { get; }

    /// <summary>
    /// HTTP Headers key-value pairs.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; }

    /// <summary>
    /// Timestamp when the webhook was received (UTC).
    /// </summary>
    public DateTime ReceivedAt { get; }

    public WebhookReceivedEventArgs(string source, string payload, Dictionary<string, string> headers)
    {
        Source = source;
        Payload = payload;
        Headers = headers;
        ReceivedAt = DateTime.UtcNow;
    }
}
