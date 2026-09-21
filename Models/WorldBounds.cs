namespace Overcast.Models;

internal readonly record struct WorldBounds(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Intersects(WorldBounds other)
    {
        return X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
    }

    public WorldBounds Inflate(float amount)
    {
        return new WorldBounds(X - amount, Y - amount, Width + (amount * 2), Height + (amount * 2));
    }
}
