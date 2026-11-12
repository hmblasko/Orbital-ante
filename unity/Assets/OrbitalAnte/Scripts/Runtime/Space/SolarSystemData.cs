using System;
using System.Collections.Generic;
using OrbitalAnte.Content;
using UnityEngine;

namespace OrbitalAnte.Space
{
    /// <summary>
    /// The solar system as data: every body's size, gravity, orbit and spin, plus the asteroid belt. An asset is
    /// created at Assets/OrbitalAnte/Resources/SolarSystem/SolarSystem, so all of it can be changed in the
    /// Inspector. Sizes are the same every run; the seed only decides where along its orbit each body starts.
    /// </summary>
    [CreateAssetMenu(menuName = "Orbital Ante/Solar System", fileName = "SolarSystem")]
    public sealed class SolarSystemData : ContentDefinition
    {
        public const string ResourceFolder = "SolarSystem";
        public const string DefaultId = "SolarSystem";

        [Tooltip("The Sun, planets and moons. Distances in metres, times in seconds, angles in degrees.")]
        public List<BodySpec> bodies = SolarSystemDefaults.Bodies();
        public AsteroidBeltSpec asteroidBelt = new AsteroidBeltSpec();

        public override string DisplayName => "Solar system";

        public static SolarSystemData CreateDefaults()
        {
            var data = CreateInstance<SolarSystemData>();
            data.name = DefaultId;
            return data;
        }

        /// <summary>The asset if it exists, otherwise the built-in defaults.</summary>
        public static SolarSystemData Load() => ContentDatabase.Get<SolarSystemData>(DefaultId);

        /// <summary>Every body for a run: the fixed ones plus this seed's asteroids.</summary>
        public List<BodySpec> BuildBodies(int seed)
        {
            var all = new List<BodySpec>(bodies.Count + asteroidBelt.count);
            string sunId = SolarSystemDefaults.SunId;
            for (int i = 0; i < bodies.Count; i++)
            {
                all.Add(bodies[i].Clone());
                if (string.IsNullOrEmpty(bodies[i].parentId)) sunId = bodies[i].id;
            }
            all.AddRange(AsteroidBelt.Generate(seed, asteroidBelt, sunId));
            return all;
        }

        public override void Validate(List<string> problems)
        {
            if (bodies == null || bodies.Count == 0)
            {
                problems.Add("has no bodies.");
                return;
            }
            for (int i = 0; i < bodies.Count; i++)
            {
                BodySpec b = bodies[i];
                string who = string.IsNullOrEmpty(b.id) ? "body " + (i + 1) : b.id;
                if (string.IsNullOrEmpty(b.id)) problems.Add(who + " has no id.");
                if (b.radius <= 0) problems.Add(who + ": radius must be above zero.");
                if (b.surfaceGravity <= 0) problems.Add(who + ": surface gravity must be above zero.");
                if (!string.IsNullOrEmpty(b.parentId) && b.orbitRadius <= 0) problems.Add(who + ": orbit radius must be above zero.");
                if (b.dayLength < 0) problems.Add(who + ": day length cannot be negative.");
                if (b.ringOuterRadius > 0 && b.ringInnerRadius <= b.radius) problems.Add(who + ": rings must start outside the surface.");
            }
            if (asteroidBelt.count < 0 || asteroidBelt.count > 300) problems.Add("asteroid count should be between 0 and 300.");
            if (asteroidBelt.outerRadius <= asteroidBelt.innerRadius) problems.Add("the asteroid belt's outer edge must be beyond its inner edge.");

            // The deeper checks (one Sun, no loops, moons clear of their planets) need the model itself.
            try
            {
                var model = new SolarSystemModel(BuildBodies(1), 1);
                for (int i = 1; i < model.Count; i++)
                {
                    int parent = model.ParentOf(i);
                    BodySpec b = model.Spec(i);
                    if (b.kind == BodyKind.Asteroid) continue;
                    if (b.orbitRadius - b.radius <= model.Spec(parent).radius)
                        problems.Add(b.id + " orbits inside " + model.Spec(parent).id + ".");
                    else if (parent != 0 && b.orbitRadius - model.InfluenceRadius(i) <= model.LockRadius(parent))
                        problems.Add(b.id + " orbits too close to " + model.Spec(parent).id + ". Move it out a little.");
                }
            }
            catch (ArgumentException e)
            {
                problems.Add(e.Message);
            }
        }
    }
}
