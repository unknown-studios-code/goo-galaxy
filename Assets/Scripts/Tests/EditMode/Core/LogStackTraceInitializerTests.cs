using System;
using GooGalaxy.Runtime.Core.Startup;
using NUnit.Framework;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Core
{
    [TestFixture]
    public class LogStackTraceInitializerTests
    {
        private static readonly LogType[] _logTypes = (LogType[])Enum.GetValues(typeof(LogType));

        private readonly StackTraceLogType[] _original = new StackTraceLogType[_logTypes.Length];

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < _logTypes.Length; i++)
            {
                _original[i] = Application.GetStackTraceLogType(_logTypes[i]);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _logTypes.Length; i++)
            {
                Application.SetStackTraceLogType(_logTypes[i], _original[i]);
            }
        }

        [TestCase(LogType.Log)]
        [TestCase(LogType.Warning)]
        public void ApplyStackTraceLogTypes_ReleaseBuild_DropsStackTracesForLogAndWarning(LogType logType)
        {
            // GIVEN
            Application.SetStackTraceLogType(logType, StackTraceLogType.ScriptOnly);

            // WHEN
            LogStackTraceInitializer.ApplyStackTraceLogTypes(isDebugBuild: false);

            // THEN
            Assert.That(
                Application.GetStackTraceLogType(logType),
                Is.EqualTo(StackTraceLogType.None),
                $"{logType} still captures stack traces in a release player."
            );
        }

        [Test]
        public void ApplyStackTraceLogTypes_ReleaseBuild_LeavesErrorStackTracesUnchanged()
        {
            // GIVEN
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);

            // WHEN
            LogStackTraceInitializer.ApplyStackTraceLogTypes(isDebugBuild: false);

            // THEN
            Assert.That(
                Application.GetStackTraceLogType(LogType.Error),
                Is.EqualTo(StackTraceLogType.ScriptOnly),
                "Errors must keep their traces in release players."
            );
        }

        [TestCase(LogType.Log)]
        [TestCase(LogType.Warning)]
        public void ApplyStackTraceLogTypes_DebugBuild_KeepsTheConfiguredStackTrace(LogType logType)
        {
            // GIVEN
            Application.SetStackTraceLogType(logType, StackTraceLogType.ScriptOnly);

            // WHEN
            LogStackTraceInitializer.ApplyStackTraceLogTypes(isDebugBuild: true);

            // THEN
            Assert.That(
                Application.GetStackTraceLogType(logType),
                Is.EqualTo(StackTraceLogType.ScriptOnly),
                $"{logType} lost its trace in a development build."
            );
        }
    }
}
