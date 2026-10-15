using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Build
{
    [TestFixture]
    public class EditorProjectSettingsTests
    {
        private const string ProjectWideActionsConfigKey = "com.unity.input.settings.actions";

        [Test]
        public void LineEndingsForNewScripts_Project_IsUnix()
        {
            // GIVEN

            // WHEN
            LineEndingsMode mode = EditorSettings.lineEndingsForNewScripts;

            // THEN
            Assert.That(
                mode,
                Is.EqualTo(LineEndingsMode.Unix),
                "New scripts would be created with non-LF line endings, which .editorconfig (end_of_line = lf) and the format check reject; set Project Settings > Editor > Line Endings For New Scripts to Unix."
            );
        }

        [Test]
        public void SimulationMode_Physics_IsScript()
        {
            // GIVEN

            // WHEN
            SimulationMode mode = Physics.simulationMode;

            // THEN
            Assert.That(
                mode,
                Is.EqualTo(SimulationMode.Script),
                "3D physics is simulating automatically — only 2D physics is used, so the 3D step is wasted work."
            );
        }

        [Test]
        public void GetConfigObjectNames_Project_HasNoProjectWideActionsKey()
        {
            // GIVEN

            // WHEN
            string[] names = EditorBuildSettings.GetConfigObjectNames();

            // THEN
            Assert.That(
                names,
                Does.Not.Contain(ProjectWideActionsConfigKey),
                "Project-wide input actions are assigned again — the game reads only MatchInput.inputactions through PointerInputView, so a project-wide asset would ship and be enabled at startup with nothing reading it; clear Project Settings > Input System Package > Project-wide Actions."
            );
        }
    }
}
