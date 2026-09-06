// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Examples.Clarity;

/// <summary>
/// A realistic multi-service hotel-booking scenario that spans the full clarity
/// range — excellent reservation naming, an adequate booking manager, a
/// deliberately poor generic-verb <see cref="IDataProcessor"/>, and a
/// cohesion-violating repository — then scores each with
/// <see cref="ClarityAnalyzer"/> and renders a
/// <see cref="ClarityReportRenderer"/> suite report.
/// </summary>
public static class HotelClarityDemo
{
    /// <summary>Analyzes each scenario's trace into a scored clarity result.</summary>
    public static IReadOnlyList<ScenarioClarity> Analyze()
    {
        return
        [
            new ScenarioClarity(
                "Guest books a room", ClarityAnalyzer.Analyze(ReservationTrace(NewContext()))),
            new ScenarioClarity(
                "Booking via manager", ClarityAnalyzer.Analyze(BookingTrace(NewContext()))),
            new ScenarioClarity(
                "Legacy data processing", ClarityAnalyzer.Analyze(ProcessingTrace(NewContext()))),
            new ScenarioClarity(
                "Guest repository operations", ClarityAnalyzer.Analyze(RepositoryTrace(NewContext()))),
        ];
    }

    /// <summary>Renders the Markdown clarity suite report for all scenarios.</summary>
    public static string RenderReport()
    {
        return ClarityReportRenderer.Render(Analyze());
    }

    /// <summary>Excellent naming: a reservation confirmed across an availability checker and a payment gateway.</summary>
    public static TraceTree ReservationTrace(INarrativeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var availability = Trace<IAvailabilityChecker>(new DefaultAvailabilityChecker(), context);
        var payments = Trace<IPaymentGateway>(new DefaultPaymentGateway(), context);
        var reservations = Trace<IReservationService>(
            new DefaultReservationService(availability, payments), context);

        reservations.ConfirmReservation("G-1001", "deluxe", "2025-06-15", "2025-06-18");
        return context.CaptureTrace();
    }

    /// <summary>Adequate naming: a single, mildly generic booking manager.</summary>
    public static TraceTree BookingTrace(INarrativeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var booking = Trace<IBookingManager>(new DefaultBookingManager(), context);

        booking.HandleBooking("Jane Smith", "suite", "2025-07-01", "2025-07-05");
        return context.CaptureTrace();
    }

    /// <summary>Poor naming: a generic-verb data processor.</summary>
    public static TraceTree ProcessingTrace(INarrativeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var processor = Trace<IDataProcessor>(new DefaultDataProcessor(), context);

        processor.Execute("room-data", 42);
        return context.CaptureTrace();
    }

    /// <summary>Cohesion mismatch: one repository that finds, renders and emails.</summary>
    public static TraceTree RepositoryTrace(INarrativeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var repository = Trace<IGuestRepository>(new DefaultGuestRepository(), context);

        repository.FindGuestById("G-1001");
        repository.RenderReport();
        repository.DispatchEmail("G-1001", "Your reservation is confirmed");
        return context.CaptureTrace();
    }

    private static SyncNarrativeContext NewContext()
    {
        return new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
    }

    private static T Trace<T>(T target, INarrativeContext context)
        where T : class
    {
        return NarrativeTraceProxy.Create(target, context);
    }
}
