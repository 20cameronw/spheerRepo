# Console labs

Two small console programs that run the **same sources** as the game (no Unity editor window needed, no Unity license used). They compile the repo sources in place with the Roslyn compiler and the .NET 8 runtime that ship inside the Unity editor, so they work on any machine that has the Unity version from `ProjectSettings/ProjectVersion.txt` installed (set `UNITY_PATH` to override the editor location).

| Lab | Sources it compiles | What it is for |
|-----|---------------------|----------------|
| `tools/labs/universe` | `Scripts/Core/**` (the deterministic universe generator) | tests, density maps, galaxy gallery, generator statistics |
| `tools/labs/economy`  | `Scripts/Core/**` + `Scripts/Economy/**` | tests, the balance simulator, JSON exports, reports, charts |

Everything the labs build or render goes to `tools/labs/out/` (git-ignored). The committed, reviewable economy snapshot lives in `docs/economy/`.

## Commands

Run from the repository root. Every command rebuilds first when a source file changed.

```powershell
# universe
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\run.ps1 test [--filter Determinism]
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\run.ps1 render [dir]        # 4 PNGs: 30,000 ly density, home galaxy, 2,000 ly systems, tier rings
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\run.ps1 gallery [file]
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\run.ps1 density cx cy size px file
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\run.ps1 inspect [region|biomes|planned]
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\universe\crosscheck.ps1                # digests on CoreCLR vs Unity's Mono

# economy
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 test [--filter Travel]
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 sim all days=730 [stats=1] [goals=1] [key=value ...]
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 tables [all|res|rec|str|tool|upg|rsr|slots]
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 shards regular
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 export  docs\economy\data
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 report  docs\economy
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 codex   docs\economy
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\run.ps1 charts  docs\economy
powershell -NoProfile -ExecutionPolicy Bypass -File tools\labs\economy\crosscheck.ps1                 # digests on CoreCLR vs Unity's Mono
```

`key=value` arguments to `sim` override tuning keys (see `docs/economy/TUNING_KEYS.md`), so a balance question is a one-line experiment: `sim regular days=365 cost.era.3=1.2`.

## Rules of thumb

* The labs and the game use the identical sources. If a lab test passes but the Unity or CI test fails (or the other way round) that is a bug in the platform assumptions, not in the lab: report it.
* Anything in `Scripts/Core` and `Scripts/Economy` must stay engine-free, deterministic and free of platform math (`Math.Pow/Exp/Log/Sin/Cos`), clocks and static mutable state. `SourceHygieneTests` enforces it.
* After changing the catalog or the default tuning, regenerate `docs/economy` (export, report, codex, charts). `SnapshotTests` fails until the committed manifest matches.
