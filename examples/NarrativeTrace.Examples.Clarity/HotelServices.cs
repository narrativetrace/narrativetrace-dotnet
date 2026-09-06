// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Examples.Clarity;

/// <inheritdoc cref="IAvailabilityChecker" />
public sealed class DefaultAvailabilityChecker : IAvailabilityChecker
{
    public int FindAvailableRooms(string roomType, string checkIn, string checkOut)
    {
        return roomType.Length;
    }
}

/// <inheritdoc cref="IPaymentGateway" />
public sealed class DefaultPaymentGateway : IPaymentGateway
{
    public string AuthorizePayment(string guestId, decimal amount)
    {
        return string.Create(CultureInfo.InvariantCulture, $"auth-{guestId}-{amount}");
    }
}

/// <inheritdoc cref="IReservationService" />
public sealed class DefaultReservationService(
    IAvailabilityChecker availabilityChecker,
    IPaymentGateway paymentGateway) : IReservationService
{
    public Reservation ConfirmReservation(
        string guestId, string roomType, string checkIn, string checkOut)
    {
        availabilityChecker.FindAvailableRooms(roomType, checkIn, checkOut);
        paymentGateway.AuthorizePayment(guestId, 450m);
        return new Reservation(guestId, roomType, 450m);
    }
}

/// <inheritdoc cref="IBookingManager" />
public sealed class DefaultBookingManager : IBookingManager
{
    public string HandleBooking(string guest, string roomType, string checkIn, string checkOut)
    {
        return $"{guest} booked a {roomType}";
    }
}

/// <inheritdoc cref="IDataProcessor" />
public sealed class DefaultDataProcessor : IDataProcessor
{
    public string Execute(string data, int count)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{data}:{count}");
    }
}

/// <inheritdoc cref="IGuestRepository" />
public sealed class DefaultGuestRepository : IGuestRepository
{
    public Guest FindGuestById(string guestId)
    {
        return new Guest(guestId, "Jane Smith");
    }

    public string RenderReport()
    {
        return "report";
    }

    public void DispatchEmail(string guestId, string message)
    {
        // no-op notification sink for the demo
    }
}
