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
            host.transform.localScale = Vector3.one * 0.82f;
            Object.Destroy(host.GetComponent<Collider>());

            host.GetComponent<MeshFilter>().mesh = BuildDisc();
            var discRenderer = host.GetComponent<MeshRenderer>();
            ApplyGameShader(discRenderer);
            SetTexture(discRenderer.material, BuildFaceTexture());
            _disc = host.transform;

            AddPointer();
            AddBody();
            AddControlBezel();
            AddReadout();
            BetUpAnchor = AddBetPad("BetUp", 0.30f, new Color(0.25f, 0.62f, 0.28f));
            BetDownAnchor = AddBetPad("BetDown", -0.30f, new Color(0.62f, 0.25f, 0.25f));

            // Layer 0 (Default) casts shadows but never rendered -- the camera's culling mask
            // does not include it. Door is known good: visible in game and inside
            // InteractionManager's search mask, so the prop stays clickable.
            SetLayerRecursive(Root, RenderLayer);

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
            if (TryCloneCabinet()) return;

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(Root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.05f, 0.06f);
            body.transform.localScale = new Vector3(1.05f, 1.55f, 0.12f);
            Object.Destroy(body.GetComponent<Collider>());

            var renderer = body.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, new Color(0.35f, 0.05f, 0.08f));
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

        // A "<" sitting just inside the rim. The previous wedge hung outside the disc and
        // past the body edge, which looked bolted on rather than part of the machine.
        private void AddPointer()
        {
            var pointer = new GameObject("Pointer");
            pointer.transform.SetParent(_disc.parent, false);
            pointer.transform.localPosition = new Vector3(0.40f, DiscY, -0.03f);

            var mesh = new Mesh { name = "PrizeWheelPointer" };
            const float len = 0.10f, halfH = 0.055f, d = 0.012f;
            mesh.vertices = new[]
            {
                new Vector3(-len, 0f, -d), new Vector3(0f,  halfH, -d), new Vector3(0f, -halfH, -d),
                new Vector3(-len, 0f,  d), new Vector3(0f,  halfH,  d), new Vector3(0f, -halfH,  d),
            };
            mesh.triangles = new[]
            {
                0, 1, 2,   3, 5, 4,
                0, 2, 5,   0, 5, 3,
                0, 3, 4,   0, 4, 1,
                1, 4, 5,   1, 5, 2,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            pointer.AddComponent<MeshFilter>().mesh = mesh;
            var renderer = pointer.AddComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, new Color(0.98f, 0.88f, 0.35f));
        }

        private const float DiscY = 0.28f;        // disc sits high on the face
        private const float ControlsY = -0.42f;   // bet controls share one strip below it

        private Il2CppTMPro.TextMeshPro _readout;

        // A recessed strip behind the readout and pads so they read as one control group rather
        // than three unrelated objects floating on the front.
        private void AddControlBezel()
        {
            var bezel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bezel.name = "ControlBezel";
            bezel.transform.SetParent(Root.transform, false);
            bezel.transform.localPosition = new Vector3(0f, ControlsY, -0.02f);
            bezel.transform.localScale = new Vector3(0.86f, 0.19f, 0.06f);
            Object.Destroy(bezel.GetComponent<Collider>());

            var renderer = bezel.GetComponent<MeshRenderer>();
            ApplyGameShader(renderer);
            SetColor(renderer.material, new Color(0.07f, 0.07f, 0.09f));
        }


        // Shows the stake, and the win when a round resolves. Font is borrowed from one already
        // loaded by the game so the text matches the rest of the UI.
        private void AddReadout()
        {
            var go = new GameObject("Readout");
            go.transform.SetParent(Root.transform, false);
            go.transform.localPosition = new Vector3(0f, ControlsY, -0.06f);

            _readout = go.AddComponent<Il2CppTMPro.TextMeshPro>();
            _readout.fontSize = 1.5f;
            _readout.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
            _readout.color = new Color(0.98f, 0.88f, 0.35f);
            _readout.rectTransform.sizeDelta = new Vector2(0.5f, 0.18f);

            var font = Resources.FindObjectsOfTypeAll<Il2CppTMPro.TMP_FontAsset>().FirstOrDefault();
            if (font != null) _readout.font = font;

            SetText("");
        }

        public void SetText(string text)
        {
            if (_readout != null) _readout.text = text;
        }

        private GameObject AddBetPad(string name, float x, Color colour)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = name;
            pad.transform.SetParent(Root.transform, false);
            pad.transform.localPosition = new Vector3(x, ControlsY, -0.055f);
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

        public IEnumerator Spin(int slice, float duration = 3.2f)
        {
            if (_disc == null) yield break;

            float from = _disc.localEulerAngles.z;
            float to = AngleForSlice(slice) - 360f * 4f;   // four decorative full turns
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);   // ease-out so it slows into the slice
                _disc.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(from, to, eased));
                yield return null;
            }

            _disc.localEulerAngles = new Vector3(0f, 0f, AngleForSlice(slice));
        }
    }
}
