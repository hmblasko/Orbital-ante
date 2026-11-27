using System;

namespace OrbitalAnte.Ante
{
    /// <summary>
    /// The four attributes bought with points between antes. They last for the run only. Movement is the
    /// player's walking and jumping: nothing here ever touches ship speed.
    /// </summary>
    public sealed class AttributeShop
    {
        readonly AnteBalance _balance;
        readonly int[] _levels = new int[4];
        readonly Func<int> _ante;

        /// <param name="ante">The ante now, so prices follow what points are worth (21 Nov 2026). Null keeps the listed prices.</param>
        public AttributeShop(AnteBalance balance, Func<int> ante = null)
        {
            _balance = balance;
            _ante = ante;
        }

        /// <summary>
        /// What prices are multiplied by: the ante's value scale K, so a level costs the same effort in every ante
        /// (21 Nov 2026: the run simulator found points piling up far past anything the shop sold by ante 6).
        /// </summary>
        public float PriceScale => PriceScaleAt(CurrentAnte);

        int CurrentAnte => Math.Max(1, _ante?.Invoke() ?? 1);
        float PriceScaleAt(int ante) => _ante == null || !_balance.shopPricesFollowValue ? 1f : (float)ScoreMath.ValueScale(_balance, ante);

        public int Level(PlayerAttribute attribute) => _levels[(int)attribute];

        public bool IsMaxed(PlayerAttribute attribute) => Level(attribute) >= MaxLevel(attribute);

        int MaxLevel(PlayerAttribute attribute)
        {
            int[] prices = _balance.PricesFor(attribute);
            return Math.Min(_balance.maxAttributeLevel, prices == null ? 0 : prices.Length);
        }

        /// <summary>Price of the next level, or -1 if there is none.</summary>
        public int NextPrice(PlayerAttribute attribute) => PriceAt(attribute, CurrentAnte);

        int PriceAt(PlayerAttribute attribute, int ante)
        {
            if (IsMaxed(attribute)) return -1;
            int listed = _balance.PricesFor(attribute)[Level(attribute)];
            float scale = PriceScaleAt(ante);
            if (scale <= 1f) return listed;
            double scaledPrice = Math.Round(listed * scale / 10.0, MidpointRounding.AwayFromZero) * 10;
            return (int)Math.Min(int.MaxValue, scaledPrice);
        }

        public readonly struct PurchaseQuote
        {
            public readonly int Price;
            public readonly int PriceNextAnte;
            public readonly bool CanBuy;
            public readonly int WalletAfter;
            public readonly int PointsNeeded;

            internal PurchaseQuote(int price, int priceNextAnte, RunLedger ledger)
            {
                Price = price;
                PriceNextAnte = priceNextAnte;
                CanBuy = ledger.CanAfford(price);
                WalletAfter = CanBuy ? ledger.SpendablePoints - price : ledger.SpendablePoints;
                PointsNeeded = price > ledger.SpendablePoints ? price - ledger.SpendablePoints : 0;
            }
        }

        /// <summary>What buying this level would leave in the wallet, and what waiting would cost for the same level.</summary>
        public PurchaseQuote Quote(PlayerAttribute attribute, RunLedger ledger)
        {
            int ante = CurrentAnte;
            int price = PriceAt(attribute, ante);
            int later = price < 0 || ante == int.MaxValue ? price : PriceAt(attribute, ante + 1);
            return new PurchaseQuote(price, later, ledger);
        }

        /// <summary>Buys one level from the wallet. The ante score and the leaderboard score are not touched.</summary>
        public bool TryBuy(PlayerAttribute attribute, RunLedger ledger)
        {
            int price = NextPrice(attribute);
            if (price < 0 || ledger == null || !ledger.TrySpend(price)) return false;
            _levels[(int)attribute]++;
            return true;
        }

        /// <summary>Multiply weapon damage by this. +8% per level, added.</summary>
        public float DamageMultiplier => 1f + _balance.damagePerLevel * Level(PlayerAttribute.Damage);
        /// <summary>Multiply player walk speed and jump by this. +6% per level, added.</summary>
        public float MovementMultiplier => 1f + _balance.movementPerLevel * Level(PlayerAttribute.Movement);
        /// <summary>Multiply oxygen capacity by this. +10% per level, added.</summary>
        public float OxygenMultiplier => 1f + _balance.oxygenPerLevel * Level(PlayerAttribute.Oxygen);
        /// <summary>Multiply incoming damage by this. 6% less per level, compounding.</summary>
        public float IncomingDamageMultiplier => (float)Math.Pow(1.0 - _balance.defensePerLevel, Level(PlayerAttribute.Defense));

        public int[] ExportLevels() => (int[])_levels.Clone();

        public void ImportLevels(int[] levels)
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                int level = levels != null && i < levels.Length ? levels[i] : 0;
                _levels[i] = Math.Max(0, Math.Min(MaxLevel((PlayerAttribute)i), level));
            }
        }
    }
}
