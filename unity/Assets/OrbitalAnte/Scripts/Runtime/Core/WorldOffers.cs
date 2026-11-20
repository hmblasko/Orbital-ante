using System.Collections.Generic;
using UnityEngine;

namespace OrbitalAnte.Core
{
    /// <summary>
    /// Something out in the world the player can look at and use with Interact (a crate to pick up, a supply pod
    /// to open). The suit asks every offer in turn, nearest first is the offer's own business; the first that
    /// answers takes the prompt and the press. Added for the ante tasks (20 Nov 2026), so the ship code needs no
    /// knowledge of them.
    /// </summary>
    public interface IWorldOffer
    {
        /// <param name="eye">The player's eye, world space.</param>
        /// <param name="forward">Where they are looking.</param>
        /// <param name="dt">Seconds since last frame (for holds).</param>
        /// <param name="prompt">What to show; empty for nothing.</param>
        /// <param name="progress">How far a hold has got, 0 to 1, or below 0 for no hold.</param>
        /// <returns>True when this offer took the player's attention this frame.</returns>
        bool Offer(Vector3 eye, Vector3 forward, float dt, out string prompt, out float progress);
    }

    public static class WorldOffers
    {
        static readonly List<IWorldOffer> _offers = new List<IWorldOffer>();

        public static void Add(IWorldOffer offer) { if (offer != null && !_offers.Contains(offer)) _offers.Add(offer); }
        public static void Remove(IWorldOffer offer) => _offers.Remove(offer);

        public static bool Ask(Vector3 eye, Vector3 forward, float dt, out string prompt, out float progress)
        {
            prompt = string.Empty;
            progress = -1f;
            for (int i = 0; i < _offers.Count; i++)
            {
                if (_offers[i] == null) continue;
                if (_offers[i].Offer(eye, forward, dt, out prompt, out progress)) return true;
            }
            return false;
        }

        public static void ResetStatics() => _offers.Clear();
    }
}
