using Orkystra.Contracts.Connectors;

namespace Orkystra.Application.Connectors;

public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IProviderAdapter> _providers = new(StringComparer.OrdinalIgnoreCase);

    public ProviderRegistry(IEnumerable<IProviderAdapter> providers)
    {
        foreach (var provider in providers ?? throw new ArgumentNullException(nameof(providers)))
        {
            _providers[provider.ProviderId] = provider;
        }
    }

    public IReadOnlyCollection<IProviderAdapter> ListAll() => _providers.Values.ToArray();

    public IReadOnlyCollection<IProviderAdapter> ListByDomain(ProviderDomain domain) =>
        _providers.Values.Where(provider => provider.Domain == domain).ToArray();

    public bool TryGet(string providerId, out IProviderAdapter? provider) =>
        _providers.TryGetValue(providerId, out provider);

    public async ValueTask<IReadOnlyCollection<IProviderAdapter>> ListByCapabilityAsync(
        Func<ProviderCapabilitySet, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var matches = new List<IProviderAdapter>();
        foreach (var provider in _providers.Values)
        {
            var capabilities = await provider.GetCapabilitiesAsync(cancellationToken);
            if (predicate(capabilities))
            {
                matches.Add(provider);
            }
        }

        return matches;
    }
}
