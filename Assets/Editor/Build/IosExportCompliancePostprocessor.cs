using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace GooGalaxy.Editor.Build
{
    /// <remarks>
    /// Edits the plist as plain XML rather than through <c>PlistDocument</c>: that type lives in the iOS
    /// module's <c>UnityEditor.iOS.Extensions.Xcode</c> assembly, which an editor without the module (the
    /// Linux CI jobs) does not have, so referencing it would break compilation there.
    /// </remarks>
    internal sealed class IosExportCompliancePostprocessor : IPostprocessBuildWithReport
    {
        internal const string ExportComplianceKey = "ITSAppUsesNonExemptEncryption";

        private const string InfoPlistFileName = "Info.plist";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
            {
                return;
            }

            string plistPath = Path.Combine(report.summary.outputPath, InfoPlistFileName);

            if (!File.Exists(plistPath))
            {
                throw new BuildFailedException(
                    $"IosExportCompliancePostprocessor: '{plistPath}' does not exist, so the export compliance key was not written "
                        + "and TestFlight would stall on \"Missing Compliance\". Check that the Xcode project was exported to the build output path."
                );
            }

            ApplyExportCompliance(plistPath);
        }

        internal static void ApplyExportCompliance(string plistPath)
        {
            var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
            XDocument document;

            using (var reader = XmlReader.Create(plistPath, readerSettings))
            {
                document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            }

            ApplyExportCompliance(document);

            var writerSettings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };

            using var writer = XmlWriter.Create(plistPath, writerSettings);
            document.Save(writer);
        }

        // Declares no non-exempt encryption: the only encryption is standard TLS (HTTPS through the OS today; Unity Transport's DTLS once Relay lands). Without the key every TestFlight upload stalls on "Missing Compliance". Revisit this value whenever a package adds encryption beyond standard protocols.
        internal static void ApplyExportCompliance(XDocument document)
        {
            XElement root = (document.Root?.Element("dict")) ?? throw new InvalidDataException("Info.plist has no root <dict> element.");

            XElement existingKey = root.Elements("key").FirstOrDefault(key => key.Value == ExportComplianceKey);

            if (existingKey == null)
            {
                AppendKey(root);
            }
            else
            {
                XElement value =
                    existingKey.ElementsAfterSelf().FirstOrDefault()
                    ?? throw new InvalidDataException($"Info.plist key '{ExportComplianceKey}' is the last child of the root <dict> and has no value element.");

                value.ReplaceWith(new XElement("false"));
            }

            // WORKAROUND: XDocument.Load turns an absent DOCTYPE internal subset into an empty one, which Save writes as a stray "[]" that Apple's plist parser may reject. Rebuilding the DOCTYPE with a null subset drops it.
            if (document.DocumentType != null)
            {
                XDocumentType doctype = document.DocumentType;
                doctype.ReplaceWith(new XDocumentType(doctype.Name, doctype.PublicId, doctype.SystemId, null));
            }
        }

        private static void AppendKey(XElement root)
        {
            // Whitespace is preserved on load, so the new pair must be indented by hand to match its siblings.
            XText separator = root.Nodes().OfType<XText>().FirstOrDefault(text => string.IsNullOrWhiteSpace(text.Value));
            var key = new XElement("key", ExportComplianceKey);
            var value = new XElement("false");

            if (separator == null || root.LastNode is not XText closing || !string.IsNullOrWhiteSpace(closing.Value))
            {
                root.Add(key, value);
                return;
            }

            closing.AddBeforeSelf(new XText(separator.Value), key, new XText(separator.Value), value);
        }
    }
}
