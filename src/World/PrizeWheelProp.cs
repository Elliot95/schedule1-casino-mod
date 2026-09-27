using System.Collections;
using System.Linq;
using UnityEngine;
using CasinoExpansion.Core;
using Object = UnityEngine.Object;

namespace CasinoExpansion.World
{
    // Plain class, deliberately not a MonoBehaviour: custom MonoBehaviours in Il2Cpp need
    // [RegisterTypeInIl2Cpp] plus an IntPtr constructor, and avoiding them removes a whole class
    // of interop pitfalls. Animation is driven by a MelonCoroutine instead.
    public sealed class PrizeWheelProp
    {
        // Slice count is decided at calibration, not fixed here: the jackpot is diluted by
        // adding losing slices, so the wheel's geometry has to follow whatever that produced.
        private static int SliceCount => Core.PrizeWheelSlices.Count;
        private static int GrandPrizeSlice => Core.PrizeWheelSlices.GrandPrize;

        private static int Segments => SliceCount * 4;   // whole slices, so wedge edges land exactly
        private const int TextureSize = 256;

        public GameObject Root { get; private set; }
        public WheelEffects Effects { get; } = new WheelEffects();
        public GameObject SpinButtonAnchor { get; private set; }
        public GameObject BetUpAnchor { get; private set; }
        public GameObject BetDownAnchor { get; private set; }
        private Transform _disc;

        public void Build(Vector3 position, Quaternion rotation)
        {
            Root = new GameObject("PrizeWheel");
            Root.transform.position = position;
            Root.transform.rotation = rotation;

            // Borrow a primitive's material so the shader matches whatever render pipeline the
            // game uses -- Shader.Find guesses wrong across URP/built-in.
            var host = GameObject.CreatePrimitive(PrimitiveType.Quad);
            host.name = "Disc";
            host.transform.SetParent(Root.transform, false);
            host.transform.localPosition = new Vector3(0f, DiscY, -0.05f);
            host.transform.localScale = Vector3.one * 0.86f;
            Object.Destroy(host.GetComponent<Collider>());

            host.GetComponent<MeshFilter>().mesh = BuildDisc();
            var discRenderer = host.GetComponent<MeshRenderer>();
            ApplyGameShader(discRenderer);
            SetTexture(discRenderer.material, BuildFaceTexture());
            _disc = host.transform;

            AddPointer();
            AddBody();
            AddConcealerBar();
            AddControlBezel();
            AddReadout();
            SpinButtonAnchor = AddSpinButton();
            BetUpAnchor = AddBetPad("BetUp", 0.30f, new Color(0.25f, 0.62f, 0.28f));
            BetDownAnchor = AddBetPad("BetDown", -0.30f, new Color(0.62f, 0.25f, 0.25f));

            // Layer 0 (Default) casts shadows but never rendered -- the camera's culling mask
            // does not include it. Door is known good: visible in game and inside
            // InteractionManager's search mask, so the prop stays clickable.
            SetLayerRecursive(Root, RenderLayer);

            Effects.Build(Root.transform, new Vector3(0f, -1.12f, 0.03f), 1.40f,
                MelonLoader.MelonLogger.Msg, MelonLoader.MelonLogger.Warning);

            LogCameras();
            MelonLoader.MelonCoroutines.Start(LogStateAfterCulling());
        }

        // isVisible is only meaningful once culling has run, so reading it in the same frame the
        // object is created always reports false and proves nothing.
        private IEnumerator LogStateAfterCulling()
        {
            yield return new WaitForSeconds(1f);
            if (Root != null) LogState();
        }

        private const int RenderLayer = 18;   // Door

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        private static void LogCameras()
        {
            foreach (var cam in Object.FindObjectsOfType<Camera>())
            {
                int mask = cam.cullingMask;
                MelonLoader.MelonLogger.Msg(
                    $"[wheel] camera '{cam.name}' enabled={cam.enabled} mask=0x{mask:X8} " +
                    $"default={(mask & 1) != 0} door={(mask & (1 << 18)) != 0}");
            }
        }

        private void LogState()
        {
            var r = _disc != null ? _disc.GetComponent<MeshRenderer>() : null;
            var mf = _disc != null ? _disc.GetComponent<MeshFilter>() : null;

            MelonLoader.MelonLogger.Msg(
                $"[wheel] state: active={Root.activeInHierarchy} " +
                $"worldPos={Root.transform.position} lossyScale={Root.transform.lossyScale} " +
                $"discRenderer={(r == null ? "null" : $"enabled={r.enabled} visible={r.isVisible} bounds={r.bounds.size}")} " +
                $"verts={(mf?.mesh == null ? -1 : mf.mesh.vertexCount)} tris={(mf?.mesh == null ? -1 : mf.mesh.triangles.Length / 3)}");
        }

        // Borrow a real slot machine cabinet so the wheel reads as a machine standing on the
        // floor rather than a panel hanging in the air. The clone is visual only: its
        // NetworkObject and NetworkBehaviours are stripped immediately, since a runtime copy of a
        // networked object has no valid spawn identity and would misbehave for remote clients.
        private void AddBody()
        {
            // Gold frame is simply a slightly larger panel sitting behind the dark one, so the
            // border shows around every edge. Cheaper and far more predictable than cloning the
            // billboard, whose real dimensions never matched what its bounds implied.
            MakePanel("Frame", new Vector3(1.62f, 2.46f, 0.06f), new Vector3(0f, 0f, 0.10f),
                new Color(0.86f, 0.68f, 0.18f));
            MakePanel("Board", new Vector3(1.48f, 2.32f, 0.08f), new Vector3(0f, 0f, 0.06f),
                new Color(0.16f, 0.04f, 0.06f));
        }

        // The roadside billboard at the Slums Gas Station: a flat panel, already framed, which
        // is a far better backboard than a primitive cube. Its width runs along Z rather than X,
        // so it is rotated to face the player, and its advertising material is replaced with flat
        // colour -- the artwork would otherwise read as a advert rather than a machine.
        private void MakePanel(string name, Vector3 scale, Vector3 pos, Color colour)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(Root.transform, false);
            panel.transform.localPosition = pos;
            panel.transform.localScale = scale;
            Object.Destroy(panel.GetComponent<Collider>());

            var renderer = panel.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, colour);
        }

        private bool TryCloneBillboard()
        {
            try
            {
                var donor = Object.FindObjectsOfType<MeshRenderer>()
                    .FirstOrDefault(r => r.transform.name.StartsWith("Billboard side"));
                if (donor == null) return false;

                var board = Object.Instantiate(donor.gameObject);
                board.name = "Backboard";

                foreach (var io in board.GetComponentsInChildren<Il2CppScheduleOne.Interaction.InteractableObject>(true))
                    Object.Destroy(io.gameObject);
                foreach (var col in board.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(col);

                board.transform.SetParent(Root.transform, false);
                board.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                board.transform.localPosition = new Vector3(0f, -0.05f, 0.09f);
                board.transform.localScale = new Vector3(1f, 0.40f, 0.155f);

                foreach (var r in board.GetComponentsInChildren<MeshRenderer>(true))
                {
                    ApplyGameShader(r);
                    SetColor(r.material, new Color(0.30f, 0.06f, 0.08f));
                }

                MelonLoader.MelonLogger.Msg($"[wheel] cloned billboard backboard, bounds {CombinedBounds(board).size}");
                return true;
            }
            catch (System.Exception e)
            {
                MelonLoader.MelonLogger.Warning($"[wheel] billboard clone failed, using plain panel: {e.Message}");
                return false;
            }
        }

        private bool TryCloneCabinet()
        {
            var donor = Object.FindObjectOfType<Il2CppScheduleOne.Casino.SlotMachine>();
            if (donor == null) return false;

            try
            {
                var cabinet = Object.Instantiate(donor.gameObject);
                cabinet.name = "Cabinet";

                foreach (var nb in cabinet.GetComponentsInChildren<Il2CppFishNet.Object.NetworkBehaviour>(true))
                    Object.Destroy(nb);
                foreach (var no in cabinet.GetComponentsInChildren<Il2CppFishNet.Object.NetworkObject>(true))
                    Object.Destroy(no);

                // The donor's own controls would otherwise offer their prompts on our machine.
                foreach (var io in cabinet.GetComponentsInChildren<Il2CppScheduleOne.Interaction.InteractableObject>(true))
                    Object.Destroy(io.gameObject);

                cabinet.transform.SetParent(Root.transform, false);
                cabinet.transform.localPosition = new Vector3(0f, -1.15f, 0.35f);
                cabinet.transform.localRotation = Quaternion.identity;

                var bounds = CombinedBounds(cabinet);
                MelonLoader.MelonLogger.Msg($"[wheel] cloned slot cabinet, bounds {bounds.size}");
                return true;
            }
            catch (System.Exception e)
            {
                MelonLoader.MelonLogger.Warning($"[wheel] cabinet clone failed, using plain body: {e.Message}");
                return false;
            }
        }

        private static Bounds CombinedBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        // GameObject.CreatePrimitive hands back a built-in "Standard" material. If the game runs
        // a scriptable render pipeline, that shader has no valid camera pass, so the object
        // renders nothing while its ShadowCaster pass still works -- shadows on the ground and
        // no visible geometry, which is exactly what happened. Borrowing a shader from something
        // the game actually draws sidesteps guessing which pipeline this is.
        private static Shader _gameShader;

        private static Shader GameShader()
        {
            if (_gameShader != null) return _gameShader;

            // Ask by name. Harvesting whatever turned up first was non-deterministic --
            // FindObjectsOfType has no defined order -- and one run grabbed the weather cloud
            // shader, which draws nothing at prop scale and has no texture slot.
            _gameShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_gameShader != null)
            {
                MelonLoader.MelonLogger.Msg($"[wheel] using shader '{_gameShader.name}'");
                return _gameShader;
            }

            // Fallback: require a real surface shader, not an effect one.
            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                var m = r.sharedMaterial;
                if (m?.shader == null || m.shader.name == "Standard") continue;
                if (!m.HasProperty("_BaseMap")) continue;

                _gameShader = m.shader;
                MelonLoader.MelonLogger.Msg($"[wheel] harvested fallback shader '{_gameShader.name}' from '{r.name}'");
                return _gameShader;
            }

            MelonLoader.MelonLogger.Warning("[wheel] no usable shader found");
            return null;
        }

        private static void ApplyGameShader(Renderer renderer)
        {
            var shader = GameShader();
            if (shader != null) renderer.material.shader = shader;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static void SetTexture(Material m, Texture tex)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            MelonLoader.MelonLogger.Msg($"[wheel] material shader '{m.shader.name}', " +
                                        $"_BaseMap={m.HasProperty("_BaseMap")} _MainTex={m.HasProperty("_MainTex")}");
        }

        // Two thin arms meeting at a point, so it reads as a "<" chevron rather than the solid
        // triangle it was. Kept small and inside the rim -- nothing should overhang the body.
        private void AddPointer()
        {
            var pointer = new GameObject("Pointer");
            pointer.transform.SetParent(Root.transform, false);
            pointer.transform.localPosition = new Vector3(0.365f, DiscY, -0.11f);

            AddChevronArm(pointer.transform, +32f);
            AddChevronArm(pointer.transform, -32f);
        }

        private void AddChevronArm(Transform parent, float angle)
        {
            var arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = "Arm";
            arm.transform.SetParent(parent, false);
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            arm.transform.localPosition = arm.transform.localRotation * new Vector3(0.035f, 0f, 0f);
            arm.transform.localScale = new Vector3(0.075f, 0.016f, 0.016f);
            Object.Destroy(arm.GetComponent<Collider>());

            var renderer = arm.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, new Color(0.98f, 0.88f, 0.35f));
        }

        private const float DiscY = 0.45f;        // disc sits high, fully clear of the controls
        private const float ControlsY = -0.58f;   // bet controls sit below the disc, never overlapping

        private Il2CppTMPro.TextMeshPro _readout;

        // A recessed strip behind the readout and pads so they read as one control group rather
        // than three unrelated objects floating on the front.
        // Sits across the bottom of the board, in front of the particle emitters, so bursts read
        // as rising from behind the machine instead of spawning at a visible point.
        private void AddConcealerBar()
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "ConcealerBar";
            bar.transform.SetParent(Root.transform, false);
            bar.transform.localPosition = new Vector3(0f, -1.06f, -0.10f);
            bar.transform.localScale = new Vector3(1.48f, 0.17f, 0.16f);
            Object.Destroy(bar.GetComponent<Collider>());

            var renderer = bar.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, new Color(0.10f, 0.10f, 0.12f));
        }

        private void AddControlBezel()
        {
            var bezel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bezel.name = "ControlBezel";
            bezel.transform.SetParent(Root.transform, false);
            bezel.transform.localPosition = new Vector3(0f, ControlsY + 0.08f, -0.015f);
            bezel.transform.localScale = new Vector3(1.14f, 0.74f, 0.05f);
            Object.Destroy(bezel.GetComponent<Collider>());

            var renderer = bezel.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, new Color(0.07f, 0.07f, 0.09f));
        }


        private Il2CppTMPro.TextMeshPro _betLabel;

        // Modelled on the slot machines' own screen: an inset panel with a lit border, a small
        // persistent bet figure and a larger win figure, rather than one multipurpose line.
        private void AddReadout()
        {
            MakePanel("ScreenBorder", new Vector3(0.94f, 0.50f, 0.05f),
                new Vector3(0f, ControlsY + 0.07f, -0.03f), new Color(0.55f, 0.42f, 0.10f));
            MakePanel("Screen", new Vector3(0.88f, 0.44f, 0.05f),
                new Vector3(0f, ControlsY + 0.07f, -0.04f), new Color(0.03f, 0.03f, 0.04f));

            var font = BorrowSlotFont();

            _readout = MakeLabel("WinLabel", new Vector3(0f, ControlsY + 0.13f, -0.05f), 0.62f, font,
                new Color(0.99f, 0.86f, 0.26f));
            _betLabel = MakeLabel("BetLabel", new Vector3(0f, ControlsY - 0.02f, -0.05f), 0.34f, font,
                new Color(0.75f, 0.75f, 0.80f));

            SetText("");
        }

        // The slot machine's own label font, so the numbers match the machines beside it.
        private static Il2CppTMPro.TMP_FontAsset BorrowSlotFont()
        {
            var slot = Object.FindObjectOfType<Il2CppScheduleOne.Casino.SlotMachine>();
            if (slot?.BetAmountLabel?.font != null) return slot.BetAmountLabel.font;
            return Resources.FindObjectsOfTypeAll<Il2CppTMPro.TMP_FontAsset>().FirstOrDefault();
        }

        private Il2CppTMPro.TextMeshPro MakeLabel(string name, Vector3 pos, float size,
            Il2CppTMPro.TMP_FontAsset font, Color colour)
        {
            var go = new GameObject(name);
            var label = go.AddComponent<Il2CppTMPro.TextMeshPro>();

            // Sits between the board and the buttons, so an overlong figure slides behind them
            // rather than overlapping. Wrapping off keeps it on one line whatever the amount.
            label.fontSize = size;
            label.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = Il2CppTMPro.TextOverflowModes.Overflow;
            label.color = colour;
            if (font != null) label.font = font;

            label.rectTransform.SetParent(null, false);
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = pos;
            label.rectTransform.sizeDelta = new Vector2(0.84f, 0.16f);
            return label;
        }

        public void SetText(string text)
        {
            if (_readout != null) _readout.text = text;
        }

        public void SetBet(string text)
        {
            if (_betLabel != null) _betLabel.text = text;
        }

        // A real button on the front, sat directly above the stake strip, replacing the slot
        // machine handle mesh that came along with the donor and floated beside the wheel.
        private GameObject AddSpinButton()
        {
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "SpinButton";
            button.transform.SetParent(Root.transform, false);
            button.transform.localPosition = new Vector3(0f, ControlsY + 0.30f, -0.07f);
            button.transform.localScale = new Vector3(0.34f, 0.11f, 0.05f);
            Object.Destroy(button.GetComponent<Collider>());

            var renderer = button.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, new Color(0.85f, 0.65f, 0.12f));
            return button;
        }

        private GameObject AddBetPad(string name, float x, Color colour)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = name;
            pad.transform.SetParent(Root.transform, false);
            pad.transform.localPosition = new Vector3(x, ControlsY - 0.02f, -0.07f);
            pad.transform.localScale = new Vector3(0.13f, 0.11f, 0.05f);
            Object.Destroy(pad.GetComponent<Collider>());

            var renderer = pad.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, colour);
            return pad;
        }

        private static Mesh BuildDisc()
        {
            int segments = Segments;
            var verts = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            verts[0] = Vector3.zero;
            uvs[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(a), y = Mathf.Sin(a);
                verts[i + 1] = new Vector3(x * 0.5f, y * 0.5f, 0f);
                uvs[i + 1] = new Vector2(0.5f + x * 0.5f, 0.5f + y * 0.5f);
            }

            // Double-sided on purpose. A single-sided disc is invisible from behind while still
            // casting a shadow, which is exactly how the first version failed -- building both
            // faces removes any dependence on getting the winding or facing direction right.
            var tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int a = i + 1, b = (i + 1) % segments + 1;

                tris[i * 3] = 0;
                tris[i * 3 + 1] = a;
                tris[i * 3 + 2] = b;

                int back = segments * 3 + i * 3;
                tris[back] = 0;
                tris[back + 1] = b;
                tris[back + 2] = a;
            }

            var mesh = new Mesh { name = "PrizeWheelDisc" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Slices are coloured by the suit of the card they represent, so the face reads as a deck
        // rather than arbitrary bands. The grand prize slice is gold.
        private static Texture2D BuildFaceTexture()
        {
            var tex = new Texture2D(TextureSize, TextureSize) { name = "PrizeWheelFace" };
            var red = new Color(0.72f, 0.11f, 0.13f);
            var black = new Color(0.10f, 0.10f, 0.12f);
            var gold = new Color(0.93f, 0.78f, 0.28f);
            var rim = new Color(0.05f, 0.05f, 0.06f);

            float half = TextureSize / 2f;
            for (int py = 0; py < TextureSize; py++)
            for (int px = 0; px < TextureSize; px++)
            {
                float dx = px - half, dy = py - half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / half;

                if (dist > 1f) { tex.SetPixel(px, py, Color.clear); continue; }
                if (dist > 0.94f) { tex.SetPixel(px, py, rim); continue; }

                float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                int sliceCount = SliceCount;
                int slice = (int)(ang / 360f * sliceCount) % sliceCount;

                // Only the jackpot pays on the current table, so paying slices are marked red
                // and everything that loses stays black -- the face should not promise wins the
                // table does not actually hand out.
                Color c = slice == GrandPrizeSlice
                    ? gold
                    : (Core.Prizes.CashMultiplier(slice) > 0f ? red : black);

                // Thin dark divider at each wedge boundary.
                float within = ang / 360f * sliceCount - slice;
                if (within < 0.04f || within > 0.96f) c = rim;

                tex.SetPixel(px, py, c);
            }

            tex.Apply();
            return tex;
        }

        public void Destroy()
        {
            if (Root != null) Object.Destroy(Root);
            Root = null;
            _disc = null;
        }

        // Slice 0 starts at 3 o'clock in the face texture, so landing a slice under the pointer
        // there means no extra offset -- keep this in step with the pointer's position.
        public static float AngleForSlice(int slice) => -(slice + 0.5f) / SliceCount * 360f;

        private const int SpinRotations = 10;
        private float _angle;   // accumulated, never read back from the transform

        public IEnumerator Spin(int slice, float duration = 3.6f)
        {
            if (_disc == null) yield break;

            // Always clockwise. Reading the angle back off the transform gives a wrapped 0-360
            // value, and LerpAngle then takes the SHORTER path -- which made spins visibly run
            // backwards. Accumulating our own angle and plain-Lerping keeps direction honest.
            float from = _angle;
            float to = AngleForSlice(slice);
            while (to > from - 360f * SpinRotations) to -= 360f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);   // ease-out so it slows into the slice
                _disc.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(from, to, eased));
                yield return null;
            }

            _angle = to;
            _disc.localEulerAngles = new Vector3(0f, 0f, to);
        }
    }
}
