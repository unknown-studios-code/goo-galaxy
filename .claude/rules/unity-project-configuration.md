---
description: "Use when configuring Unity project settings or writing code affected by domain reload. Covers Enter Play Mode, static field resets, Burst, asset presets, URP tiers, build profiles, and asmdefs."
paths:
  - "Assets/**/*.cs"
  - "Assets/**/*.asmdef"
---

# Unity Project Configuration

## 1. Overview

This document defines standard project settings, editor preferences, and compilation options. Adhering to these guidelines ensures consistent asset imports, fast editor iteration, and predictable mobile builds. Settings live in `ProjectSettings/*.asset` and Build Profile assets, which Unity authors — describe the change as menu path plus field plus value instead of editing those files.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Standard practices for classes and ScriptableObjects)
- **Debugging** → [unity-debugging.md](unity-debugging.md) (Diagnosing static state that survives a disabled domain reload)
- **Performance Optimization** → [unity-performance-optimization.md](unity-performance-optimization.md) (Runtime cost of the settings chosen here)

## 3. Core Rules

- **Rule 1 (Enter Play Mode Options):** Enable "Enter Play Mode Options" and disable both "Reload Domain" and "Reload Scene" (Project Settings → Editor) to skip domain and scene reloads and cut seconds off every iteration.
- **Rule 2 (Static State Reset):** Because domain reload is disabled, static fields, static events, singletons, and static caches persist between play sessions. Reset every one of them explicitly from a method decorated with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`, and place that reset next to the state it clears (as `MatchEvents` does for its event fields).
- **Rule 3 (Asset Pipeline Refresh):** Disable "Auto Refresh" (Edit → Preferences → Asset Pipeline) so imports and recompiles happen when you choose. Refresh manually with `Ctrl+R` after editing scripts outside the editor.
- **Rule 4 (Burst Compiler):** Enable Burst compilation and Synchronous Compilation (Project Settings → Burst AOT Settings) so Burst-compiled jobs are ready before the first frame that needs them instead of stuttering on first use.
- **Rule 5 (Import Presets):** Store import presets under `Assets/Settings/Presets/` — textures (Albedo, Normal, SingleSprite, SpriteAtlas) and audio (Music, Ambience, SFX, UI) — and register them in the Preset Manager with a folder or name filter so imports are configured automatically. Create a new preset by configuring one asset, then Preset icon → "Save current to…". Never rely on a manually applied preset for content that ships.
- **Rule 6 (URP Quality Tiers):** Keep the render pipeline assets and their matching renderers under `Assets/Settings/Rendering/`, one per tier (Performant / Balanced / HighFidelity), with volume profiles alongside them. Each tier must be a shippable configuration on its target hardware, not a copy with one toggle changed. Switch tiers in code through `QualitySettings.SetQualityLevel(index)` (indices follow Project Settings → Quality order) and verify the effect the change depends on is enabled in the tier's URP asset — `_CameraOpaqueTexture` and `_CameraDepthTexture` in particular are per-asset opt-ins.
- **Rule 7 (Assembly Definitions):** Every feature folder under `Assets/Scripts/Runtime/{Feature}/` owns one `.asmdef` named `GooGalaxy.Runtime.{Feature}`; editor code under `Assets/Editor/{Domain}/` owns `GooGalaxy.Editor.{Domain}`. Dependencies point one way: features may reference `Runtime.Shared`, `Runtime.Shared` references nothing, and editor assemblies may reference runtime assemblies but never the reverse. Keep `Auto Referenced` off for runtime assemblies, expose internals to tests through `InternalsVisibleTo` in `AssemblyInfo.cs` rather than widening access modifiers, and add a platform or define constraint instead of guarding a whole assembly with `#if`.
- **Rule 8 (Build Profiles & Platform Modules):** Build configuration lives in Build Profile assets under `Assets/Settings/Build Profiles/` — target platform, development flag, scripting backend, compression, and scene list. Keep one profile per shipping target plus a development profile; do not encode build settings in scripts. Install only the platform modules the project targets (Android and iOS here) to keep editor overhead and import times down.
- **Rule 9 (Mobile Scripting Backend):** Both mobile targets build with IL2CPP. Set the Android target architectures to ARM64 (plus ARMv7 only when an actual device demands it), keep the managed stripping level at the highest setting the game still runs on, and preserve reflection-only types with a `link.xml` when stripping removes something — stripping bugs surface as `MissingMethodException` at runtime in a build, never in the editor.
- **Rule 10 (Editor-Owned Assets):** `.asset`, `.meta`, `.prefab`, `.unity`, Build Profiles, and everything under `ProjectSettings/` are authored by Unity. Provide step-by-step editor instructions (menu path, field, value) rather than writing those files.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadConfig> : MonoBehaviour
{
    // ❌ Static state that silently carries over between play sessions
    private static int _score = 0;
    private static List<Transform> _cachedTransforms = new();

    // ❌ Static event whose subscribers accumulate on every play
    public static event Action<int> ScoreChanged;
}
```

### ✅ Do (Good)

```csharp
public class <GoodConfig> : MonoBehaviour
{
    private static readonly List<Transform> _cachedTransforms = new();

    private static int _score;

    public static event Action<int> ScoreChanged;

    // ✅ Every static reset in one place, next to the state it owns
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _score = 0;
        _cachedTransforms.Clear();
        ScoreChanged = null;
    }
}
```

## 5. Quick Reference & Decision Matrix

| Configuration Category | Path / Location                         | Target Value / Practice                                                      |
| :--------------------- | :-------------------------------------- | :--------------------------------------------------------------------------- |
| Iteration Settings     | **Edit → Project Settings → Editor**    | Enter Play Mode = Enabled; Reload Domain = Disabled; Reload Scene = Disabled |
| Preferences            | **Edit → Preferences → Asset Pipeline** | Auto Refresh = Disabled (`Ctrl+R` to refresh)                                |
| Math Optimization      | **Edit → Project Settings → Burst AOT** | Enable Burst = Enabled; Synchronous Compilation = Enabled                    |
| Asset Presets          | `Assets/Settings/Presets/`              | Registered in Preset Manager with folder/name filters                        |
| Rendering Quality      | `Assets/Settings/Rendering/`            | Performant / Balanced / HighFidelity + matching renderers and volumes        |
| Build Configuration    | `Assets/Settings/Build Profiles/`       | One profile per shipping target; IL2CPP, ARM64 on Android                    |
| Code Compilation       | `Assets/Scripts/Runtime/{Feature}/`     | One `.asmdef` per feature; `Shared` is the dependency-free leaf              |
| Test Access            | `{Feature}/AssemblyInfo.cs`             | `InternalsVisibleTo` instead of widening access modifiers                    |
