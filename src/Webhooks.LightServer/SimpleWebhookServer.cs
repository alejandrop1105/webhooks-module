using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Webhooks.LightServer;

/// <summary>
/// A lightweight, standalone HTTP server for receiving webhooks.
/// Supports HMAC-SHA256 signature validation internally.
/// </summary>
public class SimpleWebhookServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _secretKey;
    private bool _isRunning;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Event triggered when a valid webhook is received.
    /// </summary>
    public event EventHandler<WebhookReceivedEventArgs>? OnWebhookReceived;

    /// <summary>
    /// Event for internal logs (info, error).
    /// </summary>
    public event EventHandler<string>? OnLog;

    /// <summary>
    /// Initializes a new instance of the webhook server.
    /// </summary>
    /// <param name="port">The port to listen on (e.g., 8080).</param>
    /// <param name="secretKey">The secret key required for HMAC validation.</param>
    /// <param name="pathPrefix">Optional path prefix (default: "webhook/").</param>
    public SimpleWebhookServer(int port, string secretKey, string pathPrefix = "webhook/")
    {
        if (!HttpListener.IsSupported)
            throw new PlatformNotSupportedException("HttpListener is not supported on this OS.");

        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        _listener = new HttpListener();

        // Ensure the prefix ends with '/'
        if (!pathPrefix.EndsWith("/")) pathPrefix += "/";

        // Using localhost for safety locally. 
        // Note: For external access via Cloudflare Tunnel, ensure the tunnel points to localhost:port
        string prefix = $"http://localhost:{port}/{pathPrefix}";
        _listener.Prefixes.Add(prefix);
    }

    /// <summary>
    /// Starts the server loop in a background task.
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        try
        {
            _listener.Start();
            _isRunning = true;
            _cts = new CancellationTokenSource();

            Log($"Server started. Listening on {_listener.Prefixes.First()}");
            Log($"Endpoint pattern: {_listener.Prefixes.First()}{{source}}");

            Task.Run(() => ListenLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            Log($"Failed to start server: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Stops the server.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cts?.Cancel();
        _listener.Stop();
        Log("Server stopped.");
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (_isRunning && !token.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = ProcessRequestAsync(context);
            }
            catch (HttpListenerException) when (!_isRunning) { } // Shutting down
            catch (Exception ex)
            {
                Log($"Listener error: {ex.Message}");
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            using var response = context.Response;

            // 1. Method Validation
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            // 2. Source Extraction
            // URL structure: /webhook/{source}
            var segments = request.Url?.Segments;
            string source = "unknown";
            if (segments != null && segments.Length > 0)
            {
                // Last segment might be "woocommerce" or "woocommerce/"
                source = segments.Last().Trim('/');
            }

            // 3. Read Payload
            string payload;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                payload = await reader.ReadToEndAsync();
            }

            // 4. Extract Headers
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (request.Headers.AllKeys.Length > 0)
            {
                foreach (string key in request.Headers.AllKeys)
                {
                    if (key != null)
                        headers[key] = request.Headers[key] ?? "";
                }
            }

            // 5. Signature Validation
            if (!ValidateSignature(payload, headers, source))
            {
                Log($"[Warning] Invalid signature from {request.RemoteEndPoint} for source '{source}'");
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            // 6. Success -> Raise Event
            OnWebhookReceived?.Invoke(this, new WebhookReceivedEventArgs(source, payload, headers));

            response.StatusCode = (int)HttpStatusCode.OK;
            byte[] buffer = Encoding.UTF8.GetBytes("Received");
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Log($"Error processing request: {ex.Message}");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        finally
        {
            context.Response.Close();
        }
    }

    private bool ValidateSignature(string payload, Dictionary<string, string> headers, string source)
    {
        // Try to find the signature in standard headers
        string? signatureHeader = GetSignature(headers);
        if (string.IsNullOrEmpty(signatureHeader))
        {
            // If no signature header is found, we can't validate.
            // Depending on strictness, we might fallback to false or true (dev mode).
            // For security, default to false.
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(_secretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        // 1. Check as Base64 (WooCommerce, etc.)
        string computedBase64 = Convert.ToBase64String(hashBytes);
        if (IsSignatureValid(computedBase64, signatureHeader)) return true;

        // 2. Check as Hex (GitHub, etc.)
        string computedHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        // Remove "sha256=" prefix if present (common in GitHub)
        string cleanSignature = signatureHeader;
        if (cleanSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            cleanSignature = cleanSignature.Substring(7);

        if (IsSignatureValid(computedHex, cleanSignature)) return true;

        return false;
    }

    private bool IsSignatureValid(string computed, string received)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(received));
    }

    private string? GetSignature(Dictionary<string, string> headers)
    {
        // WooCommerce
        if (headers.TryGetValue("X-WC-Webhook-Signature", out var wc)) return wc;
        // GitHub
        if (headers.TryGetValue("X-Hub-Signature-256", out var gh)) return gh;
        // Stripe (simplified)
        if (headers.TryGetValue("Stripe-Signature", out var stripe))
        {
            // Stripe signature is complex (t=...,v1=...). This is a simplified check.
            // Real implementation would parse 'v1=' part.
            // For now, return as is. Validation might fail if not fully implemented.
            return stripe;
        }
        // Generic
        if (headers.TryGetValue("X-Webhook-Signature", out var gen)) return gen;

        return null;
    }

    private void Log(string message)
    {
        OnLog?.Invoke(this, $"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public void Dispose()
    {
        Stop();
        ((IDisposable)_listener).Dispose();
        _cts?.Dispose();
    }
}
