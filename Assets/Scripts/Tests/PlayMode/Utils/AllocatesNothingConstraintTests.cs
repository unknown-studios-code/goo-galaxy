using System.Collections;
using GooGalaxy.Tests.Utils;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine.TestTools;

namespace GooGalaxy.Tests.PlayMode.Utils
{
    // The EditMode fixture of the same name covers the constraint's contract. This one exists so the PlayMode suite, whose
    // steady-state allocation tests use the same constraint, cannot pass vacuously if the recorder stops counting in
    // Play Mode.
    [TestFixture]
    public class AllocatesNothingConstraintTests
    {
        private readonly object[] _sink = new object[1];

        [TearDown]
        public void TearDown()
        {
            _sink[0] = null;
        }

        [UnityTest]
        [Category("Allocation")]
        [Timeout(5000)]
        public IEnumerator ApplyTo_DelegateThatAllocatesAfterAFrameHasPassed_Fails()
        {
            // GIVEN
            TestDelegate code = AllocateAnObject;
            code();
            yield return null;

            // WHEN
            ConstraintResult result = new AllocatesNothingConstraint().ApplyTo(code);

            // THEN
            Assert.That(result.IsSuccess, Is.False);
        }

        private void AllocateAnObject()
        {
            _sink[0] = new object();
        }
    }
}
