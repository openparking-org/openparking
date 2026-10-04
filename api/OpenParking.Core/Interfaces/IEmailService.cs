using OpenParking.Core.Entities;

namespace OpenParking.Core.Interfaces;

/// <summary>
/// Transactional email service backed by Resend (design.md §4.2).
/// Never call this directly from controllers — only from service layer.
/// </summary>
public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string token);
    Task SendWelcomeAsync(string toEmail, string name);
    Task SendBookingConfirmationAsync(string toEmail, string name, Booking booking);
    Task SendReceiptAsync(string toEmail, string name, ParkingSession session);
    Task SendPenaltyNoticeAsync(string toEmail, string name, Penalty penalty);
    Task SendPermitOutcomeAsync(string toEmail, string name, DisabilityPermit permit);
    Task SendOverstayWarningAsync(string toEmail, string name, ParkingSession session);
}
