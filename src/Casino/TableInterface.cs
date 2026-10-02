using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using BjUI = Il2CppScheduleOne.Casino.UI.BlackjackInterface;
using Controller = Il2CppScheduleOne.Casino.CasinoGameController;

namespace CasinoExpansion.Casino
{
    // The in-round prompt: our own buttons, on the canvas that stays up while a hand is in play.
    //
    // Two things forced this. The bet panel, where the rest of the mod chrome lives, is CLOSED
    // during a hand -- so prompts built there are invisible exactly when they are needed, and
    // every one of them timed out and answered itself. And driving vanilla's own Hit and Stand
    // buttons, which was the previous attempt, pushed blackjack's vocabulary onto games that do
    // not have hits or stands, and would have broken the vanilla table it borrowed them from.
    //
    // So: the Hit button is used as a STYLE DONOR and nothing more. Cloning it inherits the
    // font, the fill, the hover state and the canvas scaling for free, which is the same trick
    // the bet-panel chrome uses, and the clones carry our labels and our handlers.
    public static class TableInterface
    {
        private static BjUI _ui;
        private static GameObject _root;
        private static Il2CppTMPro.TextMeshProUGUI _promptLabel;
        private static readonly List<Button> Buttons = new List<Button>();
        private static readonly List<Il2CppTMPro.TextMeshProUGUI> Labels = new List<Il2CppTMPro.TextMeshProUGUI>();

        private const int MaxChoices = 4;

        private static BjUI Interface()
        {
            if (_ui == null) _ui = Object.FindObjectOfType<BjUI>();
            return _ui;
        }

        public static TableSession Active { get; set; }

        // Built once against the interface's own Hit button. Returns false if the table has no
        // such interface, so the caller can fall back to the bet-panel row.
        private static bool Build()
        {
            if (_root != null) return true;

            var ui = Interface();
            var donor = ui?.HitButton?.gameObject;
            if (donor == null) return false;

            var parent = donor.transform.parent;
            if (parent == null) return false;

            _root = new GameObject("ModPrompt");
            _root.transform.SetParent(parent, false);

            var donorRect = donor.GetComponent<RectTransform>();
            Vector2 home = donorRect.anchoredPosition;
            Vector2 size = donorRect.rect.size;

            // The prompt sits above the buttons, in the gap the vanilla layout leaves clear.
            _promptLabel = CloneLabel(donor, _root.transform,
                home + new Vector2(0f, size.y * 1.6f), new Vector2(size.x * 1.6f, size.y));

            for (int i = 0; i < MaxChoices; i++)
            {
                int answer = i;
                var go = Object.Instantiate(donor, _root.transform);
                go.name = $"ModChoice{i}";

                var rect = go.GetComponent<RectTransform>();
                rect.anchoredPosition = home - new Vector2(0f, i * (size.y + 8f));
                rect.sizeDelta = size;

                var button = go.GetComponent<Button>();
                if (button != null)
                {
                    Mute(button.onClick);
                    button.onClick.AddListener((UnityAction)(() => Active?.Answer(answer)));
                }

                Buttons.Add(button);
                Labels.Add(go.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>(true));
                go.SetActive(false);
            }

            _root.SetActive(false);
            MelonLogger.Msg($"[iface] prompt built on '{parent.name}' from the Hit button");
            return true;
        }

        private static Il2CppTMPro.TextMeshProUGUI CloneLabel(GameObject donor, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = Object.Instantiate(donor, parent);
            go.name = "ModPromptText";

            var rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var button = go.GetComponent<Button>();
            if (button != null) { Mute(button.onClick); button.interactable = false; }

            var image = go.GetComponent<Image>();
            if (image != null) image.color = new Color(0f, 0f, 0f, 0.55f);

            var label = go.GetComponentInChildren<Il2CppTMPro.TextMeshProUGUI>(true);
            if (label != null) label.fontSize = 15f;
            return label;
        }

        private static void Mute(UnityEventBase evt)
        {
            if (evt == null) return;
            for (int i = 0; i < evt.GetPersistentEventCount(); i++)
                evt.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        public static bool Show(string prompt, string[] options)
        {
            if (!Build()) return false;

            try
            {
                _root.SetActive(true);
                if (_promptLabel != null) _promptLabel.text = prompt;

                for (int i = 0; i < Buttons.Count; i++)
                {
                    bool used = i < options.Length;
                    Buttons[i]?.gameObject.SetActive(used);
                    if (used && Labels[i] != null) Labels[i].text = options[i];
                }
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[iface] could not show the prompt: {e.Message}");
                return false;
            }
        }

        public static void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        // The table's own Dealer/You readout, reused as-is: it is two numbers in the right
        // place, and every game here has a dealer side and a player side.
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

        public static void Finish()
        {
            Active = null;
            Hide();
            try { Interface()?.HideScores(); } catch { }
        }
    }
}
