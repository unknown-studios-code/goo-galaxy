using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.UI.Constants;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace GooGalaxy.Tests.PlayMode.UI
{
    // Flow-named per Rule 2's PlayMode exception in unity-testing.md: this pins MatchHudView.uss's
    // content-height rule -- top and bottom bars take their content height, the board window absorbs the rest --
    // through the real UXML and stylesheets at two device ratios. No single type owns that outcome, so this is
    // named for the rule rather than for MatchHudView.
    [TestFixture]
    public class MatchHudPortraitRatioTests
    {
        private const string MatchHudViewUxmlPath = "Assets/UI/UXML/MatchHudView.uxml";

        // The device width both tested ratios share, matching MatchHudPanelSettings.asset's own reference width.
        private const int ReferenceWidth = 1080;

        private const int SixteenByNineHeight = 1920;
        private const int TwentyByNineHeight = 2400;

        // The DoD figure this fixture exists to pin: a 20:9 phone reports 480px more height than a 16:9 one at
        // the same 1080 width, and every one of those pixels is supposed to reach the board window.
        private const int ExpectedBoardWindowHeightDelta = TwentyByNineHeight - SixteenByNineHeight;

        // Yoga can hand a remainder pixel to one box and not its neighbour, so an equality below is asserted
        // within this margin rather than exactly -- same basis as MatchHudViewTests.WidthToleranceInPixels.
        private const float HeightToleranceInPixels = 1f;

        private const int LayoutSettleFrameBudget = 10;

        private readonly List<Object> _spawned = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
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
        public IEnumerator Layout_AtSixteenByNine_EveryNamedElementResolvesNonNegativeAndFitsItsParent()
        {
            // GIVEN — this proves the USS layout rule at one panel size. It does not exercise device scaling:
            // BuildPanelAsync writes the panel's pixel size directly, with no platform DPI in play. It does not
            // exercise the safe area either — Screen.safeArea reports the full window in a test runner, so
            // SafeAreaElement has nothing to inset here. Both stay a manual Device Simulator check, per the task.
            VisualElement root = null;

            // WHEN
            yield return BuildPanelAsync(ReferenceWidth, SixteenByNineHeight, built => root = built);

            // THEN
            AssertNoNamedElementIsDegenerate(root, SixteenByNineHeight);
            AssertHandStripChildrenFitInsideTheStrip(root, SixteenByNineHeight);
        }

        [UnityTest]
        public IEnumerator Layout_AtTwentyByNine_EveryNamedElementResolvesNonNegativeAndFitsItsParent()
        {
            // GIVEN — see Layout_AtSixteenByNine_EveryNamedElementResolvesNonNegativeAndFitsItsParent for what
            // this fixture does and does not prove; identical here.
            VisualElement root = null;

            // WHEN
            yield return BuildPanelAsync(ReferenceWidth, TwentyByNineHeight, built => root = built);

            // THEN
            AssertNoNamedElementIsDegenerate(root, TwentyByNineHeight);
            AssertHandStripChildrenFitInsideTheStrip(root, TwentyByNineHeight);
        }

        [UnityTest]
        public IEnumerator Layout_BoardWindowHeight_AbsorbsTheDifferenceBetweenPortraitRatios()
        {
            // GIVEN
            VisualElement sixteenByNineRoot = null;
            VisualElement twentyByNineRoot = null;
            yield return BuildPanelAsync(ReferenceWidth, SixteenByNineHeight, built => sixteenByNineRoot = built);
            yield return BuildPanelAsync(ReferenceWidth, TwentyByNineHeight, built => twentyByNineRoot = built);

            // WHEN
            float sixteenByNineBoardHeight = RequireElement(sixteenByNineRoot, HudSelectors.BoardWindow).resolvedStyle.height;
            float twentyByNineBoardHeight = RequireElement(twentyByNineRoot, HudSelectors.BoardWindow).resolvedStyle.height;

            // THEN
            Assert.That(twentyByNineBoardHeight - sixteenByNineBoardHeight, Is.EqualTo(ExpectedBoardWindowHeightDelta).Within(HeightToleranceInPixels));
        }

        [UnityTest]
        public IEnumerator Layout_TopBarHeight_StaysEqualAcrossPortraitRatios()
        {
            // GIVEN
            VisualElement sixteenByNineRoot = null;
            VisualElement twentyByNineRoot = null;
            yield return BuildPanelAsync(ReferenceWidth, SixteenByNineHeight, built => sixteenByNineRoot = built);
            yield return BuildPanelAsync(ReferenceWidth, TwentyByNineHeight, built => twentyByNineRoot = built);

            // WHEN
            float sixteenByNineTopBarHeight = RequireElement(sixteenByNineRoot, HudSelectors.TopBar).resolvedStyle.height;
            float twentyByNineTopBarHeight = RequireElement(twentyByNineRoot, HudSelectors.TopBar).resolvedStyle.height;

            // THEN
            Assert.That(twentyByNineTopBarHeight, Is.EqualTo(sixteenByNineTopBarHeight).Within(HeightToleranceInPixels));
        }

        [UnityTest]
        public IEnumerator Layout_BottomBarHeight_StaysEqualAcrossPortraitRatios()
        {
            // GIVEN
            VisualElement sixteenByNineRoot = null;
            VisualElement twentyByNineRoot = null;
            yield return BuildPanelAsync(ReferenceWidth, SixteenByNineHeight, built => sixteenByNineRoot = built);
            yield return BuildPanelAsync(ReferenceWidth, TwentyByNineHeight, built => twentyByNineRoot = built);

            // WHEN
            float sixteenByNineBottomBarHeight = RequireElement(sixteenByNineRoot, HudSelectors.BottomBar).resolvedStyle.height;
            float twentyByNineBottomBarHeight = RequireElement(twentyByNineRoot, HudSelectors.BottomBar).resolvedStyle.height;

            // THEN
            Assert.That(twentyByNineBottomBarHeight, Is.EqualTo(sixteenByNineBottomBarHeight).Within(HeightToleranceInPixels));
        }

        // Every const HudSelectors declares as a UXML name, found the same way HudSelectorContractTests finds
        // them in EditMode: by excluding the USS-class consts rather than listing the name consts by hand, so a
        // name added later is covered automatically. See HudSelectors' own remarks for the "Block" suffix, "Is"
        // prefix and BEM separator filter this relies on.
        private static IEnumerable<string> ElementNames()
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

                yield return value;
            }
        }

        private static VisualElement RequireElement(VisualElement root, string elementName)
        {
            VisualElement element = root.Q<VisualElement>(elementName);
            Assert.That(element, Is.Not.Null, $"Test setup expects '{elementName}' to resolve under the built panel.");

            return element;
        }

        // GreaterThanOrEqualTo(0f) catches both halves of "non-negative, non-NaN" in one constraint: every
        // comparison against a NaN operand evaluates false in .NET, so a NaN width or height fails this the same
        // way a negative one would.
        private static void AssertNoNamedElementIsDegenerate(VisualElement root, int screenHeight)
        {
            foreach (string elementName in ElementNames())
            {
                VisualElement element = RequireElement(root, elementName);
                Assert.That(
                    element.resolvedStyle.width,
                    Is.GreaterThanOrEqualTo(0f),
                    $"'{elementName}' resolved width {element.resolvedStyle.width} at screen height {screenHeight}."
                );
                Assert.That(
                    element.resolvedStyle.height,
                    Is.GreaterThanOrEqualTo(0f),
                    $"'{elementName}' resolved height {element.resolvedStyle.height} at screen height {screenHeight}."
                );
            }
        }

        // The five card slots and the divider are hand-strip's whole child list (four hand slots, the divider,
        // and the queued slot), positioned by flexbox alone. A child's layout rect is reported relative to its
        // parent's own content-box origin -- confirmed against the live editor -- so comparing xMax straight
        // against the strip's contentRect.xMax needs no coordinate conversion.
        private static void AssertHandStripChildrenFitInsideTheStrip(VisualElement root, int screenHeight)
        {
            VisualElement handStrip = RequireElement(root, HudSelectors.HandStrip);
            float rightEdge = handStrip.contentRect.xMax;

            for (int i = 0; i < handStrip.childCount; i++)
            {
                VisualElement child = handStrip.ElementAt(i);
                Assert.That(
                    child.layout.xMax,
                    Is.LessThanOrEqualTo(rightEdge + HeightToleranceInPixels),
                    $"hand-strip child #{i} ('{child.name}') right edge {child.layout.xMax} exceeds the strip's content width {rightEdge} at screen height {screenHeight}."
                );
            }
        }

        private IEnumerator BuildPanelAsync(int width, int height, Action<VisualElement> onRootReady)
        {
            var documentGO = new GameObject(nameof(MatchHudPortraitRatioTests));
            _spawned.Add(documentGO);

            PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            // World space with a fixed size gives the panel its pixel dimensions directly, in world units read
            // 1:1 as pixels at ConstantPixelSize scale 1 -- confirmed against the live editor. The mode this
            // fixture would otherwise reach for, ScaleWithScreenSize behind a render-texture target (matching
            // MatchHudPanelSettings.asset), resolved correctly under Edit Mode probing but never left an
            // un-styled default once genuinely in Play Mode -- same configuration, measured both ways, two
            // different outcomes. World space is the one path proven to settle deterministically in both, and it
            // must be set before the panel is first queried: changing it on a document already queried left the
            // resolved size stuck at its old value, also confirmed live.
            panelSettings.renderMode = PanelRenderMode.WorldSpace;
            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.scale = 1f;
            _spawned.Add(panelSettings);

            UIDocument document = documentGO.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = new Vector2(width, height);

            VisualTreeAsset visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MatchHudViewUxmlPath);
            Assert.That(visualTreeAsset, Is.Not.Null, $"Test setup expects '{MatchHudViewUxmlPath}' to exist and import as a VisualTreeAsset.");
            document.visualTreeAsset = visualTreeAsset;

            int frameBudget = LayoutSettleFrameBudget;

            // Polls the root reaching the requested height rather than trusting the first frame it exists: the
            // panel resolves against a stale (NaN) layout for at least one frame, and the requested height
            // differs from that NaN default, so this cannot exit before the size actually lands.
            while (frameBudget-- > 0)
            {
                VisualElement candidateRoot = document.rootVisualElement;

                if ((candidateRoot != null) && Mathf.Approximately(candidateRoot.resolvedStyle.height, height))
                {
                    onRootReady(candidateRoot);
                    yield break;
                }

                yield return null;
            }

            Assert.Fail($"Test setup expects the panel to settle to height {height} within the layout settle budget.");
        }
    }
}
