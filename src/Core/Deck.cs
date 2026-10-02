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
    //
    // A shoe of one or more decks, shuffled together and dealt from the top. Each game picks
    // its own count, matching the table it is modelled on: blackjack and baccarat are dealt
    // from large shoes in real casinos, and with a six-deck shoe a player splitting repeatedly
    // cannot run the table out of cards -- which a single deck genuinely could.
    //
    // Nothing here persists between rounds. The shoe is rebuilt from a fresh seed every hand,
    // so there is no penetration and counting cards would tell you nothing.
    public sealed class Deck
    {
        private readonly List<int> _order;
        private readonly int _seed;
        private int _pos;

        public Deck(int seed, int decks = 1)
        {
            if (decks < 1) decks = 1;

            _seed = seed;
            _order = new List<int>(52 * decks);
            for (int d = 0; d < decks; d++)
                for (int i = 0; i < 52; i++) _order.Add(i);

            Shuffle(_order, seed);
        }

        // xorshift32 rather than System.Random: identical sequence on every
        // runtime, which Random does not guarantee.
        private static void Shuffle(List<int> order, int seed)
        {
            uint s = seed == 0 ? 1u : (uint)seed;
            for (int i = order.Count - 1; i > 0; i--)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                int j = (int)(s % (uint)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        public int Remaining => _order.Count - _pos;

        public Card Draw()
        {
            // Running a shoe dry should not be able to end a round with an exception. Reshuffle
            // deterministically -- every client derives the same next seed, so they stay in step.
            if (_pos >= _order.Count)
            {
                Shuffle(_order, _seed ^ unchecked((int)0x9E3779B9));
                _pos = 0;
            }

            return Card.FromIndex(_order[_pos++]);
        }
    }
}
