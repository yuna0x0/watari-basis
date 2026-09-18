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
