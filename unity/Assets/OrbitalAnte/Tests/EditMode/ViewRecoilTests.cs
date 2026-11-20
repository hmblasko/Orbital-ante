using NUnit.Framework;
using OrbitalAnte.Player;

namespace OrbitalAnte.Tests
{
    /// <summary>The gun's kick on the view in the real game (the astronaut), with no Unity objects.</summary>
    public class ViewRecoilTests
    {
        static float Settle(ViewRecoil recoil, float seconds, float dt, out float right)
        {
            float up = 0f;
            right = 0f;
            for (float t = 0f; t < seconds; t += dt)
            {
                recoil.Step(dt, out float u, out float r);
                up += u;
                right += r;
            }
            return up;
        }

        [Test]
        public void AKick_TurnsTheViewAtOnce_OnTheNextFrame()
        {
            var recoil = new ViewRecoil();
            recoil.Kick(1f, 0.4f, 0.5f, 14f);
            recoil.Step(0.0001f, out float up, out float right);
            Assert.That(up, Is.EqualTo(1f).Within(0.01f));
            Assert.That(right, Is.EqualTo(0.4f).Within(0.01f));
        }

        [Test]
        public void OnlyTheReturnFraction_SettlesBack_TheRestStays()
        {
            var recoil = new ViewRecoil();
            recoil.Kick(2f, -1f, 0.55f, 14f);
            float up = Settle(recoil, 2f, 1f / 60f, out float right);
            Assert.That(up, Is.EqualTo(2f * 0.45f).Within(0.001f));
            Assert.That(right, Is.EqualTo(-1f * 0.45f).Within(0.001f));
            Assert.That(recoil.Unsettled, Is.EqualTo(0f));
        }

        [Test]
        public void HoldingTheTrigger_MakesTheGunClimb()
        {
            var recoil = new ViewRecoil();
            float climbed = 0f;
            for (int shot = 0; shot < 10; shot++)
            {
                recoil.Kick(0.75f, 0f, 0.55f, 14f);
                for (int frame = 0; frame < 6; frame++)
                {
                    recoil.Step(1f / 60f, out float up, out _);
                    climbed += up;
                }
            }
            // Ten shots climb by the part of each kick that does not come back.
            Assert.That(climbed, Is.EqualTo(10 * 0.75f * 0.45f).Within(0.01f));
        }

        [Test]
        public void SettlingNeverOvershoots_AndIsTheSameAtAnyFrameRate()
        {
            var fast = new ViewRecoil();
            var slow = new ViewRecoil();
            fast.Kick(3f, 1f, 1f, 14f);
            slow.Kick(3f, 1f, 1f, 14f);
            float a = Settle(fast, 1f, 1f / 144f, out float ar);
            float b = Settle(slow, 1f, 1f / 20f, out float br);
            Assert.That(a, Is.EqualTo(0f).Within(0.001f));
            Assert.That(b, Is.EqualTo(0f).Within(0.001f));
            Assert.That(ar, Is.EqualTo(0f).Within(0.001f));
            Assert.That(br, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Clear_ForgetsEverything_AndBadNumbersAreIgnored()
        {
            var recoil = new ViewRecoil();
            recoil.Kick(float.NaN, 1f, 0.5f, 14f);
            recoil.Kick(1f, 1f, float.NaN, float.NaN);
            recoil.Clear();
            recoil.Step(0.1f, out float up, out float right);
            Assert.That(up, Is.EqualTo(0f));
            Assert.That(right, Is.EqualTo(0f));
        }
    }
}
