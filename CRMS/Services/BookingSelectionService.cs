
using System;

namespace CRMS.Services
{
    // Du lieu xe duoc chon tu F05
    public sealed record BookingSelection(
        long CustomerId,
        long VehicleId,
        string RentalMode,
        DateTime StartAt,
        DateTime EndAt
    );

    // Luu tam thong tin de module Booking su dung
    public static class BookingSelectionService
    {
        public static BookingSelection? CurrentSelection
        {
            get;
            private set;
        }

        public static void SetSelection(
            BookingSelection selection)
        {
            CurrentSelection = selection;
        }

        public static void Clear()
        {
            CurrentSelection = null;
        }
    }
}
