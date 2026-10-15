using System.IO;
using System.Linq;
using System.Xml.Linq;
using GooGalaxy.Editor.Build;
using NUnit.Framework;

namespace GooGalaxy.Tests.EditMode.Build
{
    [TestFixture]
    public class IosExportCompliancePostprocessorTests
    {
        private const string Key = IosExportCompliancePostprocessor.ExportComplianceKey;

        private const string Header =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n";

        private const string PlistWithoutKey = Header + "<plist version=\"1.0\"><dict><key>CFBundleName</key><string>Game</string></dict></plist>";

        private const string PlistWithKeyTrue =
            Header + "<plist version=\"1.0\"><dict><key>" + Key + "</key><true/><key>CFBundleName</key><string>Game</string></dict></plist>";

        private const string PlistWithKeyLast =
            Header + "<plist version=\"1.0\"><dict><key>CFBundleName</key><string>Game</string><key>" + Key + "</key></dict></plist>";

        private const string PlistWithoutRootDict = Header + "<plist version=\"1.0\"><array/></plist>";

        [Test]
        public void ApplyExportCompliance_KeyMissing_AddsTheKeyAsFalse()
        {
            // GIVEN
            var document = XDocument.Parse(PlistWithoutKey);

            // WHEN
            IosExportCompliancePostprocessor.ApplyExportCompliance(document);

            // THEN
            Assert.That(ReadValueElementNameAfterEachKey(document), Is.EqualTo(new[] { "false" }));
        }

        [Test]
        public void ApplyExportCompliance_KeyTrue_ChangesTheValueToFalse()
        {
            // GIVEN
            var document = XDocument.Parse(PlistWithKeyTrue);

            // WHEN
            IosExportCompliancePostprocessor.ApplyExportCompliance(document);

            // THEN
            Assert.That(ReadValueElementNameAfterEachKey(document), Is.EqualTo(new[] { "false" }));
        }

        [Test]
        public void ApplyExportCompliance_CalledTwice_KeepsExactlyOneKeyEntry()
        {
            // GIVEN
            var document = XDocument.Parse(PlistWithoutKey);
            IosExportCompliancePostprocessor.ApplyExportCompliance(document);

            // WHEN
            IosExportCompliancePostprocessor.ApplyExportCompliance(document);

            // THEN
            Assert.That(ReadValueElementNameAfterEachKey(document), Has.Length.EqualTo(1));
        }

        [Test]
        public void ApplyExportCompliance_OtherKeys_AreLeftUntouched()
        {
            // GIVEN
            var document = XDocument.Parse(PlistWithoutKey);

            // WHEN
            IosExportCompliancePostprocessor.ApplyExportCompliance(document);

            // THEN
            Assert.That(ReadBundleName(document), Is.EqualTo("Game"));
        }

        [Test]
        public void ApplyExportCompliance_Doctype_RoundTripsWithoutAnEmptyInternalSubset()
        {
            // GIVEN
            var document = XDocument.Parse(PlistWithoutKey);

            // WHEN
            IosExportCompliancePostprocessor.ApplyExportCompliance(document);

            // THEN
            string saved = document.ToString();
            Assert.That(saved, Does.Contain("<!DOCTYPE plist PUBLIC").And.Not.Contain("[]"), "An empty \"[]\" subset may be rejected by Apple's plist parser.");
        }

        [Test]
        public void ApplyExportCompliance_NoRootDict_ThrowsInvalidDataException()
        {
            // GIVEN
            var document = XDocument.Parse(PlistWithoutRootDict);

            // WHEN / THEN
            Assert.Throws<InvalidDataException>(() => IosExportCompliancePostprocessor.ApplyExportCompliance(document));
        }

        [Test]
        public void ApplyExportCompliance_KeyIsLastChild_ThrowsInvalidDataException()
        {
            // GIVEN
            var document = XDocument.Parse(PlistWithKeyLast);

            // WHEN / THEN
            Assert.Throws<InvalidDataException>(() => IosExportCompliancePostprocessor.ApplyExportCompliance(document));
        }

        private static string[] ReadValueElementNameAfterEachKey(XDocument document)
        {
            return document
                .Root.Element("dict")
                .Elements("key")
                .Where(key => key.Value == Key)
                .Select(key => key.ElementsAfterSelf().First().Name.LocalName)
                .ToArray();
        }

        private static string ReadBundleName(XDocument document)
        {
            XElement bundleKey = document.Root.Element("dict").Elements("key").First(key => key.Value == "CFBundleName");
            return bundleKey.ElementsAfterSelf().First().Value;
        }
    }
}
