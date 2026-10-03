namespace OpenParking.Core.Models;

/// <summary>
/// The priced result of a reservation window, returned before any money is taken.
/// Every input that moved the number is kept so the figure can be explained back
/// to the driver and audited later.
/// </summary>
public class FeeQuote
{
    public int BillableHours { get; init; }
    public decimal BaseHourlyRate { get; init; }

    /// <summary>Surge actually applied, after clamping to pricing.max_surge_multiplier.</summary>
    public decimal AppliedSurgeMultiplier { get; init; }

    /// <summary>Surge the caller asked for, retained when it exceeded the cap.</summary>
    public decimal RequestedSurgeMultiplier { get; init; }

    public bool SurgeWasCapped => RequestedSurgeMultiplier > AppliedSurgeMultiplier;
    public decimal EffectiveHourlyRate { get; init; }
    public decimal Amount { get; init; }
}

/// <summary>
/// Final charge for a completed parking session: the time actually used, plus any
/// overstay penalty left after the grace period and the penalty cap are applied.
/// </summary>
public class SessionCharge
{
    public int BillableHours { get; init; }
    public decimal ParkingFee { get; init; }

    /// <summary>Whole minutes past the booked end time, before grace is deducted.</summary>
    public int OverstayMinutes { get; init; }

    /// <summary>Overstay still chargeable once overstay.grace_period_mins is removed.</summary>
    public int ChargeableOverstayMinutes { get; init; }

    public int PenaltyHours { get; init; }

    /// <summary>Penalty before overstay.max_penalty_cap is applied.</summary>
    public decimal UncappedPenaltyFee { get; init; }

    public decimal PenaltyFee { get; init; }
    public bool PenaltyWasCapped => UncappedPenaltyFee > PenaltyFee;
    public bool IsOverstay => ChargeableOverstayMinutes > 0;
    public decimal TotalFee { get; init; }
}
