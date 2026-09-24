using System.Collections;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Runtime.UI.Views;
using GooGalaxy.Runtime.UI.Views.Elements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.UI
{
    /// <remarks>
    /// Split out of <see cref="MatchHudViewTests" /> because these are the only tests in the fixture that dispatch
    /// through a real <see cref="EventSystem" /> and <see cref="InputSystemUIInputModule" /> — the rest of the HUD
    /// surface is exercised through direct method calls and needs none of the input-system reset this base class
    /// performs on every test. Deriving from <see cref="InputTestFixture" /> gives every test here an isolated
    /// Input System severed from the real runtime, which is what makes a click land the same way whether or not
    /// the test runner's window has OS focus — see GOOM-17.
    /// </remarks>
    [TestFixture]
    public class MatchHudViewPointerTests : InputTestFixture
    {
        // Two frames are usually enough — one for the UIDocument's panel to attach, one for Yoga to resolve
        // layout against it — and the rest is headroom for a loaded CI machine, spent only when the wait
        // loop's condition has not already been met.
        private const int LayoutSettleFrameBudget = 10;

        // Arbitrary, and only needed here: the bare fixture tree carries no stylesheet, and a card-slot's height
        // comes entirely from the authored USS, so without this a hand slot resolves to a real width but a
        // zero-area worldBound — a rect a pointer can never land inside no matter where it is aimed.
        private const float HandSlotClickTestHeight = 64f;

        private GameObject _documentGO;
        private PanelSettings _panelSettings;
        private UIDocument _document;
        private MatchHudView _view;
        private CardSlotElement _handSlotZero;
        private CardSlotElement _handSlotOne;
        private GameObject _eventSystemGO;
        private Mouse _mouse;
        private int? _raisedHandSlotIndex;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Unity's Test Framework reuses one fixture instance across every test in the class rather than
            // constructing a fresh one per test, so a field a test writes has to be reset here or it leaks into
            // whichever test runs next.
            _raisedHandSlotIndex = null;

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            _documentGO = new GameObject(nameof(MatchHudView));
            _document = _documentGO.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;

            int rootWaitBudget = LayoutSettleFrameBudget;

            while ((_document.rootVisualElement == null) && rootWaitBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(_document.rootVisualElement, Is.Not.Null, "Test setup expects the UIDocument to have created its root within the wait budget.");

            BuildHudTree(_document.rootVisualElement);

            _view = _documentGO.AddComponent<MatchHudView>();

            int frameBudget = LayoutSettleFrameBudget;

            while (!_view.IsPanelReady && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(_view.IsPanelReady, Is.True, "Test setup expects the panel to have initialized within the layout settle budget.");
        }

        // A plain override of InputTestFixture's sync TearDown, not a second [UnityTearDown]: measured against
        // the full suite, Unity actually runs [UnitySetUp]/[UnityTearDown] as the outer wrapper around the sync
        // [SetUp]/[Test]/[TearDown] inner command — UnitySetUp before SetUp, TearDown before UnityTearDown — the
        // reverse of what an untested reading of the two attributes suggests. Cleanup that instead ran from a
        // same-named [UnityTearDown] executed after InputTestFixture's own TearDown() had already restored the
        // real InputManager, so RemoveDevice below was asserting against a device the isolated one had already
        // discarded — reproducible only in the full suite, never in this fixture alone. Overriding the sync
        // method removes the ambiguity: this always runs first, and calls base.TearDown() last.
        public override void TearDown()
        {
            if (_documentGO != null)
            {
                Object.Destroy(_documentGO);
            }

            if (_panelSettings != null)
            {
                Object.Destroy(_panelSettings);
            }

            if (_eventSystemGO != null)
            {
                Object.Destroy(_eventSystemGO);
            }

            if (_mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
            }

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator HandSlotPressed_PointerDownOnAFilledSlot_RaisesWithThatSlotIndex()
        {
            // GIVEN
            var state = new HandSlotState(new CardId("subject_alpha"), "Subject Alpha", 3, HandSlotKind.Specimen, CardAccent.None);
            _view.SetHandSlot(0, in state);
            _view.HandSlotPressed += HandleHandSlotPressed;
            yield return SettleHandSlotLayoutAsync(_handSlotZero);
            CreateEventSystem();
            Vector2 screenPoint = CorrectlyFlippedScreenPointFor(_handSlotZero.worldBound.center);

            // WHEN
            yield return LeftClickAtAsync(screenPoint);

            // THEN
            Assert.That(_raisedHandSlotIndex, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator HandSlotPressed_PointerDownOnASlotOtherThanTheFirst_RaisesThatSlotsOwnIndex()
        {
            // GIVEN — slot one rather than slot zero, because HandleHandSlotPointerDown resolves the index by
            // scanning _handSlots for the pressed element. Pressing only the first slot passes whether the
            // handler reports the element it matched or a hardcoded zero, so this is the case that actually pins
            // the loop — and the hand strip is due to be reworked when card artwork lands.
            var state = new HandSlotState(new CardId("acid_crawler"), "Acid Crawler", 2, HandSlotKind.Specimen, CardAccent.None);
            _view.SetHandSlot(1, in state);
            _view.HandSlotPressed += HandleHandSlotPressed;
            yield return SettleHandSlotLayoutAsync(_handSlotOne);
            CreateEventSystem();
            Vector2 screenPoint = CorrectlyFlippedScreenPointFor(_handSlotOne.worldBound.center);

            // WHEN
            yield return LeftClickAtAsync(screenPoint);

            // THEN
            Assert.That(_raisedHandSlotIndex, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HandSlotPressed_PointerDownOnAnEmptySlot_DoesNotRaise()
        {
            // GIVEN -- slot zero starts empty, since SetUp never draws a card into it. A filled slot is clicked
            // first, so a silent empty slot proves HandleHandSlotPointerDown's own filter rather than a dispatch
            // pipeline that never fired: this test must stand on its own when run alone.
            var state = new HandSlotState(new CardId("acid_crawler"), "Acid Crawler", 2, HandSlotKind.Specimen, CardAccent.None);
            _view.SetHandSlot(1, in state);
            _view.HandSlotPressed += HandleHandSlotPressed;
            yield return SettleHandSlotLayoutAsync(_handSlotZero);
            yield return SettleHandSlotLayoutAsync(_handSlotOne);
            CreateEventSystem();
            yield return LeftClickAtAsync(CorrectlyFlippedScreenPointFor(_handSlotOne.worldBound.center));
            Assert.That(_raisedHandSlotIndex, Is.EqualTo(1), "Test setup expects a click on a filled slot to raise.");
            _raisedHandSlotIndex = null;
            Vector2 screenPoint = CorrectlyFlippedScreenPointFor(_handSlotZero.worldBound.center);

            // WHEN
            yield return LeftClickAtAsync(screenPoint);

            // THEN
            Assert.That(_raisedHandSlotIndex, Is.Null);
        }

        // The screen point that, once IsScreenPointInDiscardZone applies its Y flip, converts to targetPanelPoint
        // — computed by calibrating RuntimePanelUtils.ScreenToPanel's own scale from a reference conversion
        // rather than assuming panel units equal screen pixels 1:1, which does not hold under every test
        // runner's DPI scale (measured here at roughly 4.8:1).
        private Vector2 CorrectlyFlippedScreenPointFor(Vector2 targetPanelPoint)
        {
            Vector2 unflipped = UnflippedScreenPointFor(targetPanelPoint);

            return new Vector2(unflipped.x, Screen.height - unflipped.y);
        }

        // The screen point an unflipped ScreenToPanel call reads straight through as targetPanelPoint.
        private Vector2 UnflippedScreenPointFor(Vector2 targetPanelPoint)
        {
            IPanel panel = _document.rootVisualElement.panel;
            Vector2 reference = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));

            return new Vector2(targetPanelPoint.x / (reference.x / Screen.width), targetPanelPoint.y / (reference.y / Screen.height));
        }

        // Waits for the given hand slot's own layout to settle to a non-zero area, so the tests above can compute
        // a real worldBound to click into. An explicit height is forced first: this bare tree carries no
        // stylesheet (per BuildHudTree's own remarks below), and a card-slot's height is entirely CSS-driven, so
        // left alone the slot resolves to a real width but a zero-height rect — one no pointer position, however
        // aimed, can ever land inside.
        private IEnumerator SettleHandSlotLayoutAsync(CardSlotElement slot)
        {
            slot.style.height = HandSlotClickTestHeight;
            int frameBudget = LayoutSettleFrameBudget;

            while (((slot.resolvedStyle.width <= 0f) || (slot.resolvedStyle.height <= 0f)) && frameBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(
                (slot.resolvedStyle.width > 0f, slot.resolvedStyle.height > 0f),
                Is.EqualTo((true, true)),
                $"Test setup expects '{slot.name}' layout to have settled to a non-zero area before computing a click target."
            );
        }

        // Adds the EventSystem + InputSystemUIInputModule that GOOM-17 found missing from both gameplay scenes.
        // Left with no actions assigned, InputSystemUIInputModule.OnEnable assigns Unity's own built-in defaults
        // (Point bound to <Pointer>/position, left-click bound to <Mouse>/leftButton) — the same shape
        // GameplaySceneInputWiringTests proves the authored scenes carry, so this fixture needs no
        // InputActionAsset of its own.
        private void CreateEventSystem()
        {
            _eventSystemGO = new GameObject(nameof(EventSystem));
            _eventSystemGO.AddComponent<EventSystem>();
            _eventSystemGO.AddComponent<InputSystemUIInputModule>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        // Drives the fixture's own isolated Mouse through InputTestFixture's Set/Press/Release helpers and lets
        // EventSystem's own Process() pick it up, rather than calling MatchHudView's private pointer handler
        // directly — a real PointerDownEvent traveling through the runtime dispatch pipeline is exactly what
        // GOOM-17's shipped scenes could never deliver with no EventSystem present. Polls for the callback rather
        // than a fixed frame count, and the bounded exit also covers the negative case: an empty slot is expected
        // to leave _raisedHandSlotIndex null for the whole budget. The trailing release leaves the virtual device
        // the way a real click always ends, rather than removing it mid-press in teardown.
        private IEnumerator LeftClickAtAsync(Vector2 screenPoint)
        {
            Set(_mouse.position, screenPoint);
            InputSystem.Update();
            yield return null;

            Press(_mouse.leftButton);
            InputSystem.Update();

            int frameBudget = LayoutSettleFrameBudget;

            while (!_raisedHandSlotIndex.HasValue && frameBudget-- > 0)
            {
                yield return null;
            }

            Release(_mouse.leftButton);
            InputSystem.Update();
        }

        private void HandleHandSlotPressed(int slotIndex)
        {
            _raisedHandSlotIndex = slotIndex;
        }

        // Builds every element name and custom element type MatchHudView.uxml declares that CacheElements
        // actually resolves by name, directly onto the UIDocument's root, rather than cloning the authored
        // VisualTreeAsset: per Rule 6 in unity-testing.md, fixtures build in code unless the authored asset
        // itself is what is under test. Only the two hand slots these tests click into are kept as fields — every
        // other element CacheElements requires is still built, since RequireElement fails the whole panel without
        // it, but nothing here reads it back afterward.
        private void BuildHudTree(VisualElement root)
        {
            var background = new VisualElement { name = HudSelectors.Background };
            var safeArea = new SafeAreaElement { name = HudSelectors.SafeArea };
            var topBar = new VisualElement { name = HudSelectors.TopBar };

            var opponentScoreElement = new ScoreBadgeElement { name = HudSelectors.OpponentScore };
            var matchTimer = new Label { name = HudSelectors.MatchTimer };
            var opponentBadgeElement = new OpponentBadgeElement { name = HudSelectors.OpponentBadge };

            topBar.Add(opponentScoreElement);
            topBar.Add(matchTimer);
            topBar.Add(opponentBadgeElement);

            var boardWindow = new VisualElement { name = HudSelectors.BoardWindow };
            var bottomBar = new VisualElement { name = HudSelectors.BottomBar };
            var statusRow = new VisualElement { name = HudSelectors.StatusRow };

            var localScoreElement = new ScoreBadgeElement { name = HudSelectors.LocalScore };
            var emoteSlot = new Button { name = HudSelectors.EmoteSlot };
            statusRow.Add(localScoreElement);
            statusRow.Add(emoteSlot);

            var catchUpLine = new Label { name = HudSelectors.CatchUpLine };
            var energyGaugeElement = new EnergyGaugeElement { name = HudSelectors.EnergyGauge };
            var discardZone = new VisualElement { name = HudSelectors.DiscardZone };
            discardZone.AddToClassList(HudSelectors.DiscardZoneBlock);

            var handStrip = new VisualElement { name = HudSelectors.HandStrip };
            handStrip.AddToClassList(HudSelectors.HandStrip);

            _handSlotZero = new CardSlotElement { name = HudSelectors.HandSlotZero };
            _handSlotOne = new CardSlotElement { name = HudSelectors.HandSlotOne };
            var handSlotTwo = new CardSlotElement { name = HudSelectors.HandSlotTwo };
            var handSlotThree = new CardSlotElement { name = HudSelectors.HandSlotThree };
            var nextCardSlot = new CardSlotElement { name = HudSelectors.NextCardSlot };
            nextCardSlot.AddToClassList(HudSelectors.CardSlotNext);

            handStrip.Add(_handSlotZero);
            handStrip.Add(_handSlotOne);
            handStrip.Add(handSlotTwo);
            handStrip.Add(handSlotThree);
            handStrip.Add(nextCardSlot);

            bottomBar.Add(statusRow);
            bottomBar.Add(catchUpLine);
            bottomBar.Add(energyGaugeElement);
            bottomBar.Add(discardZone);
            bottomBar.Add(handStrip);

            safeArea.Add(topBar);
            safeArea.Add(boardWindow);
            safeArea.Add(bottomBar);

            background.Add(safeArea);

            var countdownScrim = new VisualElement { name = HudSelectors.CountdownScrim };
            var countdownOverlayElement = new CountdownOverlayElement { name = HudSelectors.CountdownOverlay };
            var overtimeBanner = new VisualElement { name = HudSelectors.OvertimeBanner };

            var outcomeOverlay = new VisualElement { name = HudSelectors.OutcomeOverlay };
            var outcomeTitle = new Label { name = HudSelectors.OutcomeTitle };
            var outcomeReason = new Label { name = HudSelectors.OutcomeReason };
            outcomeOverlay.Add(outcomeTitle);
            outcomeOverlay.Add(outcomeReason);

            background.Add(countdownScrim);
            background.Add(countdownOverlayElement);
            background.Add(overtimeBanner);
            background.Add(outcomeOverlay);

            root.Add(background);
        }
    }
}
