using OrbitalAnte.Dismemberment;
using UnityEngine;

namespace OrbitalAnte.Enemies
{
    /// <summary>
    /// The faction's band colour on a body: a tint on the helmet and chest only (through a property block, so the
    /// shared materials stay shared). A stand-in until Faction A's own suits are modelled with their band.
    /// </summary>
    public static class FactionLook
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock _block;

        public static void Band(Mannequin body, FactionFieldRules rules)
        {
            if (body == null || rules == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();
            _block.Clear();
            _block.SetColor(BaseColor, new Color(rules.bandRed, rules.bandGreen, rules.bandBlue, 1f));
            Tint(body.Part(BodyPartId.Torso));
            Tint(body.Part(BodyPartId.Head));
        }

        static void Tint(BodyPart part)
        {
            if (part == null) return;
            // Only this part's own shapes: the arms and head hang under the torso and keep their own paint.
            foreach (Renderer r in part.GetComponentsInChildren<Renderer>(true))
                if (r.GetComponentInParent<BodyPart>() == part) r.SetPropertyBlock(_block);
        }
    }
}
