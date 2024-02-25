// Copyright 2023-2024 Ville Penttinen
// Distributed under the MIT License.
// https://github.com/vipentti/dotnet-check-updates/blob/main/LICENSE.md

namespace DotnetCheckUpdates.Core;

// Loosely based on
// https://github.com/dotnet/aspnetcore/blob/main/src/Extensions/Features/src/IFeatureCollection.cs
internal interface IFeatureCollection
{
    /// <summary>
    /// Gets or sets a given feature. Setting a null value removes the feature.
    /// </summary>
    /// <param name="key"></param>
    /// <returns>The requested feature, or null if it is not present.</returns>
    object? this[Type key] { get; set; }

    /// <summary>
    /// Retrieves the requested feature from the collection.
    /// </summary>
    /// <typeparam name="TFeature">The feature key.</typeparam>
    /// <returns>The requested feature, or null if it is not present.</returns>
    TFeature? Get<TFeature>();

    /// <summary>
    /// Sets the given feature in the collection.
    /// </summary>
    /// <typeparam name="TFeature">The feature key.</typeparam>
    /// <param name="instance">The feature value.</param>
    void Set<TFeature>(TFeature? instance);
}

internal class FeatureCollection : IFeatureCollection
{
    public FeatureCollection() { }

    private readonly Dictionary<Type, object> _features = [];

    public object? this[Type key]
    {
        get => _features.TryGetValue(key, out var feature) ? feature : null;
        set
        {
            if (value is null)
            {
                _features.Remove(key);
                return;
            }

            _features[key] = value;
        }
    }

    /// <inheritdoc />
    public TFeature? Get<TFeature>()
    {
        if (typeof(TFeature).IsValueType)
        {
            var feature = this[typeof(TFeature)];
            if (feature is null && Nullable.GetUnderlyingType(typeof(TFeature)) is null)
            {
                throw new InvalidOperationException(
                    $"{typeof(TFeature).FullName} does not exist in the feature collection "
                        + $"and because it is a struct the method can't return null. Use 'featureCollection[typeof({typeof(TFeature).FullName})] is not null' to check if the feature exists."
                );
            }
            return (TFeature?)feature;
        }
        return (TFeature?)this[typeof(TFeature)];
    }

    /// <inheritdoc />
    public void Set<TFeature>(TFeature? instance)
    {
        this[typeof(TFeature)] = instance;
    }
}
