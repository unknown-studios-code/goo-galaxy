using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Runtime.UI.Views;
using GooGalaxy.Runtime.UI.Views.Elements;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.UI
{
    [TestFixture]
    public class MatchHudViewTests
    {
        // Two frames are usually enough — one for the UIDocument's panel to attach, one for Yoga to resolve
        // layout against it — and the rest is headroom for a loaded CI machine, spent only when the wait
        // loop's condition has not already been met.
        private const int LayoutSettleFrameBudget = 10;

        // Loaded only by the two tests whose whole subject is a CSS cascade rule that no C# drives: the shared
        // fixture tree below carries no stylesheet at all, by design, so the accent- and scrim-only tests never
        // need it. Rule 6's exception in unity-testing.md applies there — the authored stylesheet is what is
        // under test — but nowhere else in this fixture.
        private const string MatchHudViewUssPath = "Assets/UI/USS/MatchHudView.uss";

        private const string DesignTokensUssPath = "Assets/UI/USS/DesignTokens.uss";

        // A row of five flex-grow: 1 slots does not always divide the strip's pixel width evenly; Yoga hands the
        // remainder pixel to some children and not others, so equal width is asserted within this margin rather
        // than exactly.
        private const float WidthToleranceInPixels = 1.5f;

        private GameObject _documentGO;
        private PanelSettings _panelSettings;
        private UIDocument _document;
        private MatchHudView _view;
        private ScoreBadgeElement _localScoreElement;
        private ScoreBadgeElement _opponentScoreElement;
        private OpponentBadgeElement _opponentBadgeElement;
        private EnergyGaugeElement _energyGaugeElement;
        private CardSlotElement _handSlotZero;
        private CardSlotElement _handSlotOne;
        private CardSlotElement _handSlotTwo;
        private CardSlotElement _handSlotThree;
        private CardSlotElement _nextCardSlot;
        private CountdownOverlayElement _countdownOverlayElement;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_documentGO != null)
            {
                Object.Destroy(_documentGO);
            }

            if (_panelSettings != null)
            {
                Object.Destroy(_panelSettings);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PanelInitialized_AfterLayoutSettles_HandSlotsHaveNonZeroSize()
        {
            // GIVEN
            int frameBudget = LayoutSettleFrameBudget;

            // WHEN
            while ((_handSlotZero.resolvedStyle.width <= 0f) && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(_handSlotZero.resolvedStyle.width, Is.GreaterThan(0f));
        }

        [Test]
        public void PanelInitialized_AfterBuilding_TheDocumentRootReportsPickingModeIgnore()
        {
            // GIVEN — set by UIToolkitView.TryInitializePanel so a full-screen root never swallows a click meant
            // for the board behind it.

            // WHEN / THEN
            Assert.That(_document.rootVisualElement.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        [Test]
        public void SetLocalScore_GivenAValue_LandsOnTheLocalScoreBadge()
        {
            // GIVEN

            // WHEN
            _view.SetLocalScore(7);

            // THEN
            Assert.That(_localScoreElement.Score, Is.EqualTo(7));
        }

        [Test]
        public void SetSeats_GivenPlayerIds_TintsEachScoreBadgeForItsOwnPlayer()
        {
            // GIVEN

            // WHEN
            _view.SetSeats(1, 2);

            // THEN
            Assert.That((_localScoreElement.PlayerId, _opponentScoreElement.PlayerId), Is.EqualTo((1, 2)));
        }

        [Test]
        public void SetOpponentLabel_GivenText_LandsOnTheOpponentBadge()
        {
            // GIVEN

            // WHEN
            _view.SetOpponentLabel("RIVAL");

            // THEN
            Assert.That(_opponentBadgeElement.LabelText, Is.EqualTo("RIVAL"));
        }

        [Test]
        public void SetHandSlot_GivenAFilledState_LandsOnTheTargetSlot()
        {
            // GIVEN
            var state = new HandSlotState(new CardId("subject_alpha"), "Subject Alpha", 3, HandSlotKind.Specimen, CardAccent.None);

            // WHEN
            _view.SetHandSlot(0, in state);

            // THEN
            Assert.That((_handSlotZero.State.DisplayName, _handSlotZero.State.EnergyCost), Is.EqualTo(("Subject Alpha", 3)));
        }

        [Test]
        public void SetHandSlot_GivenAProtocolCardWithAnAccent_AccentBarShowsTheMatchingModifierClass()
        {
            // GIVEN — Cryo-Stasis is a Spell, which MatchHudPresenter.ResolveSlotKind maps to
            // HandSlotKind.Protocol; a state built as Specimen would be one design never produces.
            var state = new HandSlotState(new CardId("cryo_stasis"), "Cryo-Stasis", 2, HandSlotKind.Protocol, CardAccent.Control);

            // WHEN
            _view.SetHandSlot(0, in state);

            // THEN
            VisualElement accentBar = _handSlotZero.Q(className: HudSelectors.CardSlotAccent);
            Assert.That(
                (accentBar.ClassListContains(HudSelectors.IsHidden), accentBar.ClassListContains(HudSelectors.CardSlotAccentControl)),
                Is.EqualTo((false, true))
            );
        }

        [TestCase(CardAccent.Baseline, HudSelectors.CardSlotAccentBaseline)]
        [TestCase(CardAccent.Control, HudSelectors.CardSlotAccentControl)]
        [TestCase(CardAccent.Explosive, HudSelectors.CardSlotAccentExplosive)]
        [TestCase(CardAccent.Defensive, HudSelectors.CardSlotAccentDefensive)]
        [TestCase(CardAccent.Corrosive, HudSelectors.CardSlotAccentCorrosive)]
        public void SetHandSlot_GivenACardAccent_AccentBarShowsOnlyTheMatchingModifierClass(CardAccent accent, string expectedClass)
        {
            // GIVEN
            var state = new HandSlotState(new CardId("accent_card"), "Accent Card", 1, HandSlotKind.Specimen, accent);
            VisualElement accentBar = _handSlotZero.Q(className: HudSelectors.CardSlotAccent);

            // WHEN
            _view.SetHandSlot(0, in state);

            // THEN
            Assert.That((accentBar.ClassListContains(HudSelectors.IsHidden), accentBar.ClassListContains(expectedClass)), Is.EqualTo((false, true)));
        }

        [Test]
        public void SetHandSlot_GivenCardAccentNone_AccentBarIsHidden()
        {
            // GIVEN
            var state = new HandSlotState(new CardId("unaccented_card"), "Unaccented", 1, HandSlotKind.Specimen, CardAccent.None);

            // WHEN
            _view.SetHandSlot(0, in state);

            // THEN — the class toggle is a plain field write in CardSlotElement.SetState, so it needs no frame
            // to observe; only the CSS-driven display it would otherwise produce needs a live stylesheet.
            VisualElement accentBar = _handSlotZero.Q(className: HudSelectors.CardSlotAccent);
            Assert.That(accentBar.ClassListContains(HudSelectors.IsHidden), Is.True);
        }

        [Test]
        public void SetHandSlot_GivenHandSlotStateEmpty_AccentBarIsHidden()
        {
            // GIVEN

            // WHEN
            _view.SetHandSlot(0, in HandSlotState.Empty);

            // THEN
            VisualElement accentBar = _handSlotZero.Q(className: HudSelectors.CardSlotAccent);
            Assert.That(accentBar.ClassListContains(HudSelectors.IsHidden), Is.True);
        }

        [Test]
        public void SetHandSlot_GivenSameCardAndKindWithADifferentAccent_AccentBarModifierClassChanges()
        {
            // GIVEN — the same CardId and Kind on both pushes isolates the accent as the only field that moved.
            // CardSlotElement.SetState early-outs on CardId and Kind matching a previous draw; if that guard
            // did not also compare Accent, the bar would still carry Explosive's modifier class after this
            // second push.
            var cardId = new CardId("volatile_mass");
            var firstState = new HandSlotState(cardId, "Volatile Mass", 4, HandSlotKind.Specimen, CardAccent.Explosive);
            var secondState = new HandSlotState(cardId, "Volatile Mass", 4, HandSlotKind.Specimen, CardAccent.Control);
            _view.SetHandSlot(0, in firstState);
            VisualElement accentBar = _handSlotZero.Q(className: HudSelectors.CardSlotAccent);

            // WHEN
            _view.SetHandSlot(0, in secondState);

            // THEN
            Assert.That(
                (accentBar.ClassListContains(HudSelectors.CardSlotAccentExplosive), accentBar.ClassListContains(HudSelectors.CardSlotAccentControl)),
                Is.EqualTo((false, true))
            );
        }

        [Test]
        public void SetHandSlot_GivenSameCardAndKindWithADifferentEnergyCost_UpdatesTheCostLabel()
        {
            // GIVEN — CardId, Kind and Accent all match the previous draw, isolating EnergyCost as the field
            // that moved. SetState's early-out has to compare it too, or a re-costed card would keep showing
            // its old price.
            var cardId = new CardId("volatile_mass");
            var firstState = new HandSlotState(cardId, "Volatile Mass", 4, HandSlotKind.Specimen, CardAccent.Explosive);
            var secondState = new HandSlotState(cardId, "Volatile Mass", 5, HandSlotKind.Specimen, CardAccent.Explosive);
            _view.SetHandSlot(0, in firstState);

            // WHEN
            _view.SetHandSlot(0, in secondState);

            // THEN
            Assert.That(_handSlotZero.State.EnergyCost, Is.EqualTo(5));
        }

        [Test]
        public void SetHandSlot_GivenSameCardAndKindWithADifferentDisplayName_UpdatesTheNameLabel()
        {
            // GIVEN — CardId, Kind and Accent all match the previous draw, isolating DisplayName as the field
            // that moved. SetState's early-out has to compare it too, or a renamed card would keep showing its
            // old name.
            var cardId = new CardId("volatile_mass");
            var firstState = new HandSlotState(cardId, "Volatile Mass", 4, HandSlotKind.Specimen, CardAccent.Explosive);
            var secondState = new HandSlotState(cardId, "Volatile Mass Prime", 4, HandSlotKind.Specimen, CardAccent.Explosive);
            _view.SetHandSlot(0, in firstState);

            // WHEN
            _view.SetHandSlot(0, in secondState);

            // THEN
            Assert.That(_handSlotZero.State.DisplayName, Is.EqualTo("Volatile Mass Prime"));
        }

        [UnityTest]
        public IEnumerator HandStrip_LiveStylesheetApplied_AllFiveCardSlotsResolveToEqualWidth()
        {
            // GIVEN — width equalization is CSS-only: next-card-slot sits as a direct hand-strip child again
            // (the wrapper that used to single it out is gone) and shares .card-slot's flex-grow: 1 /
            // flex-basis: 0 with no override, so this loads the authored stylesheet. Asserting against the
            // fixture's normally bare tree would pass by coincidence of the default column layout rather than
            // by the rule this test exists to pin.
            StyleSheet designTokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(DesignTokensUssPath);
            StyleSheet matchHudView = AssetDatabase.LoadAssetAtPath<StyleSheet>(MatchHudViewUssPath);
            Assert.That(designTokens, Is.Not.Null, $"Test setup expects '{DesignTokensUssPath}' to exist and import as a StyleSheet.");
            Assert.That(matchHudView, Is.Not.Null, $"Test setup expects '{MatchHudViewUssPath}' to exist and import as a StyleSheet.");
            _document.rootVisualElement.styleSheets.Add(designTokens);
            _document.rootVisualElement.styleSheets.Add(matchHudView);
            int frameBudget = LayoutSettleFrameBudget;

            // WHEN — waited on .card-slot's own flex-grow landing, not on the widths converging: the fixture's
            // unstyled default (column direction, align-items: stretch) already gives every child the same full
            // width, so polling the widths themselves cannot tell "the row rule applied" apart from "no
            // stylesheet ever took effect".
            while ((_handSlotZero.resolvedStyle.flexGrow < 1f) && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That(_handSlotZero.parent.resolvedStyle.flexDirection, Is.EqualTo(FlexDirection.Row));
            Assert.That(_handSlotOne.resolvedStyle.width, Is.EqualTo(_handSlotZero.resolvedStyle.width).Within(WidthToleranceInPixels));
            Assert.That(_handSlotTwo.resolvedStyle.width, Is.EqualTo(_handSlotZero.resolvedStyle.width).Within(WidthToleranceInPixels));
            Assert.That(_handSlotThree.resolvedStyle.width, Is.EqualTo(_handSlotZero.resolvedStyle.width).Within(WidthToleranceInPixels));
            Assert.That(_nextCardSlot.resolvedStyle.width, Is.EqualTo(_handSlotZero.resolvedStyle.width).Within(WidthToleranceInPixels));
        }

        [UnityTest]
        public IEnumerator CardSlot_LiveStylesheetApplied_OnlyTheQueuedSlotShowsTheScrim()
        {
            // GIVEN — the scrim's visibility is CSS-only (".card-slot--next .card-slot__scrim { display: flex; }"
            // against a base rule of "display: none"), with nothing in C# toggling it, so this is the other test
            // in the fixture that loads the authored stylesheet: without the real cascade there is nothing here
            // to prove. Both sheets are loaded together, as MatchHudView.uss's own rules assume DesignTokens.uss
            // is present for its custom properties.
            StyleSheet designTokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(DesignTokensUssPath);
            StyleSheet matchHudView = AssetDatabase.LoadAssetAtPath<StyleSheet>(MatchHudViewUssPath);
            Assert.That(designTokens, Is.Not.Null, $"Test setup expects '{DesignTokensUssPath}' to exist and import as a StyleSheet.");
            Assert.That(matchHudView, Is.Not.Null, $"Test setup expects '{MatchHudViewUssPath}' to exist and import as a StyleSheet.");
            _document.rootVisualElement.styleSheets.Add(designTokens);
            _document.rootVisualElement.styleSheets.Add(matchHudView);
            VisualElement handScrim = _handSlotZero.Q(className: HudSelectors.CardSlotScrim);
            VisualElement queuedScrim = _nextCardSlot.Q(className: HudSelectors.CardSlotScrim);
            int frameBudget = LayoutSettleFrameBudget;

            // WHEN — waited on the hand slot's scrim, not the queued one: DisplayStyle.Flex is also
            // VisualElement's default before any stylesheet applies, so the queued scrim reads as "already
            // correct" from frame zero regardless of whether the cascade ever ran. The hand slot's expected
            // None differs from that default, so it is the only one of the two that can prove the wait worked.
            while ((handScrim.resolvedStyle.display != DisplayStyle.None) && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That((handScrim.resolvedStyle.display, queuedScrim.resolvedStyle.display), Is.EqualTo((DisplayStyle.None, DisplayStyle.Flex)));
        }

        [Test]
        public void SetHandSlotAffordable_GivenFalse_DimsTheTargetSlot()
        {
            // GIVEN

            // WHEN
            _view.SetHandSlotAffordable(0, false);

            // THEN
            Assert.That(_handSlotZero.IsAffordable, Is.False);
        }

        [Test]
        public void SetEnergy_GivenAState_LandsOnTheEnergyGauge()
        {
            // GIVEN
            var state = new EnergyGaugeState(0.5f, 5, 10, EnergyGaugeAccent.CatchUp);

            // WHEN
            _view.SetEnergy(in state);

            // THEN
            Assert.That((_energyGaugeElement.State.WholeEnergy, _energyGaugeElement.State.Accent), Is.EqualTo((5, EnergyGaugeAccent.CatchUp)));
        }

        [Test]
        public void SetCountdownSeconds_GivenAValue_LandsOnTheCountdownOverlay()
        {
            // GIVEN

            // WHEN
            _view.SetCountdownSeconds(3);

            // THEN
            Assert.That(_countdownOverlayElement.Seconds, Is.EqualTo(3));
        }

        [TestCaseSource(nameof(SelfIgnoringHudElementFactories))]
        public void Constructor_ForEachSelfIgnoringHudElement_DefaultsToPickingModeIgnore(Func<VisualElement> createElement)
        {
            // GIVEN / WHEN — SafeAreaElement is excluded: it is marked picking-mode="Ignore" in the markup
            // instead of its constructor. CardSlotElement is excluded too: it deliberately keeps Position so
            // the GOOM-17 gesture work lands a touch on the slot rather than on its labels.
            VisualElement element = createElement();

            // THEN
            Assert.That(element.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        [Test]
        public void Constructor_CardSlotElement_LastChildIsTheScrim()
        {
            // THEN — stacking is child order and USS has no z-index, so the scrim has to be added last
            // to paint over the cost and the name; a later edit that appends a child after it would silently
            // lose that cue.
            VisualElement lastChild = _handSlotZero.ElementAt(_handSlotZero.childCount - 1);
            Assert.That(lastChild.ClassListContains(HudSelectors.CardSlotScrim), Is.True);
        }

        [Test]
        public void Constructor_CardSlotElement_AccentBarStartsHidden()
        {
            // THEN — construction happens in this fixture's SetUp, with no push against the slot since, so this
            // is the state a freshly built slot renders before MatchHudPresenter ever draws a card into it.
            VisualElement accentBar = _handSlotZero.Q(className: HudSelectors.CardSlotAccent);
            Assert.That(accentBar.ClassListContains(HudSelectors.IsHidden), Is.True);
        }

        [UnityTest]
        public IEnumerator SafeAreaElement_OnGeometryChanged_AppliesInlinePaddingFromTheSafeArea()
        {
            // GIVEN — an untouched style reports StyleKeyword.Null; RefreshSafeArea writing to it (even a zero
            // inset, which is what a windowed test runner with no notch reports) flips it to Undefined, so this
            // is a machine-independent proof the geometry-changed handler actually ran.
            var safeArea = new SafeAreaElement();
            _document.rootVisualElement.Add(safeArea);
            int frameBudget = LayoutSettleFrameBudget;

            // WHEN — no explicit call to RefreshSafeArea: the element re-pads itself from its own handler.
            while ((safeArea.style.paddingLeft.keyword == StyleKeyword.Null) && frameBudget-- > 0)
            {
                yield return null;
            }

            // THEN
            Assert.That((safeArea.style.paddingLeft.keyword, safeArea.style.paddingLeft.value.value >= 0f), Is.EqualTo((StyleKeyword.Undefined, true)));
        }

        // One factory per element type this sweep covers, so a regression names the type rather than reporting
        // a tuple mismatch.
        private static IEnumerable<TestCaseData> SelfIgnoringHudElementFactories()
        {
            yield return new TestCaseData((Func<VisualElement>)(() => new ScoreBadgeElement())).SetName("ScoreBadgeElement");
            yield return new TestCaseData((Func<VisualElement>)(() => new OpponentBadgeElement())).SetName("OpponentBadgeElement");
            yield return new TestCaseData((Func<VisualElement>)(() => new EnergyGaugeElement())).SetName("EnergyGaugeElement");
            yield return new TestCaseData((Func<VisualElement>)(() => new CountdownOverlayElement())).SetName("CountdownOverlayElement");
        }

        // Builds every element name and custom element type MatchHudView.uxml declares that CacheElements
        // actually resolves by name, directly onto the UIDocument's root, rather than cloning the authored
        // VisualTreeAsset: per Rule 6 in unity-testing.md, fixtures build in code unless the authored asset
        // itself is what is under test. Two elements the markup also declares are left out —
        // HudSelectors.HandStripDivider and HudSelectors.OvertimeBannerLabel — because CacheElements never
        // looks either up by name, so their absence changes nothing CacheElements can observe. It does change
        // what the equal-width test below measures: the real markup's divider takes a fixed width out of the
        // hand strip that this bare tree never has to give up, so that test's widths hold only because this
        // fixture omits the sibling that would compete for them, not because the two trees are identical. This
        // fixture stands in with the real production element types, wired under the real HudSelectors names, so
        // CacheElements resolves everything it does look up exactly as it would against the authored markup —
        // and carries no stylesheet, since most tests here assert C# state, not a CSS-driven one. The authored
        // UXML's own name-to-element contract is covered separately, in EditMode, by HudSelectorContractTests;
        // the two tests further up that load MatchHudView.uss are the exception the rule allows, because a CSS
        // cascade is exactly their subject.
        private void BuildHudTree(VisualElement root)
        {
            var background = new VisualElement { name = HudSelectors.Background };
            var safeArea = new SafeAreaElement { name = HudSelectors.SafeArea };
            var topBar = new VisualElement { name = HudSelectors.TopBar };

            _opponentScoreElement = new ScoreBadgeElement { name = HudSelectors.OpponentScore };
            var matchTimer = new Label { name = HudSelectors.MatchTimer };
            _opponentBadgeElement = new OpponentBadgeElement { name = HudSelectors.OpponentBadge };

            topBar.Add(_opponentScoreElement);
            topBar.Add(matchTimer);
            topBar.Add(_opponentBadgeElement);

            var boardWindow = new VisualElement { name = HudSelectors.BoardWindow };
            var bottomBar = new VisualElement { name = HudSelectors.BottomBar };
            var statusRow = new VisualElement { name = HudSelectors.StatusRow };

            _localScoreElement = new ScoreBadgeElement { name = HudSelectors.LocalScore };
            var emoteSlot = new Button { name = HudSelectors.EmoteSlot };
            statusRow.Add(_localScoreElement);
            statusRow.Add(emoteSlot);

            var catchUpLine = new Label { name = HudSelectors.CatchUpLine };
            _energyGaugeElement = new EnergyGaugeElement { name = HudSelectors.EnergyGauge };

            var handStrip = new VisualElement { name = HudSelectors.HandStrip };

            // HudSelectors.HandStrip doubles as the UXML name and the BEM block class on this element (see
            // HudSelectors' own remarks on its nine dual-purpose consts), matching MatchHudView.uxml's
            // name="hand-strip" class="hand-strip" — the class is what .hand-strip's flex-direction: row rule in
            // MatchHudView.uss actually matches against, and the two stylesheet-driven tests below need it.
            handStrip.AddToClassList(HudSelectors.HandStrip);

            _handSlotZero = new CardSlotElement { name = HudSelectors.HandSlotZero };
            _handSlotOne = new CardSlotElement { name = HudSelectors.HandSlotOne };
            _handSlotTwo = new CardSlotElement { name = HudSelectors.HandSlotTwo };
            _handSlotThree = new CardSlotElement { name = HudSelectors.HandSlotThree };
            _nextCardSlot = new CardSlotElement { name = HudSelectors.NextCardSlot };

            // Matches the modifier MatchHudView.uxml authors on next-card-slot (class="card-slot card-slot--next")
            // — nothing in C# ever applies it, so the fixture has to carry it for a card-slot--next test to mean
            // anything.
            _nextCardSlot.AddToClassList(HudSelectors.CardSlotNext);

            handStrip.Add(_handSlotZero);
            handStrip.Add(_handSlotOne);
            handStrip.Add(_handSlotTwo);
            handStrip.Add(_handSlotThree);
            handStrip.Add(_nextCardSlot);

            bottomBar.Add(statusRow);
            bottomBar.Add(catchUpLine);
            bottomBar.Add(_energyGaugeElement);
            bottomBar.Add(handStrip);

            safeArea.Add(topBar);
            safeArea.Add(boardWindow);
            safeArea.Add(bottomBar);

            background.Add(safeArea);

            var countdownScrim = new VisualElement { name = HudSelectors.CountdownScrim };
            _countdownOverlayElement = new CountdownOverlayElement { name = HudSelectors.CountdownOverlay };
            var overtimeBanner = new VisualElement { name = HudSelectors.OvertimeBanner };

            var outcomeOverlay = new VisualElement { name = HudSelectors.OutcomeOverlay };
            var outcomeTitle = new Label { name = HudSelectors.OutcomeTitle };
            var outcomeReason = new Label { name = HudSelectors.OutcomeReason };
            outcomeOverlay.Add(outcomeTitle);
            outcomeOverlay.Add(outcomeReason);

            background.Add(countdownScrim);
            background.Add(_countdownOverlayElement);
            background.Add(overtimeBanner);
            background.Add(outcomeOverlay);

            root.Add(background);
        }
    }
}
