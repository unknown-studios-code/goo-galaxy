using GooGalaxy.Runtime.Input.Constants;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GooGalaxy.Tests.Utils
{
    /// <summary>
    /// Builds the <c>InputActionAsset</c> a PlayMode fixture wires into <c>PointerInputView</c>: the authored
    /// asset in the editor, and the same map built in code everywhere else.
    /// </summary>
    /// <remarks>
    /// The editor branch instantiates the project's real <c>MatchInput.inputactions</c>, so the bindings under
    /// test are the ones that ship. A player build has no <c>AssetDatabase</c> to load that asset through, so the
    /// fallback declares only the one action map and the two actions <see cref="InputActionNames" /> names,
    /// bound to the base <c>Pointer</c> device class so either a mouse or a touchscreen drives them — the same
    /// fallback shape <c>SpellAimDeviceFlowTests.CreateMatchInputActions</c> already established.
    /// </remarks>
    public static class MatchInputTestActionsFactory
    {
#if UNITY_EDITOR
        private const string MatchInputAssetPath = "Assets/Settings/Input/MatchInput.inputactions";
#else
        private const string PointerPositionBindingPath = "<Pointer>/position";
        private const string PointerPressBindingPath = "<Pointer>/press";
#endif

        /// <summary>Builds a fresh <c>InputActionAsset</c> carrying the match's pointer bindings.</summary>
        /// <returns>A new instance the caller owns and must destroy.</returns>
        public static InputActionAsset Create()
        {
#if UNITY_EDITOR
            InputActionAsset sourceActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(MatchInputAssetPath);
            Assert.That(sourceActions, Is.Not.Null, $"Test setup expects '{MatchInputAssetPath}' to exist and import as an InputActionAsset.");

            return Object.Instantiate(sourceActions);
#else
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = actions.AddActionMap(InputActionNames.MatchMap);
            map.AddAction(InputActionNames.PointerPosition, InputActionType.PassThrough, PointerPositionBindingPath);
            map.AddAction(InputActionNames.PointerPress, InputActionType.Button, PointerPressBindingPath);

            return actions;
#endif
        }
    }
}
