---
description: "Use when writing Unity UI Toolkit code, UXML, or USS. Covers USS/CSS differences, BEM naming, flexbox, data binding, MVP, custom elements, and ListView virtualization."
paths:
  - "Assets/**/*.cs"
  - "Assets/**/*.uxml"
  - "Assets/**/*.uss"
---

# Unity UI Toolkit Reference

## 1. Overview

This document defines coding, styling, and architectural rules for building UI with UI Toolkit. UI Toolkit is the only UI system in this project — uGUI (`Canvas`, `RectTransform`, `Image`, `Text`) is never used. The target is a portrait mobile game whose UI shares a tight frame budget with the match simulation.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Standard event subscription syntax and general C# naming)
- **Design Patterns** → [unity-design-patterns.md](unity-design-patterns.md) (Template Method, dependency injection, and MVP architecture)
- **Performance Optimization** → [unity-performance-optimization.md](unity-performance-optimization.md) (Caching element references and preventing update-loop allocations)

## 3. Core Rules

- **Rule 1 (File Organization & Naming):** Keep UXML in `Assets/UI/UXML/` and USS in `Assets/UI/USS/`, with matching PascalCase names (`MainMenuView.uxml` / `MainMenuView.uss`). Reference stylesheets from UXML with `<Style src="project://database/..."/>`; USS has no `@import`. Format `.uxml` and `.uss` with 2-space indentation and a 120-character line limit, per `.editorconfig`.
- **Rule 2 (USS Is a CSS Subset):** Flexbox only — no `display: grid`. Lengths in `px` and `%` only — no `em`, `rem`, `vh`, `vw`, or `calc()`. No `z-index`: stacking follows the UXML declaration order. No `@media` queries. Colors use `rgb()`/`rgba()` or a `var()` token — hexadecimal is not parsed. `box-sizing: border-box` is the default and cannot be changed. Custom properties are declared in `:root {}` only. Hit testing is **not** styleable: `picking-mode` is a UXML attribute and a C# property (`VisualElement.pickingMode`), never a USS declaration — writing it in USS logs one "Unknown property" warning at asset import and is then dropped, so the element silently keeps `PickingMode.Position`. An unknown USS property never fails a build and never re-warns at play time, which is what makes this class of mistake survive review.
- **Rule 3 (Unity-Specific Properties):** Text and background styling use Unity's namespaced properties, not their CSS counterparts: `-unity-font-definition`, `-unity-font-style`, `-unity-text-align` (`upper|middle|lower` + `left|center|right`), `-unity-text-outline-width/-color`, `-unity-background-scale-mode`, `-unity-background-image-tint-color`, and `-unity-slice-left/right/top/bottom` with `-unity-slice-scale` for 9-slice sprites. Asset URLs use `url('project://database/Assets/...')` for project assets or `resource('...')` for `Resources`; relative `url('../Icons/icon.png')` resolves from the USS file.
- **Rule 4 (Selectors & Pseudo-Classes):** USS supports `:hover`, `:active`, `:focus`, `:checked`, `:disabled`, `:enabled`, and `:root`. It does not support `:nth-child()`, `:not()`, `:first-child`, `:last-child`, `:is()`, or `:where()`. Keep selectors flat and specific (`.block__element`) — deep descendant chains are brittle and slower to match.
- **Rule 5 (BEM & Element Identity):** Name USS classes `block-name__element-name--modifier-name` and UXML `name` attributes in kebab-case. Use `name` for elements queried from C# and keep it unique inside its block; use `class` for styling and reuse. Express runtime state as additive modifier classes (`.is-selected`, `.is-disabled`) toggled with `AddToClassList`/`RemoveFromClassList`/`EnableInClassList`, never by writing inline styles. Centralize selector strings as `const` in a static class so a rename is a compile error rather than a silent no-op.
- **Rule 6 (Flexbox Layout):** Lay out with flex containers: `flex-direction`, `justify-content`, `align-items`, `flex-grow`/`flex-shrink`/`flex-basis`, and `position: absolute` only to leave the flow deliberately. `gap` is not supported — space children with `margin` on the children. Use `display: none` to remove an element from layout, and `visibility: hidden` when the space must be preserved; do not toggle `display` every frame.
- **Rule 7 (Element Querying & Caching):** Query with `Q<T>("element-name")` once, when the panel is available, and cache the references in fields. Never call `Q`/`Query` inside `Update`, a binding callback, or a `ListView` bind callback. Guard against a null result and fail loudly — a null element means the UXML name changed. When code asks "is the pointer over the UI?" with `panel.Pick(...)`, remember that a UXML root is a full-screen flex host with the default `PickingMode.Position`: it answers "yes" for every point on screen and blocks all world input behind the panel. Mark every purely structural container `picking-mode="Ignore"` in UXML so picks reach the real widgets or nothing, and convert the screen point with `RuntimePanelUtils.ScreenToPanel` — panel space is top-left origin, screen space is bottom-left, and picking with the raw point mirrors the hit test vertically.
- **Rule 8 (Event Subscription):** Register UI callbacks with `RegisterCallback<T>` when the view initializes and unregister them symmetrically when it tears down; the pairing is the same discipline as `OnEnable`/`OnDisable`. Prefer named handler methods so unregistration is possible.
- **Rule 9 (Custom Visual Elements):** Declare custom elements with `[UxmlElement]` on a `partial` class deriving from `VisualElement`, and expose inspector-facing fields with `[UxmlAttribute]`. The `UxmlFactory`/`UxmlTraits` pair is removed in Unity 6 and must not be used.
- **Rule 10 (MVP & Data Binding):** Separate Model (engine-free data), View (elements, styling, and raising user intent), and Presenter (decisions and state). Bind runtime data by assigning `dataSource` and marking bound members with `[CreateProperty]`; implement `INotifyBindablePropertyChanged` on the source and raise it in setters, otherwise the UI updates only on the initial assignment. `SerializedObject.Bind()` is editor-only and never appears in runtime code.
- **Rule 11 (ListView & Virtualization):** Use `ListView` (or `TreeView`/`MultiColumnListView`) for any collection that can exceed a screenful — a `ScrollView` full of children instantiates and lays out every item. Set `fixedItemHeight` (or `virtualizationMethod = DynamicHeight` when rows genuinely vary), provide `makeItem` to build a reusable row and `bindItem` to fill it, and assign `itemsSource`. Both callbacks run during scrolling: allocate nothing, query nothing, and reset every field the row can carry over from its previous item. Call `RefreshItems()` after mutating the source collection; rebuild the whole view only when the source instance itself changes.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```xml
<!-- ❌ CamelCase name, non-BEM classes, and inline styles -->
<ui:VisualElement name="navBar" style="padding: 10px; background-color: #FF0000;">
    <ui:Button name="ShopBtn" class="primaryButton" text="Shop" />
</ui:VisualElement>
```

```css
/* ❌ Hexadecimal color, unsupported properties and selector */
.primaryButton {
  background-color: #ff6432;
  z-index: 10;
  gap: 8px;
  width: calc(100% - 20px);
}

/* ❌ picking-mode is not a USS property. This warns once at import, is dropped, and the element keeps
   PickingMode.Position — a full-screen host styled this way silently swallows every world-space click. */
.hud-root {
  picking-mode: ignore;
}

.navbar-menu :nth-child(2) {
  color: rgb(255, 255, 255);
}
```

### ✅ Do (Good)

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
  <Style src="project://database/Assets/UI/USS/MainStyles.uss" />
  <!-- ✅ Structural hosts opt out of picking here, not in USS, so world input reaches the board behind them -->
  <ui:VisualElement name="hud-root" class="hud" picking-mode="Ignore">
    <ui:VisualElement name="navbar-menu" class="navbar-menu">
      <ui:Button name="navbar-menu__shop-button" class="navbar-menu__shop-button button button--primary" text="Shop" />
    </ui:VisualElement>
  </ui:VisualElement>
</ui:UXML>
```

```css
:root {
  --color-primary: rgb(40, 120, 240);
  --color-text: rgb(255, 255, 255);
  --spacing-s: 8px;
}

.navbar-menu {
  flex-direction: row;
  padding: var(--spacing-s);
}

/* ✅ gap is unsupported — space children with margins */
.navbar-menu > * {
  margin-right: var(--spacing-s);
}

.button {
  height: 32px;
  -unity-text-align: middle-center;
}

.button--primary {
  background-color: var(--color-primary);
  color: var(--color-text);
}

.button:disabled {
  opacity: 0.4;
}
```

### 🚫 Don't (Bad)

```csharp
// ❌ Removed Unity 6 API and editor-only binding in runtime code
public class <BadCustomElement> : VisualElement
{
    public new class UxmlFactory : UxmlFactory<<BadCustomElement>, UxmlTraits> { }
    public new class UxmlTraits : VisualElement.UxmlTraits { }
}

public class <BadView> : MonoBehaviour
{
    private void Update()
    {
        // ❌ Query inside an update loop, repeated every frame
        Label label = GetComponent<UIDocument>().rootVisualElement.Q<Label>("title-label");
        label.text = $"Score: {_score}"; // ❌ Allocation per frame
    }
}
```

### ✅ Do (Good)

```csharp
// ✅ Unity 6 custom element
[UxmlElement]
public partial class <CustomElement> : VisualElement
{
    [UxmlAttribute]
    public float MaxValue { get; set; } = 100f;

    public <CustomElement>()
    {
        var label = new Label();
        label.AddToClassList(<Selectors>.CustomElementLabel);
        Add(label);
    }
}

// ✅ Reactive model: bindings update because the source notifies
public class <ModelData> : ScriptableObject, INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    [SerializeField]
    private int _health = 100;

    [CreateProperty]
    public int Health
    {
        get => _health;
        set
        {
            if (_health == value)
            {
                return;
            }

            _health = value;
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Health)));
        }
    }
}

// ✅ View: cache once, register and unregister symmetrically, virtualize the list
[RequireComponent(typeof(UIDocument))]
public class <ViewComponent> : MonoBehaviour
{
    [SerializeField]
    private <ModelData> _model;

    private VisualElement _root;
    private Button _submitButton;
    private ListView _cardList;

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        _root.dataSource = _model;

        _submitButton = _root.Q<Button>(<Selectors>.SubmitButton);
        _cardList = _root.Q<ListView>(<Selectors>.CardList);

        _cardList.fixedItemHeight = 48f;
        _cardList.makeItem = () => new <CardRowElement>();
        _cardList.bindItem = BindCardRow;

        _submitButton.RegisterCallback<ClickEvent>(HandleSubmitClicked);
    }

    private void OnDisable()
    {
        _submitButton.UnregisterCallback<ClickEvent>(HandleSubmitClicked);
        _cardList.bindItem = null;
        _cardList.makeItem = null;
    }

    private void BindCardRow(VisualElement element, int index)
    {
        // ✅ Recycled row: no allocation, no query, every field reset
        var row = (<CardRowElement>)element;
        row.SetCard(_model.Cards[index]);
    }

    private void HandleSubmitClicked(ClickEvent evt)
    {
        // ✅ Views raise intent; the presenter decides
    }
}
```

## 5. Quick Reference & Decision Matrix

| Feature        | CSS Standard                | USS Subset / Unity Extension                         |
| :------------- | :-------------------------- | :--------------------------------------------------- |
| Layout System  | Flexbox + Grid              | Flexbox (Yoga) only                                  |
| Size Units     | px, %, em, rem, vh, vw      | px and % only                                        |
| Math           | `calc(100% - 20px)`         | Not supported — use `flex-grow` or fixed values      |
| Layering       | `z-index: 10`               | Not supported — UXML declaration order decides       |
| Color Formats  | Hex, `rgb()`, `rgba()`      | `rgb()`/`rgba()` or `var()` only; hex is not parsed  |
| Variables      | Any selector                | `:root` only                                         |
| Media Queries  | `@media`                    | Not supported — separate USS files, swap at runtime  |
| Stylesheet ref | `@import`                   | `<Style src="project://database/..."/>` in UXML      |
| Layout Gaps    | `gap: 10px`                 | Not supported — margins on children                  |
| Pseudo-classes | `:nth-child`, `:not`, `:is` | `:hover :active :focus :checked :disabled :enabled`  |
| Text align     | `text-align: center`        | `-unity-text-align: middle-center`                   |
| Transform      | `transform: scale(1.1)`     | Individual `scale`, `rotate`, `translate` properties |
| Box model      | Configurable `box-sizing`   | Always `border-box`                                  |
| Hit testing    | `pointer-events: none`      | Not a USS property — UXML `picking-mode` or C#       |

| Problem Symptom         | Primary Cause                       | Troubleshooting Checklist                                                                                  |
| :---------------------- | :---------------------------------- | :--------------------------------------------------------------------------------------------------------- |
| UI elements are missing | display/visibility or parent bounds | Ensure `display: flex`, `visibility: visible`, and a non-zero container size in the UI Toolkit Debugger    |
| Clicks do not fire      | Blocking overlay or picking mode    | Set picking mode in **UXML** (`picking-mode="Ignore"`) or C# — never USS; check the full-screen root first |
| Query returns null      | Wrong name or query ran too early   | Verify the `name` in UXML (case-sensitive) and query after the panel exists                                |
| Bindings never update   | Source does not notify              | `[CreateProperty]` on the member and `INotifyBindablePropertyChanged` raised in the setter                 |
| List scrolling stutters | Work inside `bindItem`              | Remove allocations and queries from `bindItem`; set `fixedItemHeight`; reuse the row built by `makeItem`   |
| Styles do not apply     | Broken stylesheet reference         | Check the `<Style src="..."/>` path and case-sensitive class names                                         |
