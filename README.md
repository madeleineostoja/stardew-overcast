# Overcast

Overcast adds restrained, drifting cloud-shadow silhouettes to outdoor Stardew Valley locations. The shade is anchored in the world, moves with a gentle built-in wind, and stays below menus, the HUD, and the cursor.

## Requirements

- Stardew Valley 1.6
- SMAPI 4.0 or later

[Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) (GMCM) is supported as an optional convenience, not a requirement.

## Install and remove

1. Install SMAPI.
2. Unzip the Overcast release into Stardew Valley's `Mods` directory.
3. Start the game. Configure the mod through GMCM when installed, or edit the generated `config.json`.

To remove Overcast, delete its folder from `Mods`. It does not write save data.

## Configuration

Invalid manually edited numeric values are clamped to safe ranges when the mod loads.

| Option | Default | Description |
| --- | --- | --- |
| `Enabled` | `true` | Master switch. Fades out before all cloud simulation and drawing stop. |
| `Opacity` | `0.12` | Maximum clear-day shadow opacity; time, weather, mask edges, and layer tuning reduce it further. Range: `0.00`–`0.50`. |
| `Speed` | `1.0` | Multiplies the built-in gentle wind speed. Range: `0.00`–`2.00`. |
| `Scale` | `1.0` | Multiplies each cloud's varied size. Range: `0.50`–`1.50`. |
| `EnableAtNight` | `false` | Retains the weather-appropriate effect overnight instead of fading it out after evening. |
| `EnableDuringRain` | `false` | Enables a reduced effect for rain-like and obscuring wet weather. |
| `EnableDuringSnow` | `false` | Enables a reduced effect for snow, blizzards, and rain/snow mixtures. |
| `EnableDuringStorms` | `false` | Enables a reduced effect for lightning and severe weather. |
| `SecondLayerEnabled` | `true` | Enables a sparse, lower-opacity secondary stratum. |

Changes from GMCM apply live and are saved through SMAPI.

## Compatibility

Overcast automatically detects Weather Wonders (`Kana.WeatherWonders`) without requiring it. Its known weather IDs use these policies:

- **Cloudy:** more clouds at lower contrast.
- **Heatwave:** clear-weather behaviour.
- **Drizzle, Deluge, Acid Rain, Muddy Rain, and Mist:** reduced rain behaviour, controlled by `EnableDuringRain`.
- **Blizzard and Rain/Snow Mix:** reduced snow behaviour, controlled by `EnableDuringSnow`.
- **Dry Lightning, Hailstorm, and Sandstorm:** reduced storm behaviour, controlled by `EnableDuringStorms`.

Unknown custom Weather Wonders IDs fall back to Stardew's location-specific rain, snow, lightning, debris/wind, and green-rain flags; otherwise they use clear-weather behaviour. Storm takes precedence over rain, and snow takes precedence over rain for mixed weather.

The mod deliberately has no external wind-vector API and no Realistic Wind Effects integration; both mods use their own visual wind behaviour. Nightshade, Immersive Lighting, recolours, and modded outdoor maps have no special compatibility code. Overcast draws on SMAPI's `RenderedWorld` layer, but SMAPI does not guarantee a fixed cross-mod callback ordering; any visual grading from another mod may therefore occur before or after this shade.

The compatibility stack (Nightshade, Immersive Lighting, Realistic Wind Effects, representative recolours, Weather Wonders, and a modded outdoor map) still needs in-game release validation. In particular, record the observed Nightshade ordering rather than treating the order above as a guarantee.

Overcast skips greenhouses, mines, volcano dungeons, caves, houses, shops, and other indoor locations by using the game's outdoor/location state. It does not inspect maps, tiles, entities, or terrain.

## Performance and visual notes

The supplied masks are loaded once and drawn with ordinary alpha blending. Overcast maintains a small capped population, culls clouds outside the world viewport, and does not use shaders, render targets, scene analysis, or per-frame allocations. The secondary layer is intentionally sparse. Before release, compare disabled, primary-only, and default two-layer behaviour on the Retroid Pocket Classic/Cinderbox target at 100%, 80%, and 75% render scale; no target-device measurements are bundled with this source checkout.
