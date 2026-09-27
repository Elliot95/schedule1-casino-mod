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
            public GameObject OptionList;
            public readonly List<Text_> Options = new List<Text_>();
            public Controller Controller;
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

            chrome.Rules.Label.alignment = Il2CppTMPro.TextAlignmentOptions.TopLeft;
            chrome.Players.Label.alignment = Il2CppTMPro.TextAlignmentOptions.TopLeft;

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
