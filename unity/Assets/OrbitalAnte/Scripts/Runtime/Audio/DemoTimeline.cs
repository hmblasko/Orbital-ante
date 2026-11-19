using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitalAnte.Audio
{
    public enum DemoStepKind
    {
        Caption,
        Context,
        Threat,
        Heat,
        Space,
        Play,
        LoopOn,
        LoopOff,
        LoopLevel,
        Reward,
        End,
    }

    public struct DemoStep
    {
        public double Time;
        public DemoStepKind Kind;
        /// <summary>Caption text, context id or event id.</summary>
        public string Text;
        /// <summary>Threat level, loop level (0..1), air amount, or left/right position (-1..1) for Play.</summary>
        public float Value;
        /// <summary>Distance in metres for Play; fade seconds for loops.</summary>
        public float Value2;
        public int Number;
    }

    /// <summary>
    /// The audio showcase: five minutes that walk through a whole slice of the game by sound alone
    /// (exploration, a discovery, rising tension, a fight, the payout, take-off, and back to calm).
    /// It is a script of game events, not of music: the same script is fed to the real audio systems in the
    /// game and to the offline preview, and the music director works out the score for itself. No Unity types.
    /// </summary>
    public static class DemoTimeline
    {
        public const double Length = 300.0;

        public static List<DemoStep> Build()
        {
            var s = new List<DemoStep>();
            var rng = new Random(20261113);
            double R(double a, double b) => a + (b - a) * rng.NextDouble();

            void Cap(double t, string text) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.Caption, Text = text });
            void Ctx(double t, string id) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.Context, Text = id });
            void Threat(double t, float v) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.Threat, Value = v });
            void Heat(double t, IntensityEvent e) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.Heat, Number = (int)e });
            void Space(double t, float air, Enclosure e) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.Space, Value = air, Number = (int)e });
            void Play(double t, string id, float side = 0f, float metres = 0f) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.Play, Text = id, Value = side, Value2 = metres });
            void On(double t, string id, float level = 1f, float fade = 1.5f) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.LoopOn, Text = id, Value = level, Value2 = fade });
            void Level(double t, string id, float level, float fade) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.LoopLevel, Text = id, Value = level, Value2 = fade });
            void Off(double t, string id, float fade = 2f) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.LoopOff, Text = id, Value2 = fade });
            void Reward(double t, RewardKind k) => s.Add(new DemoStep { Time = t, Kind = DemoStepKind.Reward, Number = (int)k });
            void Steps(double from, double to, string id, double pace)
            {
                for (double t = from; t < to; t += pace * R(0.94, 1.06)) Play(t, id);
            }
            // The player firing: every shot heats the moment, and some of them land.
            void Burst(double t, string id, int shots, double gap, double hitChance, string impact)
            {
                for (int i = 0; i < shots; i++)
                {
                    double at = t + i * gap;
                    Play(at, id);
                    Heat(at, IntensityEvent.PlayerShot);
                    if (rng.NextDouble() < hitChance)
                    {
                        Play(at + 0.045, impact, (float)R(-0.5, 0.5), (float)R(10, 24));
                        Play(at + 0.05, "hit.marker");
                        Heat(at, IntensityEvent.HitDealt);
                    }
                }
            }
            void Enemy(double t, string id, int shots, double gap, float side, float metres, double hurtChance)
            {
                for (int i = 0; i < shots; i++)
                {
                    double at = t + i * gap;
                    Play(at, id, side, metres);
                    Heat(at, IntensityEvent.EnemyShotNearby);
                    if (rng.NextDouble() < hurtChance)
                    {
                        Play(at + 0.06, "player.hurt");
                        Heat(at + 0.06, IntensityEvent.DamageTaken);
                    }
                    else if (rng.NextDouble() < 0.3)
                    {
                        Play(at + 0.07, rng.NextDouble() < 0.5 ? "impact.ricochet" : "impact.dirt", (float)R(-0.8, 0.8), (float)R(2, 7));
                    }
                }
            }
            void Pop(double t, string size, float side, float metres, bool kill)
            {
                Play(t, "gore.pop." + size, side, metres);
                Heat(t, IntensityEvent.PartSevered);
                Play(t + R(0.5, 0.9), "gore.splat", side, metres + 2f);
                if (kill)
                {
                    Play(t + 0.03, "hit.kill");
                    Heat(t, IntensityEvent.Kill);
                    Reward(t + 0.25, RewardKind.Tier1);
                }
            }

            // ---- 0:00 Exploration: on foot on a thin-aired world.
            Cap(0, "EXPLORATION  -  on foot, thin air, nobody about");
            Space(0, 0.3f, Enclosure.Open);
            Ctx(0, "surface");
            Threat(0, 8);
            On(0, "amb.wind.thin", 1f, 3f);
            On(0, "player.breath.loop", 0.7f, 3f);
            Steps(4, 19, "player.step.regolith", 0.56);
            Threat(16, 26);
            Play(21, "player.interact");
            Steps(24, 36, "player.step.regolith", 0.56);
            Play(37.5, "player.jetpack.burst");
            Play(39.4, "player.land");
            Threat(34, 34);
            Steps(41, 52, "player.step.regolith", 0.56);

            // ---- 0:55 Discovery: an old dish, still listening.
            Cap(55, "DISCOVERY  -  an old dish, still switched on");
            On(53, "amb.radio.static", 0.5f, 4f);
            Ctx(55, "mystery");
            Threat(55, 28);
            Steps(57, 63, "player.step.regolith", 0.7);
            Play(66, "player.interact");
            Reward(67, RewardKind.Discovery);
            Level(68, "amb.radio.static", 0.9f, 3f);
            Threat(72, 48);
            Play(80, "player.interact");
            Reward(81, RewardKind.LootCommon);
            Reward(81.6, RewardKind.Tier2);
            for (int i = 0; i < 14; i++) Reward(82.2 + i * 0.07, RewardKind.PointsTick);
            Off(90, "amb.radio.static", 5f);

            // ---- 1:38 Rising tension: the signal woke something up.
            Cap(98, "RISING TENSION  -  the signal woke something up");
            Ctx(98, "surface");
            Threat(98, 36);
            Steps(104, 116, "player.step.regolith", 0.5);
            Threat(108, 44);
            Enemy(113, "weapon.rifle.shot", 3, 0.11, -0.7f, 90f, 0);
            Threat(116, 52);
            Play(119, "weapon.mag_in");
            Play(119.5, "weapon.bolt_rack");
            Enemy(122, "weapon.smg.shot", 5, 0.075, 0.6f, 60f, 0);
            Play(125, "player.suit.alarm");
            Threat(125, 57);
            Steps(126, 131, "player.step.regolith", 0.36);

            // ---- 2:13 Combat.
            Cap(133, "COMBAT  -  rifle and hand cannon against rifles and SMGs");
            Threat(133, 64);
            Enemy(133.2, "weapon.rifle.shot", 4, 0.1, -0.4f, 35f, 0);
            Threat(142, 72);
            Burst(136.6, "weapon.rifle.shot", 6, 0.1, 0.5, "impact.flesh");
            Enemy(138.0, "weapon.smg.shot", 7, 0.07, 0.7f, 28f, 0);
            Burst(139.4, "weapon.rifle.shot", 8, 0.1, 0.6, "impact.armor");
            Pop(140.3, "medium", 0.3f, 16f, false);
            Burst(141.6, "weapon.rifle.shot", 5, 0.1, 0.7, "impact.flesh");
            Pop(142.2, "head", 0.2f, 15f, true);
            Enemy(143.2, "weapon.rifle.shot", 5, 0.1, -0.8f, 30f, 0);
            Play(144.6, "weapon.mag_out");
            Play(145.5, "weapon.mag_in");
            Play(146.0, "weapon.bolt_rack");
            Burst(146.8, "weapon.handcannon.shot", 1, 0.4, 1, "impact.armor");
            Burst(147.5, "weapon.handcannon.shot", 1, 0.4, 1, "impact.flesh");
            Pop(147.6, "big", -0.5f, 18f, false);
            Enemy(148.4, "weapon.smg.shot", 8, 0.07, 0.5f, 20f, 0);
            Burst(149.6, "weapon.handcannon.shot", 1, 0.4, 1, "impact.flesh");
            Pop(149.7, "torso", 0.4f, 14f, true);
            Reward(150.4, RewardKind.Multiplier);

            Cap(152, "CHAOS  -  reinforcements; the space guns come out");
            Threat(152, 90);
            Enemy(152.3, "weapon.rifle.shot", 6, 0.1, -0.6f, 26f, 0.3);
            Burst(153.4, "weapon.raygun.shot", 4, 0.28, 0.75, "impact.flesh");
            Pop(154.1, "small", -0.3f, 12f, false);
            Enemy(155.0, "weapon.smg.shot", 9, 0.07, 0.8f, 18f, 0.2);
            Burst(156.2, "weapon.laser_shotgun.shot", 1, 0.5, 1, "impact.flesh");
            Pop(156.3, "torso", 0.1f, 8f, true);
            Burst(157.6, "weapon.laser_shotgun.shot", 1, 0.5, 1, "impact.armor");
            Pop(157.7, "big", -0.2f, 9f, false);
            Enemy(158.6, "weapon.rifle.shot", 5, 0.1, 0.3f, 40f, 0.25);
            Burst(160.0, "weapon.laser_revolver.shot", 3, 0.42, 0.8, "impact.flesh");
            Pop(160.9, "head", -0.4f, 20f, true);
            Enemy(162.0, "weapon.smg.shot", 6, 0.07, -0.7f, 24f, 0.2);
            Burst(163.4, "weapon.rifle.shot", 10, 0.1, 0.6, "impact.flesh");
            Pop(164.0, "medium", 0.5f, 17f, false);
            Pop(164.5, "medium", 0.5f, 17f, false);
            Enemy(165.4, "weapon.rifle.shot", 4, 0.1, 0.6f, 55f, 0.2);
            Play(167.0, "weapon.sniper.shot");
            Heat(167.0, IntensityEvent.PlayerShot);
            Play(167.12, "impact.flesh", 0.6f, 55f);
            Play(167.13, "hit.headshot");
            Pop(167.15, "head", 0.6f, 55f, true);
            Play(168.2, "weapon.sniper.bolt");
            Enemy(169.5, "weapon.smg.shot", 5, 0.07, -0.3f, 22f, 0.2);
            Burst(171.0, "weapon.rifle.shot", 7, 0.1, 0.7, "impact.flesh");
            Pop(171.6, "torso", -0.3f, 22f, true);
            Reward(172.2, RewardKind.Tier3);

            // ---- The last one runs; the fight is over and the music is allowed to end properly.
            Threat(172.5, 20);
            Play(174.0, "weapon.suppressed.shot");
            Play(174.6, "weapon.suppressed.shot");
            Play(174.68, "impact.flesh", 0.2f, 30f);
            Play(174.7, "hit.marker");
            Threat(178, 6);

            // ---- 3:10 Loot and scoring.
            Cap(190, "LOOT AND SCORING  -  the payout");
            Steps(186, 191, "player.step.regolith", 0.56);
            Play(192, "player.interact");
            Reward(192.6, RewardKind.LootCommon);
            Reward(194.4, RewardKind.Tier1);
            Reward(195.6, RewardKind.Tier2);
            Reward(197.0, RewardKind.Tier3);
            Reward(198.8, RewardKind.Multiplier);
            On(200.2, "reward.points.rack", 1f, 0.05f);
            Off(203.4, "reward.points.rack", 0.05f);
            Play(203.4, "reward.points.rack_end");
            Reward(205.0, RewardKind.Tier4);
            Play(209.5, "player.interact");
            Reward(210.2, RewardKind.LootRare);
            Threat(206, 22);

            // ---- 3:36 Back aboard and take-off.
            Cap(216, "TAKE-OFF  -  back aboard, buckle in, light the engine");
            Steps(214, 219, "player.step.regolith", 0.5);
            Steps(219.5, 223, "player.step.metal", 0.52);
            Space(222, 1f, Enclosure.Cockpit);
            Off(222, "amb.wind.thin", 1.5f);
            On(222, "ship.cabin.loop", 1f, 1.5f);
            Level(222, "player.breath.loop", 0.25f, 2f);
            Play(224.5, "ship.seatbelt");
            Play(227.0, "ship.switch");
            Play(227.7, "ship.switch");
            Play(228.5, "ship.switch");
            Play(229.6, "ship.confirm");
            Play(231.5, "ship.engine.ignite");
            On(231.9, "ship.engine.loop", 1f, 1.2f);
            Ctx(232.0, "flight");
            Threat(232, 30);
            Threat(240, 42);
            Level(244, "ship.engine.loop", 0.6f, 4f);
            Play(250, "ship.rcs");
            Play(251.2, "ship.rcs");
            Threat(252, 56);
            Play(262, "ship.rcs");
            Level(262, "ship.engine.loop", 0.9f, 2f);

            // ---- 4:35 Engine off. Coasting.
            Cap(275, "RETURN TO CALM  -  engine off, coasting");
            Off(274, "ship.engine.loop", 2.5f);
            Threat(272, 20);
            Ctx(275, "space");
            Space(276, 1f, Enclosure.Cockpit);
            Play(279, "ship.rcs");
            Play(281, "ship.switch");
            Level(280, "player.breath.loop", 0.5f, 4f);
            // The quiet theme comes back for the last few bars: pad and bells only.
            Ctx(283, "surface");
            Threat(283, 8);
            Off(292, "ship.cabin.loop", 7f);
            Off(292, "player.breath.loop", 7f);
            s.Add(new DemoStep { Time = Length, Kind = DemoStepKind.End });

            // OrderBy keeps steps that share a time in the order they were written.
            return s.OrderBy(step => step.Time).ToList();
        }
    }
}
