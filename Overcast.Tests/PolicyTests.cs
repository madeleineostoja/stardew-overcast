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
            Opacity = float.NaN,
            Speed = 9f,
            Scale = 0.1f,
        };

        Assert.True(config.Normalize());
        Assert.Equal(0f, config.Opacity);
        Assert.Equal(3f, config.Speed);
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
