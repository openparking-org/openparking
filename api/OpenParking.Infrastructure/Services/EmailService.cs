using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenParking.Core.Entities;
using OpenParking.Core.Interfaces;

namespace OpenParking.Infrastructure.Services;

/// <summary>
/// Sends transactional emails via the Resend REST API (design.md §4.2).
/// Config keys: RESEND_API_KEY, RESEND_FROM_EMAIL, RESEND_FROM_NAME
/// Timeout: 5s. Retry: 2x. Dead-letter log on all retries exhausted.
/// </summary>
public class EmailService(
    HttpClient http,
    IConfiguration config,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly string _from =
        $"{config["RESEND_FROM_NAME"] ?? "OpenParking"} <{config["RESEND_FROM_EMAIL"] ?? "noreply@openparking.lk"}>";

    public async Task SendWelcomeAsync(string toEmail, string name)
    {
        await SendAsync(toEmail, "Welcome to OpenParking 🅿️", $"""
            <h2>Hi {name},</h2>
            <p>Welcome to OpenParking! Your account has been created successfully.</p>
            <p>You can now search for parking spots, make bookings, and manage your sessions.</p>
            <br><p>— The OpenParking Team</p>
            """);
    }

    public async Task SendBookingConfirmationAsync(string toEmail, string name, Booking booking)
    {
        await SendAsync(toEmail, "Booking Confirmed 🅿️", $"""
            <h2>Hi {name},</h2>
            <p>Your booking has been confirmed!</p>
            <ul>
              <li><strong>Booking ID:</strong> {booking.Id}</li>
              <li><strong>Start:</strong> {booking.StartTime:dd MMM yyyy HH:mm} UTC</li>
              <li><strong>End:</strong> {booking.EndTime:dd MMM yyyy HH:mm} UTC</li>
              <li><strong>Estimated Fee:</strong> LKR {booking.EstimatedFee:F2}</li>
            </ul>
            <p>Open the OpenParking app to view your QR code for entry.</p>
            """);
    }

    public async Task SendReceiptAsync(string toEmail, string name, ParkingSession session)
    {
        await SendAsync(toEmail, "Receipt Ready 🧾", $"""
            <h2>Hi {name},</h2>
            <p>Your parking session is complete. Here is your receipt:</p>
            <ul>
              <li><strong>Duration:</strong> {session.OverstayMinutes} min overstay</li>
              <li><strong>Total Fee:</strong> LKR {session.TotalFee:F2}</li>
              <li><strong>Penalty:</strong> LKR {session.PenaltyFee:F2}</li>
            </ul>
            {(session.ReceiptPdfUrl != null ? $"<p><a href='{session.ReceiptPdfUrl}'>Download PDF Receipt</a></p>" : "")}
            """);
    }

    public async Task SendPenaltyNoticeAsync(string toEmail, string name, Penalty penalty)
    {
        await SendAsync(toEmail, "Penalty Notice ⚠️", $"""
            <h2>Hi {name},</h2>
            <p>An overstay penalty has been issued for your parking session.</p>
            <ul>
              <li><strong>Amount:</strong> LKR {penalty.Amount:F2}</li>
              <li><strong>Reason:</strong> {penalty.Reason}</li>
              <li><strong>Issued:</strong> {penalty.IssuedAt:dd MMM yyyy HH:mm} UTC</li>
            </ul>
            <p>You may dispute this penalty within 7 days through the OpenParking app.</p>
            """);
    }

    public async Task SendPermitOutcomeAsync(string toEmail, string name, DisabilityPermit permit)
    {
        var approved = permit.Status == PermitStatus.Verified;
        var subject  = approved ? "Permit Verified ✅" : "Permit Update ❌";
        var body     = approved
            ? $"<h2>Hi {name},</h2><p>Your disability permit has been <strong>verified</strong>. You can now book disabled parking slots.</p>"
            : $"<h2>Hi {name},</h2><p>Your disability permit could not be verified. Reason: {permit.RejectionReason ?? "See app for details"}.</p><p>Please resubmit with a clearer image.</p>";

        await SendAsync(toEmail, subject, body);
    }

    public async Task SendOverstayWarningAsync(string toEmail, string name, ParkingSession session)
    {
        await SendAsync(toEmail, "Overstay Warning ⏰", $"""
            <h2>Hi {name},</h2>
            <p>Your booked parking time has expired. Please return to your vehicle immediately to avoid a penalty.</p>
            <ul>
              <li><strong>Session ID:</strong> {session.Id}</li>
              <li><strong>Overstay:</strong> {session.OverstayMinutes} minutes</li>
            </ul>
            """);
    }

    // ── Private send helper ────────────────────────────────────────────────

    private async Task SendAsync(string toEmail, string subject, string htmlBody, int attempt = 1)
    {
        const int MaxRetries = 2;
        try
        {
            var payload = new
            {
                from    = _from,
                to      = new[] { toEmail },
                subject,
                html    = htmlBody
            };

            var response = await http.PostAsJsonAsync("https://api.resend.com/emails", payload);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Resend API returned {response.StatusCode}: {error}");
            }
        }
        catch (Exception ex) when (attempt <= MaxRetries)
        {
            logger.LogWarning(ex, "Email send attempt {Attempt}/{Max} failed for {Email}. Retrying…",
                attempt, MaxRetries, toEmail);
            await Task.Delay(500 * attempt); // back off before retry
            await SendAsync(toEmail, subject, htmlBody, attempt + 1);
        }
        catch (Exception ex)
        {
            // All retries exhausted — log as dead letter (design.md §4.2)
            logger.LogError(ex,
                "DEAD_LETTER_EMAIL: Failed to send '{Subject}' to {Email} after {Max} attempts.",
                subject, toEmail, MaxRetries);
            // Do NOT rethrow — email failure must never crash a user-facing request
        }
    }
}
