using GooGalaxy.Runtime.UI.Models;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.UI
{
    // Fill clamping, the at-cap boundary, the whole-number readout floor, and the overtime-over-catch-up accent
    // precedence are not this struct's own behavior: the struct only stores what it is given, with no clamping
    // or resolution logic of its own. Fill/at-cap/accent-precedence are resolved by MatchHudPresenter and are
    // covered there; the fill clamp a gauge applies at draw time is covered in MatchHudViewTests, against the
    // real EnergyGaugeElement. Reflecting a behavior that does not exist on the type under test would either
    // assert nothing meaningful or assert a private implementation detail — see the class remarks on both.
    [TestFixture]
    public class EnergyGaugeStateTests
    {
        [Test]
        public void Constructor_GivenValues_PreservesEveryField()
        {
            // GIVEN

            // WHEN
            var state = new EnergyGaugeState(0.75f, 7, 10, EnergyGaugeAccent.CatchUp);

            // THEN
            Assert.That((state.NormalizedFill, state.WholeEnergy, state.MaxEnergy, state.Accent), Is.EqualTo((0.75f, 7, 10, EnergyGaugeAccent.CatchUp)));
        }

        [Test]
        public void Empty_DefaultValue_IsZeroFillZeroEnergyAndNoAccent()
        {
            // GIVEN

            // WHEN
            EnergyGaugeState empty = EnergyGaugeState.Empty;

            // THEN
            Assert.That((empty.NormalizedFill, empty.WholeEnergy, empty.MaxEnergy, empty.Accent), Is.EqualTo((0f, 0, 0, EnergyGaugeAccent.None)));
        }
    }
}
