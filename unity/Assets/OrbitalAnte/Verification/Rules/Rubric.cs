using System;
using System.Collections.Generic;

namespace OrbitalAnte.Verification
{
    /// <summary>
    /// The aesthetic scoring rubric from Matthew's quality brief (12 Nov 2026). The weights and the pass mark
    /// are fixed here, in code, so they cannot drift between comparable reviews. Changing them is a design
    /// decision for Matthew, not for the Critic, the Builder or the Orchestrator.
    /// </summary>
    public static class Rubric
    {
        public const int Version = 1;
        /// <summary>Lowest overall score that satisfies the aesthetic half of the gate.</summary>
        public const double PassMark = 8.5;

        public sealed class Category
        {
            public readonly string Id;
            public readonly string Name;
            public readonly double Weight;
            public Category(string id, string name, double weight) { Id = id; Name = name; Weight = weight; }
        }

        public static readonly Category[] Categories =
        {
            new Category("cohesion", "Aesthetic cohesion and art direction", 20),
            new Category("modeling", "Modeling, geometry, and silhouettes", 20),
            new Category("environment", "Environmental composition and visual interest", 15),
            new Category("materials", "Materials, lighting, and visual effects", 15),
            new Category("animation", "Animation and movement presentation", 15),
            new Category("readability", "Gameplay readability and visual feedback", 10),
            new Category("polish", "Overall polish and distinctive appeal", 5),
        };

        public static Category Find(string id)
        {
            foreach (Category c in Categories) if (c.Id == id) return c;
            return null;
        }

        /// <summary>
        /// Weighted score over the categories that apply, with their weights scaled up to fill 100%. A category
        /// that does not apply is left out; it is never given a free 10. Rounded to one decimal only at the end.
        /// Returns null when nothing applies.
        /// </summary>
        public static double? Overall(IEnumerable<KeyValuePair<string, double>> applicableScores)
        {
            double sum = 0, weight = 0;
            foreach (KeyValuePair<string, double> pair in applicableScores)
            {
                Category c = Find(pair.Key);
                if (c == null) throw new ArgumentException("Unknown rubric category '" + pair.Key + "'.");
                sum += pair.Value * c.Weight;
                weight += c.Weight;
            }
            if (weight <= 0) return null;
            return Math.Round(sum / weight, 1, MidpointRounding.AwayFromZero);
        }

        public static string Classify(double score)
        {
            if (score >= 9.5) return "Exceptional commercial-quality presentation";
            if (score >= 9.0) return "Outstanding, highly polished indie quality";
            if (score >= 8.5) return "Strong commercial-quality indie presentation";
            if (score >= 8.0) return "Very good, but additional refinement needed";
            if (score >= 7.0) return "Good indie quality";
            if (score >= 6.0) return "Adequate but noticeably unpolished";
            if (score >= 5.0) return "Programmer-art or early prototype quality";
            if (score >= 3.0) return "Weak or inconsistent presentation";
            return "Severely broken or incomplete";
        }
    }
}
