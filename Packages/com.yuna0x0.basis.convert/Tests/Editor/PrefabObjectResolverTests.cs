using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Gate on the mechanism the whole prefab reader rests on: every VRChat component found in
    /// the file must be tied back to the live GameObject that carries it.
    /// <para>
    /// Needs a real VRChat avatar imported into the Basis project, which cannot be committed,
    /// so this is skipped rather than failed when the fixture is absent.
    /// </para>
    /// </summary>
    public class PrefabObjectResolverTests
    {
        private static readonly string FixturePath =
            LocalFixtures.Find(LocalFixtures.ShinanoPrefab);

        [Test]
        public void EveryIdentifiedComponentResolvesToItsGameObject()
        {
            if (FixturePath == null)
            {
                Assert.Ignore($"Fixture not present: {LocalFixtures.ShinanoPrefab}.");
            }

            List<UnityYamlDocument> documents = UnityYamlScanner.ScanFile(FixturePath);
            PrefabObjectResolver resolver = PrefabObjectResolver.Create(FixturePath, documents);
            Assert.That(resolver.Root, Is.Not.Null, "Fixture prefab failed to load.");

            Dictionary<long, UnityYamlDocument> gameObjectDocuments =
                new Dictionary<long, UnityYamlDocument>();
            foreach (UnityYamlDocument document in documents)
            {
                if (document.ClassId == UnityYamlScanner.ClassIdGameObject)
                {
                    gameObjectDocuments[document.FileId] = document;
                }
            }

            Dictionary<SourceComponentKind, int> resolvedByKind =
                new Dictionary<SourceComponentKind, int>();
            List<string> failures = new List<string>();
            int identified = 0;

            foreach (UnityYamlDocument document in documents)
            {
                if (document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour
                    || !document.TryGetScriptIdentity(out string guid, out long scriptFileId))
                {
                    continue;
                }

                SourceComponentKind kind = KnownScriptIdentities.Resolve(guid, scriptFileId);
                if (kind == SourceComponentKind.Unknown)
                {
                    failures.Add($"unrecognised script identity {guid}:{scriptFileId}");
                    continue;
                }

                identified++;

                if (!document.TryGetTopLevelFileIdReference("m_GameObject", out long ownerFileId)
                    || !gameObjectDocuments.TryGetValue(ownerFileId, out UnityYamlDocument owner))
                {
                    failures.Add($"{kind} at &{document.FileId} has no owner document");
                    continue;
                }

                if (!resolver.TryResolveGameObject(owner, out GameObject live))
                {
                    failures.Add($"{kind} at &{document.FileId} did not resolve "
                        + $"(owner &{owner.FileId}, stripped={owner.Stripped})");
                    continue;
                }

                resolvedByKind.TryGetValue(kind, out int count);
                resolvedByKind[kind] = count + 1;
                Assert.That(live.transform.IsChildOf(resolver.Root.transform), Is.True,
                    $"{kind} resolved to an object outside the avatar: {live.name}");
            }

            foreach (KeyValuePair<SourceComponentKind, int> pair in resolvedByKind)
            {
                TestContext.WriteLine($"{pair.Key}: {pair.Value} resolved");
            }

            Assert.That(identified, Is.GreaterThan(0), "Nothing was identified in the fixture.");
            Assert.That(failures, Is.Empty,
                "Unresolved components:\n  " + string.Join("\n  ", failures));
        }

        [Test]
        public void ScannerReadsDocumentHeadersAndScalars()
        {
            string[] lines =
            {
                "%YAML 1.1",
                "%TAG !u! tag:unity3d.com,2011:",
                "--- !u!1 &123456",
                "GameObject:",
                "  m_Name: Hips",
                "--- !u!1 &777 stripped",
                "GameObject:",
                "  m_CorrespondingSourceObject: {fileID: -42, guid: 2a2c05204084d904aa4945ccff20d8e5, type: 3}",
                "  m_PrefabInstance: {fileID: 999}",
                "--- !u!114 &555",
                "MonoBehaviour:",
                "  m_GameObject: {fileID: 777}",
                "  m_Script: {fileID: 1661641543, guid: 2a2c05204084d904aa4945ccff20d8e5, type: 3}",
                "  pull: 0.2",
            };

            List<UnityYamlDocument> documents = UnityYamlScanner.Scan(lines);
            Assert.That(documents.Count, Is.EqualTo(3));

            Assert.That(documents[0].ClassId, Is.EqualTo(UnityYamlScanner.ClassIdGameObject));
            Assert.That(documents[0].FileId, Is.EqualTo(123456L));
            Assert.That(documents[0].Stripped, Is.False);
            Assert.That(documents[0].TypeName, Is.EqualTo("GameObject"));
            Assert.That(documents[0].GetTopLevelValue("m_Name"), Is.EqualTo("Hips"));

            Assert.That(documents[1].Stripped, Is.True);
            Assert.That(documents[1].TryGetTopLevelObjectReference(
                "m_CorrespondingSourceObject", out string guid, out long fileId), Is.True);
            Assert.That(guid, Is.EqualTo("2a2c05204084d904aa4945ccff20d8e5"));
            Assert.That(fileId, Is.EqualTo(-42L));
            Assert.That(documents[1].TryGetTopLevelFileIdReference(
                "m_PrefabInstance", out long instance), Is.True);
            Assert.That(instance, Is.EqualTo(999L));

            Assert.That(documents[2].ClassId, Is.EqualTo(UnityYamlScanner.ClassIdMonoBehaviour));
            Assert.That(documents[2].TryGetScriptIdentity(
                out string scriptGuid, out long scriptFileId), Is.True);
            Assert.That(KnownScriptIdentities.Resolve(scriptGuid, scriptFileId),
                Is.EqualTo(SourceComponentKind.VrcPhysBone));
            Assert.That(documents[2].GetTopLevelValue("pull"), Is.EqualTo("0.2"));
        }
    }
}
