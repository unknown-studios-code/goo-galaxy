---
description: "Use when configuring Unity project settings or writing code affected by domain reload. Covers Enter Play Mode, static field resets, Burst, asset presets, URP tiers, and asmdefs."
applyTo: "Assets/Scripts/**/*.cs"
---

# Unity Project Configuration

## 1. Overview

This document defines standard project settings, editor preferences, and compilation options. Adhering to these guidelines ensures consistent asset imports, fast editor iteration, and optimal compile-time optimizations.

## 2. Cross-References

- **Code Style** → [unity-code-style.instructions.md](unity-code-style.instructions.md) (Standard practices for classes and ScriptableObjects)
- **Debugging** → [unity-debugging.instructions.md](unity-debugging.instructions.md) (Checking runtime initialization issues)

## 3. Core Rules

- **Rule 1 (Enter Play Mode Options):** Enable "Enter Play Mode Options" and disable both "Reload Domain" and "Reload Scene" in Project Settings (Editor) to skip domain/scene reloads and speed up iteration.
- **Rule 2 (Subsystem Static Field Reset):** Reset all static variables, static collections, and static event subscribers explicitly. Because domain reloading is disabled, static fields persist between play sessions in the Editor. Decorate the reset method with `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`.
- **Rule 3 (Asset Pipeline refresh):** Disable "Auto Refresh" in Unity Preferences to prevent unwanted background compilations during editing. Trigger asset refreshes manually when switching back to the editor.
- **Rule 4 (Burst Compiler Optimization):** Enable Burst compilation and select Synchronous Compilation in Project Settings (Burst AOT Settings) to avoid first-frame CPU stutters during runtime execution.
- **Rule 5 (Import Settings Presets):** Store custom import presets for textures (Albedo, Normal, SingleSprite, SpriteAtlas) and audio assets (Music, Ambience, SFX, UI) under `Assets/Settings/Presets/`. Configure the Preset Manager to automatically apply these presets on import.
- **Rule 6 (URP Quality Tiers):** Maintain rendering quality assets (Performant, Balanced, HighFidelity) under `Assets/Settings/Rendering/`. Route rendering volumes and post-processing profiles accordingly.
- **Rule 7 (Assembly Compilation Boundaries):** Organize large logical subsystems using Assembly Definition files (`.asmdef`) to reduce recompilation scopes and enforce architectural boundaries.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```csharp
public class <BadConfig> : MonoBehaviour
{
    // ❌ Static variables are not reset; value carries over between play button presses
    private static int _score = 0;
    private static List<Transform> _cachedTransforms = new();
}
```

### ✅ Do (Good)

```csharp
public class <GoodConfig> : MonoBehaviour
{
    private static int _score = 0;
    private static readonly List<Transform> _cachedTransforms = new();

    // ✅ Explicitly reset all statics on play mode startup
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _score = 0;
        _cachedTransforms.Clear();
    }
}
```

## 5. Quick Reference & Decision Matrix

| Configuration Category | Path / Location                          | Target Value / Practice                                                      |
| :--------------------- | :--------------------------------------- | :--------------------------------------------------------------------------- |
| Iteration Settings     | **Edit → Project Settings → Editor**     | Enter Play Mode = Enabled; Reload Domain = Disabled; Reload Scene = Disabled |
| Preferences            | **Edit → Preferences → Asset Pipeline**  | Auto Refresh = Disabled                                                      |
| Math Optimization      | **Edit → Project Settings → Burst AOT**  | Enable Burst = Enabled; Synchronous Comp. = Enabled                          |
| Asset Presets          | `Assets/Settings/Presets/`               | Automate imports via Preset Manager filters                                  |
| Rendering Quality      | `Assets/Settings/Rendering/`             | Assign Performant (low), Balanced (mid), HighFidelity (high)                 |
| Code Compilation       | Subfolder roots (e.g. `Runtime/Shared/`) | Define assembly boundaries via `.asmdef` assets                              |
