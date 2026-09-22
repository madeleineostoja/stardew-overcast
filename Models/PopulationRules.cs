namespace Overcast.Models;

internal static class PopulationRules
{
    public const int PrimaryCap = 11;
    public const int SecondaryCap = 4;
    private const float MaximumWeatherDensity = 1.3f;

    public static int GetTarget(WorldBounds field, float globalScale, float density, bool secondary)
    {
        const float baseScale = 0.85f;
        var approximateCloudArea = 1536f * 1024f * baseScale * baseScale * globalScale * globalScale;
        var ratio = secondary ? 0.24f : 0.68f;
        var cap = secondary ? SecondaryCap : PrimaryCap;
        var areaTarget = field.Width * field.Height / approximateCloudArea * ratio;

        // Reserve enough headroom for cloudy and windy weather to increase the population before it reaches the hard cap.
        var clearWeatherTarget = Math.Min(areaTarget, cap / MaximumWeatherDensity);
        return Math.Clamp((int)MathF.Round(clearWeatherTarget * density, MidpointRounding.AwayFromZero), 1, cap);
    }
}
