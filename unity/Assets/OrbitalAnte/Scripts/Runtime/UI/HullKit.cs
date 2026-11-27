using UnityEngine;
using UnityEngine.UI;

namespace OrbitalAnte.UI
{
    /// <summary>
    /// The look of the player's screen on foot (Matthew, 26 Nov 2026: chose "Hull stencil", option 2 of the player
    /// screen mock-ups in <c>docs/reports/player-hud.md</c>): dark bolted plates like painted spacecraft hardware,
    /// big numbers in a stencil face, words in a mono face, cream paint, hazard orange for anything to act on.
    ///
    /// Everything here is a building block; what each plate says is decided by its owner (the ante loop, the
    /// suit, the gun ladder) with the numbers from <see cref="HullReadout"/>. Shapes are pictures made once in
    /// code, and colours that change in play are set on the renderer (<see cref="Tint"/>), which does not make
    /// the canvas lay itself out again.
    /// </summary>
    public static class HullKit
    {
        /// <summary>Cream paint: numbers and anything that is simply true.</summary>
        public static readonly Color Paint = new Color(0.937f, 0.902f, 0.824f);
        /// <summary>Hazard orange: anything to act on.</summary>
        public static readonly Color Orange = new Color(1f, 0.478f, 0.102f);
        /// <summary>Small names above the numbers.</summary>
        public static readonly Color Dim = new Color(0.663f, 0.635f, 0.580f);
        /// <summary>Second lines under the numbers.</summary>
        public static readonly Color Sub = new Color(0.812f, 0.780f, 0.706f);
        public static readonly Color Good = new Color(0.624f, 0.878f, 0.478f);
        public static readonly Color Cool = new Color(0.561f, 0.827f, 1f);
        public static readonly Color PlateFace = new Color(0.118f, 0.122f, 0.129f, 0.94f);
        public static readonly Color PlateEdge = new Color(0.278f, 0.282f, 0.294f, 0.98f);
        /// <summary>The dark well a block, tile or tank sits in, and a block that is not lit.</summary>
        public static readonly Color Well = new Color(0.047f, 0.047f, 0.051f, 0.96f);
        public static readonly Color Unlit = new Color(0.20f, 0.204f, 0.212f, 1f);
        static readonly Color Screw = new Color(0.333f, 0.341f, 0.353f);

        /// <summary>Gap from the screen's edge to a plate, in the 1280 x 800 reference canvas.</summary>
        public const float Margin = 24f;

        static Font _stencil, _mono, _monoBold;
        static Sprite _chevron, _hazard, _reticle, _white;

        /// <summary>
        /// The numbers' face: Saira Stencil One (SIL Open Font Licence, <c>Resources/Fonts</c>). Falls back to the
        /// game's bold lettering if the file is missing.
        /// </summary>
        public static Font StencilFont
        {
            get
            {
                if (_stencil != null) return _stencil;
                _stencil = Resources.Load<Font>("Fonts/SairaStencilOne-Regular");
                if (_stencil == null) _stencil = UiKit.BoldFont;
                return _stencil;
            }
        }

        /// <summary>The words' face: Overpass Mono SemiBold (SIL Open Font Licence).</summary>
        public static Font MonoFont
        {
            get
            {
                if (_mono != null) return _mono;
                _mono = Resources.Load<Font>("Fonts/OverpassMono-SemiBold");
                if (_mono == null) _mono = UiKit.Font;
                return _mono;
            }
        }

        public static Font MonoBoldFont
        {
            get
            {
                if (_monoBold != null) return _monoBold;
                _monoBold = Resources.Load<Font>("Fonts/OverpassMono-Bold");
                if (_monoBold == null) _monoBold = UiKit.BoldFont;
                return _monoBold;
            }
        }

        /// <summary>Gives an existing label the stencil face (no fake bold on top).</summary>
        public static Text AsStencil(Text label)
        {
            label.font = StencilFont;
            label.fontStyle = FontStyle.Normal;
            return label;
        }

        /// <summary>Gives an existing label the mono face.</summary>
        public static Text AsMono(Text label, bool bold = true)
        {
            label.font = bold ? MonoBoldFont : MonoFont;
            label.fontStyle = FontStyle.Normal;
            return label;
        }

        /// <summary>A stencil number or word. Pass white for one whose colour changes in play, and colour it with <see cref="Tint"/>.</summary>
        public static Text Stencil(Transform parent, string text, int size, Color colour, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Text label = AsStencil(UiKit.Label(parent, text, size, colour, anchor));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        public static Text Mono(Transform parent, string text, int size, Color colour, TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = true)
        {
            Text label = AsMono(UiKit.Label(parent, text, size, colour, anchor), bold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            return label;
        }

        /// <summary>Sets a colour on the renderer only when it differs, so an unchanged graphic costs the canvas nothing.</summary>
        public static void Tint(Graphic g, Color c)
        {
            if (g.canvasRenderer.GetColor() != c) g.canvasRenderer.SetColor(c);
        }

        /// <summary>
        /// Places a rect inside a plate by its left edge and the height of its middle, both measured from the
        /// plate's top left corner (y down, as the mock-ups were drawn).
        /// </summary>
        public static RectTransform At(RectTransform rt, float x, float middleY, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -middleY);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static Text At(Text label, float x, float middleY, float width, float height)
        {
            At((RectTransform)label.transform, x, middleY, width, height);
            return label;
        }

        /// <summary>The same, by the right edge (for words that end at a column's right side).</summary>
        public static Text AtRight(Text label, float right, float middleY, float width, float height)
        {
            var rt = (RectTransform)label.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(right, -middleY);
            rt.sizeDelta = new Vector2(width, height);
            return label;
        }

        /// <summary>
        /// A bolted plate pinned to a corner or edge of the screen: a thin lighter edge, a dark face and two screw
        /// heads in the top corners. Children go straight on the returned rect.
        /// </summary>
        public static RectTransform Plate(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size, bool screws = true)
        {
            RectTransform plate = UiKit.NewRect(name, parent);
            UiKit.Pin(plate, anchor, offset, size);
            UiKit.AddImage(plate, PlateEdge);
            RectTransform face = UiKit.NewRect("Face", plate);
            UiKit.Stretch(face);
            face.offsetMin = new Vector2(1f, 1f);
            face.offsetMax = new Vector2(-1f, -1f);
            UiKit.AddImage(face, PlateFace);
            if (screws)
            {
                for (int i = 0; i < 2; i++)
                {
                    RectTransform s = UiKit.NewRect("Screw", plate);
                    s.anchorMin = s.anchorMax = new Vector2(i, 1f);
                    s.pivot = new Vector2(0.5f, 0.5f);
                    s.anchoredPosition = new Vector2(i == 0 ? 9f : -9f, -9f);
                    s.sizeDelta = new Vector2(6f, 6f);
                    UiKit.AddImage(s, Screw, UiKit.Circle);
                }
            }
            return plate;
        }

        /// <summary>A plain rectangle of colour inside a plate, placed like <see cref="At(RectTransform,float,float,float,float)"/> but by its top.</summary>
        public static Image Box(RectTransform plate, string name, float x, float top, float width, float height, Color colour)
        {
            RectTransform rt = UiKit.NewRect(name, plate);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -top);
            rt.sizeDelta = new Vector2(width, height);
            return UiKit.AddImage(rt, colour);
        }

        /// <summary>
        /// A row of blocks that light from the left. They are white underneath: light them with <see cref="Light"/>.
        /// </summary>
        public static Image[] Blocks(RectTransform plate, string name, float x, float top, int count, float blockWidth, float height, float gap, Sprite sprite = null)
        {
            var blocks = new Image[count];
            for (int i = 0; i < count; i++)
            {
                blocks[i] = Box(plate, name + i, x + i * (blockWidth + gap), top, blockWidth, height, Color.white);
                if (sprite != null) blocks[i].sprite = sprite;
                Tint(blocks[i], Unlit);
            }
            return blocks;
        }

        /// <summary>Lights the first <paramref name="lit"/> of a row in <paramref name="colour"/>; the rest go dark.</summary>
        public static void Light(Image[] blocks, int lit, Color colour)
        {
            for (int i = 0; i < blocks.Length; i++) Tint(blocks[i], i < lit ? colour : Unlit);
        }

        /// <summary>The same, with the last lit block in its own colour (a plate being worn through, say).</summary>
        public static void Light(Image[] blocks, int lit, Color colour, Color last)
        {
            for (int i = 0; i < blocks.Length; i++) Tint(blocks[i], i >= lit ? Unlit : i == lit - 1 ? last : colour);
        }

        /// <summary>
        /// One tile of the flip clock: a dark tile with a seam across its middle and a stencil digit. The digit is
        /// white underneath, coloured with <see cref="Tint"/>.
        /// </summary>
        public static Text FlipTile(RectTransform plate, float x, float top, float width = 38f, float height = 54f)
        {
            Image edge = Box(plate, "Tile", x, top, width, height, PlateEdge);
            RectTransform well = UiKit.NewRect("Well", edge.transform);
            UiKit.Stretch(well);
            well.offsetMin = new Vector2(1f, 1f);
            well.offsetMax = new Vector2(-1f, -1f);
            UiKit.AddImage(well, Well);
            Text digit = Stencil(edge.transform, "0", 40, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch((RectTransform)digit.transform);
            RectTransform seam = UiKit.NewRect("Seam", edge.transform);
            seam.anchorMin = new Vector2(0f, 0.5f);
            seam.anchorMax = new Vector2(1f, 0.5f);
            seam.pivot = new Vector2(0.5f, 0.5f);
            seam.anchoredPosition = Vector2.zero;
            seam.sizeDelta = new Vector2(-2f, 1f);
            UiKit.AddImage(seam, new Color(0f, 0f, 0f, 0.55f));
            return digit;
        }

        // ------------------------------------------------------------------ pictures made once

        static Texture2D NewTexture(int width, int height, string name, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = name,
            };
        }

        /// <summary>A plain white picture, for images that are cut (filled) and need a sprite to cut.</summary>
        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;
                Texture2D tex = NewTexture(2, 2, "OA_HullWhite");
                tex.SetPixels32(new[] { (Color32)Color.white, (Color32)Color.white, (Color32)Color.white, (Color32)Color.white });
                tex.Apply(false, true);
                _white = Sprite.Create(tex, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
                _white.name = "OA_HullWhite";
                return _white;
            }
        }

        /// <summary>A white chevron pointing right: one plate of the suit's armour.</summary>
        public static Sprite Chevron
        {
            get
            {
                if (_chevron != null) return _chevron;
                const int w = 34, h = 56;
                Texture2D tex = NewTexture(w, h, "OA_HullChevron");
                var px = new Color32[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // A band half the picture wide that leans right toward the middle row.
                        float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
                        float left = (1f - v) * (w * 0.5f);
                        float fx = x + 0.5f;
                        float a = Mathf.Clamp01(fx - left + 0.5f) * Mathf.Clamp01(left + w * 0.5f - fx + 0.5f);
                        px[y * w + x] = new Color(1f, 1f, 1f, a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _chevron = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _chevron.name = "OA_HullChevron";
                return _chevron;
            }
        }

        /// <summary>Hazard stripes, orange and near black, that repeat (use with <see cref="Image.Type.Tiled"/>).</summary>
        public static Sprite Hazard
        {
            get
            {
                if (_hazard != null) return _hazard;
                const int size = 32;
                Texture2D tex = NewTexture(size, size, "OA_HullHazard", TextureWrapMode.Repeat);
                var px = new Color32[size * size];
                Color dark = new Color(0.094f, 0.098f, 0.106f, 1f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Diagonal bands half the picture wide, softened over a pixel at each edge.
                        float t = Mathf.Repeat((x + y + 0.5f) / size, 1f);
                        float d = Mathf.Min(Mathf.Abs(t - 0.5f), Mathf.Min(t, 1f - t)) * size;
                        float orange = t < 0.5f ? 1f : 0f;
                        float blend = Mathf.Clamp01(d);
                        Color c = Color.Lerp(Color.Lerp(Orange, dark, 0.5f), orange > 0.5f ? Orange : dark, blend);
                        c.a = 1f;
                        px[y * size + x] = c;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _hazard = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _hazard.name = "OA_HullHazard";
                return _hazard;
            }
        }

        /// <summary>A strip of hazard stripes stretched across the top or bottom edge of a plate.</summary>
        public static Image HazardStrip(RectTransform plate, bool atTop, float height)
        {
            RectTransform rt = UiKit.NewRect("Hazard", plate);
            rt.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, atTop ? 1f : 0f);
            rt.anchoredPosition = new Vector2(0f, atTop ? -1f : 1f);
            rt.sizeDelta = new Vector2(-2f, height);
            Image img = UiKit.AddImage(rt, Color.white, Hazard);
            img.type = Image.Type.Tiled;
            // 32 px of picture drawn 24 px across: stripes about the weight of the mock-up's.
            img.pixelsPerUnitMultiplier = 32f / 24f;
            return img;
        }

        /// <summary>
        /// The aiming mark: four corner brackets in cream round a small orange square, each with a dark edge so it
        /// reads over sand, sky and the dark of space alike. Drawn at 48 x 48 in the reference canvas.
        /// </summary>
        public static Sprite Reticle
        {
            get
            {
                if (_reticle != null) return _reticle;
                const int size = 96;
                Texture2D tex = NewTexture(size, size, "OA_HullReticle");
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // Folded into one corner: the picture is the same in all four.
                        float ax = Mathf.Abs(x + 0.5f - size * 0.5f), ay = Mathf.Abs(y + 0.5f - size * 0.5f);
                        float bracket = Mathf.Max(BracketArm(ax, ay), BracketArm(ay, ax));
                        float bracketEdge = Mathf.Max(BracketArm(ax, ay, 3f), BracketArm(ay, ax, 3f));
                        float square = Mathf.Clamp01(5f - Mathf.Max(ax, ay) + 0.5f);
                        float squareEdge = Mathf.Clamp01(7.5f - Mathf.Max(ax, ay) + 0.5f);
                        Color c = new Color(0f, 0f, 0f, Mathf.Max(bracketEdge, squareEdge) * 0.7f);
                        c = Color.Lerp(c, new Color(Paint.r, Paint.g, Paint.b, 1f), bracket);
                        c = Color.Lerp(c, new Color(Orange.r, Orange.g, Orange.b, 1f), square);
                        px[y * size + x] = c;
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _reticle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _reticle.name = "OA_HullReticle";
                return _reticle;
            }
        }

        /// <summary>
        /// One arm of a corner bracket in the folded quarter: a bar 5 px thick along the outside (40 px from the
        /// middle), running 16 px in from the corner. <paramref name="grow"/> fattens it for the dark edge.
        /// </summary>
        static float BracketArm(float along, float across, float grow = 0f)
        {
            const float outer = 42f, thick = 5f, length = 17f;
            float a = Mathf.Clamp01(across - (outer - thick - grow) + 0.5f) * Mathf.Clamp01(outer + grow - across + 0.5f);
            float b = Mathf.Clamp01(along - (outer - length - grow) + 0.5f) * Mathf.Clamp01(outer + grow - along + 0.5f);
            return a * b;
        }
    }
}
