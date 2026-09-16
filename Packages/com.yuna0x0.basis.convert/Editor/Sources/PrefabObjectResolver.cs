using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace yuna0x0.Basis.Convert.Sources
{
    /// <summary>
    /// Ties a file identifier in a prefab's YAML to the live object it refers to.
    /// <para>
    /// Three routes are needed, because a real avatar is built out of nested prefabs and
    /// variants. An object authored directly in the file is addressable by its own local file
    /// identifier. An object coming from a nested prefab appears in the file only as a "stripped"
    /// back reference whose identifier the AssetDatabase does not know, and is matched instead
    /// through the source object it corresponds to, disambiguated by its prefab instance when one
    /// nested prefab is used more than once. The third is for the documents of a prefab this one
    /// is a variant of, whose identifiers name objects in that file rather than this one; see
    /// <see cref="CreateForInherited"/>.
    /// </para>
    /// <para>
    /// No route matches on names, so the duplicate bone names avatars routinely have cannot
    /// cause a mismatch.
    /// </para>
    /// </summary>
    public sealed class PrefabObjectResolver
    {
        private readonly Dictionary<long, UnityYamlDocument> _documents =
            new Dictionary<long, UnityYamlDocument>();

        private readonly Dictionary<long, Object> _byLocalFileId = new Dictionary<long, Object>();

        private readonly Dictionary<SourceIdentity, List<Object>> _bySourceIdentity =
            new Dictionary<SourceIdentity, List<Object>>();

        public GameObject Root { get; private set; }

        /// <summary>
        /// Set when the documents being read come from a prefab this one inherits from rather
        /// than from its own file. Their file ids are identities in that file, so they are
        /// looked up as the object each live object corresponds to instead of as local ids.
        /// </summary>
        private string _inheritedGuid;

        private readonly struct SourceIdentity
        {
            private readonly string _guid;
            private readonly long _fileId;
            private readonly long _prefabInstanceFileId;

            public SourceIdentity(string guid, long fileId, long prefabInstanceFileId)
            {
                _guid = guid;
                _fileId = fileId;
                _prefabInstanceFileId = prefabInstanceFileId;
            }

            public override int GetHashCode()
            {
                int hash = _guid == null ? 0 : _guid.GetHashCode();
                hash = (hash * 397) ^ _fileId.GetHashCode();
                return (hash * 397) ^ _prefabInstanceFileId.GetHashCode();
            }

            public override bool Equals(object other)
            {
                return other is SourceIdentity identity
                    && identity._guid == _guid
                    && identity._fileId == _fileId
                    && identity._prefabInstanceFileId == _prefabInstanceFileId;
            }
        }

        public static PrefabObjectResolver Create(
            string assetPath, IReadOnlyList<UnityYamlDocument> documents)
        {
            PrefabObjectResolver resolver = new PrefabObjectResolver
            {
                Root = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath),
            };

            if (documents != null)
            {
                foreach (UnityYamlDocument document in documents)
                {
                    resolver._documents[document.FileId] = document;
                }
            }

            if (resolver.Root == null)
            {
                return resolver;
            }

            foreach (Transform live in resolver.Root.GetComponentsInChildren<Transform>(true))
            {
                resolver.Index(live.gameObject);

                foreach (Component component in live.GetComponents<Component>())
                {
                    // Null entries are components whose script is missing, which is every
                    // VRChat component here. They carry no data through the object API, which
                    // is why the YAML is read instead.
                    if (component != null)
                    {
                        resolver.Index(component);
                    }
                }
            }

            return resolver;
        }

        /// <summary>
        /// Reads a prefab's documents but resolves them onto the objects of a variant built from
        /// it. The variant's asset loads fully merged, and each object records the base object it
        /// came from, which is what the base's file ids name.
        /// </summary>
        public static PrefabObjectResolver CreateForInherited(
            string variantAssetPath, string inheritedAssetPath,
            IReadOnlyList<UnityYamlDocument> inheritedDocuments)
        {
            PrefabObjectResolver resolver = Create(variantAssetPath, inheritedDocuments);
            resolver._inheritedGuid = AssetDatabase.AssetPathToGUID(inheritedAssetPath);
            return string.IsNullOrEmpty(resolver._inheritedGuid) ? null : resolver;
        }

        /// <summary>
        /// The live object a file id in the inherited prefab corresponds to. A variant
        /// instantiates its base once, so anything but a single match is left alone.
        /// </summary>
        private bool TryResolveInherited(long fileId, out Object resolved)
        {
            resolved = null;
            if (_inheritedGuid == null)
            {
                return false;
            }

            if (_bySourceIdentity.TryGetValue(
                    new SourceIdentity(_inheritedGuid, fileId, 0L), out List<Object> matches)
                && matches.Count == 1)
            {
                resolved = matches[0];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Answers ids this file does not hold: references an outer prefab set on this one, to
        /// objects of its own file, which the plan translated into ids it owns.
        /// </summary>
        public System.Func<long, Object> Foreign;

        public bool TryResolve(long fileId, out Object resolved)
        {
            resolved = null;
            if (fileId == 0L)
            {
                return false;
            }

            if (Foreign != null)
            {
                Object foreign = Foreign(fileId);
                if (foreign != null)
                {
                    resolved = foreign;
                    return true;
                }
            }

            // In inherited mode the ids are the base file's, so a local id could collide.
            if (TryResolveInherited(fileId, out resolved))
            {
                return true;
            }

            if (_inheritedGuid == null && _byLocalFileId.TryGetValue(fileId, out resolved))
            {
                return true;
            }

            if (!_documents.TryGetValue(fileId, out UnityYamlDocument document))
            {
                return false;
            }

            return TryResolve(document, out resolved);
        }

        public bool TryResolve(UnityYamlDocument document, out Object resolved)
        {
            resolved = null;
            if (document == null)
            {
                return false;
            }

            if (TryResolveInherited(document.FileId, out resolved))
            {
                return true;
            }

            if (_inheritedGuid == null && !document.Stripped
                && _byLocalFileId.TryGetValue(document.FileId, out resolved))
            {
                return true;
            }

            if (!document.TryGetTopLevelObjectReference(
                    "m_CorrespondingSourceObject", out string sourceGuid, out long sourceFileId)
                || sourceGuid == null)
            {
                return false;
            }

            document.TryGetTopLevelFileIdReference("m_PrefabInstance", out long instanceFileId);

            if (_bySourceIdentity.TryGetValue(
                    new SourceIdentity(sourceGuid, sourceFileId, instanceFileId),
                    out List<Object> matches)
                && matches.Count > 0)
            {
                resolved = matches[0];
                return true;
            }

            // The prefab instance could not be identified on one side or the other. The source
            // object alone is unambiguous unless the same nested prefab is instantiated twice.
            if (_bySourceIdentity.TryGetValue(
                    new SourceIdentity(sourceGuid, sourceFileId, 0L), out matches)
                && matches.Count == 1)
            {
                resolved = matches[0];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a reference to a Transform. Accepts a Transform or a GameObject identifier,
        /// since VRChat components reference bones as Transforms while ownership is recorded as
        /// a GameObject.
        /// </summary>
        public bool TryResolveTransform(long fileId, out Transform transform)
        {
            transform = null;
            if (!TryResolve(fileId, out Object resolved))
            {
                return false;
            }

            transform = resolved switch
            {
                Transform value => value,
                GameObject value => value.transform,
                Component value => value.transform,
                _ => null,
            };

            return transform != null;
        }

        public bool TryResolveGameObject(UnityYamlDocument document, out GameObject gameObject)
        {
            gameObject = null;
            if (!TryResolve(document, out Object resolved))
            {
                return false;
            }

            gameObject = resolved switch
            {
                GameObject value => value,
                Component value => value.gameObject,
                _ => null,
            };

            return gameObject != null;
        }

        private void Index(Object live)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    live, out string _, out long localFileId))
            {
                _byLocalFileId[localFileId] = live;
            }

            long instanceFileId = 0L;
            Object handle = PrefabUtility.GetPrefabInstanceHandle(live);
            if (handle != null
                && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    handle, out string _, out long handleFileId))
            {
                instanceFileId = handleFileId;
            }

            // Steps one prefab up, so a variant of a variant needs the whole chain walked. The
            // cap is there because a broken chain must not spin.
            Object source = live;
            for (int depth = 0; depth < 16; depth++)
            {
                source = PrefabUtility.GetCorrespondingObjectFromSource(source);
                if (source == null
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        source, out string sourceGuid, out long sourceFileId))
                {
                    return;
                }

                Add(new SourceIdentity(sourceGuid, sourceFileId, instanceFileId), live);
                if (instanceFileId != 0L)
                {
                    Add(new SourceIdentity(sourceGuid, sourceFileId, 0L), live);
                }
            }
        }

        private void Add(SourceIdentity identity, Object live)
        {
            if (!_bySourceIdentity.TryGetValue(identity, out List<Object> bucket))
            {
                bucket = new List<Object>();
                _bySourceIdentity[identity] = bucket;
            }

            bucket.Add(live);
        }
    }
}
