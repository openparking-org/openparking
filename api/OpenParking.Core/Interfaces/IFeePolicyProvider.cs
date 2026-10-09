using OpenParking.Core.Models;

namespace OpenParking.Core.Interfaces;

/// <summary>Resolves the current <see cref="FeePolicy"/> from runtime settings.</summary>
public interface IFeePolicyProvider
{
    Task<FeePolicy> GetAsync();
}
