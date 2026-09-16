using System.Collections.Generic;
using UnityEditor;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Sources;

namespace yuna0x0.Basis.Convert.Pipeline
{
    /// <summary>
    /// Follows an avatar's expression menu tree and parameter list off disk.
    /// <para>
    /// Submenus are separate assets referenced by guid, so the tree is walked rather than read
    /// from one file. Guids already seen are skipped: a menu that references itself, directly or
    /// through a chain, would otherwise recurse forever. An asset that cannot be read is recorded
    /// on the inventory with the reason, since a menu that is not there and a menu that is there
    /// in a form this cannot read look the same from the outside.
    /// </para>
    /// </summary>
    public static class ExpressionInventoryLoader
    {
        /// <summary>Depth cap, in case a tree is pathological in some way visiting does not catch.</summary>
        private const int MaxDepth = 16;

        public static VrcExpressionInventory Load(string menuGuid, string parametersGuid)
        {
            return Load(menuGuid, 0L, parametersGuid, 0L);
        }

        public static VrcExpressionInventory Load(
            string menuGuid, long menuFileId, string parametersGuid, long parametersFileId)
        {
            VrcExpressionInventory inventory = new VrcExpressionInventory();

            HashSet<string> visited = new HashSet<string>();
            LoadMenu(menuGuid, menuFileId, "menu", inventory, visited, 0);

            UnityYamlDocument parameters =
                LoadDocument(parametersGuid, parametersFileId, "parameters", inventory);
            if (parameters != null)
            {
                inventory.Parameters = VrcExpressionReader.ReadParameters(parameters);
            }

            return inventory;
        }

        private static void LoadMenu(
            string guid, long fileId, string role, VrcExpressionInventory inventory,
            HashSet<string> visited, int depth)
        {
            if (string.IsNullOrEmpty(guid) || depth >= MaxDepth || !visited.Add(guid + ":" + fileId))
            {
                return;
            }

            UnityYamlDocument document = LoadDocument(guid, fileId, role, inventory);
            if (document == null)
            {
                return;
            }

            VrcExpressionMenu menu = VrcExpressionReader.ReadMenu(document, guid);
            inventory.Menus.Add(menu);

            foreach (VrcExpressionControl control in menu.Controls)
            {
                if (control.Type == VrcExpressionControlType.SubMenu)
                {
                    LoadMenu(control.SubMenuGuid, control.SubMenuFileId, "submenu",
                        inventory, visited, depth + 1);
                }
            }
        }

        /// <summary>
        /// The document for one asset. A file id names an asset packed into a file with others;
        /// 0 means the file's own main asset, which is the first script document in it.
        /// </summary>
        private static UnityYamlDocument LoadDocument(
            string guid, long fileId, string role, VrcExpressionInventory inventory)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                Record(inventory, role, guid, path, fileId, VrcExpressionAssetProblemKind.Missing);
                return null;
            }

            List<UnityYamlDocument> documents = UnityYamlScanner.ScanFile(path);
            if (documents.Count == 0)
            {
                Record(inventory, role, guid, path, fileId, VrcExpressionAssetProblemKind.NotText);
                return null;
            }

            foreach (UnityYamlDocument document in documents)
            {
                if (document.ClassId != UnityYamlScanner.ClassIdMonoBehaviour)
                {
                    continue;
                }

                if (fileId == 0L || document.FileId == fileId)
                {
                    return document;
                }
            }

            Record(inventory, role, guid, path, fileId, VrcExpressionAssetProblemKind.NoDocument);
            return null;
        }

        private static void Record(VrcExpressionInventory inventory, string role, string guid,
            string path, long fileId, VrcExpressionAssetProblemKind kind)
        {
            inventory.Problems.Add(new VrcExpressionAssetProblem
            {
                Role = role,
                Guid = guid,
                Path = path ?? string.Empty,
                FileId = fileId,
                Kind = kind,
            });
        }
    }
}
