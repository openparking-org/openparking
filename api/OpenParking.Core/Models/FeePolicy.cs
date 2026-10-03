namespace OpenParking.Core.Models;

/// <summary>
/// The pricing and enforcement policy a fee is computed under, resolved from
/// SystemSettings at the moment of calculation. Passing it in explicitly keeps
/// <see cref="FeeRules"/> free of I/O, so every rule can be unit tested and the
/// exact policy behind a figure can be logged alongside it.
/// </summary>
public sealed record FeePolicy
{
    /// <summary>Billing granularity (design.md §19.2). Fixed, not a setting.</summary>
    public const int BillingBlockMinutes = 15;

    public bool PricingEnabled { get; init; } = true;
    public decimal BaseHourlyRate { get; init; } = 5.00m;

    /// <summary>pricing.max_surge_multiplier — ceiling on any surge, including the AI agent's.</summary>
    public decimal MaxSurgeMultiplier { get; init; } = 2.50m;

    /// <summary>overstay.grace_period_mins — forgiven outright, not merely deferred.</summary>
    public int GracePeriodMinutes { get; init; } = 15;

    public decimal PenaltyPerHour { get; init; } = 25.00m;

    /// <summary>overstay.max_penalty_cap — the most a single overstay can cost.</summary>
    public decimal PenaltyCap { get; init; } = 150.00m;

    /// <summary>Fraction off parking (not penalties) for verified disability permit holders.</summary>
    public decimal DisabilityDiscount { get; init; } = 0.15m;
}

/// <summary>Price of a reservation window, computed before the driver commits.</summary>
public sealed record FeeQuote(
    int BillableBlocks,
    decimal HourlyRate,
    decimal RequestedSurge,
    decimal AppliedSurge,
    decimal DiscountApplied,
    decimal Amount)
{
    public bool SurgeWasCapped => AppliedSurge < RequestedSurge;
}

/// <summary>Final bill for a completed session, itemised so it can be explained.</summary>
public sealed record SessionCharge(
    int BillableBlocks,
    decimal ParkingFee,
    int OverstayMinutes,
    int ChargeableOverstayMinutes,
    int PenaltyHours,
    decimal UncappedPenalty,
    decimal PenaltyFee)
{
    public decimal TotalFee => ParkingFee + PenaltyFee;
    public bool PenaltyWasCapped => UncappedPenalty > PenaltyFee;
}
