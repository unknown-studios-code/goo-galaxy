using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Utils;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.EditMode.Shared
{
    // The destroyed-behind-an-interface case is the whole reason this utility exists: `== null` on an
    // interface-typed field binds C#'s reference comparison, so a torn-down MonoBehaviour reads as alive and the
    // consumer proceeds against it. The fixture proves the utility sees through the seam that a direct comparison
    // does not, so it asserts the direct comparison alongside it rather than only the utility's answer.
    [TestFixture]
    public class UnityReferenceTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }

            _go = null;
        }

        [Test]
        public void IsUnavailable_LiveMonoBehaviourBehindAnInterface_ReturnsFalse()
        {
            // GIVEN
            _go = new GameObject(nameof(UnityReferenceTests));
            IDiscardLedger ledger = _go.AddComponent<FakeLedgerBehaviour>();

            // WHEN
            bool isUnavailable = UnityReference.IsUnavailable(ledger);

            // THEN
            Assert.That(isUnavailable, Is.False);
        }

        [Test]
        public void IsUnavailable_DestroyedMonoBehaviourBehindAnInterface_ReturnsTrue()
        {
            // GIVEN
            _go = new GameObject(nameof(UnityReferenceTests));
            IDiscardLedger ledger = _go.AddComponent<FakeLedgerBehaviour>();
            Object.DestroyImmediate(_go);
            _go = null;

            // WHEN
            bool isUnavailable = UnityReference.IsUnavailable(ledger);

            // THEN
            Assert.That(isUnavailable, Is.True);
        }

        [Test]
        public void IsUnavailable_DestroyedMonoBehaviourBehindAnInterface_SeesWhatAReferenceComparisonMisses()
        {
            // GIVEN — pins the defect the utility exists to prevent, so a future refactor back to `== null`
            // fails here instead of silently charging a player through a destroyed presenter.
            _go = new GameObject(nameof(UnityReferenceTests));
            IDiscardLedger ledger = _go.AddComponent<FakeLedgerBehaviour>();
            Object.DestroyImmediate(_go);
            _go = null;

            // WHEN
            bool referenceComparisonSaysNull = ledger == null;

            // THEN
            Assert.That((referenceComparisonSaysNull, UnityReference.IsUnavailable(ledger)), Is.EqualTo((false, true)));
        }

        [Test]
        public void IsUnavailable_PlainManagedImplementation_ReturnsFalse()
        {
            // GIVEN — nothing to unwrap, so the Object overload never enters the picture.
            IDiscardLedger ledger = new FakeLedger();

            // WHEN
            bool isUnavailable = UnityReference.IsUnavailable(ledger);

            // THEN
            Assert.That(isUnavailable, Is.False);
        }

        [Test]
        public void IsUnavailable_NullReference_ReturnsTrue()
        {
            // GIVEN

            // WHEN
            bool isUnavailable = UnityReference.IsUnavailable<IDiscardLedger>(null);

            // THEN
            Assert.That(isUnavailable, Is.True);
        }

        private sealed class FakeLedger : IDiscardLedger
        {
            public bool CanAffordDiscard(int playerId)
            {
                return true;
            }

            public bool TryPayForDiscard(int playerId)
            {
                return true;
            }

            public void RefundDiscard(int playerId) { }
        }

        private sealed class FakeLedgerBehaviour : MonoBehaviour, IDiscardLedger
        {
            public bool CanAffordDiscard(int playerId)
            {
                return true;
            }

            public bool TryPayForDiscard(int playerId)
            {
                return true;
            }

            public void RefundDiscard(int playerId) { }
        }
    }
}
