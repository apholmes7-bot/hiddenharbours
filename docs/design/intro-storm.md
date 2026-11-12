# The intro storm (a campaign barrier)

**Owner's ruling:** a strong windy storm appears if the player tries to sail south during the intro.
It pushes them back into the intro region, so they follow the campaign's path. It stops when the dory
is repaired.

## What it is

An authored wall, one asset: `Assets/_Project/Data/Resources/CampaignBarriers/IntroStormEastWaterSouth.asset`
(`CampaignBarrierDef`, id `campaign_barrier.intro_storm_east_water_south`). It stands along the whole
south edge of the East Water until the saved flag `onboarded` is set (the dory's repair).

It is **not the world's weather.** The deterministic sim is not read for it, changed by it or saved
with it. The wall is a function of where the player's boat is and one saved flag.

## What the player meets, sailing south

All distances are metres inside the south edge and are fields on the asset.

| Line | Distance | What happens |
|---|---|---|
| Start | 170 | the wind rises, the sea builds, rain starts; a push north begins |
| (35 % of the way in) | about 137 | she thinks the wall's one line |
| Full | 90 | the storm is at full strength; the push is 1.2 m/s² |
| Limit | 60 | the push grows by 2 m/s² for every metre past this line, up to 8 m/s², and speed toward the edge is braked |
| (the South Water's door, when its lane adds it) | 27 to 21 | never reached while the wall stands |

The push is an acceleration, so a rowed dory and a heavy power boat are turned back alike. Only
force is added to the hull: no position or speed is written, nothing teleports, grounds or is
damaged. With the helm let go, the storm sets the boat back north of the limit line and leaves her
afloat.

## How it is built

- `CampaignBarrierDirector` (World) installs itself; **no region scene is edited.** It finds the
  player's boat through Core's helm slot and adds force to her body.
- `LocalWeather` (Core) is the seam to the look and sound. The director sets a shade (strength,
  wind, visibility, sea state); the mood grade, the rain, the wave field and the wind sound pass the
  sim's sample through `LocalWeather.Shade(...)`. `IEnvironmentService.Sample()` itself is untouched,
  so gameplay, the HUD's wind and the save all see the sim's weather.
- The line is published as an inner-voice `AmbientSpeechRequested`, at most once a minute.
- With no save service (a test fixture, the title shell) no wall stands. During the arrival opening
  no wall stands.

## Adding another wall

Add one `CampaignBarrierDef` asset to the same Resources folder: a region id, a point on the edge,
the inward direction, the flag that lifts it, the distances, the push and the line. St Peters' south
edge will need its own when that lane opens an exit to the South Water.
