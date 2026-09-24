using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Presenters;
using GooGalaxy.Runtime.UI.Views;
using GooGalaxy.Runtime.UI.Views.Elements;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.UI
{
    // Flow-named per Rule 2's PlayMode exception in unity-testing.md: the steady state this fixture measures is
    // produced by MatchEvents, MatchHudPresenter and MatchHudView acting together, so no single type owns it.
    [TestFixture]
    public class MatchHudSteadyStateAllocationTests
    {
        private const int LocalPlayerId = 1;

        // The GDD's authored Energy figures (see EnergyPresenter's own default field values): a 10-point cap
        // regenerating at one point every 2.8 seconds, opened here below the cap so the run measures active
        // regeneration rather than a bar parked at the ceiling.
        private const float EnergyCap = 10f;
        private const float EnergyRegenRate = 1f / 2.8f;
        private const float EnergyStartingValue = 2f;

        // Matches EnergyPresenter's own publish quantum: energy is reported to have moved once it has drifted by
        // this much, so this is the step a real regen tick would actually publish.
        private const float EnergyPublishQuantum = 0.05f;

        // EnergyChanged publishes at ~7.14 Hz (regen 1/2.8, quantum 0.05); floored to a whole number per
        // simulated second so the loop below has a fixed iteration count, per Rule 7 in unity-testing.md.
        private const int EnergyChangedPerSecond = 7;

        private const int MatchClockTickedPerSecond = 1;

        private const int SimulatedSeconds = 10;

        // Both wells within HudClockFormatter's 0..599 cached range, so formatting never falls through to the
        // allocating composition branch.
        private const int MatchClockStartSeconds = 90;

        private readonly List<Object> _spawned = new();

        private GameObject _presenterGO;
        private MatchHudPresenter _presenter;
        private MatchHudView _view;
        private MatchController _matchController;
        private EnergyPresenter _energyPresenter;
        private DeckPresenter _deckPresenter;
        private CardPresenter _cardPresenter;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MatchEvents.ResetEvents();

            foreach (Object created in _spawned)
            {
                if (created != null)
                {
                    Object.Destroy(created);
                }
            }

            _spawned.Clear();

            yield return null;
        }

        [UnityTest]
        [Category("Allocation")]
        public IEnumerator SteadyState_ClockTickingAndEnergyRegenerating_AllocatesNoManagedMemory()
        {
            // GIVEN — NotAllocatingGCMemory() scopes its measurement to the delegate: only what runs on the
            // calling thread, inside RaiseSteadyStateEvents' own call stack, counts. EnergyPresenter.Update()
            // ticks on the real Unity frame loop, entirely outside that call stack, so its own regen-driven
            // publishing cannot land in the measurement — which is what made the process-wide ProfilerRecorder
            // attempt at this same test unusable (an 8x run-to-run spread, tracked back to that exact ticking).
            // The same delegate-scoping means this would not see an allocation from a scheduled callback or
            // another thread; MatchHudPresenter's handlers run synchronously on this one, so neither applies
            // here. This proves allocation in the Editor only; it says nothing about the < 0.5 ms/frame device
            // budget, which still needs a hardware capture.
            yield return BuildPresenterAndViewAsync();
            WarmUpSteadyStatePaths();

            // WHEN / THEN — the act is the delegate itself, which the constraint both runs and measures.
            Assert.That(RaiseSteadyStateEvents, NotAllocatingGCMemory());
        }

        // Raises ten simulated seconds of the steady state in one synchronous call, so the whole sequence sits
        // inside the delegate NotAllocatingGCMemory() measures — no yield, no frame boundary, nothing here can
        // interleave with anything this fixture does not control.
        private static void RaiseSteadyStateEvents()
        {
            float energy = EnergyStartingValue;
            int remainingSeconds = MatchClockStartSeconds;

            for (int second = 0; second < SimulatedSeconds; second++)
            {
                for (int tick = 0; tick < EnergyChangedPerSecond; tick++)
                {
                    energy += EnergyPublishQuantum;
                    MatchEvents.RaiseEnergyChanged(LocalPlayerId, energy);
                }

                remainingSeconds -= MatchClockTickedPerSecond;
                MatchEvents.RaiseMatchClockTicked(remainingSeconds);
            }
        }

        // Builds every element name and custom element type MatchHudView.uxml declares that CacheElements
        // actually resolves by name, the same tree MatchHudViewTests.BuildHudTree builds and for the same
        // reason (Rule 6 in unity-testing.md: fixtures build in code unless the authored asset itself is what is
        // under test — here it is the presenter-to-view allocation path, not the markup). Duplicated rather than
        // shared, because the two fixtures need it for genuinely different reasons and Rule 6 of the testing
        // rules reserves a shared base for when several fixtures need the identical world for the identical
        // reason.
        private static void BuildHudTree(VisualElement root)
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

            var handSlotZero = new CardSlotElement { name = HudSelectors.HandSlotZero };
            var handSlotOne = new CardSlotElement { name = HudSelectors.HandSlotOne };
            var handSlotTwo = new CardSlotElement { name = HudSelectors.HandSlotTwo };
            var handSlotThree = new CardSlotElement { name = HudSelectors.HandSlotThree };
            var nextCardSlot = new CardSlotElement { name = HudSelectors.NextCardSlot };

            handStrip.Add(handSlotZero);
            handStrip.Add(handSlotOne);
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

        private static T BuildBareComponent<T>(string goName)
            where T : Component
        {
            var go = new GameObject(goName);
            T component = go.AddComponent<T>();

            return component;
        }

        // Mirrors MatchHudPresenterTests.BuildBareMatchController: auto-start is turned off before the object
        // activates, ahead of the race SetMatchConfigForTests itself documents.
        private static MatchController BuildBareMatchController()
        {
            var go = new GameObject("MatchController_Bare");
            go.SetActive(false);
            MatchController component = go.AddComponent<MatchController>();
            component.SetMatchConfigForTests(null, 0, isAutoStartEnabled: false);
            go.SetActive(true);

            return component;
        }

        // Fully qualified rather than reached through a `using UnityEngine.TestTools.Constraints;`, which would
        // shadow NUnit.Framework.Is (used unqualified throughout this fixture) and force every other Is.* call
        // here to disambiguate. See HudClockFormatterTests.NotAllocatingGCMemory for the .ApplyTo() pitfall this
        // static form sidesteps — confirmed live against the same allocating and non-allocating delegates.
        private static UnityEngine.TestTools.Constraints.AllocatingGCMemoryConstraint NotAllocatingGCMemory()
        {
            return UnityEngine.TestTools.Constraints.ConstraintExtensions.AllocatingGCMemory(Is.Not);
        }

        private void WarmUpSteadyStatePaths()
        {
            // Pays for every one-time cost the measured delegate must not see: the energy value-text table
            // built on the first push at this cap, HudClockFormatter's own cache (built at type initialization,
            // but not guaranteed touched yet if this is the first fixture in the session to reach it), the JIT
            // for every method on the call path, and opening the play window so a clock tick actually reaches
            // PushTimer instead of being dropped as a countdown value.
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            MatchEvents.RaiseMatchClockTicked(MatchClockStartSeconds);
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, EnergyStartingValue);
        }

        private IEnumerator BuildPresenterAndViewAsync()
        {
            _matchController = BuildBareMatchController();
            _spawned.Add(_matchController.gameObject);

            _energyPresenter = BuildBareComponent<EnergyPresenter>("EnergyPresenter_Bare");
            _spawned.Add(_energyPresenter.gameObject);
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(EnergyCap, EnergyRegenRate, EnergyStartingValue));

            _deckPresenter = BuildBareDeckPresenter();

            _cardPresenter = BuildBareComponent<CardPresenter>("CardPresenter_Bare");
            _spawned.Add(_cardPresenter.gameObject);

            var documentGO = new GameObject(nameof(MatchHudView));
            _spawned.Add(documentGO);

            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _spawned.Add(panelSettings);

            UIDocument document = documentGO.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;

            int rootWaitBudget = 10;

            while ((document.rootVisualElement == null) && rootWaitBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(document.rootVisualElement, Is.Not.Null, "Test setup expects the UIDocument to have created its root within the wait budget.");

            BuildHudTree(document.rootVisualElement);

            _view = documentGO.AddComponent<MatchHudView>();

            int panelWaitBudget = 10;

            while (!_view.IsPanelReady && panelWaitBudget-- > 0)
            {
                yield return null;
            }

            Assert.That(_view.IsPanelReady, Is.True, "Test setup expects the panel to have initialized within the layout settle budget.");

            _presenterGO = new GameObject(nameof(MatchHudPresenter));
            _presenterGO.SetActive(false);
            _spawned.Add(_presenterGO);
            _presenter = _presenterGO.AddComponent<MatchHudPresenter>();
            _presenter.Construct(_matchController, _energyPresenter, _deckPresenter, _cardPresenter);

            // The real view, not a fake: passed through the same test seam MatchHudPresenterTests uses for its
            // double, because MatchHudView also implements IMatchHudView. A fake records calls with no engine
            // cost behind them, which is exactly the cost this fixture exists to measure.
            _presenter.SetViewForTests(_view);
            _presenterGO.SetActive(true);
        }

        // Mirrors MatchHudPresenterTests.BuildBareDeckPresenter: DeckPresenter.Awake() asserts on its own Kit
        // reference, so the Kit is assigned before the object activates.
        private DeckPresenter BuildBareDeckPresenter()
        {
            var go = new GameObject("DeckPresenter_Bare");
            go.SetActive(false);
            _spawned.Add(go);

            DeckPresenter presenter = go.AddComponent<DeckPresenter>();

            KitDataSO kit = ScriptableObject.CreateInstance<KitDataSO>();
            _spawned.Add(kit);
            presenter.SetKit(kit, DeckState.DefaultHandSize);

            go.SetActive(true);

            return presenter;
        }
    }
}
