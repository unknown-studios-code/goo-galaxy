using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.UI.Constants;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace GooGalaxy.Tests.EditMode.UI
{
    [TestFixture]
    public class HudSelectorContractTests
    {
        private const string UxmlPath = "Assets/UI/UXML/MatchHudView.uxml";

        // The structural hosts the real UXML marks picking-mode="Ignore" so a full-screen layout container never
        // swallows a click meant for the board or a widget beneath it. Named here because pickingMode, unlike an
        // element's presence, cannot be discovered by reflecting over HudSelectors alone — nothing states which
        // named elements are meant to be non-interactive containers versus interactive widgets.
        private static readonly string[] _structuralHostNames =
        {
            HudSelectors.Background,
            HudSelectors.SafeArea,
            HudSelectors.TopBar,
            HudSelectors.BoardWindow,
            HudSelectors.BottomBar,
            HudSelectors.StatusRow,
            HudSelectors.HandStrip,
            HudSelectors.CountdownScrim,
        };

        private VisualElement _clonedTree;

        [SetUp]
        public void SetUp()
        {
            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(visualTreeAsset, Is.Not.Null, $"Test setup expects '{UxmlPath}' to exist and import as a VisualTreeAsset.");

            _clonedTree = visualTreeAsset.CloneTree();
        }

        [TestCaseSource(nameof(ElementNameSelectors))]
        public void HudSelectorConst_ThatIsNotAUssClass_ResolvesToAnElementInTheUxml(string constFieldName)
        {
            // GIVEN
            string value = (string)typeof(HudSelectors).GetField(constFieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);

            // WHEN
            VisualElement resolved = _clonedTree.Q<VisualElement>(value);

            // THEN
            Assert.That(resolved, Is.Not.Null, $"HudSelectors.{constFieldName} (\"{value}\") does not resolve to an element in MatchHudView.uxml.");
        }

        [TestCaseSource(nameof(_structuralHostNames))]
        public void StructuralHost_InMatchHudViewUxml_ReportsPickingModeIgnore(string elementName)
        {
            // GIVEN
            VisualElement host = _clonedTree.Q<VisualElement>(elementName);
            Assert.That(host, Is.Not.Null, $"Test setup expects '{elementName}' to resolve in MatchHudView.uxml.");

            // WHEN / THEN
            Assert.That(host.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        // Every const HudSelectors declares as a UXML `name`, found by excluding the USS-class consts rather than
        // listing the name consts by hand, so a const added later is covered automatically. A const that is
        // *only* a USS class is marked by the "Block" suffix, the "Is" prefix, or a BEM "__element"/"--modifier"
        // separator; anything that escapes all three is a UXML name — see HudSelectors' own remarks. Nine name
        // consts (OpponentBadge, MatchTimer, EnergyGauge, CatchUpLine, HandStrip, EmoteSlot, CountdownOverlay,
        // OvertimeBanner, OutcomeOverlay) are also the block class that styles the element they name, one const
        // carrying both roles rather than two that could be renamed apart — so escaping the filter proves a
        // const names an element, never that it carries no class role too.
        private static IEnumerable<string> ElementNameSelectors()
        {
            foreach (FieldInfo field in typeof(HudSelectors).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                if (field.Name.EndsWith("Block") || field.Name.StartsWith("Is"))
                {
                    continue;
                }

                string value = (string)field.GetValue(null);

                if (value.Contains("__") || value.Contains("--"))
                {
                    continue;
                }

                yield return field.Name;
            }
        }
    }
}
