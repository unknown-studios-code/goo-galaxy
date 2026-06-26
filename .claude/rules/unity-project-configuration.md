---
paths:
  - "Assets/Scripts/**/*.cs"
---

# Unity Project Configuration

> **Cross-references:** Code style → [unity-code-style.md](unity-code-style.md). Editor settings for faster iteration and consistent imports. **Target: Unity 6.3, URP 17.3.**

## Enter Play Mode Options

**Edit → Project Settings → Editor**

| Setting                 | Value       | Why                                  |
| ----------------------- | ----------- | ------------------------------------ |
| Enter Play Mode Options | ✅ Enabled  | Customize reload behavior            |
| Reload Domain           | ❌ Disabled | Skips C# domain reload (~2–5s saved) |
| Reload Scene            | ❌ Disabled | Keeps scene state, faster iteration  |

### Domain Reload Disabled: Required Patterns

Static fields **persist between play sessions**:

```csharp
// ❌ Won't reset on re-play
private static int _playerCount = 0;

// ✅ Reset explicitly
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics() => _playerCount = 0;
```

**Also affected:** static events (subscribers accumulate), singletons, static collections/caches.

---

## Asset Pipeline

**Unity → Settings (macOS) / Edit → Preferences (Windows)**

| Setting      | Value       |
| ------------ | ----------- |
| Auto Refresh | ❌ Disabled |

Manual refresh: `Cmd+R` (macOS) / `Ctrl+R` (Windows).

---

## Burst Compiler

**Edit → Project Settings → Burst AOT Settings**

| Setting                  | Value                            |
| ------------------------ | -------------------------------- |
| Enable Burst Compilation | ✅                               |
| Synchronous Compilation  | ✅ (avoids first-frame stutters) |

---

## Presets

Location: `Assets/Settings/Presets/`

### Texture Import

| Preset                        | Use Case                         |
| ----------------------------- | -------------------------------- |
| `AlbedoTextureImporter`       | Diffuse/color (sRGB, compressed) |
| `NormalTextureImporter`       | Normal maps (linear)             |
| `SingleSpriteTextureImporter` | Individual UI sprites            |
| `SpriteAtlasTextureImporter`  | Sprite atlas textures            |

### Audio Import (`Assets/Settings/Presets/Audio/`)

| Preset                  | Use Case                                   |
| ----------------------- | ------------------------------------------ |
| `MusicAudioImporter`    | Background music (streaming)               |
| `AmbienceAudioImporter` | Environmental loops (compressed in memory) |
| `SFXAudioImporter`      | Sound effects (decompress on load)         |
| `UIAudioImporter`       | UI feedback (small, decompress on load)    |

### Applying Presets

- **Auto:** Edit → Project Settings → Preset Manager → add filter + assign preset.
- **Manual:** Select asset → Inspector → preset icon → choose from dropdown.

### Creating Presets

Configure asset import settings → preset icon → Save Current To... → `Assets/Settings/Presets/`.

---

## Rendering Configuration

Location: `Assets/Settings/Rendering/`

### URP Quality Tiers

| Asset              | Target              |
| ------------------ | ------------------- |
| `URP-Performant`   | Mobile, low-end     |
| `URP-Balanced`     | Mid-range (default) |
| `URP-HighFidelity` | Desktop, high-end   |

Each tier has a matching Renderer asset.

### Volume Profiles

| Asset                  | Purpose                         |
| ---------------------- | ------------------------------- |
| `DefaultVolumeProfile` | Global post-processing defaults |
| `SampleSceneProfile`   | Scene-specific overrides        |

---

## Assembly Definitions

Currently using default assembly (all scripts in one). For larger projects, add `.asmdef` to:

- Reduce recompilation scope
- Enforce code boundaries
- Speed iteration

---

## Quick Reference

| Setting         | Path                                         |
| --------------- | -------------------------------------------- |
| Enter Play Mode | Edit → Project Settings → Editor             |
| Auto Refresh    | Unity → Settings (Preferences)               |
| Burst           | Edit → Project Settings → Burst AOT Settings |
| Quality Levels  | Edit → Project Settings → Quality            |
| Preset Manager  | Edit → Project Settings → Preset Manager     |
| URP Assets      | Assets/Settings/Rendering/                   |
| Presets         | Assets/Settings/Presets/                     |
