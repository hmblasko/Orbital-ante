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

        public AttributeShop(AnteBalance balance)
        {
            _balance = balance;
        }

        public int Level(PlayerAttribute attribute) => _levels[(int)attribute];

        public bool IsMaxed(PlayerAttribute attribute) => Level(attribute) >= MaxLevel(attribute);

        int MaxLevel(PlayerAttribute attribute)
        {
            int[] prices = _balance.PricesFor(attribute);
            return Math.Min(_balance.maxAttributeLevel, prices == null ? 0 : prices.Length);
        }

        /// <summary>Price of the next level, or -1 if there is none.</summary>
        public int NextPrice(PlayerAttribute attribute)
        {
            return IsMaxed(attribute) ? -1 : _balance.PricesFor(attribute)[Level(attribute)];
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
