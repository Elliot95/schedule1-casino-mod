using System.Collections.Generic;

namespace CasinoExpansion.Core
{
    public readonly struct Card
    {
        public readonly int Suit;   // 0 Spades, 1 Hearts, 2 Diamonds, 3 Clubs
        public readonly int Rank;   // 1 Ace .. 13 King

        public Card(int suit, int rank) { Suit = suit; Rank = rank; }

        public int Index => Suit * 13 + (Rank - 1);       // 0..51
        public static Card FromIndex(int i) => new Card(i / 13, i % 13 + 1);

        public override string ToString() =>
            $"{"A23456789TJQK"[Rank - 1]}{"SHDC"[Suit]}";
    }

    // Deterministic across clients: same seed always yields the same order, so the
    // host only needs to replicate the seed rather than every dealt card.
    public sealed class Deck
    {
        private readonly List<int> _order = new List<int>(52);
        private int _pos;

        public Deck(int seed)
        {
            for (int i = 0; i < 52; i++) _order.Add(i);

            // xorshift32 rather than System.Random: identical sequence on every
            // runtime, which Random does not guarantee.
            uint s = seed == 0 ? 1u : (uint)seed;
            for (int i = _order.Count - 1; i > 0; i--)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                int j = (int)(s % (uint)(i + 1));
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
        }

        public int Remaining => 52 - _pos;

        public Card Draw() => Card.FromIndex(_order[_pos++]);
    }
}
