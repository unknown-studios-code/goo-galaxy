using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GooGalaxy.Editor.Automation
{
    /// <remarks>
    /// The Visual Studio package writes a generated project only as far as an installed IDE lets it, and
    /// both repairs here exist because that assumption does not hold in a container, where no installation
    /// resolves and the package degrades silently instead of failing.
    /// <para>
    /// <c>LangVersion</c> falls back to <c>latest</c>, which lets Roslyn enable language features Unity
    /// rejects, so the analyzers report rules against code that could never compile.
    /// </para>
    /// <para>
    /// The analyzer list is worse: <c>SetAnalyzerAndSourceGeneratorProperties</c> returns before reading
    /// <c>RoslynAnalyzerDllPaths</c>, so the project references no analyzer at all — the Unity rules never
    /// run, and the suppressors that keep <c>IDE0044</c> off serialized fields never fire. A gate in that
    /// state passes while checking almost nothing, which is the failure this project set out to remove.
    /// </para>
    /// </remarks>
    internal sealed class SolutionPostprocessor : AssetPostprocessor
    {
        private const string WindowsNewline = "\r\n";

        private static readonly Regex _langVersionPattern = new(@"<LangVersion>[^<]*</LangVersion>", RegexOptions.Compiled);

        private static string OnGeneratedCSProject(string path, string content)
        {
            ScriptCompilerOptions options = ResolveAssembly(Path.GetFileNameWithoutExtension(path))?.compilerOptions;

            if (options == null)
            {
                return content;
            }

            content = ApplyLanguageVersion(content, options.LanguageVersion);

            return RestoreAnalyzers(content, options.RoslynAnalyzerDllPaths, path);
        }

        private static Assembly ResolveAssembly(string assemblyName)
        {
            Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);

            return assemblies.FirstOrDefault(candidate => candidate.name == assemblyName) ?? assemblies.FirstOrDefault();
        }

        private static string ApplyLanguageVersion(string content, string languageVersion)
        {
            if (string.IsNullOrEmpty(languageVersion))
            {
                return content;
            }

            return _langVersionPattern.Replace(content, $"<LangVersion>{languageVersion}</LangVersion>");
        }

        private static string RestoreAnalyzers(string content, string[] analyzers, string path)
        {
            string[] missing = (analyzers ?? Array.Empty<string>()).Where(analyzer => !content.Contains(Path.GetFileName(analyzer))).ToArray();

            if (missing.Length == 0)
            {
                return content;
            }

            StringBuilder group = new StringBuilder("  <ItemGroup>").Append(WindowsNewline);

            foreach (string analyzer in missing)
            {
                group.Append("    <Analyzer Include=\"").Append(analyzer).Append("\" />").Append(WindowsNewline);
            }

            group.Append("  </ItemGroup>").Append(WindowsNewline).Append("</Project>");

            Debug.Log($"SolutionPostprocessor: restored {missing.Length} analyzer references to '{Path.GetFileName(path)}' that the package omitted.");

            return content.Replace("</Project>", group.ToString());
        }
    }
}
