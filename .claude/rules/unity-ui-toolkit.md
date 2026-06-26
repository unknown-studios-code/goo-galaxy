---
paths:
  - "Assets/Scripts/**/*.cs"
  - "Assets/UI/**/*.uxml"
  - "Assets/UI/**/*.uss"
---

# Unity UI Toolkit Reference

> **Target: Unity 6.3.4f1 only.** C# style → [unity-code-style.md](unity-code-style.md). Design patterns (Template Method, MVP) → [unity-design-patterns.md](unity-design-patterns.md).
> Official docs: https://docs.unity3d.com/6000.3/Documentation/Manual/UIElements.html

---

## File Naming & Organization

- **PascalCase** for UXML/USS files: `MainMenu.uxml`, `PlayerHUD.uss`.
- UXML → `Assets/UI/UXML/`, USS → `Assets/UI/USS/`.
- Match USS filename to UXML: `MainMenu.uss` ↔ `MainMenu.uxml`.

---

## USS vs CSS — Critical Differences

**USS is a CSS subset with Unity extensions. No grid, no `calc()`, hex colors do not work.**

| Feature                       | CSS                    | USS                                   |
| ----------------------------- | ---------------------- | ------------------------------------- |
| Layout                        | Flexbox + Grid         | **Flexbox only**                      |
| Units                         | px, %, em, rem, vw, vh | **px and % only**                     |
| `calc()`                      | ✅                     | ❌                                    |
| `z-index`                     | ✅                     | ❌ — use element order                |
| Hex colors                    | ✅ `#FF6432`           | ❌ — use `rgb()`/`rgba()` only        |
| `:nth-child()`, `:not()`      | ✅                     | ❌                                    |
| `:hover`, `:active`, `:focus` | ✅                     | ✅                                    |
| CSS variables                 | Any selector           | **`:root {}` only**                   |
| `@media`, `@import`           | ✅                     | ❌ — use `<Style src="..."/>` in UXML |
| `display: grid`               | ✅                     | ❌                                    |
| `gap`                         | ✅                     | ❌ — use `margin` on children         |

### Unity-Specific Properties

```css
/* Text */
-unity-font-style: bold; /* normal, italic, bold, bold-and-italic */
-unity-text-align: middle-center; /* upper/middle/lower + left/center/right */
-unity-font-definition: url("...");
-unity-text-outline-width: 1px;
-unity-text-outline-color: rgb(0, 0, 0);

/* Background */
-unity-background-scale-mode: scale-to-fit; /* scale-and-crop, stretch-to-fill */
-unity-background-image-tint-color: rgb(255, 0, 0);

/* 9-slice */
-unity-slice-left: 10;
-unity-slice-right: 10;
-unity-slice-top: 10;
-unity-slice-bottom: 10;

/* Overflow */
overflow: hidden;

/* Picking */
picking-mode: position; /* receive pointer events (default) */
picking-mode: ignore; /* pass through */
```

### URL Paths

```css
background-image: url("project://database/Assets/UI/Icons/icon.png"); /* recommended */
background-image: resource("UI/Icons/icon");
background-image: url("../Icons/icon.png");
```

---

## BEM Naming Convention

**Pattern:** `block-name__element-name--modifier-name`

| Part     | Role                   | Example                  |
| -------- | ---------------------- | ------------------------ |
| Block    | Standalone component   | `navbar-menu`, `sidebar` |
| Element  | Part of block (`__`)   | `__item`, `__button`     |
| Modifier | Variation/state (`--`) | `--active`, `--primary`  |

- **`name`** (UXML attribute): kebab-case, unique within block — for C# queries.
- **`class`** (UXML attribute): BEM, reusable styles.
- **Selectors:** flat + specific — prefer `.block__element` over `.a .b .c`.
- **Modifiers:** additive classes — `.button--small` alongside `.button`.
- **State classes:** `.is-selected`, `.is-disabled` — toggled from C#.

**DO:**

- ✅ `navbar-menu`, `navbar-menu__item`, `navbar-menu__item--active`
- ✅ `button--primary`, `button--small`

**DON'T:**

- ❌ `menu` (generic), `navBarMenu` (camelCase), `navbar_menu` (underscores)
- ❌ `navbar-item` (missing block), `navbar-menu__item-active` (missing `--`)

### UXML Example

```xml
<ui:VisualElement name="navbar-menu" class="navbar-menu">
  <ui:Button name="navbar-menu__shop-button"
             class="navbar-menu__shop-button button button--primary" text="Shop" />
</ui:VisualElement>
```

### USS Example

```css
.navbar-menu {
  padding: 8px;
}
.navbar-menu > * {
  margin-right: 8px;
} /* gap not supported */
.navbar-menu__shop-button {
  min-width: 120px;
}
.button {
  height: 32px;
  padding-left: 12px;
  padding-right: 12px;
}
.button--primary {
  background-color: rgb(40, 120, 240);
  color: rgb(255, 255, 255);
}
.button--small {
  height: 24px;
  font-size: 11px;
}
.is-selected {
  border-color: rgb(255, 200, 0);
  border-width: 2px;
}
.is-disabled {
  opacity: 0.5;
}
```

### C# Selector Constants

```csharp
private const string NavbarMenu = "navbar-menu";
private const string ShopButton = "navbar-menu__shop-button";
var navbar = root.Q<VisualElement>(NavbarMenu);
```

### Toggling Classes

```csharp
btn.AddToClassList("button--primary");
btn.RemoveFromClassList("button--small");
btn.EnableInClassList("is-selected", true);
btn.ToggleInClassList("button--primary");
```

---

## Flexbox Layout

Unity UI Toolkit uses **Yoga layout engine** (CSS Flexbox subset). Every container is row or column.

### Container (Parent)

```css
flex-direction: column; /* default */
flex-direction: row;
flex-wrap: wrap;
justify-content: flex-start | flex-end | center | space-between | space-around;
align-items: stretch | flex-start | flex-end | center;
```

> ❌ **`gap` is not supported.** Use `margin` on children: `margin-right: 8px;`

### Items (Children)

```css
flex-grow: 1;
flex-shrink: 0;
flex-basis: auto;
flex: 1; /* shorthand: grow=1, shrink=1, basis=0 */
align-self: center;
```

### Common Patterns

```css
.fullscreen {
  flex-grow: 1;
}
.toolbar {
  flex-direction: row;
  justify-content: space-between;
  align-items: center;
  padding: 10px;
}
.toolbar > * {
  margin-right: 8px;
}
.centered-container {
  flex-direction: column;
  justify-content: center;
  align-items: center;
  flex-grow: 1;
}
.content-area {
  flex-grow: 1;
  flex-shrink: 1;
}
.sidebar {
  width: 240px;
  flex-shrink: 0;
}
```

### Positioning

```css
position: relative;
left: 10px;
top: 10px;
position: absolute;
left: 0;
right: 0;
top: 0;
bottom: 0;
```

---

## USS Properties Quick Reference

### Display & Visibility

```css
display: flex; /* default */
display: none; /* removed from layout */
visibility: visible;
visibility: hidden; /* keeps space */
opacity: 1;
overflow: hidden;
```

### Sizing

```css
width: 100px;
width: 50%;
width: auto;
height: 100px;
min-width: 50px;
max-width: 200px;
```

### Spacing & Borders

```css
padding: 10px;
padding: 10px 20px;
padding: 10px 20px 15px 25px;
margin: 10px;
margin-left: auto; /* push right */
border-width: 2px;
border-color: rgb(0, 0, 0);
border-radius: 8px;
```

### Backgrounds & Text

```css
background-color: rgb(50, 50, 50);
background-image: url("project://database/Assets/UI/bg.png");
color: rgb(0, 0, 0);
font-size: 16px;
-unity-font-style: bold;
-unity-text-align: middle-center;
white-space: nowrap;
```

---

## USS Variables (Design Tokens)

Declared in `:root {}` **only**:

```css
:root {
  --color-primary: rgb(72, 144, 226);
  --color-surface: rgb(40, 40, 40);
  --color-text: rgb(210, 210, 210);
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 16px;
  --spacing-lg: 24px;
  --radius-sm: 4px;
  --radius-md: 8px;
  --font-size-sm: 12px;
  --font-size-md: 14px;
  --font-size-lg: 18px;
}

.card {
  background-color: var(--color-surface);
  border-radius: var(--radius-md);
  padding: var(--spacing-md);
}
```

---

## Pseudo-Classes

| Pseudo-class             | Trigger               |
| ------------------------ | --------------------- |
| `:hover`                 | Mouse over            |
| `:active`                | Pressed               |
| `:focus`                 | Keyboard focus        |
| `:disabled` / `:enabled` | `SetEnabled()` state  |
| `:checked`               | Toggle/RadioButton on |
| `:selected`              | Item selected in list |
| `:root`                  | Root visual element   |

```css
.button--primary:hover {
  background-color: rgb(90, 160, 240);
}
.button--primary:active {
  background-color: rgb(55, 120, 200);
  scale: 0.97;
}
.input:disabled {
  opacity: 0.4;
}
```

> ❌ `:nth-child()`, `:not()`, `:first-child`, `:last-child` — **not supported**.

---

## Transitions

```css
.button {
  transition-property: background-color, scale;
  transition-duration: 0.15s, 0.1s;
  transition-timing-function: ease, ease-out;
}
.button:hover {
  background-color: rgb(90, 155, 225);
  scale: 1.05 1.05;
}
.button:active {
  scale: 0.97;
}
```

Animatable: `background-color`, `color`, `opacity`, `scale`, `translate`, `rotate`, `width`, `height`, `margin`, `padding`, `border-color`, `border-width`.

Transform properties (no shorthand): `scale: 1.5 1.5;` `rotate: 45deg;` `translate: 10px 20px;`

---

## UXML Structure

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <Style src="project://database/Assets/UI/Styles/MainStyles.uss" />
    <ui:VisualElement name="root" class="container">
        <!-- Content -->
    </ui:VisualElement>
</ui:UXML>
```

**Data binding in UXML:**

```xml
<ui:VisualElement data-source-type="MyDataClass, Assembly-CSharp" name="data-root">
    <ui:Label binding-path="PropertyName" />
</ui:VisualElement>
```

---

## UXML Elements Quick Reference

### Text

| Element     | Key Attributes                                       |
| ----------- | ---------------------------------------------------- |
| `Label`     | `text`                                               |
| `TextField` | `value`, `placeholder-text`, `multiline`, `password` |

### Buttons & Selection

| Element            | Key Attributes     |
| ------------------ | ------------------ |
| `Button`           | `text`             |
| `Toggle`           | `label`, `value`   |
| `RadioButtonGroup` | `value` (index)    |
| `DropdownField`    | `choices`, `index` |

### Numeric Input

| Element                       | Key Attributes                                      |
| ----------------------------- | --------------------------------------------------- |
| `IntegerField` / `FloatField` | `label`, `value`                                    |
| `Slider`                      | `low-value`, `high-value`, `value`                  |
| `SliderInt`                   | `low-value`, `high-value`, `value`                  |
| `MinMaxSlider`                | `low-limit`, `high-limit`, `min-value`, `max-value` |

### Display

| Element       | Key Attributes                                    |
| ------------- | ------------------------------------------------- |
| `ProgressBar` | `value`, `high-value`, `title`                    |
| `Image`       | (set via C# or USS)                               |
| `HelpBox`     | `text`, `message-type` (`Info`/`Warning`/`Error`) |

### Containers

| Element         | Key Attributes                                           |
| --------------- | -------------------------------------------------------- |
| `VisualElement` | Base container for layout                                |
| `ScrollView`    | `mode` (`Vertical`/`Horizontal`/`VerticalAndHorizontal`) |
| `GroupBox`      | `text`                                                   |
| `Foldout`       | `text`, `value` (expanded)                               |
| `Box`           | Simple bordered container                                |

### Lists & Trees

| Element               | Key Attributes                                                 |
| --------------------- | -------------------------------------------------------------- |
| `ListView`            | `fixed-item-height`, `selection-type`, `virtualization-method` |
| `TreeView`            | `fixed-item-height`                                            |
| `MultiColumnListView` | `<ui:Columns>` children                                        |
| `MultiColumnTreeView` | `<ui:Columns>` children                                        |

### Tabs

```xml
<ui:TabView name="main-tabs">
    <ui:Tab label="Inventory" name="inventory-tab">...</ui:Tab>
</ui:TabView>
```

---

## ListView Setup (C#)

```csharp
var listView = root.Q<ListView>("inventory-list");
listView.makeItem = () => new Label();
listView.bindItem = (element, index) => ((Label)element).text = _items[index].Name;
listView.itemsSource = _items;
listView.RefreshItems(); // after data changes
```

For `MultiColumnListView`:

```csharp
table.columns["name-column"].makeCell = () => new Label();
table.columns["name-column"].bindCell = (element, index) => ((Label)element).text = _data[index].Name;
table.itemsSource = _data;
```

---

## Data Binding (Unity 6+ Runtime)

### Reactive Data Source (UI updates on change)

```csharp
using Unity.Properties;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Game Data/Player")]
public class PlayerDataSO : ScriptableObject, INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    [SerializeField] private int _health = 100;

    [CreateProperty]
    public int Health
    {
        get => _health;
        set { if (_health != value) { _health = value; Notify(nameof(Health)); } }
    }

    private void Notify(string prop) => propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(prop));
}
```

> ⚠️ Without `INotifyBindablePropertyChanged`, bindings only update on initial assignment.

### Simple (Read-Only) Data Source

```csharp
[CreateAssetMenu(fileName = "ItemData", menuName = "Game Data/Item")]
public class ItemDataSO : ScriptableObject
{
    [SerializeField] private string _itemName;
    [CreateProperty] public string ItemName => _itemName;
}
```

### Setting Data Source in C#

```csharp
private void OnEnable()
{
    var root = _uiDocument.rootVisualElement;
    root.Q<VisualElement>("player-panel").dataSource = _playerData;
}
```

### Manual Binding via SetBinding()

```csharp
label.SetBinding("text", new DataBinding
{
    dataSourcePath = new PropertyPath(nameof(PlayerDataSO.PlayerName)),
    bindingMode = BindingMode.ToTarget
});
```

### Binding Modes

| Mode           | Direction        | Use Case                  |
| -------------- | ---------------- | ------------------------- |
| `ToTarget`     | Data → UI        | Display only              |
| `ToSource`     | UI → Data        | Input writes back         |
| `TwoWay`       | Both             | Settings, editable fields |
| `ToTargetOnce` | Data → UI (once) | Initial value             |

> ❌ **`SerializedObject.Bind()` is Editor-only** — never use for runtime UIDocument.

---

## Custom VisualElements (Unity 6)

❌ **Deprecated** — `UxmlFactory`/`UxmlTraits`:
✅ **Unity 6.3 API** — `[UxmlElement]` + `[UxmlAttribute]`:

```csharp
[UxmlElement]
public partial class HealthBar : VisualElement
{
    [UxmlAttribute] public float maxHealth { get; set; } = 100f;
    [UxmlAttribute] public string label { get; set; } = "HP";

    public HealthBar()
    {
        var lbl = new Label(label);
        lbl.AddToClassList("health-bar__label");
        Add(lbl);
    }

    public void SetValue(float current)
    {
        float pct = Mathf.Clamp01(current / maxHealth) * 100f;
        // update fill element...
    }
}
```

Usage in UXML: `<MyNamespace.HealthBar max-health="100" label="HP" />`

---

## Querying Elements

```csharp
// By name
var button = root.Q<Button>("submit-button");

// By type
var firstLabel = root.Q<Label>();

// By class
var cards = root.Query<VisualElement>(className: "card").ToList();

// Chained
var panelButton = root.Q<VisualElement>("settings-panel").Q<Button>("close-button");

// Null safety
root.Q<Button>("optional-button")?.SetEnabled(false);
```

> ❌ **Never query in Update** — cache in `OnEnable()`.

### Query Timing

```csharp
// ✅ OnEnable — UIDocument.rootVisualElement is guaranteed ready
private void OnEnable()
{
    var root = _uiDocument.rootVisualElement;
    _button = root.Q<Button>("my-button");
    _button.clicked += OnButtonClicked;
}
private void OnDisable() => _button.clicked -= OnButtonClicked;
```

---

## Show/Hide

```csharp
// Remove from layout:
element.style.display = DisplayStyle.None;
element.style.display = DisplayStyle.Flex; // restore

// Keep space:
element.style.visibility = Visibility.Hidden;
element.style.visibility = Visibility.Visible;
```

---

## Event Handling

```csharp
// Button clicks
private void OnEnable() => _button.clicked += OnClicked;
private void OnDisable() => _button.clicked -= OnClicked;

// Enable/disable
button.SetEnabled(false);

// Value changes
textField.RegisterValueChangedCallback(evt => Debug.Log(evt.newValue));

// Keyboard
element.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return) Submit(); });

// Stop propagation
button.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
```

### EventRegistry (Project Standard)

```csharp
using GameSystems;
private readonly EventRegistry _eventRegistry = new();

private void OnEnable()
{
    _eventRegistry.RegisterCallback<ClickEvent>(_submitButton, OnSubmitClicked);
    _eventRegistry.RegisterValueChangedCallback<float>(_volumeSlider, OnVolumeChanged);
}

private void OnDisable() => _eventRegistry.Dispose(); // Bulk cleanup
```

---

## VisualTreeAsset Instantiation

```csharp
[SerializeField] private VisualTreeAsset _cardTemplate;

private void PopulateCards()
{
    _container.Clear();
    foreach (var data in _cards)
    {
        var element = _cardTemplate.Instantiate();
        element.Q<Label>("card-title").text = data.Title;
        element.dataSource = data;

        var d = data; // capture loop variable
        element.Q<Button>("action-button").clicked += () => OnCardClicked(d);

        _container.Add(element);
    }
}
```

---

## MVP Pattern

**Architecture:** Model (data) → dataSource → View (UI) ← user input → Presenter (logic) → modifies Model.

**View naming:** `*View` suffix, inherits `UITKBaseClass`. Views handle UI only — no business logic.

| Class Type | Example                  | Responsibility                                     |
| ---------- | ------------------------ | -------------------------------------------------- |
| View       | `BuildingsView.cs`       | UI display, event subscription only                |
| Presenter  | `BuildingsController.cs` | Game logic, data manipulation, event orchestration |
| Model      | `FactionDataSO.cs`       | Pure data — no UI or logic                         |

### Model (Reactive Data)

```csharp
using Unity.Properties;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "PlayerStats", menuName = "Game Data/Player Stats")]
public class PlayerStatsModel : ScriptableObject, INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _currentHealth = 100;
    [SerializeField] private string _playerName = "Hero";

    [CreateProperty] public int MaxHealth => _maxHealth;

    [CreateProperty]
    public int CurrentHealth
    {
        get => _currentHealth;
        set { int clamped = Mathf.Clamp(value, 0, _maxHealth); if (_currentHealth != clamped) { _currentHealth = clamped; Notify(nameof(CurrentHealth)); Notify(nameof(HealthPercent)); } }
    }

    [CreateProperty]
    public string PlayerName
    {
        get => _playerName;
        set { if (_playerName != value) { _playerName = value; Notify(nameof(PlayerName)); } }
    }

    [CreateProperty] public float HealthPercent => _maxHealth > 0 ? (float)_currentHealth / _maxHealth * 100f : 0f;

    private void Notify(string prop) => propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(prop));
}
```

### View (UI Display Only)

```csharp
[RequireComponent(typeof(UIDocument))]
public class PlayerStatsView : UITKBaseClass
{
    [SerializeField] private PlayerStatsModel _playerStats;
    [SerializeField] private PlayerStatsPresenter _presenter;

    private VisualElement _rootPanel;
    private Button _healButton;
    private readonly EventRegistry _eventRegistry = new();

    protected override void InitializeElements()
    {
        _rootPanel = _rootVisualElement.Q<VisualElement>("player-stats-panel");
        if (_rootPanel != null && _playerStats != null)
        {
            _rootPanel.dataSource = _playerStats;
        }

        _healButton = _rootVisualElement.Q<Button>("heal-button");
    }

    protected override void RegisterCallbacks()
    {
        if (_healButton != null)
        {
            _eventRegistry.RegisterCallback<ClickEvent>(_healButton, _ => _presenter?.OnHealClicked());
        }
    }

    protected override void UnregisterCallbacks() => _eventRegistry.Dispose();

    public override void ShowPanel(bool show)
    {
        if (_rootPanel != null)
        {
            _rootPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
```

### Presenter (Game Logic)

```csharp
public class PlayerStatsPresenter : MonoBehaviour
{
    [SerializeField] private PlayerStatsModel _playerStats;
    [SerializeField] private int _healAmount = 25;
    [SerializeField] private int _damageAmount = 10;

    public void OnHealClicked()
    {
        if (_playerStats == null) return;
        _playerStats.CurrentHealth += _healAmount;
    }

    public void ApplyDamage(int amount)
    {
        if (_playerStats == null) return;
        _playerStats.CurrentHealth -= amount;
        if (_playerStats.CurrentHealth <= 0) OnPlayerDeath();
    }

    private void OnPlayerDeath() => Debug.Log("Player died!");
}
```

### UXML (with Bindings)

```xml
<ui:VisualElement name="player-stats-panel" class="stats-panel"
                  data-source-type="PlayerStatsModel, Assembly-CSharp">
    <ui:Label binding-path="PlayerName" name="player-name" class="stats-panel__title" />
    <ui:ProgressBar binding-path="HealthPercent" low-value="0" high-value="100" name="health-bar" />
    <ui:Button name="heal-button" text="Heal" class="stats-panel__button" />
</ui:VisualElement>
```

**Key rules:**

1. Model implements `INotifyBindablePropertyChanged` + `[CreateProperty]` on all bound properties.
2. Call `Notify(propertyName)` in setters — triggers UI update.
3. View sets `dataSource` in `InitializeElements()`, forwards user input to Presenter.
4. Presenter only modifies Model — UI updates automatically via bindings.

---

## Performance Tips

1. **Cache VisualElement refs** in `OnEnable` — never `Q<>()` in `Update`.
2. **Use USS classes** for style changes, not inline `element.style.*`.
3. **Use `ListView`** for lists > ~20 items (virtualized).
4. **Avoid `Query<>().ToList()`** in frequent code — allocates.
5. **Use USS variables** for colors/sizes.
6. **Minimize UXML nesting** — each level adds traversal cost.
7. **Use `EventRegistry`** for bulk cleanup.

---

## Common Mistakes

| ❌ Wrong                                 | ✅ Correct                                  | Note                   |
| ---------------------------------------- | ------------------------------------------- | ---------------------- |
| `color: #FF0000;`                        | `color: rgb(255, 0, 0);`                    | No hex                 |
| `text-align: center;`                    | `-unity-text-align: middle-center;`         | Unity prefix           |
| `font-weight: bold;`                     | `-unity-font-style: bold;`                  | Different property     |
| `background: url(...)`                   | `background-image: url(...)`                | No shorthand           |
| `element.visible = false`                | `element.style.display = DisplayStyle.None` | Use style              |
| `binding-path="health"`                  | `binding-path="Health"`                     | Case-sensitive         |
| `button.onClick`                         | `button.clicked`                            | Use `clicked`          |
| `button.enabled = false`                 | `button.SetEnabled(false)`                  | Use method             |
| Missing `[CreateProperty]`               | Add attribute                               | Required for binding   |
| Missing `INotifyBindablePropertyChanged` | Implement interface                         | Required for reactive  |
| `root.Q("name")`                         | `root.Q<VisualElement>("name")`             | Include type           |
| Query+subscribe in `Awake`               | Do in `OnEnable`                            | Pairs with `OnDisable` |
| `navBarMenu`                             | `navbar-menu`                               | kebab-case             |
| `navbar-item`                            | `navbar-menu__item`                         | BEM `__`               |
| `element.Add(template)`                  | `element.Add(template.Instantiate())`       | Must instantiate       |
| `UxmlFactory`/`UxmlTraits`               | `[UxmlElement]`/`[UxmlAttribute]`           | Unity 6                |
| `SerializedObject.Bind()` runtime        | `binding-path` + `dataSource`               | Editor only            |

---

## Troubleshooting

### Binding Not Updating

1. `[CreateProperty]` on property? 2. `INotifyBindablePropertyChanged`? 3. `Notify()` called? 4. `dataSource` assigned? 5. `binding-path` matches exactly (case-sensitive)?

### Element Not Visible

1. `display` not `None`? 2. `visibility` not `Hidden`? 3. Parent has size? 4. Open UI Toolkit Debugger.

### Button Not Responding

1. Subscribed in `OnEnable`? 2. Not disabled? 3. No overlay blocking? 4. `picking-mode: position`?

### Query Returns Null

1. `name` in UXML? 2. Query in `OnEnable`? 3. Correct type? 4. Case-sensitive match?

### USS Not Applied

1. `<Style src="..." />` in UXML? 2. Class names match (case-sensitive)? 3. Check UI Toolkit Debugger.
