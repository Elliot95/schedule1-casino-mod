using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using BjUI = Il2CppScheduleOne.Casino.UI.BlackjackInterface;
using Controller = Il2CppScheduleOne.Casino.CasinoGameController;

namespace CasinoExpansion.Casino
{
    // Runs mod prompts through the table's own in-round interface -- the Hit/Stand buttons and
    // the Dealer/You score readout that vanilla blackjack uses.
    //
    // This exists because the bet panel, where the rest of the mod chrome lives, is CLOSED
    // while a hand is in play. Decision buttons built there are invisible exactly when they are
    // needed, so every prompt timed out and answered itself -- which is how a hand hit its way
    // to 23 without the player touching anything.
    //
    // Vanilla's own buttons are reused rather than cloned wholesale: HitClicked and
    // StandClicked are prefixed, so a click answers our prompt and never reaches the vanilla
    // controller. Games offering a third or fourth choice get clones of the Hit button added to
    // the same container, which inherit its look for free.
    public static class TableInterface
    {
        private static BjUI _ui;
        private static readonly List<Button> Extra = new List<Button>();
        private static Button _hit, _stand;
        private static string _hitHome = "Hit", _standHome = "Stand";

        private static BjUI Interface()
        {
            if (_ui == null) _ui = Object.FindObjectOfType<BjUI>();
            return _ui;
        }

        public static bool Available => Interface() != null;

        // Hit is exposed directly; Stand is not, so it is found among the input container's
        // buttons by its label rather than by a guessed child index.
        private static void Locate()
        {
            var ui = Interface();
            if (ui == null || _hit != null) return;

            _hit = ui.HitButton;
            if (_hit == null) return;

            _hitHome = LabelOf(_hit)?.text ?? "Hit";

            var parent = _hit.transform.parent;
            if (parent == null) return;

            var buttons = parent.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                if (b == null || b.Pointer == _hit.Pointer) continue;

                var label = LabelOf(b);
                if (label != null && label.text.IndexOf("stand", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _stand = b;
                    _standHome = label.text;
                    break;
                }
            }
        }

        private static Il2CppTMPro.TextMeshProUGUI LabelOf(Button b) =>
            b == null ? null : b.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>(true);

        // Shows the prompt on the table's own input panel. Returns false if the interface is
        // not available, so the session can fall back to the bet-panel buttons.
        public static bool Prompt(Controller controller, string[] options)
        {
            var ui = Interface();
            if (ui == null) return false;

            Locate();
            if (_hit == null) return false;

            try
            {
                // Puts the input container on screen with its usual fade, the same call vanilla
                // makes when it is a player's turn.
                ui.LocalPlayerReadyForInput();

                SetLabel(_hit, options.Length > 0 ? options[0] : _hitHome);
                if (_stand != null) SetLabel(_stand, options.Length > 1 ? options[1] : _standHome);

                EnsureExtras(options.Length);
                for (int i = 0; i < Extra.Count; i++)
                {
                    bool used = i + 2 < options.Length;
                    Extra[i].gameObject.SetActive(used);
                    if (used) SetLabel(Extra[i], options[i + 2]);
                }
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[iface] could not show the prompt: {e.Message}");
                return false;
            }
        }

        public static void Done(Controller controller)
        {
            try
            {
                foreach (var b in Extra) if (b != null) b.gameObject.SetActive(false);
                if (_hit != null) SetLabel(_hit, _hitHome);
                if (_stand != null) SetLabel(_stand, _standHome);
            }
            catch { }
        }

        // End of hand: the scores come down and no stray click can answer a dead prompt.
        public static void Finish()
        {
            Active = null;
            try { Interface()?.HideScores(); } catch { }
        }

        // The Dealer/You readout. Shown with the game's own call so it fades in as usual.
        public static void Scores(string dealer, string player)
        {
            var ui = Interface();
            if (ui == null) return;

            try
            {
                ui.ShowScores();
                if (ui.DealerScoreLabel != null) ui.DealerScoreLabel.text = dealer;
                if (ui.PlayerScoreLabel != null) ui.PlayerScoreLabel.text = player;
            }
            catch { }
        }

        private static void SetLabel(Button b, string text)
        {
            var label = LabelOf(b);
            if (label != null) label.text = text;
        }

        // Clones of the Hit button, stacked under the pair, for games offering more than two
        // choices. Built once and reused, because the interface is a persistent singleton.
        private static void EnsureExtras(int optionCount)
        {
            int needed = Mathf.Max(0, optionCount - 2);
            if (Extra.Count >= needed || _hit == null) return;

            var parent = _hit.transform.parent;
            var home = _hit.GetComponent<RectTransform>();
            var step = _stand != null
                ? _stand.GetComponent<RectTransform>().anchoredPosition - home.anchoredPosition
                : new Vector2(0f, -48f);

            while (Extra.Count < needed)
            {
                int index = Extra.Count + 2;
                var go = Object.Instantiate(_hit.gameObject, parent);
                go.name = $"ModChoice{index}";

                var rect = go.GetComponent<RectTransform>();
                rect.anchoredPosition = home.anchoredPosition + step * index;

                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);

                    int answer = index;
                    button.onClick.AddListener((UnityAction)(() => Answer(answer)));
                }

                go.SetActive(false);
                Extra.Add(button);
            }
        }

        // Every route into an answer goes through here, so a click on a vanilla button and a
        // click on a clone are handled identically.
        private static void Answer(int index)
        {
            var session = Active;
            session?.Answer(index);
        }

        // The session currently asking. Set by TableSession around a prompt; the button
        // prefixes need to know who to answer without walking the scene.
        public static TableSession Active { get; set; }

        [HarmonyPatch(typeof(BjUI), nameof(BjUI.HitClicked))]
        internal static class HitPatch
        {
            private static bool Prefix()
            {
                if (Active == null || !Active.Waiting) return true;   // vanilla hand, let it through
                Active.Answer(0);
                return false;
            }
        }

        [HarmonyPatch(typeof(BjUI), nameof(BjUI.StandClicked))]
        internal static class StandPatch
        {
            private static bool Prefix()
            {
                if (Active == null || !Active.Waiting) return true;
                Active.Answer(1);
                return false;
            }
        }
    }
}
