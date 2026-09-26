using System.Collections;
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
            Object.Destroy(host.GetComponent<Collider>());

            host.GetComponent<MeshFilter>().mesh = BuildDisc();
            var discRenderer = host.GetComponent<MeshRenderer>();
            ApplyGameShader(discRenderer);
            SetTexture(discRenderer.material, BuildFaceTexture());
            _disc = host.transform;

            AddPointer();
            AddBody();

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

        // Wheel needs a collider for the interaction raycast, on a layer inside
        // InteractionManager's search mask -- layer 0 (Default) is in it. There is no
        // interactable registry, so an off-mask layer means the prop is simply unhittable.
        private void AddBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(Root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.15f, 0.06f);
            body.transform.localScale = new Vector3(1.15f, 1.15f, 0.08f);

            Object.Destroy(body.GetComponent<Collider>());

            var bodyRenderer = body.GetComponent<MeshRenderer>();
            ApplyGameShader(bodyRenderer);
            SetColor(bodyRenderer.material, new Color(0.35f, 0.05f, 0.08f));
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

            foreach (var r in Object.FindObjectsOfType<MeshRenderer>())
            {
                var m = r.sharedMaterial;
                if (m == null || m.shader == null) continue;
                if (m.shader.name == "Standard") continue;   // the one we know does not draw

                _gameShader = m.shader;
                MelonLoader.MelonLogger.Msg($"[wheel] harvested shader '{_gameShader.name}' from '{r.name}'");
                return _gameShader;
            }

            MelonLoader.MelonLogger.Warning("[wheel] found no non-Standard shader to harvest");
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

        private void AddPointer()
        {
            var pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pointer.name = "Pointer";
            pointer.transform.SetParent(Root.transform, false);
            pointer.transform.localPosition = new Vector3(0f, 0.56f, -0.03f);
            pointer.transform.localScale = new Vector3(0.05f, 0.14f, 0.05f);
            Object.Destroy(pointer.GetComponent<Collider>());
            var pointerRenderer = pointer.GetComponent<MeshRenderer>();
            ApplyGameShader(pointerRenderer);
            SetColor(pointerRenderer.material, new Color(0.98f, 0.88f, 0.35f));
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
