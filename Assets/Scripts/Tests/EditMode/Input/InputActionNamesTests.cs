using System.Collections.Generic;
using System.Reflection;
using GooGalaxy.Runtime.Input.Constants;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace GooGalaxy.Tests.EditMode.Input
{
    [TestFixture]
    public class InputActionNamesTests
    {
        private const string InputActionsPath = "Assets/Settings/Input/MatchInput.inputactions";

        private InputActionAsset _inputActions;
        private InputActionMap _matchMap;

        [SetUp]
        public void SetUp()
        {
            // The map lookup runs here, once, since every action lookup below depends on it too.
            _inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(_inputActions, Is.Not.Null, $"Test setup expects '{InputActionsPath}' to exist and import as an InputActionAsset.");

            _matchMap = _inputActions.FindActionMap(InputActionNames.MatchMap, throwIfNotFound: false);
        }

        [Test]
        public void InputActionNamesMatchMap_InTheImportedAsset_ResolvesToAnActionMap()
        {
            // THEN
            Assert.That(
                _matchMap,
                Is.Not.Null,
                $"InputActionNames.MatchMap (\"{InputActionNames.MatchMap}\") does not resolve to a map in {InputActionsPath}."
            );
        }

        [TestCaseSource(nameof(ActionNameConstFieldNames))]
        public void InputActionNamesActionConst_InTheMatchMap_ResolvesToAnAction(string constFieldName)
        {
            // GIVEN
            Assert.That(_matchMap, Is.Not.Null, "Test setup expects the Match map to have resolved.");
            string value = (string)typeof(InputActionNames).GetField(constFieldName, BindingFlags.Public | BindingFlags.Static).GetValue(null);

            // WHEN
            InputAction resolved = _matchMap.FindAction(value, throwIfNotFound: false);

            // THEN
            Assert.That(resolved, Is.Not.Null, $"InputActionNames.{constFieldName} (\"{value}\") does not resolve to an action in the Match map.");
        }

        // Every const InputActionNames declares as an action name, found by excluding MatchMap — the one const
        // that names the map itself rather than an action inside it — so a const added later is covered
        // automatically.
        private static IEnumerable<string> ActionNameConstFieldNames()
        {
            foreach (FieldInfo field in typeof(InputActionNames).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(string) || field.Name == nameof(InputActionNames.MatchMap))
                {
                    continue;
                }

                yield return field.Name;
            }
        }
    }
}
