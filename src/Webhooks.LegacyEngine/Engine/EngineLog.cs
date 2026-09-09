using System;
using Serilog;
using Serilog.Core;

namespace Webhooks.LegacyEngine.Engine;

/// <summary>
/// The engine's own logger, deliberately kept OUT of Serilog's global
/// <see cref="Serilog.Log"/>.
///
/// This library used to publish its logger through <c>Log.Logger = ...</c> and tear it
/// down through <c>Log.CloseAndFlush()</c>. Both are process-wide operations, and the
/// host application (DEAM) also registers its own logger there — the one backing its
/// SQLite sink, its rolling file and its in-memory UI broadcast. The result was two
/// separate defects:
///
///   * the assignment hijacked the global logger, so the HOST's log events (MercadoPago,
///     OAuth, everything) were rerouted into <c>webhook-engine-*.txt</c> instead of the
///     host's own sinks;
///   * <c>CloseAndFlush()</c> disposed whatever logger happened to be registered — the
///     host's — and since the host holds that same instance, its logging went silently
///     dead for the remainder of the process. Symptom: the log just stops, with
///     "API HTTP Apagado" as its last line.
///
/// Owning a private instance keeps both applications' logging independent. Consumers
/// still reach the engine's log through <c>OnSystemLog</c> (fed by
/// <see cref="UiEventLogSink"/>), which is unchanged.
/// </summary>
internal static class EngineLog
{
    private static ILogger _current = Logger.None;

    /// <summary>
    /// The engine's logger. Never null: before <see cref="Configure"/> runs — and after
    /// <see cref="Shutdown"/> — it is a silent logger, so logging from a torn-down engine
    /// is a no-op rather than a crash.
    /// </summary>
    internal static ILogger Current => _current;

    /// <summary>
    /// Installs the engine's logger, disposing the previously installed one.
    /// </summary>
    internal static void Configure(ILogger logger)
    {
        ILogger anterior = _current;
        _current = logger ?? Logger.None;
        Dispose(anterior);
    }

    /// <summary>
    /// Disposes the engine's logger and falls back to silent. Only ever touches loggers
    /// this class created — never Serilog's global one.
    /// </summary>
    internal static void Shutdown()
    {
        ILogger anterior = _current;
        _current = Logger.None;
        Dispose(anterior);
    }

    private static void Dispose(ILogger logger)
    {
        if (ReferenceEquals(logger, Logger.None)) return;

        // Disposing flushes the file sink. A failure here must not propagate: Shutdown
        // runs from Dispose paths (session change, app close) where throwing would mask
        // the real teardown.
        try { (logger as IDisposable)?.Dispose(); }
        catch { }
    }
}
