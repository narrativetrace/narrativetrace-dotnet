// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;

namespace Checkout.Tests;

/// <summary>
/// The payment service, as a second traced service: a loopback Kestrel host with the NarrativeTrace
/// middleware, its own request-scoped narrative context, narrating under its own logger category.
/// Two services in one process, not a mock — the request really crosses a socket and the header
/// really arrives as a header. The middleware adopts the inbound <c>traceparent</c> per request; the
/// exporter is the seam that hands this test the callee's own trace afterwards.
/// </summary>
internal sealed class PaymentServiceHost : ITraceExporter, IAsyncDisposable
{
    private const string LoggerCategory = "NarrativeTrace.Payments";
    private const int PaymentRequired = 402;

    private WebApplication _app = null!;

    public Uri Endpoint => new(_app.Urls.First() + "/charge");

    public string? InboundTraceparent { get; private set; }

    public TraceTree LastTrace { get; private set; } = new([]);

    public static async Task<PaymentServiceHost> StartAsync(TextWriter log)
    {
        var host = new PaymentServiceHost();
        // snippet:begin own-context
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Trace).AddProvider(new CorrelationLoggerProvider(log));
        builder.Services.AddSingleton<ITraceExporter>(host); // the seam that hands over this service's own trace
        builder.Services.AddNarrativeTrace(); // a request-scoped context, the middleware, the exporter
        // snippet:end own-context
        host._app = builder.Build();
        // snippet:begin inbound
        host._app.UseNarrativeTrace(); // adopts the inbound traceparent and exports the request's trace
        host._app.MapPost("/charge", (Delegate)host.Charge);
        // snippet:end inbound
        await host._app.StartAsync();
        return host;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    public void Export(TraceTree tree, RequestContext requestContext) => LastTrace = tree;

    private async Task<IResult> Charge(HttpContext http)
    {
        InboundTraceparent = http.Request.Headers[Traceparent.HeaderName];
        var context = new LoggingNarrativeContext(
            http.RequestServices.GetRequiredService<INarrativeContext>(),
            http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory));
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var fields = (await new StreamReader(http.Request.Body).ReadToEndAsync()).Split(',', 3);
        try
        {
            var amountCents = int.Parse(fields[1], CultureInfo.InvariantCulture);
            return Results.Text(payments.Charge(fields[0], amountCents, fields[2]));
        }
        catch (PaymentDeclinedException declined)
        {
            return Results.Text(declined.Message, statusCode: PaymentRequired);
        }
    }
}
