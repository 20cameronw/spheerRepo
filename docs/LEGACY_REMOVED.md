# Legacy removal

The original Spheer prototype (idle/clicker base-builder: `_Scripts`, `MainGame` scene, old prefabs and UI sprites) was
removed when Frontier became the only game in this project. Nothing in Frontier depended on it.

**Recover it:** `git checkout legacy-spheer-v1` (tag on the last commit that still contains it), or browse that tag on GitHub.

## What was removed

| Area | Notes |
|------|-------|
| `Assets/_Scripts` | old gameplay, UI, ads stubs, save system, enemy state machines |
| `Assets/Scenes` | `MainGame`, `PhotoBooth` and their profiles; build settings now contain only the Frontier scene |
| `Assets/Prefabs`, `Assets/Materials`, `Assets/Images`, `Assets/Dark UI` | old prefabs, materials, UI sprites |
| `Assets/LeanTween`, `LightningBolt`, `MobileDependencyResolver`, `TextMesh Pro`, `Unity.VisualScripting.Generated` | third-party assets nothing referenced |
| `Assets/Resources` (legacy) | `BillingMode.json`, `UnityPlayerAccountSettings.asset`; recreated when the real services are set up |
| Stray root assets | `New Terrain.asset`, `PrestigeData.cs`, `IntroScreenAnimation.cs` |
| Packages | `com.unity.ads`, `ai.navigation`, `collab-proxy`, `multiplayer.center`, `postprocessing`, `visualscripting`, plus unused built-in modules (ai, cloth, terrain, tilemap, vehicles, video, vr, xr, wind, umbra, unityanalytics) |
| Editor menu | **Frontier > Build Icons** (it rebuilt PNGs from the removed sprite folders) |

## What was kept and moved

| From | To |
|------|----|
| 14 source FBX the `ModelLibrary` uses | `Assets/Frontier/Art/Models/Source` (moved with `AssetDatabase.MoveAsset`, GUIDs and calibrated import scales intact) |
| 8 model prefabs | `Assets/Frontier/Art/Models/Prefabs`, stripped of legacy scripts, colliders, particle systems and line renderers |
| 9 simple materials the prefabs reference | `Assets/Frontier/Art/Materials` |
| `Assets/Sounds` | `Assets/Frontier/Audio/Legacy`, kept only until proper audio replaces it |

## Verification

Planet, storage, combat and layout checks pass; the project compiles with no warnings from our code; screenshots of 41
screens captured before and after the purge differ by less than 0.4 (mean RGB, 0-255) on every static screen. The only larger
differences are mini-games whose rocks and spots are randomised per run.
