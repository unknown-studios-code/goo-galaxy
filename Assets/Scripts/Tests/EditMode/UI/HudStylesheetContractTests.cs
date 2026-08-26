using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.UI.Constants;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace GooGalaxy.Tests.EditMode.UI
{
    [TestFixture]
    public class HudStylesheetContractTests
    {
        private const string MatchHudViewUssPath = "Assets/UI/USS/MatchHudView.uss";

        private const string DesignTokensUssPath = "Assets/UI/USS/DesignTokens.uss";

        private const string KnownPresentClassSelector = "energy-gauge__fill";

        // Nine consts double as a UXML element `name` and the BEM block class that styles that same element
        // (see HudSelectors' own remarks), so they carry no "Block" suffix, "Is" prefix, or BEM separator and
        // escape the filter HudSelectorContractTests reflects over to find UXML names. Listing them here is
        // what makes this fixture's source set the *union* of that filter's complement with these nine, rather
        // than a plain complement of the other fixture's set — escaping that filter proves a const names an
        // element, never that it carries no class role too.
        private static readonly HashSet<string> _dualPurposeElementNames = new()
        {
            nameof(HudSelectors.OpponentBadge),
            nameof(HudSelectors.MatchTimer),
            nameof(HudSelectors.EnergyGauge),
            nameof(HudSelectors.CatchUpLine),
            nameof(HudSelectors.HandStrip),
            nameof(HudSelectors.EmoteSlot),
            nameof(HudSelectors.CountdownOverlay),
            nameof(HudSelectors.OvertimeBanner),
            nameof(HudSelectors.OutcomeOverlay),
        };

        private HashSet<string> _declaredClassSelectors;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            StyleSheet matchHudStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(MatchHudViewUssPath);
            Assert.That(matchHudStyleSheet, Is.Not.Null, $"Test setup expects '{MatchHudViewUssPath}' to exist and import as a StyleSheet.");

            StyleSheet designTokensStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(DesignTokensUssPath);
            Assert.That(designTokensStyleSheet, Is.Not.Null, $"Test setup expects '{DesignTokensUssPath}' to exist and import as a StyleSheet.");

            _declaredClassSelectors = new HashSet<string>();
            CollectDeclaredClassSelectors(matchHudStyleSheet, _declaredClassSelectors);
            CollectDeclaredClassSelectors(designTokensStyleSheet, _declaredClassSelectors);
        }

        [TestCaseSource(nameof(UssClassSelectorConstNames))]
        public void HudSelectorConst_ThatIsAUssClass_MatchesADeclaredSelectorInAStylesheet(string constFieldName)
        {
            // GIVEN
            string value = (string)typeof(HudSelectors).GetField(constFieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);

            // WHEN
            bool isDeclared = _declaredClassSelectors.Contains(value);

            // THEN
            Assert.That(
                isDeclared,
                Is.True,
                $"HudSelectors.{constFieldName} (\"{value}\") does not match a declared selector in MatchHudView.uss or DesignTokens.uss."
            );
        }

        [Test]
        public void DeclaredClassSelectors_KnownPresentClass_ContainsEnergyGaugeFill()
        {
            // THEN — proves the parsed-selector reading itself works, independently of HudSelectors' contents
            Assert.That(_declaredClassSelectors, Does.Contain(KnownPresentClassSelector));
        }

        // Every const HudSelectors declares as a USS class, found by the same rule HudSelectorContractTests
        // uses to find UXML names, inverted, and unioned with the nine dual-purpose consts that rule cannot
        // see: the "Block" suffix, the "Is" prefix, and the BEM "__element"/"--modifier" separators mark a
        // const as *only* a USS class; anything that escapes all three but is also listed in
        // _dualPurposeElementNames names a class too. A const added later is covered automatically as long as
        // it follows one of those three naming forms.
        private static IEnumerable<string> UssClassSelectorConstNames()
        {
            foreach (FieldInfo field in typeof(HudSelectors).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string))
                {
                    continue;
                }

                string value = (string)field.GetValue(null);
                bool isUssClassConst =
                    field.Name.EndsWith("Block")
                    || field.Name.StartsWith("Is")
                    || value.Contains("__")
                    || value.Contains("--")
                    || _dualPurposeElementNames.Contains(field.Name);

                if (isUssClassConst)
                {
                    yield return field.Name;
                }
            }
        }

        // StyleSheet's parsed selector data — StyleRule, StyleComplexSelector, StyleSelector and
        // StyleSelectorPart — is internal to UnityEngine.UIElementsModule, and not merely unexported: naming
        // any of those types directly (`typeof(StyleSelector)`) fails to compile from this assembly with
        // "inaccessible due to its protection level", and even StyleSheet's own `rules` property carries an
        // internal getter, so a `var` holding `styleSheet.rules` would fail for the same reason at the very
        // first property access. Reflection is the only way to read what Unity actually imported from the
        // .uss file, rather than parsing the text ourselves and asserting against a hand-rolled re-parse of it.
        private static void CollectDeclaredClassSelectors(StyleSheet styleSheet, HashSet<string> destination)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            object rules = typeof(StyleSheet).GetProperty("rules", flags).GetValue(styleSheet);

            foreach (object rule in (IEnumerable)rules)
            {
                object complexSelectors = rule.GetType().GetProperty("complexSelectors", flags).GetValue(rule);

                foreach (object complexSelector in (IEnumerable)complexSelectors)
                {
                    object selectors = complexSelector.GetType().GetProperty("selectors", flags).GetValue(complexSelector);

                    foreach (object selector in (IEnumerable)selectors)
                    {
                        object parts = selector.GetType().GetProperty("parts", flags).GetValue(selector);

                        foreach (object part in (IEnumerable)parts)
                        {
                            // StyleSelectorType is itself inaccessible to name, so the enum value carried by
                            // `part.type` is compared by its ToString() rather than against
                            // StyleSelectorType.Class.
                            object partType = part.GetType().GetProperty("type", flags).GetValue(part);

                            if (partType.ToString() != "Class")
                            {
                                continue;
                            }

                            string className = (string)part.GetType().GetProperty("value", flags).GetValue(part);
                            destination.Add(className);
                        }
                    }
                }
            }
        }
    }
}
