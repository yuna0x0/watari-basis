using System.Collections.Generic;
using Basis.Scripts.BasisSdk;
using NUnit.Framework;
using UnityEngine;
using yuna0x0.Basis.Convert.Model;
using yuna0x0.Basis.Convert.Writers;

namespace yuna0x0.Basis.Convert.Tests
{
    public class BasisAvatarWriterTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null)
                {
                    Object.DestroyImmediate(spawned);
                }
            }

            _spawned.Clear();
        }

        private BasisAvatar Write(float eyeLimit)
        {
            GameObject root = new GameObject("Avatar");
            _spawned.Add(root);
            return BasisAvatarWriter.Write(new ResolvedBasisAvatar
            {
                Root = root,
                Plan = new BasisAvatarPlan { EyeMaxLookAngleDegrees = eyeLimit },
            });
        }

        [Test]
        public void TheEyePositionIsScaledByTheAvatarRoot()
        {
            // The view position is in the root's own space; Basis stores metres.
            GameObject root = new GameObject("Avatar");
            _spawned.Add(root);
            root.transform.localScale = Vector3.one * 0.5f;
            root.transform.SetPositionAndRotation(new Vector3(3f, 1f, -2f), Quaternion.Euler(0f, 90f, 0f));

            BasisAvatar avatar = BasisAvatarWriter.Write(new ResolvedBasisAvatar
            {
                Root = root,
                Plan = new BasisAvatarPlan { EyePosition = new Vector2(1.6f, 0.1f) },
            });

            Assert.That(avatar.AvatarEyePosition.x, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(avatar.AvatarEyePosition.y, Is.EqualTo(0.05f).Within(1e-5f));
        }

        [Test]
        public void APlannedEyeLimitEnablesAndSetsTheAvatarsAngle()
        {
            BasisAvatar avatar = Write(12f);

            Assert.That(avatar.EyeMaxLookAngleEnabled, Is.True);
            Assert.That(avatar.EyeMaxLookAngle, Is.EqualTo(12f));
        }

        [Test]
        public void NoPlannedEyeLimitLeavesTheAvatarsAngleAlone()
        {
            BasisAvatar avatar = Write(12f);
            avatar.EyeMaxLookAngle = 30f;

            BasisAvatarWriter.Write(new ResolvedBasisAvatar
            {
                Root = avatar.gameObject,
                Plan = new BasisAvatarPlan(),
            });

            Assert.That(avatar.EyeMaxLookAngleEnabled, Is.True);
            Assert.That(avatar.EyeMaxLookAngle, Is.EqualTo(30f));
        }
    }
}
