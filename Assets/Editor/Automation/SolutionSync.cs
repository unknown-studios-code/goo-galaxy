using System;
using System.IO;
using System.Reflection;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace GooGalaxy.Editor.Automation
{
    /// <summary>
    /// Regenerates the untracked C# project files that external tooling reads.
    /// Invoked from CI through <c>-executeMethod</c>, never interactively.
    /// </summary>
    public static class SolutionSync
    {
        private const string GeneratorFactoryTypeName = "Microsoft.Unity.VisualStudio.Editor.GeneratorFactory, Unity.VisualStudio.Editor";

        private const string GeneratorStyleTypeName = "Microsoft.Unity.VisualStudio.Editor.GeneratorStyle, Unity.VisualStudio.Editor";

        private const int SdkGeneratorStyle = 1;

        /// <summary>
        /// Imports pending asset changes, writes every generated project file, then quits the Editor —
        /// with a non-zero code when no solution was produced.
        /// </summary>
        /// <remarks>
        /// The refresh has to come first: assets placed on disk from outside the Editor — a checkout, for
        /// instance — stay invisible to the AssetDatabase until then, and a sync run before the import
        /// writes project files that describe the previous state.
        /// <para>
        /// <c>CodeEditor.CurrentEditor.SyncAll()</c> cannot carry this alone. The Visual Studio package
        /// resolves an IDE installation from the stored editor preference and returns without writing
        /// anything when that lookup fails, which is every run on a fresh CI container — silently, and
        /// with a success exit code. Driving the package's own generator skips the lookup entirely.
        /// </para>
        /// </remarks>
        public static void Sync()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (!TrySyncThroughPackageGenerator())
            {
                Debug.LogWarning("SolutionSync: the Visual Studio generator was unreachable, falling back to the configured code editor.");
                CodeEditor.CurrentEditor.SyncAll();
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string[] solutions = Directory.GetFiles(projectRoot, "*.slnx");

            if (solutions.Length == 0)
            {
                solutions = Directory.GetFiles(projectRoot, "*.sln");
            }

            if (solutions.Length == 0)
            {
                Debug.LogError($"SolutionSync: no solution was written to '{projectRoot}', so dotnet format would have nothing to read.");
                EditorApplication.Exit(1);
                return;
            }

            int projectCount = Directory.GetFiles(projectRoot, "*.csproj").Length;
            Debug.Log($"SolutionSync: wrote '{Path.GetFileName(solutions[0])}' and {projectCount} project files to '{projectRoot}'.");
            EditorApplication.Exit(0);
        }

        private static bool TrySyncThroughPackageGenerator()
        {
            var styleType = Type.GetType(GeneratorStyleTypeName);
            MethodInfo getInstance = Type.GetType(GeneratorFactoryTypeName)?.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);

            if (styleType == null || getInstance == null)
            {
                return false;
            }

            object generator = getInstance.Invoke(null, new[] { Enum.ToObject(styleType, SdkGeneratorStyle) });
            MethodInfo sync = generator?.GetType().GetMethod("Sync", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

            if (sync == null)
            {
                return false;
            }

            sync.Invoke(generator, Array.Empty<object>());
            return true;
        }
    }
}
