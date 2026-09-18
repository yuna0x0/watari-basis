using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace yuna0x0.Basis.Convert.Tests
{
    /// <summary>
    /// Scenes for tests that depend on what scene an object sits in, since a saved scene is a
    /// source. Unity refuses an additive new scene while an untitled scene is open, which is
    /// the state of a headless run, so the untitled scene is used or replaced there.
    /// </summary>
    public static class TestScenes
    {
        /// <summary>An unsaved scene: the untitled one when that is what is open, else a new additive one.</summary>
        public static Scene Unsaved(out bool additive)
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                additive = false;
                return active;
            }

            additive = true;
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        /// <summary>
        /// An empty scene the test may save. Replaces an open untitled scene when that scene is
        /// clean, and skips the test rather than discard unsaved work when it is not.
        /// </summary>
        public static Scene Saveable(out bool additive)
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                // Dirty from objects tests spawned and destroyed, or from a headless run, is
                // nothing to keep; dirty with objects in it may be someone's work.
                if (active.isDirty && active.rootCount > 0 && !UnityEngine.Application.isBatchMode)
                {
                    Assert.Ignore("The open untitled scene has unsaved changes; save or discard them to run this test.");
                }

                additive = false;
                return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            additive = true;
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        }

        /// <summary>
        /// Opens a scene file the test wrote, replacing an open untitled scene or joining a
        /// saved one, with the same care as <see cref="Saveable"/>.
        /// </summary>
        public static Scene Open(string path, out bool additive)
        {
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                if (active.isDirty && active.rootCount > 0 && !UnityEngine.Application.isBatchMode)
                {
                    Assert.Ignore("The open untitled scene has unsaved changes; save or discard them to run this test.");
                }

                additive = false;
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            additive = true;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        /// <summary>Releases a scene from <see cref="Saveable"/>, <see cref="Open"/> or <see cref="Unsaved"/>.</summary>
        public static void Release(Scene scene, bool additive)
        {
            if (!scene.IsValid())
            {
                return;
            }

            if (additive)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            else if (!string.IsNullOrEmpty(scene.path))
            {
                // A saved single scene is replaced so its file can be deleted.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
