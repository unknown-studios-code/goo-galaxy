using System.Reflection;
using GooGalaxy.Runtime.Board.Data;
using GooGalaxy.Runtime.Board.Models;
using GooGalaxy.Runtime.Shared.Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Runtime.Tests.EditMode.Board
{
    [TestFixture]
    public class IGridLayoutTests
    {
        [Test]
        public void GridLayoutSO_SetProperties_ExposesCorrectly()
        {
            // GIVEN
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            FieldInfo radiusField = typeof(GridLayoutSO).GetField("_gridRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo blockedField = typeof(GridLayoutSO).GetField("_blockedCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo initMethod = typeof(GridLayoutSO).GetMethod("InitializeBlockedCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);

            // WHEN
            radiusField.SetValue(gridLayout, 4);
            blockedField.SetValue(gridLayout, new[] { new Vector2Int(1, -1), new Vector2Int(2, -2) });
            initMethod.Invoke(gridLayout, null);

            // THEN
            Assert.AreEqual(4, gridLayout.GridRadius);
            Assert.AreEqual(2, gridLayout.BlockedCoordinates.Count);
            Assert.IsTrue(gridLayout.BlockedCoordinates.Contains(new HexCoordinates(1, -1)));
            Assert.IsTrue(gridLayout.BlockedCoordinates.Contains(new HexCoordinates(2, -2)));
        }

        [Test]
        public void GridLayoutSO_OnValidate_DeduplicatesBlockedCoordinates()
        {
            // GIVEN
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            FieldInfo blockedField = typeof(GridLayoutSO).GetField("_blockedCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo onValidateMethod = typeof(GridLayoutSO).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);

            blockedField.SetValue(gridLayout, new[] { new Vector2Int(1, 0), new Vector2Int(1, 0), new Vector2Int(0, 2) });

            // WHEN
            onValidateMethod.Invoke(gridLayout, null);
            var updatedBlocked = (Vector2Int[])blockedField.GetValue(gridLayout);

            // THEN
            Assert.AreEqual(2, updatedBlocked.Length);
            Assert.AreEqual(1, updatedBlocked[0].x);
            Assert.AreEqual(0, updatedBlocked[0].y);
            Assert.AreEqual(0, updatedBlocked[1].x);
            Assert.AreEqual(2, updatedBlocked[1].y);

            Assert.AreEqual(2, gridLayout.BlockedCoordinates.Count);
            Assert.IsTrue(gridLayout.BlockedCoordinates.Contains(new HexCoordinates(1, 0)));
            Assert.IsTrue(gridLayout.BlockedCoordinates.Contains(new HexCoordinates(0, 2)));
        }

        [Test]
        public void GridLayoutSO_BlockedCoordinates_LazyInit_WorksWithoutExplicitInit()
        {
            // GIVEN
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();

            // WHEN
            IReadOnlySet<HexCoordinates> coords = gridLayout.BlockedCoordinates;

            // THEN
            Assert.IsNotNull(coords);
            Assert.AreEqual(0, coords.Count);
        }

        [Test]
        public void GridLayoutSO_NegativeRadius_ClampsToZeroOnValidate()
        {
            // GIVEN
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            FieldInfo radiusField = typeof(GridLayoutSO).GetField("_gridRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo onValidateMethod = typeof(GridLayoutSO).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);

            radiusField.SetValue(gridLayout, -5);

            // WHEN
            onValidateMethod.Invoke(gridLayout, null);

            // THEN
            Assert.AreEqual(0, gridLayout.GridRadius, "Negative radius should be clamped to 0.");
        }

        [Test]
        public void GridLayoutSO_BlockedCoordinates_EmptyArray_ReturnsEmptySet()
        {
            // GIVEN
            GridLayoutSO gridLayout = ScriptableObject.CreateInstance<GridLayoutSO>();
            FieldInfo blockedField = typeof(GridLayoutSO).GetField("_blockedCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo initMethod = typeof(GridLayoutSO).GetMethod("InitializeBlockedCoordinates", BindingFlags.NonPublic | BindingFlags.Instance);

            blockedField.SetValue(gridLayout, System.Array.Empty<Vector2Int>());
            initMethod.Invoke(gridLayout, null);

            // THEN
            Assert.AreEqual(0, gridLayout.BlockedCoordinates.Count);
        }
    }
}
