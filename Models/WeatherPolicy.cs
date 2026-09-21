namespace Overcast.Models;

internal enum WeatherKind
{
    Clear,
    Windy,
    Cloudy,
    Rain,
    Snow,
    Storm,
}

internal readonly record struct WeatherState(
    string? Id,
    bool IsRaining,
    bool IsSnowing,
    bool IsLightning,
    bool IsDebris);

internal readonly record struct WeatherModifiers(float Opacity, float Density, float Speed, bool IsAllowed);

internal static class WeatherPolicy
{
    private static readonly IReadOnlyDictionary<string, WeatherKind> WeatherWonders =
        new Dictionary<string, WeatherKind>(StringComparer.Ordinal)
        {
            ["Kana.WeatherWonders_Cloudy"] = WeatherKind.Cloudy,
            ["Kana.WeatherWonders_Heatwave"] = WeatherKind.Clear,
            ["Kana.WeatherWonders_Drizzle"] = WeatherKind.Rain,
            ["Kana.WeatherWonders_Deluge"] = WeatherKind.Rain,
            ["Kana.WeatherWonders_AcidRain"] = WeatherKind.Rain,
            ["Kana.WeatherWonders_MuddyRain"] = WeatherKind.Rain,
            ["Kana.WeatherWonders_Mist"] = WeatherKind.Rain,
            ["Kana.WeatherWonders_Blizzard"] = WeatherKind.Snow,
            ["Kana.WeatherWonders_RainSnowMix"] = WeatherKind.Snow,
            ["Kana.WeatherWonders_DryLightning"] = WeatherKind.Storm,
            ["Kana.WeatherWonders_Hailstorm"] = WeatherKind.Storm,
            ["Kana.WeatherWonders_Sandstorm"] = WeatherKind.Storm,
        };

    public static WeatherKind Classify(WeatherState weather, bool weatherWondersInstalled)
    {
        if (weatherWondersInstalled
            && weather.Id is not null
            && WeatherWonders.TryGetValue(weather.Id, out var knownKind))
        {
            return knownKind;
        }

        // Storm wins over every wet flag. Snow must win over rain for mixed weather.
        if (weather.IsLightning)
            return WeatherKind.Storm;
        if (weather.IsSnowing)
            return WeatherKind.Snow;
        if (weather.IsRaining)
            return WeatherKind.Rain;
        return weather.IsDebris ? WeatherKind.Windy : WeatherKind.Clear;
    }

    public static WeatherModifiers GetModifiers(WeatherKind kind, ModConfig config)
    {
        return kind switch
        {
            WeatherKind.Windy => new WeatherModifiers(1f, 1.15f, 1.5f, true),
            WeatherKind.Cloudy => new WeatherModifiers(0.65f, 1.3f, 0.9f, true),
            WeatherKind.Rain => new WeatherModifiers(0.4f, 0.7f, 1.1f, config.EnableDuringRain),
            WeatherKind.Snow => new WeatherModifiers(0.35f, 0.65f, 0.8f, config.EnableDuringSnow),
            WeatherKind.Storm => new WeatherModifiers(0.3f, 0.55f, 1.35f, config.EnableDuringStorms),
            _ => new WeatherModifiers(1f, 1f, 1f, true),
        };
    }
}
