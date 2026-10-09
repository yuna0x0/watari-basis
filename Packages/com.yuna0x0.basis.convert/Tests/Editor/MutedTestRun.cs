using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace yuna0x0.Basis.Convert.Tests
{
    // Tests start world scripts, and some play sounds in edit mode; the run is muted and the
    // previous state restored afterwards (runtime and editor state, not a project setting).
    [SetUpFixture]
    public class MutedTestRun
    {
        float volume;
        bool paused, muted;

        [OneTimeSetUp]
        public void Mute()
        {
            volume = AudioListener.volume;
            paused = AudioListener.pause;
            muted = EditorUtility.audioMasterMute;
            AudioListener.volume = 0f;
            AudioListener.pause = true;
            EditorUtility.audioMasterMute = true;
        }

        [OneTimeTearDown]
        public void Restore()
        {
            AudioListener.volume = volume;
            AudioListener.pause = paused;
            EditorUtility.audioMasterMute = muted;
        }
    }
}
