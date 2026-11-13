using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Coordin.Api.Tests.Infrastructure;

/// <summary>Guarda as mensagens de log em memória, para os testes conferirem o que foi (ou não) escrito.</summary>
public sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => [.. _entries];

    /// <summary>Todas as mensagens (inclusive exceções) juntas, para procurar um texto que não pode aparecer.</summary>
    public string AllText => string.Join("\n", _entries.Select(entry => entry.Message));

    public ILogger CreateLogger(string categoryName) => new ListLogger(_entries);

    public void Dispose()
    {
    }

    public sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            entries.Enqueue(new LogEntry(logLevel, exception is null ? message : $"{message} {exception}"));
        }
    }
}
