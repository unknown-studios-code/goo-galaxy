using System;
using System.Collections;
using System.Collections.Generic;
using GooGalaxy.Runtime.Cards.Data;
using GooGalaxy.Runtime.Cards.Models;
using GooGalaxy.Runtime.Cards.Presenters;
using GooGalaxy.Runtime.Deck.Data;
using GooGalaxy.Runtime.Deck.Models;
using GooGalaxy.Runtime.Deck.Presenters;
using GooGalaxy.Runtime.Energy.Models;
using GooGalaxy.Runtime.Energy.Presenters;
using GooGalaxy.Runtime.Match.Controllers;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.UI.Constants;
using GooGalaxy.Runtime.UI.Models;
using GooGalaxy.Runtime.UI.Presenters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.UI
{
    // PlayMode rather than EditMode: every behavior here is reached through MatchHudPresenter.OnEnable —
    // subscribing to MatchEvents and pushing the opening snapshot — and MonoBehaviour lifecycle callbacks do not
    // run outside Play Mode for a type without [ExecuteAlways].
    // GooGalaxy.Tests.PlayMode.Deck.DeckPresenterMatchLifecycleTests documents the identical split for the same
    // reason.
    [TestFixture]
    public class MatchHudPresenterTests
    {
        private const int LocalPlayerId = 1;
        private const int OpponentPlayerId = 2;

        // Margin above MatchHudPresenter's own private OvertimeBannerSeconds (2f), so the real-time wait below
        // reliably outlasts the async close regardless of machine speed.
        private const float OvertimeBannerWindowSeconds = 2.5f;

        private readonly List<Object> _spawned = new();

        private GameObject _presenterGO;
        private MatchHudPresenter _presenter;
        private MatchController _matchController;
        private EnergyPresenter _energyPresenter;
        private DeckPresenter _deckPresenter;
        private CardPresenter _cardPresenter;
        private FakeMatchHudView _view;

        [SetUp]
        public void SetUp()
        {
            MatchEvents.ResetEvents();

            _presenterGO = new GameObject(nameof(MatchHudPresenter));
            _presenterGO.SetActive(false);
            _spawned.Add(_presenterGO);
            _presenter = _presenterGO.AddComponent<MatchHudPresenter>();

            _matchController = BuildBareMatchController();
            _energyPresenter = BuildBareComponent<EnergyPresenter>("EnergyPresenter_Bare");
            _deckPresenter = BuildBareDeckPresenter();
            _cardPresenter = BuildBareComponent<CardPresenter>("CardPresenter_Bare");

            _presenter.Construct(_matchController, _energyPresenter, _deckPresenter, _cardPresenter);

            _view = new FakeMatchHudView();
            _presenter.SetViewForTests(_view);
        }

        [TearDown]
        public void TearDown()
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
        }

        [Test]
        public void OnEnable_CountdownSecondsAtSentinel_DoesNotPushCountdownSeconds()
        {
            // GIVEN — _countdownSeconds still holds its NoTimer sentinel: no MatchClockTicked has arrived yet, so
            // a HUD enabled mid-countdown must not render the overlay's "0" default under the scrim.

            // WHEN
            EnablePresenter();

            // THEN
            Assert.That(_view.CallLog, Does.Not.Contain(nameof(FakeMatchHudView.SetCountdownSeconds)));
        }

        [Test]
        public void HandleMatchStarted_HumanInPlayerOneSlot_ResolvesLocalSeatAsHome()
        {
            // GIVEN
            EnablePresenter();
            var config = new MatchConfiguration(0, new PlayerSlot(1, PlayerControl.LocalHuman), new PlayerSlot(2, PlayerControl.Machine), 60f, 3f, 30f);

            // WHEN
            MatchEvents.RaiseMatchStarted(config);

            // THEN
            Assert.That((_view.LocalPlayerId, _view.OpponentPlayerId), Is.EqualTo((1, 2)));
        }

        [Test]
        public void HandleMatchStarted_HumanInPlayerTwoSlot_SwapsHomeAndAway()
        {
            // GIVEN
            EnablePresenter();
            var config = new MatchConfiguration(0, new PlayerSlot(1, PlayerControl.Machine), new PlayerSlot(2, PlayerControl.LocalHuman), 60f, 3f, 30f);

            // WHEN
            MatchEvents.RaiseMatchStarted(config);

            // THEN
            Assert.That((_view.LocalPlayerId, _view.OpponentPlayerId), Is.EqualTo((2, 1)));
        }

        [Test]
        public void HandleMatchStarted_NeitherSeatAssigned_FallsBackToDefaultSeatsWithoutThrowing()
        {
            // GIVEN
            EnablePresenter();
            var config = new MatchConfiguration(0, default, default, 60f, 3f, 30f);

            // WHEN
            Assert.DoesNotThrow(() => MatchEvents.RaiseMatchStarted(config));

            // THEN
            Assert.That((_view.LocalPlayerId, _view.OpponentPlayerId), Is.EqualTo((LocalPlayerId, OpponentPlayerId)));
        }

        [Test]
        public void HandleMatchStarted_BothSeatsMachine_RendersPlayerOneAsHomeAndLogsOnce()
        {
            // GIVEN
            EnablePresenter();
            var config = new MatchConfiguration(0, new PlayerSlot(1, PlayerControl.Machine), new PlayerSlot(2, PlayerControl.Machine), 60f, 3f, 30f);
            LogAssert.Expect(
                LogType.Warning,
                string.Format(UiLogMessages.HudLocalSeatUnresolvedFormat, PlayerControl.Machine, PlayerControl.Machine, LocalPlayerId)
            );

            // WHEN
            MatchEvents.RaiseMatchStarted(config);

            // THEN
            Assert.That((_view.LocalPlayerId, _view.OpponentPlayerId), Is.EqualTo((LocalPlayerId, OpponentPlayerId)));
        }

        [Test]
        public void HandleMatchPhaseChanged_Loading_HidesHud()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Loading);

            // THEN
            Assert.That(_view.IsHudVisible, Is.False);
        }

        [Test]
        public void HandleMatchPhaseChanged_Countdown_ShowsTheHudWithTheCountdownVisible()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // THEN
            Assert.That((_view.IsHudVisible, _view.IsCountdownVisible), Is.EqualTo(((bool?)true, (bool?)true)));
        }

        [Test]
        public void HandleMatchPhaseChanged_Standard_ClearsTheCountdown()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // THEN
            Assert.That(_view.IsCountdownVisible, Is.False);
        }

        [Test]
        public void HandleMatchPhaseChanged_OvertimeCheck_FreezesTheLastStandardTimerValue()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            MatchEvents.RaiseMatchClockTicked(17);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.OvertimeCheck);

            // THEN
            Assert.That(_view.TimerSeconds, Is.EqualTo(17));
        }

        [Test]
        public void HandleMatchPhaseChanged_Overtime_ShowsTheEntryBanner()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // THEN
            Assert.That(_view.IsOvertimeBannerVisible, Is.True);
        }

        // Regression test: HideOvertimeBannerAsync used to guard on isActiveAndEnabled before resetting its
        // flag, and destroyCancellationToken does not fire on a disable — so a HUD disabled through the whole
        // banner window left the flag set, and every later PushAll re-showed the banner forever. Nothing is
        // observable from this fixture while the presenter is disabled, so this waits out the real window
        // rather than polling a condition — there is none to poll until the presenter is enabled again.
        [UnityTest]
        [Timeout(10000)]
        public IEnumerator HandleMatchPhaseChanged_OvertimeBannerDisabledThroughTheWindow_HidesOnceReenabled()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);
            _presenterGO.SetActive(false);

            // WHEN
            yield return new WaitForSecondsRealtime(OvertimeBannerWindowSeconds);
            _presenterGO.SetActive(true);

            // THEN
            Assert.That(_view.IsOvertimeBannerVisible, Is.False);
        }

        [Test]
        public void HandleMatchPhaseChanged_Ended_DoesNotShowTheOutcomeUntilMatchEndedArrives()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Ended);

            // THEN
            Assert.That(_view.IsOutcomeVisible, Is.False);
        }

        [Test]
        public void HandleMatchPhaseChanged_Results_ClearsTheOutcome()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Results);

            // THEN
            Assert.That(_view.IsOutcomeVisible, Is.False);
        }

        [Test]
        public void HandleMatchPhaseChanged_None_ClearsTheTimerWithoutRenderingZero()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);
            MatchEvents.RaiseMatchClockTicked(42);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.None);

            // THEN
            Assert.That(_view.TimerSeconds, Is.Null);
        }

        [Test]
        public void HandleMatchPhaseChanged_None_ClearsTheHandState()
        {
            // GIVEN
            CardId cardId = CreateAndRegisterCard("subject_alpha", "Subject Alpha", 2);
            EnablePresenter();
            MatchEvents.RaiseHandChanged(LocalPlayerId, new List<CardId> { cardId }, CardId.Empty);
            Assert.That(_view.HandSlots[0].IsFilled, Is.True, "Test setup expects the hand slot to be filled before the phase clears it.");

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.None);

            // THEN
            Assert.That(_view.HandSlots[0].IsFilled, Is.False);
        }

        // Regression test: ResetCatchUp publishes CatchUpChanged(id, false, 0) and self-heals when a match is
        // abandoned, but the matching overtime energy call publishes nothing — _isOvertime had no closing event
        // to recover it, so the gauge kept its overtime accent for a match nobody was still running. The timer
        // half of this abandonment is covered separately by ..._ClearsTheTimerWithoutRenderingZero above.
        [Test]
        public void HandleMatchPhaseChanged_None_ClearsTheOvertimeLatch()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);

            // WHEN
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.None);

            // THEN
            Assert.That(_view.EnergyState.Accent, Is.EqualTo(EnergyGaugeAccent.None));
        }

        [Test]
        public void HandleMatchClockTicked_DuringStandardPhase_UpdatesTheTimer()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // WHEN
            MatchEvents.RaiseMatchClockTicked(55);

            // THEN
            Assert.That(_view.TimerSeconds, Is.EqualTo(55));
        }

        [Test]
        public void HandleMatchClockTicked_DuringCountdownPhase_UpdatesTheCountdownRatherThanTheTimer()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Countdown);

            // WHEN
            MatchEvents.RaiseMatchClockTicked(3);

            // THEN
            Assert.That((_view.CountdownSeconds, _view.TimerSeconds), Is.EqualTo((3, (int?)null)));
        }

        [Test]
        public void HandleMatchClockTicked_OutsidePlayOrCountdown_IsIgnored()
        {
            // GIVEN
            EnablePresenter();
            int callCountBefore = _view.CallLog.Count;

            // WHEN
            MatchEvents.RaiseMatchClockTicked(10);

            // THEN
            Assert.That(_view.CallLog.Count, Is.EqualTo(callCountBefore));
        }

        [Test]
        public void HandleScoreChanged_LocalPlayer_UpdatesTheLocalScore()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseScoreChanged(LocalPlayerId, 7);

            // THEN
            Assert.That(_view.LocalScore, Is.EqualTo(7));
        }

        [Test]
        public void HandleScoreChanged_OpponentPlayer_UpdatesTheOpponentScore()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseScoreChanged(OpponentPlayerId, 4);

            // THEN
            Assert.That(_view.OpponentScore, Is.EqualTo(4));
        }

        [Test]
        public void HandleScoreChanged_UnknownPlayer_IsIgnored()
        {
            // GIVEN
            EnablePresenter();
            int callCountBefore = _view.CallLog.Count;

            // WHEN
            MatchEvents.RaiseScoreChanged(99, 4);

            // THEN
            Assert.That(_view.CallLog.Count, Is.EqualTo(callCountBefore));
        }

        [Test]
        public void HandleEnergyChanged_LocalPlayer_UpdatesTheEnergyGaugeFill()
        {
            // GIVEN
            EnablePresenter();
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(10f, 0f, 0f));

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 5f);

            // THEN
            Assert.That(_view.EnergyState.NormalizedFill, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void HandleEnergyChanged_OtherPlayer_IsIgnored()
        {
            // GIVEN
            EnablePresenter();
            int callCountBefore = _view.SetEnergyCallCount;

            // WHEN
            MatchEvents.RaiseEnergyChanged(OpponentPlayerId, 5f);

            // THEN
            Assert.That(_view.SetEnergyCallCount, Is.EqualTo(callCountBefore));
        }

        [Test]
        public void HandleEnergyChanged_FractionalEnergy_FloorsTheWholeNumberReadout()
        {
            // GIVEN
            EnablePresenter();
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(10f, 0f, 0f));

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 5.9f);

            // THEN
            Assert.That(_view.EnergyState.WholeEnergy, Is.EqualTo(5));
        }

        [Test]
        public void HandleEnergyChanged_EnergyAtCap_SetsTheAtCapAccent()
        {
            // GIVEN
            EnablePresenter();
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(10f, 0f, 0f));

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 10f);

            // THEN
            Assert.That(_view.EnergyState.Accent, Is.EqualTo(EnergyGaugeAccent.AtCap));
        }

        [Test]
        public void HandleEnergyChanged_EnergyBelowCap_SetsNoAccent()
        {
            // GIVEN
            EnablePresenter();
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(10f, 0f, 0f));

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 5f);

            // THEN
            Assert.That(_view.EnergyState.Accent, Is.EqualTo(EnergyGaugeAccent.None));
        }

        [Test]
        public void HandleEnergyChanged_OvertimeAndCatchUpBothActive_AccentPrefersOvertime()
        {
            // GIVEN
            EnablePresenter();
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(10f, 0f, 0f));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Overtime);
            MatchEvents.RaiseCatchUpChanged(LocalPlayerId, true, 5f);

            // WHEN
            MatchEvents.RaiseEnergyChanged(LocalPlayerId, 3f);

            // THEN
            Assert.That(_view.EnergyState.Accent, Is.EqualTo(EnergyGaugeAccent.Overtime));
        }

        [Test]
        public void HandleCatchUpChanged_LocalPlayerActivates_UpdatesTheCatchUpState()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseCatchUpChanged(LocalPlayerId, true, 12f);

            // THEN
            Assert.That((_view.IsCatchUpActive, _view.CatchUpRemainingSeconds), Is.EqualTo((true, 12)));
        }

        [Test]
        public void HandleCatchUpChanged_OtherPlayer_IsIgnored()
        {
            // GIVEN
            EnablePresenter();
            int callCountBefore = _view.SetCatchUpCallCount;

            // WHEN
            MatchEvents.RaiseCatchUpChanged(OpponentPlayerId, true, 5f);

            // THEN
            Assert.That(_view.SetCatchUpCallCount, Is.EqualTo(callCountBefore));
        }

        [Test]
        public void HandleHandChanged_LocalPlayerWithAFilledSlot_RendersTheCardInTheSlot()
        {
            // GIVEN
            CardId cardId = CreateAndRegisterCard("subject_alpha", "Subject Alpha", 3);
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseHandChanged(LocalPlayerId, new List<CardId> { cardId }, CardId.Empty);

            // THEN
            Assert.That((_view.HandSlots[0].DisplayName, _view.HandSlots[0].EnergyCost), Is.EqualTo(("Subject Alpha", 3)));
        }

        [Test]
        public void HandleHandChanged_UnresolvedCardId_RendersAnEmptySlotAndLogsAWarning()
        {
            // GIVEN
            EnablePresenter();
            var unresolvedCardId = new CardId("ghost_card");
            LogAssert.Expect(LogType.Warning, string.Format(UiLogMessages.HudCardDataMissingFormat, "ghost_card"));

            // WHEN
            MatchEvents.RaiseHandChanged(LocalPlayerId, new List<CardId> { unresolvedCardId }, CardId.Empty);

            // THEN
            Assert.That(_view.HandSlots[0].IsFilled, Is.False);
        }

        [Test]
        public void HandleHandChanged_OtherPlayer_IsIgnored()
        {
            // GIVEN
            EnablePresenter();
            int callCountBefore = _view.CallLog.Count;

            // WHEN
            MatchEvents.RaiseHandChanged(OpponentPlayerId, new List<CardId>(), CardId.Empty);

            // THEN
            Assert.That(_view.CallLog.Count, Is.EqualTo(callCountBefore));
        }

        [Test]
        public void HandleMatchEnded_LocalPlayerWins_SetsTheVictoryOutcome()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.Domination));

            // THEN
            Assert.That((_view.OutcomeTitle, _view.OutcomeReason), Is.EqualTo((HudText.OutcomeVictory, HudText.ReasonDomination)));
        }

        [Test]
        public void HandleMatchEnded_OpponentWins_SetsTheDefeatOutcome()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(OpponentPlayerId, MatchEndReason.TimeLimit));

            // THEN
            Assert.That((_view.OutcomeTitle, _view.OutcomeReason), Is.EqualTo((HudText.OutcomeDefeat, HudText.ReasonTimeLimit)));
        }

        [Test]
        public void HandleMatchEnded_Draw_SetsTheDrawOutcome()
        {
            // GIVEN
            EnablePresenter();

            // WHEN
            MatchEvents.RaiseMatchEnded(MatchOutcome.Drawn);

            // THEN
            Assert.That((_view.OutcomeTitle, _view.OutcomeReason), Is.EqualTo((HudText.OutcomeDraw, HudText.ReasonDraw)));
        }

        [Test]
        public void HandleMatchEnded_AfterTheCatchUpWindowClosed_PushesTheOutcomeAfterTheCatchUpClose()
        {
            // GIVEN — mirrors MatchController.EndMatch, which closes any open catch-up window before the phase
            // reaches Ended and MatchEnded is raised.
            EnablePresenter();
            MatchEvents.RaiseCatchUpChanged(LocalPlayerId, true, 5f);
            MatchEvents.RaiseCatchUpChanged(LocalPlayerId, false, 0f);
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Ended);

            // WHEN
            MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit));

            // THEN
            int catchUpCloseIndex = _view.CallLog.LastIndexOf("SetCatchUp:False");
            Assert.That(catchUpCloseIndex, Is.GreaterThanOrEqualTo(0), "Test setup expects the catch-up close to have been pushed.");

            int outcomeIndex = _view.CallLog.IndexOf(nameof(FakeMatchHudView.SetOutcome));
            Assert.That(outcomeIndex, Is.GreaterThan(catchUpCloseIndex));
        }

        [TestCaseSource(nameof(MatchEventRaises))]
        public void OnDisable_AfterDisabling_LeavesNoLiveEventSubscriptions(Action raiseEvent)
        {
            // GIVEN
            EnablePresenter();
            _presenterGO.SetActive(false);
            int callCountBefore = _view.CallLog.Count;

            // WHEN
            raiseEvent();

            // THEN
            Assert.That(_view.CallLog.Count, Is.EqualTo(callCountBefore));
        }

        [Test]
        public void RefreshAffordability_BeforeStandardPhase_LeavesEverySlotAffordable()
        {
            // GIVEN
            CardId cardId = CreateAndRegisterCard("subject_alpha", "Subject Alpha", 5);
            EnablePresenter();
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(10f, 0f, 0f));

            // WHEN — the phase is still None, so a card the empty balance cannot afford is not dimmed.
            MatchEvents.RaiseHandChanged(LocalPlayerId, new List<CardId> { cardId }, CardId.Empty);

            // THEN
            Assert.That(_view.HandSlotAffordability, Is.All.True);
        }

        [Test]
        public void RefreshAffordability_DuringStandardPhase_ReflectsWhetherEnergyCoversTheCost()
        {
            // GIVEN
            CardId cardId = CreateAndRegisterCard("subject_alpha", "Subject Alpha", 5);
            EnablePresenter();
            _energyPresenter.InitializePlayer(LocalPlayerId, new EnergyConfig(10f, 0f, 0f));
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // WHEN
            MatchEvents.RaiseHandChanged(LocalPlayerId, new List<CardId> { cardId }, CardId.Empty);

            // THEN
            Assert.That(_view.HandSlotAffordability[0], Is.False);
        }

        [Test]
        public void RefreshAffordability_EmptySlotDuringStandardPhase_StaysAffordable()
        {
            // GIVEN
            EnablePresenter();
            MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard);

            // WHEN
            MatchEvents.RaiseHandChanged(LocalPlayerId, new List<CardId>(), CardId.Empty);

            // THEN
            Assert.That(_view.HandSlotAffordability[0], Is.True);
        }

        // One raise per bus event MatchHudPresenter subscribes to, so a leaked subscription names the event
        // that leaked rather than reporting a count mismatch against the whole sweep.
        private static IEnumerable<TestCaseData> MatchEventRaises()
        {
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseMatchStarted(new MatchConfiguration(0)))).SetName("MatchStarted");
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseMatchPhaseChanged(MatchPhase.Standard))).SetName("MatchPhaseChanged");
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseMatchClockTicked(10))).SetName("MatchClockTicked");
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseScoreChanged(LocalPlayerId, 3))).SetName("ScoreChanged");
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseEnergyChanged(LocalPlayerId, 5f))).SetName("EnergyChanged");
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseCatchUpChanged(LocalPlayerId, true, 5f))).SetName("CatchUpChanged");
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseHandChanged(LocalPlayerId, new List<CardId>(), CardId.Empty))).SetName("HandChanged");
            yield return new TestCaseData((Action)(() => MatchEvents.RaiseMatchEnded(new MatchOutcome(LocalPlayerId, MatchEndReason.TimeLimit)))).SetName(
                "MatchEnded"
            );
        }

        private static T BuildBareComponent<T>(string goName)
            where T : Component
        {
            var go = new GameObject(goName);
            T component = go.AddComponent<T>();

            return component;
        }

        // Unlike the other three companions, MatchController.Start() auto-starts a match whenever
        // _isAutoStartEnabled is left at its Inspector default, which would call TryStartMatch on a bare
        // component with no MatchConfigSO assigned the moment a test actually yields a frame. Auto-start is
        // turned off before the object activates, ahead of the race SetMatchConfigForTests itself documents.
        private static MatchController BuildBareMatchController()
        {
            var go = new GameObject("MatchController_Bare");
            go.SetActive(false);
            MatchController component = go.AddComponent<MatchController>();
            component.SetMatchConfigForTests(null, 0, isAutoStartEnabled: false);
            go.SetActive(true);

            return component;
        }

        // Unlike the other three companions, DeckPresenter.Awake() asserts on its own Kit reference, so a bare
        // AddComponent on an already-active object logs an unhandled assertion and fails the test. Building it
        // inactive, assigning an (empty, never-dealt-from) Kit, and only then activating avoids that — the same
        // order DeckPresenterTests.BuildDeckPresenter and GameLifetimeScopeTests.CreateBoard use.
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

        private void EnablePresenter()
        {
            _presenterGO.SetActive(true);
        }

        private CardId CreateAndRegisterCard(string cardId, string displayName, int energyCost)
        {
            CardDataSO card = ScriptableObject.CreateInstance<CardDataSO>();
            card.SetAuthoredData(cardId, displayName, "Test description.", CardType.Troop, energyCost, false, false, false, false, 1, null);
            _spawned.Add(card);

            _cardPresenter.SetAuthoredCards(card);
            _cardPresenter.BuildRegistry();

            return new CardId(cardId);
        }
    }
}
