namespace Overcast.Models;

internal static class PopulationRules
{
    public const int PrimaryCap = 11;
    public const int SecondaryCap = 4;

    public static int GetTarget(WorldBounds field, float globalScale, float density, bool secondary)
    {
        const float baseScale = 0.72f;
        var approximateCloudArea = 1536f * 1024f * baseScale * baseScale * globalScale * globalScale;
        var ratio = secondary ? 0.24f : 0.68f;
        var cap = secondary ? SecondaryCap : PrimaryCap;
        return Math.Clamp((int)MathF.Ceiling(field.Width * field.Height / approximateCloudArea * density * ratio), 1, cap);
    }
}
