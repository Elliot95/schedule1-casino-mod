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
        private static readonly List<GameObject> Hidden = new List<GameObject>();
        private static float _groupAlpha = -1f;
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
                // Vanilla's own Hit and Stand sit in this same container and stay live
                // underneath ours, so a click landed on whichever happened to be on top --
                // which is how a hand meant for Player ended up backing Banker.
                HideVanillaButtons();
                ShowContainer();

                _root.SetActive(true);
                if (_promptLabel != null) _promptLabel.text = prompt;

                for (int i = 0; i < Buttons.Count; i++)
                {
                    bool used = i < options.Length;
                    Buttons[i]?.gameObject.SetActive(used);
                    if (!used) continue;

                    if (Labels[i] != null) Labels[i].text = options[i];
                    Tint(Buttons[i], options[i]);
                }
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[iface] could not show the prompt: {e.Message}");
                return false;
            }
        }

        // Baccarat's three bets are the only place a colour means something: red for the
        // default, blue for banker, green for tie, so the side being backed is readable at a
        // glance rather than by reading three similar words.
        private static readonly Color Default = new Color(0.55f, 0.16f, 0.16f, 1f);
        private static readonly Color Blue = new Color(0.16f, 0.32f, 0.60f, 1f);
        private static readonly Color Green = new Color(0.16f, 0.48f, 0.26f, 1f);

        private static void Tint(Button button, string label)
        {
            var image = button?.GetComponent<Image>();
            if (image == null) return;

            image.color = label == "Banker" ? Blue
                        : label == "Tie" ? Green
                        : Default;
        }

        public static void Hide()
        {
            if (_root != null) _root.SetActive(false);

            foreach (var go in Hidden) if (go != null) go.SetActive(true);
            Hidden.Clear();
            RestoreContainer();
        }

        // The input container is faded out by a CanvasGroup until vanilla decides it is the
        // player's turn. Buttons inside it are invisible AND unclickable, which is why a Three
        // Card Poker hand folded a straight: the prompt was never answerable, so it timed out.
        private static void ShowContainer()
        {
            var group = Interface()?.InputContainerCanvasGroup;
            if (group == null) return;

            if (_groupAlpha < 0f) _groupAlpha = group.alpha;
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private static void RestoreContainer()
        {
            var group = Interface()?.InputContainerCanvasGroup;
            if (group == null || _groupAlpha < 0f) return;

            group.alpha = _groupAlpha;
            _groupAlpha = -1f;
        }

        // Everything in the input container that is not ours. Found by walking the siblings
        // rather than by name, because only HitButton is exposed and Stand is not.
        private static void HideVanillaButtons()
        {
            var donor = Interface()?.HitButton?.gameObject;
            var parent = donor?.transform.parent;
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i).gameObject;
                if (child == null || child == _root) continue;
                if (child.name.StartsWith("Mod")) continue;
                if (!child.activeSelf) continue;

                child.SetActive(false);
                Hidden.Add(child);
            }
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
