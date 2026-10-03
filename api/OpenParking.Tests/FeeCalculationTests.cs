using OpenParking.Core.Interfaces;
using OpenParking.Core.Models;
using OpenParking.Infrastructure.Services;
using Xunit;

namespace OpenParking.Tests;

/// <summary>
/// Stands in for the database-backed settings store so the pricing rules can be
/// tested against known policy values. Mirrors the defaults seeded by
/// SystemSettingsSeeder unless a test overrides one.
/// </summary>
internal sealed class FakeSettingsService : ISettingsService
{
    private readonly Dictionary<string, string> _values = new()
    {
        [SettingsKeys.BaseHourlyRate] = "5.00",
        [SettingsKeys.PeakMultiplier] = "1.50",
        [SettingsKeys.MaxSurgeMultiplier] = "2.50",
        [SettingsKeys.OverstayGracePeriodMins] = "15",
        [SettingsKeys.OverstayPenaltyPerHour] = "25.00",
        [SettingsKeys.OverstayMaxPenaltyCap] = "150.00"
    };

    public FakeSettingsService With(string key, string value)
    {
        _values[key] = value;
        return this;
    }

    public Task<string> GetStringAsync(string key, string defaultValue = "") =>
        Task.FromResult(_values.TryGetValue(key, out var v) ? v : defaultValue);

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m) =>
        decimal.TryParse(await GetStringAsync(key), out var v) ? v : defaultValue;

    public async Task<int> GetIntAsync(string key, int defaultValue = 0) =>
        int.TryParse(await GetStringAsync(key), out var v) ? v : defaultValue;

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false) =>
        bool.TryParse(await GetStringAsync(key), out var v) ? v : defaultValue;

    public Task SetAsync(string key, string value, string updatedBy = "System")
    {
        _values[key] = value;
        return Task.CompletedTask;
    }

    public Task InvalidateCacheAsync(string key) => Task.CompletedTask;
}

public class FeeCalculationTests
{
    private static readonly DateTime Start = new(2026, 10, 4, 10, 0, 0, DateTimeKind.Utc);

    private static FeeCalculationService Service(FakeSettingsService? settings = null) =>
        new(settings ?? new FakeSettingsService());

    // ---------------------------------------------------------------
    // Reservation pricing
    // ---------------------------------------------------------------

    [Fact]
    public async Task Estimate_Bills_An_Exact_Two_Hour_Window_As_Two_Hours()
    {
        var quote = await Service().EstimateAsync(Start, Start.AddHours(2));

        Assert.Equal(2, quote.BillableHours);
        Assert.Equal(10.00m, quote.Amount);
    }

    [Fact]
    public async Task Estimate_Bills_Per_Started_Hour()
    {
        // One minute into the third hour is a third billable hour.
        var quote = await Service().EstimateAsync(Start, Start.AddHours(2).AddMinutes(1));

        Assert.Equal(3, quote.BillableHours);
        Assert.Equal(15.00m, quote.Amount);
    }

    [Fact]
    public async Task Estimate_Applies_A_One_Hour_Minimum()
    {
        var quote = await Service().EstimateAsync(Start, Start.AddMinutes(10));

        Assert.Equal(1, quote.BillableHours);
        Assert.Equal(5.00m, quote.Amount);
    }

    [Fact]
    public async Task Estimate_Reads_The_Rate_From_Settings_Rather_Than_Hardcoding_It()
    {
        var settings = new FakeSettingsService().With(SettingsKeys.BaseHourlyRate, "8.50");

        var quote = await Service(settings).EstimateAsync(Start, Start.AddHours(2));

        Assert.Equal(8.50m, quote.BaseHourlyRate);
        Assert.Equal(17.00m, quote.Amount);
    }

    [Fact]
    public async Task Estimate_Prefers_An_Explicit_Zone_Rate_Over_The_Global_Default()
    {
        var quote = await Service().EstimateAsync(Start, Start.AddHours(1), baseHourlyRate: 12.00m);

        Assert.Equal(12.00m, quote.BaseHourlyRate);
        Assert.Equal(12.00m, quote.Amount);
    }

    [Fact]
    public async Task Estimate_Rejects_A_Window_That_Ends_Before_It_Starts()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Service().EstimateAsync(Start, Start.AddHours(-1)));
    }

    // ---------------------------------------------------------------
    // Surge guard rails on the AI Action Agent's proposals
    // ---------------------------------------------------------------

    [Fact]
    public async Task Surge_Raises_The_Effective_Rate()
    {
        var quote = await Service().EstimateAsync(Start, Start.AddHours(2), surgeMultiplier: 1.5m);

        Assert.Equal(1.5m, quote.AppliedSurgeMultiplier);
        Assert.Equal(7.50m, quote.EffectiveHourlyRate);
        Assert.Equal(15.00m, quote.Amount);
        Assert.False(quote.SurgeWasCapped);
    }

    [Fact]
    public async Task Surge_Is_Capped_At_The_Administrator_Ceiling()
    {
        // An agent proposing 4.0x is held to the configured 2.5x maximum.
        var quote = await Service().EstimateAsync(Start, Start.AddHours(1), surgeMultiplier: 4.0m);

        Assert.Equal(2.5m, quote.AppliedSurgeMultiplier);
        Assert.Equal(4.0m, quote.RequestedSurgeMultiplier);
        Assert.True(quote.SurgeWasCapped);
        Assert.Equal(12.50m, quote.Amount);
    }

    [Fact]
    public async Task Surge_Never_Discounts_Below_The_Base_Rate()
    {
        var quote = await Service().EstimateAsync(Start, Start.AddHours(1), surgeMultiplier: 0.5m);

        Assert.Equal(1.0m, quote.AppliedSurgeMultiplier);
        Assert.Equal(5.00m, quote.Amount);
    }

    // ---------------------------------------------------------------
    // Session settlement and overstay enforcement
    // ---------------------------------------------------------------

    [Fact]
    public async Task Session_Leaving_On_Time_Owes_Only_The_Parking_Fee()
    {
        var end = Start.AddHours(2);

        var charge = await Service().CalculateSessionChargeAsync(Start, end, checkOutTime: end);

        Assert.False(charge.IsOverstay);
        Assert.Equal(0m, charge.PenaltyFee);
        Assert.Equal(10.00m, charge.TotalFee);
    }

    [Fact]
    public async Task Session_Leaving_Early_Still_Pays_For_The_Reserved_Window()
    {
        var end = Start.AddHours(3);

        // The slot was held for three hours whether or not the driver used it.
        var charge = await Service().CalculateSessionChargeAsync(Start, end, checkOutTime: Start.AddHours(1));

        Assert.Equal(0, charge.OverstayMinutes);
        Assert.Equal(15.00m, charge.ParkingFee);
        Assert.Equal(15.00m, charge.TotalFee);
    }

    [Fact]
    public async Task Overstay_Inside_The_Grace_Period_Is_Not_Penalised()
    {
        var end = Start.AddHours(2);

        // 10 minutes late against a 15 minute grace period.
        var charge = await Service().CalculateSessionChargeAsync(Start, end, checkOutTime: end.AddMinutes(10));

        Assert.Equal(10, charge.OverstayMinutes);
        Assert.Equal(0, charge.ChargeableOverstayMinutes);
        Assert.False(charge.IsOverstay);
        Assert.Equal(0m, charge.PenaltyFee);
        Assert.Equal(10.00m, charge.TotalFee);
    }

    [Fact]
    public async Task Overstay_Beyond_Grace_Is_Charged_Per_Started_Hour_After_Grace()
    {
        var end = Start.AddHours(2);

        // 20 minutes late: grace forgives 15, leaving 5 chargeable minutes,
        // which bill as one started penalty hour.
        var charge = await Service().CalculateSessionChargeAsync(Start, end, checkOutTime: end.AddMinutes(20));

        Assert.Equal(20, charge.OverstayMinutes);
        Assert.Equal(5, charge.ChargeableOverstayMinutes);
        Assert.Equal(1, charge.PenaltyHours);
        Assert.Equal(25.00m, charge.PenaltyFee);
        Assert.Equal(35.00m, charge.TotalFee);
    }

    [Fact]
    public async Task Overstay_Penalty_Is_Capped()
    {
        var end = Start.AddHours(1);

        // 10 hours late would be 10 x 25.00 = 250.00, above the 150.00 cap.
        var charge = await Service().CalculateSessionChargeAsync(Start, end, checkOutTime: end.AddHours(10));

        Assert.Equal(250.00m, charge.UncappedPenaltyFee);
        Assert.Equal(150.00m, charge.PenaltyFee);
        Assert.True(charge.PenaltyWasCapped);
        Assert.Equal(155.00m, charge.TotalFee);
    }

    [Fact]
    public async Task Overstay_Policy_Changes_Take_Effect_Without_A_Redeploy()
    {
        var settings = new FakeSettingsService()
            .With(SettingsKeys.OverstayGracePeriodMins, "0")
            .With(SettingsKeys.OverstayPenaltyPerHour, "10.00");
        var end = Start.AddHours(1);

        var charge = await Service(settings)
            .CalculateSessionChargeAsync(Start, end, checkOutTime: end.AddMinutes(1));

        Assert.Equal(1, charge.ChargeableOverstayMinutes);
        Assert.Equal(10.00m, charge.PenaltyFee);
    }
}
