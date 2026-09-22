using Overcast.Models;
using Xunit;

namespace Overcast.Tests;

public sealed class PolicyTests
{
    [Fact]
    public void Normalize_clamps_non_finite_and_out_of_range_values()
    {
        var config = new ModConfig
        {
            Opacity = 9f,
            Speed = 9f,
            Scale = float.NaN,
        };

        Assert.True(config.Normalize());
        Assert.Equal(0.5f, config.Opacity);
        Assert.Equal(2f, config.Speed);
        Assert.Equal(0.5f, config.Scale);
    }

    [Fact]
    public void Known_weather_wonders_id_overrides_overlapping_vanilla_flags()
    {
        var weather = new WeatherState("Kana.WeatherWonders_RainSnowMix", true, false, true, true);

        Assert.Equal(WeatherKind.Snow, WeatherPolicy.Classify(weather, true));
    }

    [Fact]
    public void Vanilla_weather_uses_storm_then_snow_precedence()
    {
        Assert.Equal(WeatherKind.Storm, WeatherPolicy.Classify(new WeatherState(null, true, true, true, false), true));
        Assert.Equal(WeatherKind.Snow, WeatherPolicy.Classify(new WeatherState("Unknown.Custom", true, true, false, false), true));
    }

    [Fact]
    public void Rain_policy_requires_its_explicit_option()
    {
        var config = new ModConfig();

        Assert.False(WeatherPolicy.GetModifiers(WeatherKind.Rain, config).IsAllowed);
        config.EnableDuringRain = true;
        Assert.True(WeatherPolicy.GetModifiers(WeatherKind.Rain, config).IsAllowed);
    }

    [Fact]
    public void Heavy_weather_wonders_effects_are_disabled_by_default()
    {
        var config = new ModConfig();
        var heatwave = new WeatherState("Kana.WeatherWonders_Heatwave", false, false, false, false);
        var futureEffect = new WeatherState("Kana.WeatherWonders_FutureEffect", false, false, false, false);

        Assert.Equal(WeatherKind.Special, WeatherPolicy.Classify(heatwave, true));
        Assert.Equal(WeatherKind.Special, WeatherPolicy.Classify(futureEffect, true));
        Assert.False(WeatherPolicy.GetModifiers(WeatherKind.Special, config).IsAllowed);

        config.EnableDuringSpecialWeather = true;
        Assert.True(WeatherPolicy.GetModifiers(WeatherKind.Special, config).IsAllowed);
    }

    [Fact]
    public void Population_density_changes_targets_before_the_hard_cap()
    {
        var typicalField = new WorldBounds(0, 0, 6880, 6320);

        var storm = PopulationRules.GetTarget(typicalField, 1f, 0.55f, false);
        var clear = PopulationRules.GetTarget(typicalField, 1f, 1f, false);
        var cloudy = PopulationRules.GetTarget(typicalField, 1f, 1.3f, false);

        Assert.True(storm < clear);
        Assert.True(clear < cloudy);
        Assert.Equal(PopulationRules.PrimaryCap, cloudy);
    }

    [Fact]
    public void Population_target_stays_within_the_stratum_hard_cap()
    {
        var enormousField = new WorldBounds(0, 0, 100_000, 100_000);

        Assert.Equal(PopulationRules.PrimaryCap, PopulationRules.GetTarget(enormousField, 0.5f, 10f, false));
        Assert.Equal(PopulationRules.SecondaryCap, PopulationRules.GetTarget(enormousField, 0.5f, 10f, true));
    }

    [Fact]
    public void Bounds_intersection_excludes_edge_touching_clouds()
    {
        var viewport = new WorldBounds(100, 100, 200, 200);

        Assert.True(viewport.Intersects(new WorldBounds(299, 100, 10, 10)));
        Assert.False(viewport.Intersects(new WorldBounds(300, 100, 10, 10)));
    }
}
