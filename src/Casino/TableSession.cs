using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using CasinoExpansion.Core;
using Controller = Il2CppScheduleOne.Casino.CasinoGameController;

namespace CasinoExpansion.Casino
{
    // Runs a round for a mod game at a table whose vanilla deal is suppressed. Plain C#, ticked
    // from Mod.OnUpdate -- a custom MonoBehaviour would need IL2CPP type registration, and a
    // NetworkBehaviour cannot be created at all.
    public sealed class TableSession
    {
        private static readonly Dictionary<int, TableSession> Sessions = new Dictionary<int, TableSession>();

        private readonly Controller _controller;
        private readonly string _gameId;
        private int _round;
        private bool _dealing;
        private bool _dealtThisReady;

        public string LastResult { get; private set; } = "";
        public float Stake { get; set; } = 10f;

        private TableSession(Controller controller)
        {
            _controller = controller;
            _gameId = $"table{controller.GetInstanceID()}";
        }

        public static TableSession For(Controller controller)
        {
            if (controller == null) return null;
            int id = controller.GetInstanceID();
            if (!Sessions.TryGetValue(id, out var session))
                Sessions[id] = session = new TableSession(controller);
            return session;
        }

        public static void TickAll()
        {
            foreach (var session in Sessions.Values) session.Tick();
        }

        private void Tick()
        {
            if (_dealing || _controller == null || !_controller.IsOpen) return;

            var id = TableModes.Get(_controller);
            var game = TableGames.For(id);
            if (game == null) return;                       // vanilla, or a game not yet written

            // One round per ready-up. Vanilla leaves the ready flag set after a hand resolves,
            // so without this the next tick deals again immediately -- an endless loop taking a
            // stake every couple of seconds.
            if (!AllReady())
            {
                _dealtThisReady = false;
                return;
            }

            if (_dealtThisReady || !Consensus(id)) return;

            _dealtThisReady = true;
            MelonCoroutines.Start(RunRound(game));
        }

        private bool AllReady()
        {
            var bj = _controller.TryCast<Il2CppScheduleOne.Casino.BlackjackGameController>();
            if (bj != null) return bj.AreAllPlayersReady();

            var rtb = _controller.TryCast<Il2CppScheduleOne.Casino.RTBGameController>();
            return rtb != null && rtb.AreAllPlayersReady();
        }

        // Every seated player must have chosen the same game. The selection replicates through
        // SetData/SendPlayerFloat, so this is correct in multiplayer as well as solo -- where
        // there is one seat and it is trivially true.
        private bool Consensus(ETableGame local)
        {
            try
            {
                var players = _controller.Players;
                if (players == null) return true;

                for (int i = 0; i < players.CurrentPlayerCount; i++)
                {
                    var data = players.GetPlayerData(i);
                    if (data == null) continue;

                    float choice = data.GetData<float>(Keys.Game(_gameId));
                    if ((int)choice != (int)local) return false;
                }
                return true;
            }
            catch { return true; }   // never block a round on a replication hiccup
        }

        public void PublishChoice(ETableGame game)
        {
            try
            {
                _controller.LocalPlayerData?.SetData<float>(Keys.Game(_gameId), (float)(int)game, true);
            }
            catch (Exception e) { MelonLogger.Warning($"[session] could not publish choice: {e.Message}"); }
        }

        private System.Collections.IEnumerator RunRound(ITableGame game)
        {
            _dealing = true;
            _round++;

            float stake = Mathf.Clamp(Stake, game.Limits.Min, game.Limits.Max);

            if (!Bank.TryTakeBet(_gameId, _round, stake))
            {
                Bank.TryGetCashBalance(out var bal);
                LastResult = $"Not enough cash: need ${stake:N0}, have ${bal:N0}";
                MelonLogger.Msg($"[session] {LastResult}");
                _dealing = false;
                yield break;
            }

            // Seed kept inside the float-exact range so it can be replicated verbatim: every
            // client rebuilds the identical deck rather than having cards sent to it.
            int seed = UnityEngine.Random.Range(1, RoundState.MaxExactInt);
            var hands = new HandSet();
            game.Deal(hands, new Deck(seed));

            LastResult = $"<b>{game.Title}</b>\nDealing...";
            yield return new WaitForSeconds(0.8f);

            // Reveal the hands before the verdict, so a round reads as a hand of cards rather
            // than a number appearing out of nowhere.
            var reveal = new System.Text.StringBuilder($"<b>{game.Title}</b>\n");
            foreach (var hand in hands.Hands)
                reveal.AppendLine($"{hand.Name}: {hand}");
            LastResult = reveal.ToString();
            yield return new WaitForSeconds(1.4f);

            var outcome = game.Resolve(hands, stake);
            if (outcome.Multiplier > 0f) Bank.ApplyPayout(_gameId, _round, stake * outcome.Multiplier);

            float won = stake * outcome.Multiplier;
            LastResult = outcome.Multiplier > 1f ? $"WON ${won:N0}\n{outcome.Summary}"
                       : outcome.Multiplier > 0f ? $"Push\n{outcome.Summary}"
                       : $"Lost ${stake:N0}\n{outcome.Summary}";

            MelonLogger.Msg($"[session] round {_round} seed {seed}: {outcome.Summary} -> x{outcome.Multiplier}");

            yield return new WaitForSeconds(1f);
            _dealing = false;
        }

        public bool HasFreshResult => !string.IsNullOrEmpty(LastResult);
    }
}
