using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using BetPanel = Il2CppScheduleOne.Casino.UI.CasinoGameBetPanel;
using Controller = Il2CppScheduleOne.Casino.CasinoGameController;

namespace CasinoExpansion.Casino
{
    // Adds the game selector, rules and ready list to the table's own bet panel.
    //
    // Patches CasinoGameBetPanel rather than the two interfaces: BlackjackInterface and
    // RTBInterface share no base class, but both own one of these and it is typed to the base
    // controller -- so this is the single seam that covers both tables.
    public static class TablePanelChrome
    {
        private sealed class Chrome
        {
            public GameObject Root;
            public Text_ Selector;
            public Text_ Rules;
            public Text_ Players;
            public Text_ Status;
            public Text_ Side;
            public GameObject DecisionRow;
            public readonly List<Text_> Decisions = new List<Text_>();
            public GameObject OptionList;
            public readonly List<Text_> Options = new List<Text_>();
            public Controller Controller;
            public BetPanel Panel;
            public RectTransform ReadyRect;
            public Vector2 ReadyHome;
            public bool LoggedKeys;
        }

        // TextMeshProUGUI is awkward to name through interop in a few places; this keeps the
        // call sites readable.
        private sealed class Text_
        {
            public GameObject Go;
            public Il2CppTMPro.TextMeshProUGUI Label;
            public Button Button;
        }

        private static readonly Dictionary<int, Chrome> Panels = new Dictionary<int, Chrome>();

        [HarmonyPatch(typeof(BetPanel), nameof(BetPanel.Open))]
        internal static class OpenPatch
        {
            private static void Postfix(BetPanel __instance, Controller game)
            {
                try { Attach(__instance, game); }
                catch (Exception e) { MelonLogger.Error($"[chrome] attach failed: {e}"); }
            }
        }

        [HarmonyPatch(typeof(BetPanel), nameof(BetPanel.Close))]
        internal static class ClosePatch
        {
            private static void Postfix(BetPanel __instance)
            {
                if (!Panels.TryGetValue(__instance.GetInstanceID(), out var chrome)) return;
                if (chrome.Root != null) chrome.Root.SetActive(false);
                if (chrome.ReadyRect != null) chrome.ReadyRect.anchoredPosition = chrome.ReadyHome;
            }
        }

        private static void Attach(BetPanel panel, Controller game)
        {
            if (panel == null || game == null) return;

            // Built once per panel: the interfaces are persistent singletons, so rebuilding on
            // every sit-down would pile up duplicates.
            if (!Panels.TryGetValue(panel.GetInstanceID(), out var chrome))
            {
                chrome = Build(panel);
                if (chrome == null) return;
                Panels[panel.GetInstanceID()] = chrome;
            }

            chrome.Panel = panel;
            chrome.Controller = game;
            chrome.Root.SetActive(true);
            if (chrome.ReadyRect != null)
                chrome.ReadyRect.anchoredPosition = chrome.ReadyHome + new Vector2(-88f, 0f);
            chrome.OptionList.SetActive(false);
            Refresh(chrome);
        }

        private static Chrome Build(BetPanel panel)
        {
            var readyGo = panel._readyButton != null ? panel._readyButton.gameObject : null;
            var titleGo = panel._betTitleLabel != null ? panel._betTitleLabel.gameObject : null;
            if (readyGo == null || titleGo == null)
            {
                MelonLogger.Warning("[chrome] bet panel missing ready button or title label");
                return null;
            }

            var parent = readyGo.transform.parent;
            var chrome = new Chrome
            {
                Root = new GameObject("ModChrome"),
            };
            chrome.Root.transform.SetParent(parent, false);

            // Positions follow the sketch: selector bottom-right of Ready, rules above it,
            // seated players bottom-left.
            // Measured, not guessed: container is 504x240 and Ready sits at (0,-81.5) sized
            // 210x40. Half-width is therefore 252, so anything wider than ~140 centred beyond
            // x=180 spills onto the felt -- which is exactly what the first pass did.
            const float HalfW = 252f;

            // Selector tucks inside, level with Ready and clear of it.
            // Ready shifts left to make room; its original position is kept so vanilla layout
            // is restored when the panel closes.
            chrome.ReadyRect = readyGo.GetComponent<RectTransform>();
            chrome.ReadyHome = chrome.ReadyRect.anchoredPosition;

            chrome.Selector = CloneButton(readyGo, chrome.Root.transform,
                new Vector2(150f, -81.5f), new Vector2(160f, 40f));

            // Wings sit deliberately outside the container, each on its own backing so they read
            // as attached panels rather than text floating over the table.
            MakeBacking(readyGo, chrome.Root.transform, new Vector2(HalfW + 108f, 24f), new Vector2(212f, 168f));
            chrome.Rules = CloneLabel(titleGo, chrome.Root.transform,
                new Vector2(HalfW + 108f, 24f), new Vector2(196f, 156f), 14f);

            MakeBacking(readyGo, chrome.Root.transform, new Vector2(-(HalfW + 100f), 24f), new Vector2(196f, 168f));
            chrome.Players = CloneLabel(titleGo, chrome.Root.transform,
                new Vector2(-(HalfW + 100f), 24f), new Vector2(180f, 156f), 15f);

            // The round readout. Without this the game deals, resolves and pays entirely in the
            // log: the table takes a stake and nothing visible happens, which reads as the mod
            // silently eating money.
            // Full width across the bottom, under the decision buttons. It was on the left
            // wing, which put the one line that changes every second furthest from where the
            // player is looking -- at the cards and at the buttons they are about to press.
            MakeBacking(readyGo, chrome.Root.transform, new Vector2(0f, -192f), new Vector2(520f, 62f));
            chrome.Status = CloneLabel(titleGo, chrome.Root.transform,
                new Vector2(0f, -192f), new Vector2(504f, 54f), 15f);

            chrome.Rules.Label.alignment = Il2CppTMPro.TextAlignmentOptions.TopLeft;
            chrome.Players.Label.alignment = Il2CppTMPro.TextAlignmentOptions.TopLeft;
            chrome.Status.Label.alignment = Il2CppTMPro.TextAlignmentOptions.Center;

            // Directly beneath the game selector, because it is the same kind of choice: made
            // before the deal and then left alone. Cycles on click rather than opening a list --
            // two or three sides do not earn a dropdown.
            chrome.Side = CloneButton(readyGo, chrome.Root.transform,
                new Vector2(150f, -38f), new Vector2(160f, 34f));
            chrome.Side.Label.fontSize = 14f;
            chrome.Side.Button.onClick.AddListener((UnityAction)(() =>
            {
                var game = TableGames.For(TableModes.Get(chrome.Controller));
                if (game == null || game.Sides.Length == 0) return;
                TableSession.For(chrome.Controller)?.CycleSide(game.Sides.Length);
                Refresh(chrome);
            }));

            // Decision buttons sit where the eye already is -- directly under Ready, in the
            // middle of the panel -- because they are time-limited and easy to miss out on a
            // wing. Two are built and shown or hidden per prompt; no game in the line-up offers
            // more than a pair of choices.
            chrome.DecisionRow = new GameObject("Decisions");
            chrome.DecisionRow.transform.SetParent(chrome.Root.transform, false);

            // Four is the most any game asks for: Ride the Bus's suit guess. Blackjack asks
            // three, everything else two. They are laid out per prompt in Refresh rather than
            // fixed here, so two buttons stay centred instead of sitting where four would.
            for (int i = 0; i < MaxDecisions; i++)
            {
                int index = i;
                var btn = CloneButton(readyGo, chrome.DecisionRow.transform,
                    new Vector2(0f, -134f), new Vector2(200f, 40f));
                btn.Label.fontSize = 16f;
                btn.Button.onClick.AddListener((UnityAction)(() =>
                    TableSession.For(chrome.Controller)?.Answer(index)));
                chrome.Decisions.Add(btn);
            }
            chrome.DecisionRow.SetActive(false);

            chrome.OptionList = new GameObject("Options");
            chrome.OptionList.transform.SetParent(chrome.Root.transform, false);

            for (int i = 0; i < TableModes.All.Length; i++)
            {
                var game = TableModes.All[i];
                // Drops downward from the selector, like a dropdown list.
                var opt = CloneButton(readyGo, chrome.OptionList.transform,
                    new Vector2(168f, -113f - i * 34f), new Vector2(150f, 32f));
                opt.Label.text = TableModes.Describe(game);
                opt.Label.fontSize = 15f;
                opt.Button.onClick.AddListener((UnityAction)(() =>
                {
                    TableModes.Set(chrome.Controller, game);
                    TableSession.For(chrome.Controller)?.PublishChoice(game);
                    chrome.OptionList.SetActive(false);
                    Refresh(chrome);
                }));
                chrome.Options.Add(opt);
            }

            chrome.Selector.Label.fontSize = 15f;
            chrome.Selector.Button.onClick.AddListener((UnityAction)(() =>
                chrome.OptionList.SetActive(!chrome.OptionList.activeSelf)));

            // Positions were guessed from screenshots and sit outside the panel; log the real
            // geometry so they can be placed against actual numbers.
            var containerRect = panel._container != null ? panel._container.GetComponent<RectTransform>() : null;
            var readyRect = readyGo.GetComponent<RectTransform>();
            MelonLogger.Msg($"[chrome] built on panel {panel.name} with {chrome.Options.Count} options. " +
                            $"container size={(containerRect != null ? containerRect.rect.size.ToString() : "?")} " +
                            $"ready pos={readyRect.anchoredPosition} size={readyRect.rect.size} " +
                            $"parent={readyGo.transform.parent.name}");
            return chrome;
        }

        // Cloning inherits the URP material, TMP setup, Button and UISelectable that a bare
        // AddComponent would not, and keeps everything inside the existing canvas hierarchy --
        // which is where all this project's text bugs came from.
        private static Text_ CloneButton(GameObject donor, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = Object.Instantiate(donor, parent);
            go.name = "ModButton";
            go.SetActive(true);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var button = go.GetComponent<Button>();
            if (button != null) MuteInherited(button.onClick);

            var label = go.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>(true);
            return new Text_ { Go = go, Label = label, Button = button };
        }

        // A dimmed clone of the ready button, used purely as a background plate behind the
        // wings so they look attached to the panel.
        private static void MakeBacking(GameObject donor, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = Object.Instantiate(donor, parent);
            go.name = "ModBacking";
            go.SetActive(true);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var button = go.GetComponent<Button>();
            if (button != null) { MuteInherited(button.onClick); button.interactable = false; }

            var image = go.GetComponent<Image>();
            if (image != null) image.color = new Color(0.04f, 0.10f, 0.06f, 0.82f);

            var label = go.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>(true);
            if (label != null) label.text = "";
        }

        private static Text_ CloneLabel(GameObject donor, Transform parent, Vector2 pos, Vector2 size, float fontSize)
        {
            var go = Object.Instantiate(donor, parent);
            go.name = "ModLabel";
            go.SetActive(true);

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var label = go.GetComponent<Il2CppTMPro.TextMeshProUGUI>()
                        ?? go.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>(true);
            if (label != null)
            {
                label.fontSize = fontSize;
                label.enableWordWrapping = true;
            }
            return new Text_ { Go = go, Label = label };
        }

        private static void MuteInherited(UnityEngine.Events.UnityEventBase evt)
        {
            if (evt == null) return;
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                evt.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        private static void Refresh(Chrome chrome)
        {
            if (chrome?.Controller == null) return;

            var game = TableModes.Get(chrome.Controller);
            if (chrome.Selector?.Label != null)
                chrome.Selector.Label.text = TableModes.Describe(game);
            if (chrome.Rules?.Label != null)
                chrome.Rules.Label.text = string.Join("\n", TableRules.For(game));
            if (chrome.Players?.Label != null)
                chrome.Players.Label.text = BuildPlayerList(chrome);

            var live = TableSession.For(chrome.Controller);
            ReadStake(chrome, live);

            if (chrome.Side != null)
            {
                var def = TableGames.For(game);
                var sides = def?.Sides ?? System.Array.Empty<string>();
                bool offered = sides.Length > 0;

                chrome.Side.Go.SetActive(offered);
                if (offered && live != null)
                {
                    int i = Mathf.Clamp(live.Side, 0, sides.Length - 1);
                    chrome.Side.Label.text = $"Bet: {sides[i]}";
                }
            }

            if (chrome.DecisionRow != null)
            {
                bool asking = live != null && live.Waiting;
                chrome.DecisionRow.SetActive(asking);
                if (asking) LayOutDecisions(chrome, live.Options);
            }

            if (chrome.Status?.Label != null)
            {
                var session = live;
                chrome.Status.Label.text =
                    session != null && session.Waiting ? $"<b>{session.Prompt}</b>" :
                    game == ETableGame.Vanilla ? "<size=80%>House rules — the table plays as normal.</size>"
                    : !string.IsNullOrEmpty(session?.LastResult) ? session.LastResult
                    : "<size=85%>Set your buy-in, then ready up.</size>";
            }
        }

        // Reads the stake straight off the slider rather than through the game's own
        // conversion. Three patched seams have now failed to raise the table maximum -- the
        // const-backed MaximumBet crashes, a GetBetLimits postfix corrupts its out params, and
        // rescaling GetBetFromSliderValue never reached the bank because SetLocalPlayerBet
        // clamps the result on its way to LocalPlayerBet. The slider's own position is the one
        // thing none of that can distort.
        private static void ReadStake(Chrome chrome, TableSession session)
        {
            if (session == null || chrome.Panel == null) return;

            var def = TableGames.For(TableModes.Get(chrome.Controller));
            if (def == null) return;                       // vanilla table, leave the stake alone

            var slider = chrome.Panel._betSlider;
            if (slider == null) return;

            float span = slider.maxValue - slider.minValue;
            float t = Mathf.Approximately(span, 0f) ? 0f
                    : Mathf.Clamp01((slider.value - slider.minValue) / span);

            float raw = def.Limits.Min + t * (def.Limits.Max - def.Limits.Min);
            session.Stake = Mathf.Round(raw / 10f) * 10f;

            // Vanilla writes the clamped figure here every refresh, so it has to be overwritten
            // or the panel reads $1,000 under a slider sitting at $50,000.
            if (chrome.Panel._betAmount != null)
                chrome.Panel._betAmount.text = $"${session.Stake:N0}";
        }

        private const int MaxDecisions = 4;

        // Buttons are sized and spread to fit however many the prompt offers, inside the
        // container's 252px half-width. Anything wider spills onto the felt, which is what the
        // first pass at this panel did.
        private static void LayOutDecisions(Chrome chrome, string[] options)
        {
            int n = Mathf.Clamp(options.Length, 1, MaxDecisions);
            float width = n switch { 1 => 220f, 2 => 200f, 3 => 160f, _ => 120f };
            float step = n switch { 1 => 0f, 2 => 216f, 3 => 170f, _ => 126f };
            float start = -step * (n - 1) / 2f;

            for (int i = 0; i < chrome.Decisions.Count; i++)
            {
                bool used = i < n;
                var btn = chrome.Decisions[i];
                btn.Go.SetActive(used);
                if (!used) continue;

                btn.Label.text = options[i];
                btn.Label.fontSize = n >= 4 ? 13f : n == 3 ? 15f : 16f;

                var rect = btn.Go.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(start + i * step, -134f);
                rect.sizeDelta = new Vector2(width, 40f);
            }
        }

        // Ready state and the round readout both change without anything calling us, so the
        // panel has to be pulled rather than pushed. Throttled: this runs every frame a table
        // is open, and rebuilding the player list that often is pure waste.
        private static float _nextTick;

        public static void TickRefresh()
        {
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 0.2f;

            foreach (var chrome in Panels.Values)
                if (chrome.Root != null && chrome.Root.activeSelf) Refresh(chrome);
        }

        // Solo, one readied player is enough; the vanilla panel's "waiting for other players"
        // never says who is actually holding things up.
        private const int MaxListed = 6;

        private static string BuildPlayerList(Chrome chrome)
        {
            var controller = chrome.Controller;
            try
            {
                var players = controller.Players;
                if (players == null) return "Players\n(none)";

                int count = players.CurrentPlayerCount;
                int readyCount = ReadyCount(controller);
                var lines = new List<string>
                {
                    $"<b>Players ({count}/{players.PlayerLimit})</b>",
                    $"<size=80%>{readyCount} of {count} ready</size>",
                };

                // Capped rather than scrolled: a player-count mod could seat far more than the
                // plate holds, and a count of the remainder is honest without a ScrollRect.
                int shown = Math.Min(count, MaxListed);
                for (int i = 0; i < shown; i++)
                {
                    var p = players.GetPlayer(i);
                    if (p == null) continue;

                    // Only the local player's state is knowable individually; everyone shows
                    // ticked once the exposed count says the whole table is ready.
                    bool ready = p.IsLocalPlayer ? IsLocalReady(controller) : readyCount >= count;
                    lines.Add($"{(ready ? "[x]" : "[  ]")} {p.PlayerName}");
                }

                if (count > shown) lines.Add($"  +{count - shown} more");
                return string.Join("\n", lines);
            }
            catch (Exception e) { return $"Players\n({e.GetType().Name})"; }
        }

        // Vanilla keeps per-player ready state privately -- the per-player bool dictionary is
        // empty, so a remote player's individual state is not knowable. What IS exposed is
        // GetPlayersReadyCount(), and our own toggle can be tracked directly, which covers the
        // solo case exactly and gives an honest total in multiplayer.
        private static readonly Dictionary<int, bool> LocalReady = new Dictionary<int, bool>();

        private static bool IsLocalReady(Controller c) =>
            c != null && LocalReady.TryGetValue(c.GetInstanceID(), out var r) && r;

        private static int ReadyCount(Controller c)
        {
            var bj = c.TryCast<Il2CppScheduleOne.Casino.BlackjackGameController>();
            if (bj != null) return bj.GetPlayersReadyCount();

            var rtb = c.TryCast<Il2CppScheduleOne.Casino.RTBGameController>();
            return rtb != null ? rtb.GetPlayersReadyCount() : 0;
        }

        [HarmonyPatch(typeof(Controller), nameof(Controller.ToggleLocalPlayerReady))]
        internal static class ReadyTogglePatch
        {
            private static void Postfix(Controller __instance)
            {
                int id = __instance.GetInstanceID();
                LocalReady[id] = !IsLocalReady(__instance);
                foreach (var chrome in Panels.Values)
                    if (chrome.Controller == __instance) Refresh(chrome);
            }
        }

        [HarmonyPatch(typeof(Controller), nameof(Controller.Close))]
        internal static class ControllerClosePatch
        {
            private static void Postfix(Controller __instance) => LocalReady.Remove(__instance.GetInstanceID());
        }
    }
}
