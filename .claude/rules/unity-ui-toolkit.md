---
paths:
  - "Assets/Scripts/**/*.cs"
  - "Assets/UI/**/*.uxml"
  - "Assets/UI/**/*.uss"
---

# Unity UI Toolkit Reference

## 1. Overview

This document defines coding, styling, and architectural rules for designing, binding, and querying UI components using Unity's UI Toolkit. It ensures layout compliance, CSS-subset constraints, BEM conventions, and reactive MVP data binding.

## 2. Cross-References

- **Code Style** → [unity-code-style.md](unity-code-style.md) (Standard event subscription syntax and general C# naming)
- **Design Patterns** → [unity-design-patterns.md](unity-design-patterns.md) (Template Method pattern, singletons, and MVP architecture details)
- **Performance Optimization** → [unity-performance-optimization.md](unity-performance-optimization.md) (Caching visual element references and preventing update-loop allocations)

## 3. Core Rules

- **Rule 1 (File Organization & Naming):** Organize UXML/USS files in `Assets/UI/UXML/` and `Assets/UI/USS/` directories. Use matching PascalCase names (e.g. `MainMenuView.uxml` and `MainMenuView.uss`).
- **Rule 2 (USS CSS-Subset Restraints):** Adhere strictly to Unity USS limits: Flexbox layout only (no CSS grid), pixel or percentage units only (no em, rem, vh, vw, or `calc()`), and no `z-index`. Expose design tokens/variables in the `:root {}` selector only. Use `rgb()` or `rgba()` notation; hexadecimal colors are prohibited.
- **Rule 3 (BEM Class Convention):** Apply BEM formatting (`block-name__element-name--modifier-name`) for all USS classes. Use kebab-case names for UXML name attributes. Modify runtime states via additive classes (e.g., `.is-selected`, `.is-disabled`) toggled from C#.
- **Rule 4 (Flexbox Yoga Layout):** Position visual elements using Flexbox containers. Because `gap` properties are not supported in USS, space children manually using margins (e.g., `margin-right`).
- **Rule 5 (Element Querying & Caching):** Query VisualElements during `OnEnable()` and cache references. Never query elements (`Q` or `Query`) inside `Update()` or performance-critical loops.
- **Rule 6 (UI Event Subscription):** Subscribe to interactive UI events in `OnEnable()` and unsubscribe in `OnDisable()`. Use the centralized `EventRegistry` utility to manage and dispose of callbacks automatically.
- **Rule 7 (Custom Visual Elements):** Define custom elements using the `[UxmlElement]` and `[UxmlAttribute]` attributes on partial classes inheriting from `VisualElement`. Do not use deprecated `UxmlFactory` or `UxmlTraits` definitions.
- **Rule 8 (MVP Binding Pattern):** Separate UI concerns: Model contains pure data, View manages visual elements and sets data sources, and Presenter controls logic. Implement reactive bindings using `INotifyBindablePropertyChanged` and `[CreateProperty]` properties. Use `binding-path` and `dataSource` assignments. Prohibit Editor-only `SerializedObject.Bind()` in runtime code.

## 4. Code & Configuration Examples

### 🚫 Don't (Bad)

```xml
<!-- ❌ CamelCase name, non-BEM classes, and inline styles -->
<ui:VisualElement name="navBar" style="padding: 10px; background-color: #FF0000;">
    <ui:Button name="ShopBtn" class="primaryButton" text="Shop" />
</ui:VisualElement>
```

```css
/* ❌ Hexadecimal colors and unsupported selectors/properties */
.primaryButton {
  background-color: #ff6432;
  z-index: 10;
  gap: 8px;
}
```

### ✅ Do (Good)

```xml
<!-- UXML Markup (UxmlTemplate.uxml) -->
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <Style src="project://database/Assets/UI/Styles/MainStyles.uss" />
    <ui:VisualElement name="navbar-menu" class="navbar-menu">
        <ui:Button name="navbar-menu__shop-button" class="navbar-menu__shop-button button button--primary" text="Shop" />
    </ui:VisualElement>
</ui:UXML>
```

```css
/* USS Styling (UxmlTemplate.uss) */
:root {
  --color-primary: rgb(40, 120, 240);
  --color-text: rgb(255, 255, 255);
}

.navbar-menu {
  padding: 8px;
  flex-direction: row;
}

.navbar-menu > * {
  margin-right: 8px; /* Use margins to substitute for unsupported gap */
}

.navbar-menu__shop-button {
  min-width: 120px;
}

.button {
  height: 32px;
}

.button--primary {
  background-color: var(--color-primary);
  color: var(--color-text);
}
```

### 🚫 Don't (Bad)

```csharp
// ❌ Deprecated UxmlFactory traits instantiation and Editor-only binding APIs
public class <BadCustomElement> : VisualElement
{
    public new class UxmlFactory : UxmlFactory<<BadCustomElement>, UxmlTraits> { }
    public new class UxmlTraits : VisualElement.UxmlTraits { }
}

public class <BadView> : MonoBehaviour
{
    private void Update()
    {
        // ❌ VisualElement queries inside Update loop
        var label = GetComponent<UIDocument>().rootVisualElement.Q<Label>("title-lbl");
    }
}
```

### ✅ Do (Good)

```csharp
// Custom Visual Element using Unity 6 attributes
[UxmlElement]
public partial class <CustomElement> : VisualElement
{
    [UxmlAttribute] public float maxValue { get; set; } = 100f;
    [UxmlAttribute] public string customLabel { get; set; } = "<Placeholder>";

    public <CustomElement>()
    {
        var label = new Label(customLabel);
        label.AddToClassList("<custom-element>__label");
        Add(label);
    }
}

// Reactive Model (ModelData.cs)
public class <ModelData> : ScriptableObject, INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    [SerializeField] private int _health = 100;

    [CreateProperty]
    public int Health
    {
        get => _health;
        set
        {
            if (_health != value)
            {
                _health = value;
                propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(nameof(Health)));
            }
        }
    }
}

// Clean View inheriting base layout flow and using EventRegistry
[RequireComponent(typeof(UIDocument))]
public class <ViewComponent> : UITKBaseClass
{
    [SerializeField] private <ModelData> _model;
    [SerializeField] private <PresenterComponent> _presenter;

    private Button _submitButton;
    private readonly EventRegistry _eventRegistry = new();

    protected override void InitializeElements()
    {
        var root = _rootVisualElement.Q<VisualElement>("panel-container");
        if (root != null && _model != null)
        {
            root.dataSource = _model; // ✅ Set binding source
        }

        _submitButton = _rootVisualElement.Q<Button>("submit-btn");
    }

    protected override void RegisterCallbacks()
    {
        if (_submitButton != null)
        {
            _eventRegistry.RegisterCallback<ClickEvent>(_submitButton, _ => _presenter?.HandleSubmit());
        }
    }

    protected override void UnregisterCallbacks()
    {
        _eventRegistry.Dispose(); // ✅ Bulk event cleanup
    }

    public override void ShowPanel(bool show)
    {
        _rootVisualElement.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
```

## 5. Quick Reference & Decision Matrix

| Feature        | CSS Standard               | USS Subset / Unity Extension                          |
| :------------- | :------------------------- | :---------------------------------------------------- |
| Layout System  | Flexbox + Grid             | Flexbox (Yoga engine) only                            |
| Size Units     | px, %, em, rem, vh, vw     | px, % only                                            |
| Math           | `calc(100% - 20px)`        | Not supported                                         |
| Layering       | `z-index: 10`              | Not supported (defined by element declaration order)  |
| Color Formats  | Hex (`#FFFFFF`), rgb, rgba | `rgb()`, `rgba()` only (Hex is forbidden)             |
| Variables      | Supported in any selector  | Supported in `:root` selector block only              |
| Media Queries  | `@media`                   | Not supported (use separate styles in UXML/USS files) |
| Layout Gaps    | `gap: 10px`                | Not supported (use child spacing margins instead)     |
| Pseudo-classes | `:nth-child`, `:not`       | Not supported                                         |

| Problem Symptom         | Primary Cause                       | Troubleshooting Checklist                                                                                          |
| :---------------------- | :---------------------------------- | :----------------------------------------------------------------------------------------------------------------- |
| UI elements are missing | display/visibility or parent bounds | Ensure `display` is `Flex`, `visibility` is `Visible`, and check container size using UI Toolkit Debugger          |
| Event calls do not fire | blocking overlays or wrong picking  | Set element `pickingMode` to `Position` and confirm button state is enabled                                        |
| Elements return null    | query timing or mismatch            | Verify element `name` exists in UXML, query inside `OnEnable()`, and check case-sensitivity                        |
| Bindings do not update  | missing property notifications      | Check for `[CreateProperty]`, verify `INotifyBindablePropertyChanged` implementation, and check `Notify()` updates |
| Styles fail to display  | USS reference broken                | Check `<Style src="..." />` paths and class names for case-sensitive matches                                       |
