// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NarrativeTrace.Core;
using NarrativeTrace.Logging;

using Xunit;

namespace NarrativeTrace.Logging.Tests;

/// <summary>
/// Wiring the event-stream bridge into a container — the step that decides
/// whether the bridge narrates in production or stays dead code.
/// </summary>
public class LoggingServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_the_listener_when_a_logger_factory_is_present()
    {
        var services = WithLogging();

        services.AddNarrativeLogging(null, Env());

        Assert.NotNull(
            services.BuildServiceProvider()
                .GetService<LoggingTraceEventListener>());
    }

    [Fact]
    public void Registers_nothing_when_no_logger_factory_is_registered()
    {
        var services = new ServiceCollection();

        services.AddNarrativeLogging(null, Env());

        Assert.Null(
            services.BuildServiceProvider()
                .GetService<LoggingTraceEventListener>());
    }

    [Theory]
    [InlineData("off")]
    [InlineData("OFF")]
    [InlineData("  Off  ")]
    public void Narration_off_vetoes_the_registration(string value)
    {
        var services = WithLogging();

        services.AddNarrativeLogging(null, Env(narration: value));

        Assert.Null(
            services.BuildServiceProvider()
                .GetService<LoggingTraceEventListener>());
    }

    [Theory]
    [InlineData("on")]
    [InlineData("true")]
    [InlineData("")]
    [InlineData("offf")]
    public void Only_the_word_off_vetoes(string value)
    {
        var services = WithLogging();

        services.AddNarrativeLogging(null, Env(narration: value));

        Assert.NotNull(
            services.BuildServiceProvider()
                .GetService<LoggingTraceEventListener>());
    }

    [Fact]
    public void The_listener_is_a_singleton()
    {
        var services = WithLogging();
        services.AddNarrativeLogging(null, Env());

        var provider = services.BuildServiceProvider();

        Assert.Same(
            provider.GetService<LoggingTraceEventListener>(),
            provider.GetService<LoggingTraceEventListener>());
    }

    [Fact]
    public void A_listener_the_host_registered_first_wins()
    {
        var mine = new LoggingTraceEventListener(NullLogger.Instance);
        var services = WithLogging();
        services.AddSingleton(mine);

        services.AddNarrativeLogging(null, Env());

        Assert.Same(
            mine,
            services.BuildServiceProvider()
                .GetService<LoggingTraceEventListener>());
    }

    [Fact]
    public void A_registered_event_stream_resolves_with_the_listener_attached()
    {
        var stream = new RecordingStream();
        var services = WithLogging();
        services.AddSingleton<IEventSubscribable>(stream);

        services.AddNarrativeLogging(null, Env());

        Assert.Empty(stream.Subscribers);
        services.BuildServiceProvider().GetRequiredService<IEventSubscribable>();
        Assert.Single(stream.Subscribers);
    }

    [Fact]
    public void The_attached_listener_logs_what_the_stream_publishes()
    {
        var stream = new RecordingStream();
        var logger = new RecordingLogger();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new SingleLoggerFactory(logger));
        services.AddSingleton<IEventSubscribable>(stream);
        services.AddNarrativeLogging(null, Env());

        services.BuildServiceProvider().GetRequiredService<IEventSubscribable>();
        stream.Publish(Enter());

        Assert.NotEmpty(logger.Entries);
    }

    [Fact]
    public void A_singleton_stream_is_only_subscribed_once()
    {
        var stream = new RecordingStream();
        var services = WithLogging();
        services.AddSingleton<IEventSubscribable>(stream);
        services.AddNarrativeLogging(null, Env());

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IEventSubscribable>();
        provider.GetRequiredService<IEventSubscribable>();

        Assert.Single(stream.Subscribers);
    }

    [Fact]
    public void A_factory_registered_stream_is_attached_too()
    {
        var stream = new RecordingStream();
        var services = WithLogging();
        services.AddSingleton<IEventSubscribable>(_ => stream);

        services.AddNarrativeLogging(null, Env());
        services.BuildServiceProvider().GetRequiredService<IEventSubscribable>();

        Assert.Single(stream.Subscribers);
    }

    [Fact]
    public void A_type_registered_stream_is_attached_too()
    {
        var services = WithLogging();
        services.AddSingleton<IEventSubscribable, RecordingStream>();

        services.AddNarrativeLogging(null, Env());
        var resolved = (RecordingStream)services.BuildServiceProvider()
            .GetRequiredService<IEventSubscribable>();

        Assert.Single(resolved.Subscribers);
    }

    [Fact]
    public void Vetoed_narration_leaves_the_stream_registration_untouched()
    {
        var stream = new RecordingStream();
        var services = WithLogging();
        services.AddSingleton<IEventSubscribable>(stream);

        services.AddNarrativeLogging(null, Env(narration: "off"));
        services.BuildServiceProvider().GetRequiredService<IEventSubscribable>();

        Assert.Empty(stream.Subscribers);
    }

    [Fact]
    public void No_event_stream_is_not_an_error_and_none_is_invented()
    {
        var services = WithLogging();

        services.AddNarrativeLogging(null, Env());

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<LoggingTraceEventListener>());
        Assert.Null(provider.GetService<IEventSubscribable>());
    }

    [Fact]
    public void Options_reach_the_listener()
    {
        var services = WithLogging();
        var logger = new RecordingLogger();
        services.AddSingleton<ILoggerFactory>(new SingleLoggerFactory(logger));

        services.AddNarrativeLogging(
            new TraceLoggingOptions { EnterLevel = LogLevel.Warning }, Env());
        services.BuildServiceProvider()
            .GetRequiredService<LoggingTraceEventListener>()
            .OnEvent(Enter());

        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => LoggingServiceCollectionExtensions.AddNarrativeLogging(
                null!, null, Env()));
        Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddNarrativeLogging(null, null!));
    }

    private static ServiceCollection WithLogging()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        return services;
    }

    private static Func<string, string?> Env(string? narration = null)
    {
        return key => key == ConfigResolver.NarrationKey ? narration : null;
    }

    private static EnterEvent Enter()
    {
        return new EnterEvent(
            new SpanContext(
                new TraceId("0af7651916cd43dd8448eb211c80319c"),
                new SpanId("b7ad6b7169203331"),
                null),
            1L,
            new MethodSignature("OrderService", "PlaceOrder", []));
    }

    /// <summary>An event stream that records who subscribed and can feed them.</summary>
    private sealed class RecordingStream : IEventSubscribable
    {
        internal List<Action<TraceEvent>> Subscribers { get; } = [];

        public void Subscribe(Action<TraceEvent> subscriber)
        {
            Subscribers.Add(subscriber);
        }

        internal void Publish(TraceEvent traceEvent)
        {
            foreach (var subscriber in Subscribers)
            {
                subscriber(traceEvent);
            }
        }
    }

    /// <summary>
    /// A logger that only records the level and message — the wiring is what is
    /// under test here, and the scope machinery already has its own tests.
    /// </summary>
    private sealed class RecordingLogger : ILogger
    {
        internal List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            internal static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class SingleLoggerFactory : ILoggerFactory
    {
        private readonly ILogger _logger;

        internal SingleLoggerFactory(ILogger logger)
        {
            _logger = logger;
        }

        public ILogger CreateLogger(string categoryName) => _logger;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }
}
