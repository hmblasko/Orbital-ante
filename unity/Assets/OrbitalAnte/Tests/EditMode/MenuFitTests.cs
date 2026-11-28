using NUnit.Framework;
using OrbitalAnte.UI;

namespace OrbitalAnte.Tests
{
    /// <summary>
    /// The pause menu's choices page stays on the screen (UI/MenuFit, 27 Nov 2026): the Deck's canvas is 800 units
    /// tall and a 16:9 monitor's 759, and a goal unlocked at the break makes the break's words longer. Plain C#.
    /// </summary>
    public class MenuFitTests
    {
        const float Deck = 800f, Wide = 759f;

        [Test]
        public void TheFullBreak_KeepsItsButtons_OnTheDeck()
        {
            // Seven rows under a 176-unit body: 56-unit buttons, as the 25 Nov 2026 review set by hand.
            Assert.AreEqual(56f, MenuFit.ButtonHeight(7, 176f, 56f, Deck));
            Assert.LessOrEqual(MenuFit.CardHeight(7, 176f, 56f), Deck - 2f * MenuFit.Margin);
        }

        [Test]
        public void EveryBreak_FitsTheDeck_AndAWideScreen_EvenWithTwoUnlockLines()
        {
            foreach (float screen in new[] { Deck, Wide })
            {
                for (int rows = 1; rows <= 7; rows++)
                {
                    foreach (float body in new[] { 0f, 120f, 176f, 190f, 220f })
                    {
                        float h = MenuFit.ButtonHeight(rows, body, 70f, screen);
                        Assert.GreaterOrEqual(h, MenuFit.LeastButton);
                        Assert.LessOrEqual(h, 70f);
                        if (h > MenuFit.LeastButton)
                            Assert.LessOrEqual(MenuFit.CardHeight(rows, body, h), screen - 2f * MenuFit.Margin + 0.01f,
                                rows + " rows, body " + body + " on " + screen);
                    }
                }
            }
        }

        [Test]
        public void EveryPage_StaysOnTheScreen_EvenWhenItsWordsAreLong()
        {
            // Unity review, 27 Nov 2026: at the button floor a long body still pushed the card off a 16:9 screen.
            foreach (float screen in new[] { Deck, Wide })
            {
                for (int rows = 0; rows <= 7; rows++)
                {
                    foreach (float body in new[] { 0f, 120f, 176f, 220f, 264f, 320f, 480f })
                    {
                        MenuFit.Fit(rows, body, 70f, screen, out float shown, out float button);
                        string at = rows + " rows, body " + body + " on " + screen;
                        Assert.GreaterOrEqual(button, MenuFit.LeastButton, at);
                        Assert.LessOrEqual(button, 70f, at);
                        Assert.LessOrEqual(shown, body, at);
                        Assert.GreaterOrEqual(shown, 0f, at);
                        Assert.LessOrEqual(MenuFit.CardHeight(rows, shown, button), screen - 2f * MenuFit.Margin + 0.01f, at);
                        if (button > MenuFit.LeastButton) Assert.AreEqual(body, shown, at + ": the buttons give way before the words");
                    }
                }
            }
            // The full break of 25 Nov 2026 is untouched.
            MenuFit.Fit(7, 176f, 56f, Deck, out float b, out float h);
            Assert.AreEqual(176f, b);
            Assert.AreEqual(56f, h);
        }

        [Test]
        public void TheCanvasHeight_IsWorkedOutFromTheScreen()
        {
            Assert.AreEqual(800f, MenuFit.CanvasHeight(1280f, 800f), 0.01f, "the Deck");
            Assert.AreEqual(800f, MenuFit.CanvasHeight(2560f, 1600f), 0.01f, "the same shape, bigger");
            Assert.AreEqual(758.9f, MenuFit.CanvasHeight(1920f, 1080f), 0.1f, "16:9");
            Assert.AreEqual(758.9f, MenuFit.CanvasHeight(3840f, 2160f), 0.1f);
            Assert.AreEqual(800f, MenuFit.CanvasHeight(0f, 0f), "no screen yet: the reference height");
            Assert.AreEqual(720f, MenuFit.CanvasHeight(1920f, 1080f, 1280f, 800f, 0f), 0.1f, "matched by width only: 1080 / 1.5");
        }

        [Test]
        public void ShortPages_KeepTheHeightTheyAskFor()
        {
            Assert.AreEqual(70f, MenuFit.ButtonHeight(2, 60f, 70f, Deck));
            Assert.AreEqual(70f, MenuFit.ButtonHeight(0, 60f, 70f, Deck));
            Assert.AreEqual(70f, MenuFit.ButtonHeight(3, 60f, 70f, 0f), "no canvas yet: the Deck's height is assumed");
        }

        [Test]
        public void Lines_CountWrappedLines()
        {
            Assert.AreEqual(0, MenuFit.Lines(null, 60));
            Assert.AreEqual(0, MenuFit.Lines(string.Empty, 60));
            Assert.AreEqual(1, MenuFit.Lines("short", 60));
            Assert.AreEqual(2, MenuFit.Lines("one\ntwo", 60));
            Assert.AreEqual(3, MenuFit.Lines(new string('x', 61) + "\nthree", 60));
            Assert.AreEqual(3, MenuFit.Lines("a\n\nb", 60), "an empty line still takes a line");
            Assert.AreEqual(176f, MenuFit.BodyHeight("one line", 176f, 22f, 64));
            Assert.AreEqual(220f, MenuFit.BodyHeight("1\n2\n3\n4\n5\n6\n7\n8\n9\n10", 176f, 22f, 64));
        }
    }
}
