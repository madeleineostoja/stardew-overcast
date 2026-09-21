namespace Overcast;

internal sealed class ModConfig
{
    public bool Enabled { get; set; } = true;
    public float Opacity { get; set; } = 0.10f;
    public float Speed { get; set; } = 1f;
    public float Scale { get; set; } = 1f;
    public bool EnableAtNight { get; set; }
    public bool EnableDuringRain { get; set; }
    public bool EnableDuringSnow { get; set; }
    public bool EnableDuringStorms { get; set; }
    public bool SecondLayerEnabled { get; set; } = true;

    public bool Normalize()
    {
        var opacity = Clamp(Opacity, 0f, 0.25f);
        var speed = Clamp(Speed, 0.1f, 3f);
        var scale = Clamp(Scale, 0.5f, 1.75f);
        var changed = opacity != Opacity || speed != Speed || scale != Scale;
        Opacity = opacity;
        Speed = speed;
        Scale = scale;
        return changed;
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return float.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : minimum;
    }
}
