# Project agent notes (BetterRimworlds.ChickenBatteryCage)

Prefer these over generic RimWorld mod tutorials. CryoRegenesis is the reference
implementation this skeleton came from.

Do **not** invent a different layout. Agents routinely break bootstrapping by:

- Putting the `.csproj` at the repo root instead of `Source/`
- Using `netcoreapp` / `net6.0` / `net8.0` instead of `net48`
- Dropping the `Release v1.6` configuration (this mod is RimWorld 1.6-only)
- Calling `harmony.PatchAll(Assembly)` (one failed patch kills every other patch)
- Writing assemblies into `Assemblies/` at the mod root instead of `1.6/Assemblies`, …
- Forgetting Harmony as a workshop dependency when the C# project references 0Harmony
- Committing `obj/`, `bin/`, `*.dll`, or `*.zip`

## Layout (do not flatten)

```
ChickenBatteryCage/
  Source/                  C# project and .sln (not the repo root)
  ChickenBatteryCage/             RimWorld mod pack (About, Defs, Languages, …)
    About/About.xml
    Defs/
    Languages/English/Keyed/
    Patches/
    Textures/
  build.sh                 builds RimWorld 1.6, then syncs into /rimworld/1.6*/Mods
  release.sh               zips the 1.6 Mods copy
```

`build.sh` uses `basename $PWD` as the mod name. Run it from the **mod repo root**,
not from `Source/`.

Assemblies are compiled into `/rimworld/1.6/Mods/<identity>/<version>/Assemblies`,
then `build.sh` copies that whole mod folder to the `1.6-steam` install. XML and
textures live unversioned under the inner pack folder in git.

## C# conventions

- Namespace: `BetterRimworlds.ChickenBatteryCage`
- Mod class: `ChickenBatteryCage : Mod` in `Source/ChickenBatteryCage.cs`
- Settings: `Source/Settings.cs`, `GetSettings<Settings>()` in the Mod ctor
- Harmony id: `BetterRimworlds.ChickenBatteryCage`
- packageId: `HopeSeekr.BetterRimworlds.ChickenBatteryCage`
- Version symbol: `RIMWORLD16` (see the csproj)
- Apply Harmony patches **per type** with `CreateClassProcessor(type).Patch()` in its
  own try/catch. Never one `PatchAll` for the assembly.

## Language

Never use “latch” as a verb (or latches / latched / latching). Prefer records,
remembers, keeps, sets once, marks, stores, remains set, one-time flag.

## Commits and changelog

- Match this repo’s subject style: short, past tense, optional `[m]` tag for minor edits.
- `CHANGELOG.md` is updated by an external tool. Do not edit, stage, or commit it
  unless the maintainer asks.

## Git signing

This environment signs commits with GPG. If signing fails for lack of a TTY, leave
changes staged and ask the maintainer to unlock gpg-agent.
