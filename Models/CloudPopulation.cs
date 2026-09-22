using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Overcast.Models;

internal sealed class CloudPopulation
{
    // This exceeds the largest mask at the maximum supported scale, so new edge clouds stay offscreen.
    private const float Padding = 2800f;
    private const float BaseScale = 0.85f;
    private static readonly Vector2 BaseWind = new Vector2(3f, 1f) * 3.3f;

    private readonly List<Texture2D> textures;
    private readonly List<Cloud> clouds = new();
    private Random random = new(0);
    private WorldBounds lastViewport;
    private bool hasViewport;

    public CloudPopulation(IReadOnlyList<Texture2D> textures)
    {
        this.textures = textures.ToList();
    }

    public int Count => clouds.Count;

    public Cloud this[int index] => clouds[index];

    public void Reset(int seed, WorldBounds viewport, float globalScale, float density, bool includeSecondary)
    {
        random = new Random(seed);
        clouds.Clear();
        lastViewport = viewport;
        hasViewport = true;
        Populate(GetSimulationRegion(viewport), globalScale, density, includeSecondary, true, null, viewport);
    }

    public bool NeedsReset(WorldBounds viewport)
    {
        if (!hasViewport)
            return true;

        var dx = Math.Abs(viewport.X - lastViewport.X);
        var dy = Math.Abs(viewport.Y - lastViewport.Y);
        return dx > Padding * 0.65f || dy > Padding * 0.65f;
    }

    public void Update(float elapsedSeconds, WorldBounds viewport, float globalScale, float speed, float density, bool includeSecondary)
    {
        var field = GetSimulationRegion(viewport);
        var retirementArea = field.Inflate(Padding * 0.75f);
        for (var index = clouds.Count - 1; index >= 0; index--)
        {
            var cloud = clouds[index];
            cloud.Move(elapsedSeconds, speed);
            if ((!includeSecondary && cloud.IsSecondary) || !cloud.GetBounds(globalScale).Intersects(retirementArea))
                clouds.RemoveAt(index);
        }

        Populate(field, globalScale, density, includeSecondary, false, GetExposedBand(viewport), null);
        lastViewport = viewport;
        hasViewport = true;
    }

    public void Advance(float elapsedSeconds, float speed)
    {
        foreach (var cloud in clouds)
            cloud.Move(elapsedSeconds, speed);
    }

    public void Clear()
    {
        clouds.Clear();
        hasViewport = false;
    }

    private void Populate(WorldBounds field, float globalScale, float density, bool includeSecondary, bool initial, WorldBounds? exposedBand, WorldBounds? visibleArea)
    {
        if (textures.Count == 0)
            return;

        var primaryTarget = PopulationRules.GetTarget(field, globalScale, density, false);
        var secondaryTarget = includeSecondary ? PopulationRules.GetTarget(field, globalScale, density, true) : 0;
        var primaryCount = CountInField(field, globalScale, false);
        var secondaryCount = CountInField(field, globalScale, true);

        while (primaryCount < primaryTarget && CountForStratum(false) < PopulationRules.PrimaryCap)
        {
            clouds.Add(CreateCloud(field, globalScale, false, initial, exposedBand, primaryCount == 0 ? visibleArea : null));
            primaryCount++;
        }

        while (secondaryCount < secondaryTarget && CountForStratum(true) < PopulationRules.SecondaryCap)
        {
            clouds.Add(CreateCloud(field, globalScale, true, initial, exposedBand, null));
            secondaryCount++;
        }
    }

    private int CountInField(WorldBounds field, float globalScale, bool secondary)
    {
        var count = 0;
        foreach (var cloud in clouds)
        {
            if (cloud.IsSecondary == secondary && cloud.GetBounds(globalScale).Intersects(field))
                count++;
        }
        return count;
    }

    private int CountForStratum(bool secondary)
    {
        var count = 0;
        foreach (var cloud in clouds)
        {
            if (cloud.IsSecondary == secondary)
                count++;
        }
        return count;
    }

    private Cloud CreateCloud(WorldBounds field, float globalScale, bool secondary, bool initial, WorldBounds? exposedBand, WorldBounds? visibleArea)
    {
        var variation = BaseScale * (0.8f + (float)random.NextDouble() * 0.45f);
        if (secondary)
            variation *= 0.9f;

        var texture = textures[random.Next(textures.Count)];
        var width = texture.Width * variation * globalScale;
        var height = texture.Height * variation * globalScale;
        Vector2 position;
        if (visibleArea is { } visible)
        {
            position = FindSpacedPosition(visible, width, height, globalScale, secondary);
        }
        else if (initial)
        {
            position = FindSpacedPosition(field, width, height, globalScale, secondary);
        }
        else if (exposedBand is { } band)
        {
            position = FindSpacedPosition(band, width, height, globalScale, secondary);
        }
        else
        {
            // Steady-state replacements start beyond the padded upwind edge.
            var crossAxis = (float)random.NextDouble();
            if (Math.Abs(BaseWind.X) >= Math.Abs(BaseWind.Y))
            {
                var x = BaseWind.X >= 0 ? field.X - width : field.Right;
                position = new Vector2(x, field.Y + crossAxis * Math.Max(1f, field.Height - height));
            }
            else
            {
                var y = BaseWind.Y >= 0 ? field.Y - height : field.Bottom;
                position = new Vector2(field.X + crossAxis * Math.Max(1f, field.Width - width), y);
            }
        }

        var speedVariation = 0.85f + (float)random.NextDouble() * 0.3f;
        var layerVelocity = secondary ? 1.18f : 1f;
        return new Cloud(texture, position, variation, random.Next(3) == 0, BaseWind * speedVariation * layerVelocity, secondary);
    }

    private Vector2 FindSpacedPosition(WorldBounds area, float width, float height, float globalScale, bool secondary)
    {
        var bestPosition = GetRandomPosition(area, width, height);
        var fewestOverlaps = int.MaxValue;
        var allowedOverlaps = secondary ? 1 : 0;
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var position = attempt == 0 ? bestPosition : GetRandomPosition(area, width, height);
            var candidate = new WorldBounds(position.X, position.Y, width, height);
            var overlaps = 0;
            foreach (var cloud in clouds)
            {
                if (!secondary && cloud.IsSecondary)
                    continue;
                if (candidate.Intersects(cloud.GetBounds(globalScale)))
                    overlaps++;
            }
            if (overlaps <= allowedOverlaps)
                return position;
            if (overlaps < fewestOverlaps)
            {
                bestPosition = position;
                fewestOverlaps = overlaps;
            }
        }
        return bestPosition;
    }

    private Vector2 GetRandomPosition(WorldBounds area, float width, float height)
    {
        return new Vector2(
            area.X + (float)random.NextDouble() * Math.Max(1f, area.Width - width),
            area.Y + (float)random.NextDouble() * Math.Max(1f, area.Height - height));
    }

    private WorldBounds? GetExposedBand(WorldBounds viewport)
    {
        if (!hasViewport)
            return null;

        var field = GetSimulationRegion(viewport);
        var previous = GetSimulationRegion(lastViewport);
        var dx = viewport.X - lastViewport.X;
        var dy = viewport.Y - lastViewport.Y;
        if (Math.Abs(dx) >= Math.Abs(dy) && Math.Abs(dx) > 1f)
            return dx > 0
                ? new WorldBounds(previous.Right, field.Y, field.Right - previous.Right, field.Height)
                : new WorldBounds(field.X, field.Y, previous.X - field.X, field.Height);
        if (Math.Abs(dy) > 1f)
            return dy > 0
                ? new WorldBounds(field.X, previous.Bottom, field.Width, field.Bottom - previous.Bottom)
                : new WorldBounds(field.X, field.Y, field.Width, previous.Y - field.Y);
        return null;
    }

    private static WorldBounds GetSimulationRegion(WorldBounds viewport)
    {
        return viewport.Inflate(Padding);
    }
}
