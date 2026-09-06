// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.Clarity;

/// <summary>A confirmed hotel reservation.</summary>
public sealed record Reservation(string GuestId, string RoomType, decimal Total);

/// <summary>A hotel guest.</summary>
public sealed record Guest(string Id, string FullName);

/// <summary>Finds rooms free for a stay — excellent, domain-specific naming.</summary>
public interface IAvailabilityChecker
{
    int FindAvailableRooms(string roomType, string checkIn, string checkOut);
}

/// <summary>Authorizes a guest's payment — excellent naming.</summary>
public interface IPaymentGateway
{
    string AuthorizePayment(string guestId, decimal amount);
}

/// <summary>Confirms a reservation across the collaborators — excellent naming.</summary>
public interface IReservationService
{
    Reservation ConfirmReservation(
        string guestId, string roomType, string checkIn, string checkOut);
}

/// <summary>Books a stay — adequate, mildly generic naming.</summary>
public interface IBookingManager
{
    string HandleBooking(string guest, string roomType, string checkIn, string checkOut);
}

/// <summary>Processes room data — deliberately poor, generic-verb naming.</summary>
public interface IDataProcessor
{
    string Execute(string data, int count);
}

/// <summary>Mixes lookup, rendering, and email — a cohesion violation.</summary>
public interface IGuestRepository
{
    Guest FindGuestById(string guestId);

    string RenderReport();

    void DispatchEmail(string guestId, string message);
}
