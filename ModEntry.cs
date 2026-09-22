using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Overcast.Models;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace Overcast;

internal sealed class ModEntry : Mod
{
    private const float FadeSeconds = 1.5f;
    private readonly Dictionary<string, WeatherKind> dailyWeather = new(StringComparer.Ordinal);
    private ModConfig config = null!;
    private CloudPopulation? population;
    private bool assetsFailed;
    private bool fieldResetRequested = true;
    private bool simulationStopped = true;
    private bool resetAfterFade;
    private bool weatherWondersInstalled;
    private float transition;
    private float secondaryTransition = 1f;
    private float displayedOpacity = 0.12f;
    private float displayedWeatherOpacity = 1f;
    private float displayedScale = 1f;
    private float displayedSpeed = 1f;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<ModConfig>();
        NormalizeConfig();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        helper.Events.Player.Warped += OnWarped;
        helper.Events.Display.RenderedWorld += OnRenderedWorld;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        weatherWondersInstalled = Helper.ModRegistry.IsLoaded("Kana.WeatherWonders");
        LoadCloudTextures();
        RegisterConfigMenu();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        dailyWeather.Clear();
        StopSimulation();
        transition = 0f;
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        dailyWeather.Clear();
        resetAfterFade = true;
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer)
            return;

        // A new location must never receive clouds from the old location.
        StopSimulation();
        var modifiers = GetWeatherModifiers(e.NewLocation);
        displayedWeatherOpacity = modifiers.Opacity;
        transition = config.Enabled && IsEligible(e.NewLocation) && modifiers.IsAllowed
            ? GetTimeFactor()
            : 0f;
        resetAfterFade = false;
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        var elapsedSeconds = Math.Min(0.1f, (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds);
        if (elapsedSeconds <= 0f)
            return;

        displayedOpacity = SmoothTo(displayedOpacity, config.Opacity, elapsedSeconds);
        displayedScale = SmoothTo(displayedScale, config.Scale, elapsedSeconds);
        displayedSpeed = SmoothTo(displayedSpeed, config.Speed, elapsedSeconds);
        secondaryTransition = MoveTowards(secondaryTransition, config.SecondLayerEnabled ? 1f : 0f, elapsedSeconds);

        var location = Game1.currentLocation;
        if (!IsEligible(location))
        {
            transition = MoveTowards(transition, 0f, elapsedSeconds);
            resetAfterFade = false;
            StopSimulation();
            return;
        }

        var modifiers = GetWeatherModifiers(location);
        displayedWeatherOpacity = SmoothTo(displayedWeatherOpacity, modifiers.Opacity, elapsedSeconds);
        var timeFactor = GetTimeFactor();
        var wantsEffect = config.Enabled && modifiers.IsAllowed && !resetAfterFade && timeFactor > 0f;
        transition = MoveTowards(transition, wantsEffect ? timeFactor : 0f, elapsedSeconds);

        if (resetAfterFade && transition <= 0.001f)
        {
            StopSimulation();
            resetAfterFade = false;
            wantsEffect = config.Enabled && modifiers.IsAllowed && timeFactor > 0f;
        }

        if (assetsFailed || population is null || location is null)
            return;

        var effectIsVisible = displayedOpacity * displayedWeatherOpacity * transition > 0.001f;
        if (!wantsEffect || config.Opacity <= 0f)
        {
            if (effectIsVisible)
                population.Advance(elapsedSeconds, displayedSpeed * modifiers.Speed);
            else
                StopSimulation();
            return;
        }

        var viewport = GetViewportBounds();
        if (fieldResetRequested)
        {
            population.Reset(GetPopulationSeed(location), viewport, displayedScale, modifiers.Density, config.SecondLayerEnabled);
            fieldResetRequested = false;
            simulationStopped = false;
        }
        else if (population.NeedsReset(viewport))
        {
            resetAfterFade = true;
        }
        else
        {
            population.Update(
                elapsedSeconds,
                viewport,
                displayedScale,
                displayedSpeed * modifiers.Speed,
                modifiers.Density,
                config.SecondLayerEnabled || secondaryTransition > 0.001f);
        }
    }

    private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
    {
        var globalAlpha = displayedOpacity * displayedWeatherOpacity * transition;
        if (globalAlpha <= 0.001f || population is null || !IsEligible(Game1.currentLocation))
            return;

        var viewport = GetViewportBounds();
        for (var index = 0; index < population.Count; index++)
        {
            var cloud = population[index];
            var bounds = cloud.GetBounds(displayedScale);
            if (!bounds.Intersects(viewport))
                continue;

            var alpha = globalAlpha * (cloud.IsSecondary ? 0.35f * secondaryTransition : 1f);
            if (alpha <= 0.001f)
                continue;

            e.SpriteBatch.Draw(
                cloud.Texture,
                Game1.GlobalToLocal(Game1.viewport, new Vector2(bounds.X, bounds.Y)),
                null,
                Color.Black * alpha,
                0f,
                Vector2.Zero,
                cloud.Variation * displayedScale,
                cloud.FlipHorizontally ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                1f);
        }
    }

    private WeatherModifiers GetWeatherModifiers(GameLocation? location)
    {
        if (location is null)
            return new WeatherModifiers(0f, 0f, 0f, false);

        var key = location.NameOrUniqueName;
        if (!dailyWeather.TryGetValue(key, out var kind))
        {
            var weather = location.GetWeather();
            var state = new WeatherState(
                weather.Weather,
                location.IsRainingHere() || location.IsGreenRainingHere(),
                location.IsSnowingHere(),
                location.IsLightningHere(),
                location.IsDebrisWeatherHere());
            kind = WeatherPolicy.Classify(state, weatherWondersInstalled);
            dailyWeather[key] = kind;
        }
        return WeatherPolicy.GetModifiers(kind, config);
    }

    private void StopSimulation()
    {
        if (simulationStopped)
            return;

        population?.Clear();
        simulationStopped = true;
        fieldResetRequested = true;
    }

    private static bool IsEligible(GameLocation? location)
    {
        return location is not null
            && location.IsOutdoors
            && !location.IsGreenhouse
            && location is not MineShaft
            && location is not VolcanoDungeon;
    }

    private float GetTimeFactor()
    {
        if (config.EnableAtNight)
            return 1f;
        if (Game1.timeOfDay < 600 || Game1.timeOfDay >= 2000)
            return 0f;
        if (Game1.timeOfDay < 800)
            return (Game1.timeOfDay - 600) / 200f;
        if (Game1.timeOfDay >= 1800)
            return (2000 - Game1.timeOfDay) / 200f;
        return 1f;
    }

    private static WorldBounds GetViewportBounds()
    {
        return new WorldBounds(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height);
    }

    private static int GetPopulationSeed(GameLocation location)
    {
        var seed = 17;
        var text = $"{Game1.uniqueIDForThisGame}:{Game1.Date.TotalDays}:{location.GetLocationContextId()}:{location.NameOrUniqueName}";
        foreach (var character in text)
            seed = unchecked(seed * 31 + character);
        return seed;
    }

    private static float SmoothTo(float value, float target, float elapsedSeconds)
    {
        return MathHelper.Lerp(value, target, Math.Min(1f, elapsedSeconds / FadeSeconds));
    }

    private static float MoveTowards(float value, float target, float elapsedSeconds)
    {
        var maximumDelta = elapsedSeconds / FadeSeconds;
        return MathF.Abs(target - value) <= maximumDelta ? target : value + MathF.Sign(target - value) * maximumDelta;
    }

    private void NormalizeConfig()
    {
        if (config.Normalize())
            Monitor.Log("One or more unsafe Overcast configuration values were clamped to supported ranges.", LogLevel.Warn);
    }

    private void LoadCloudTextures()
    {
        var directory = Path.Combine(Helper.DirectoryPath, "assets", "clouds");
        var files = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "cloud*.png", SearchOption.TopDirectoryOnly).OrderBy(Path.GetFileName).ToArray()
            : Array.Empty<string>();
        if (files.Length == 0)
        {
            assetsFailed = true;
            Monitor.Log("No cloud masks were found in assets/clouds. Restore cloud*.png files to enable Overcast.", LogLevel.Error);
            return;
        }

        try
        {
            var textures = new List<Texture2D>(files.Length);
            foreach (var file in files)
                textures.Add(Helper.ModContent.Load<Texture2D>($"assets/clouds/{Path.GetFileName(file)}"));
            population = new CloudPopulation(textures);
        }
        catch (Exception exception)
        {
            assetsFailed = true;
            Monitor.Log($"Overcast could not load its cloud masks and has been disabled: {exception.Message}", LogLevel.Error);
        }
    }

    private void RegisterConfigMenu()
    {
        var api = Helper.ModRegistry.GetApi<IConfigMenuApi>("spacechase0.GenericModConfigMenu");
        if (api is null)
            return;

        api.Register(ModManifest, () =>
        {
            config = new ModConfig();
            resetAfterFade = true;
        }, () =>
        {
            NormalizeConfig();
            Helper.WriteConfig(config);
        });

        AddBool(api, "enabled", () => config.Enabled, value => config.Enabled = value);
        AddNumber(api, "opacity", () => config.Opacity, value => config.Opacity = value, 0f, 0.5f, 0.01f);
        AddNumber(api, "speed", () => config.Speed, value => config.Speed = value, 0f, 2f, 0.05f);
        AddNumber(api, "scale", () => config.Scale, value => config.Scale = value, 0.5f, 1.5f, 0.05f);
        AddBool(api, "enable-at-night", () => config.EnableAtNight, value => config.EnableAtNight = value);
        AddBool(api, "enable-during-rain", () => config.EnableDuringRain, value => config.EnableDuringRain = value);
        AddBool(api, "enable-during-snow", () => config.EnableDuringSnow, value => config.EnableDuringSnow = value);
        AddBool(api, "enable-during-storms", () => config.EnableDuringStorms, value => config.EnableDuringStorms = value);
        AddBool(api, "enable-during-special-weather", () => config.EnableDuringSpecialWeather, value => config.EnableDuringSpecialWeather = value);
        AddBool(api, "second-layer-enabled", () => config.SecondLayerEnabled, value => config.SecondLayerEnabled = value);
    }

    private void AddBool(IConfigMenuApi api, string key, Func<bool> getValue, Action<bool> setValue)
    {
        api.AddBoolOption(ModManifest, getValue, setValue, () => T($"config.{key}.name"), () => T($"config.{key}.tooltip"), key);
    }

    private void AddNumber(IConfigMenuApi api, string key, Func<float> getValue, Action<float> setValue, float minimum, float maximum, float interval)
    {
        api.AddNumberOption(ModManifest, getValue, setValue, () => T($"config.{key}.name"), () => T($"config.{key}.tooltip"), minimum, maximum, interval, null, key);
    }

    private string T(string key)
    {
        return Helper.Translation.Get(key);
    }
}
