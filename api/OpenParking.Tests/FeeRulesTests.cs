using OpenParking.Core.Models;
using Xunit;

namespace OpenParking.Tests;

/// <summary>
/// Booking &amp; Payment fee rules. Policy values mirror SystemSettingsSeeder:
/// base 5.00/h, surge ceiling 2.50x, 15 min grace, 25.00/h penalty, 150.00 cap,
/// 15% disability discount, 15-minute billing blocks.
/// </summary>
public class FeeRulesTests
{
    private static readonly FeePolicy Policy = new();
    private static readonly DateTime T0 = new(2026, 10, 4, 9, 0, 0, DateTimeKind.Utc);

    // ── Quoting ───────────────────────────────────────────────────────────

    [Fact]
    public void Quote_BillsStartedFifteenMinuteBlocks()
    {
        var quote = FeeRules.Quote(Policy, T0, T0.AddMinutes(61), zoneHourlyRate: 4.00m, requestedSurge: 1m, hasDisabilityPermit: false);

        Assert.Equal(5, quote.BillableBlocks);   // 61 min -> 5 blocks
        Assert.Equal(5.00m, quote.Amount);       // 1.25 h x 4.00
    }

    [Fact]
    public void Quote_HasAOneBlockMinimum()
    {
        var quote = FeeRules.Quote(Policy, T0, T0.AddMinutes(3), 4.00m, 1m, false);

        Assert.Equal(1, quote.BillableBlocks);
        Assert.Equal(1.00m, quote.Amount);
    }

    [Fact]
    public void Quote_IgnoresSubSecondClockJitter()
    {
        // Two clock reads a few microseconds apart must not add a ninth block.
        var quote = FeeRules.Quote(Policy, T0, T0.AddHours(2).AddTicks(37), 100m, 1m, false);

        Assert.Equal(8, quote.BillableBlocks);
        Assert.Equal(200m, quote.Amount);
    }

    [Fact]
    public void Quote_StillStartsANewBlockOneWholeSecondOver()
    {
        var quote = FeeRules.Quote(Policy, T0, T0.AddHours(2).AddSeconds(1), 100m, 1m, false);

        Assert.Equal(9, quote.BillableBlocks);
    }

    [Fact]
    public void Quote_FallsBackToThePolicyRateWhenTheZoneHasNone()
    {
        var quote = FeeRules.Quote(Policy, T0, T0.AddHours(1), zoneHourlyRate: null, 1m, false);

        Assert.Equal(5.00m, quote.Amount);
    }

    [Fact]
    public void Quote_ClampsAnAiSurgeAboveThePolicyCeiling()
    {
        // Previously the agent's multiplier was applied as returned.
        var quote = FeeRules.Quote(Policy, T0, T0.AddHours(1), 10m, requestedSurge: 4.0m, false);

        Assert.Equal(2.50m, quote.AppliedSurge);
        Assert.True(quote.SurgeWasCapped);
        Assert.Equal(25.00m, quote.Amount);
    }

    [Fact]
    public void Quote_NeverDiscountsBelowTheBaseRateThroughSurge()
    {
        var quote = FeeRules.Quote(Policy, T0, T0.AddHours(1), 10m, requestedSurge: 0.4m, false);

        Assert.Equal(1.0m, quote.AppliedSurge);
        Assert.Equal(10.00m, quote.Amount);
    }

    [Fact]
    public void Quote_AppliesTheDisabilityDiscount()
    {
        var quote = FeeRules.Quote(Policy, T0, T0.AddHours(2), 10m, 1m, hasDisabilityPermit: true);

        Assert.Equal(17.00m, quote.Amount); // 20.00 less 15%
    }

    [Fact]
    public void Quote_IsZeroWhenPricingIsDisabled()
    {
        var quote = FeeRules.Quote(Policy with { PricingEnabled = false }, T0, T0.AddHours(3), 10m, 2m, false);

        Assert.Equal(0m, quote.Amount);
    }

    [Fact]
    public void Quote_RejectsAWindowThatEndsBeforeItStarts()
    {
        Assert.Throws<ArgumentException>(() => FeeRules.Quote(Policy, T0, T0.AddMinutes(-5), 10m, 1m, false));
    }

    // ── Settling ──────────────────────────────────────────────────────────

    [Fact]
    public void Settle_LeavingOnTimeCostsWhatWasQuoted()
    {
        // Previously the quote used fractional hours and check-out used blocks,
        // so the same 70 minutes was quoted 11.67 and billed 12.50.
        var end = T0.AddMinutes(70);
        var quote = FeeRules.Quote(Policy, T0, end, 10m, 1m, false);
        var bill = FeeRules.Settle(Policy, checkIn: T0, checkOut: end, bookedEnd: end, 10m, false);

        Assert.Equal(quote.Amount, bill.TotalFee);
        Assert.Equal(12.50m, bill.TotalFee);
    }

    [Fact]
    public void Settle_AppliesTheDisabilityDiscountAtCheckOutToo()
    {
        // Previously only the estimate was discounted; the real charge was not.
        var end = T0.AddHours(2);
        var bill = FeeRules.Settle(Policy, T0, end, end, 10m, hasDisabilityPermit: true);

        Assert.Equal(17.00m, bill.ParkingFee);
    }

    [Fact]
    public void Settle_ForgivesOverstayInsideTheGracePeriod()
    {
        // Previously one minute late cost a full penalty hour.
        var end = T0.AddHours(1);
        var bill = FeeRules.Settle(Policy, T0, checkOut: end.AddMinutes(10), bookedEnd: end, 10m, false);

        Assert.Equal(10, bill.OverstayMinutes);
        Assert.Equal(0, bill.ChargeableOverstayMinutes);
        Assert.Equal(0m, bill.PenaltyFee);
    }

    [Fact]
    public void Settle_ChargesStartedPenaltyHoursAfterGrace()
    {
        var end = T0.AddHours(1);
        var bill = FeeRules.Settle(Policy, T0, checkOut: end.AddMinutes(80), bookedEnd: end, 10m, false);

        Assert.Equal(80, bill.OverstayMinutes);
        Assert.Equal(65, bill.ChargeableOverstayMinutes);
        Assert.Equal(2, bill.PenaltyHours);
        Assert.Equal(50.00m, bill.PenaltyFee);
    }

    [Fact]
    public void Settle_CapsTheOverstayPenalty()
    {
        // Previously uncapped: ten hours late billed 250.00 against a 150.00 cap.
        var end = T0.AddHours(1);
        var bill = FeeRules.Settle(Policy, T0, checkOut: end.AddHours(10), bookedEnd: end, 10m, false);

        Assert.Equal(250.00m, bill.UncappedPenalty);
        Assert.Equal(150.00m, bill.PenaltyFee);
        Assert.True(bill.PenaltyWasCapped);
    }

    [Fact]
    public void Settle_DoesNotDiscountThePenalty()
    {
        var end = T0.AddHours(1);
        var bill = FeeRules.Settle(Policy, T0, end.AddHours(2), end, 10m, hasDisabilityPermit: true);

        Assert.Equal(50.00m, bill.PenaltyFee); // 105 chargeable min -> 2 h x 25.00, undiscounted
    }

    [Fact]
    public void Settle_WithoutABookingHasNoOverstay()
    {
        // Walk-up / ANPR sessions have no booked end time.
        var bill = FeeRules.Settle(Policy, T0, T0.AddHours(5), bookedEnd: null, 10m, false);

        Assert.Equal(0, bill.OverstayMinutes);
        Assert.Equal(0m, bill.PenaltyFee);
        Assert.Equal(50.00m, bill.ParkingFee);
    }

    [Fact]
    public void Settle_PolicyChangesMoveTheOutcome()
    {
        var strict = Policy with { GracePeriodMinutes = 0, PenaltyPerHour = 40m, PenaltyCap = 60m };
        var end = T0.AddHours(1);

        var bill = FeeRules.Settle(strict, T0, end.AddMinutes(90), end, 10m, false);

        Assert.Equal(60.00m, bill.PenaltyFee); // 2 h x 40.00 = 80.00, capped at 60.00
    }
}
