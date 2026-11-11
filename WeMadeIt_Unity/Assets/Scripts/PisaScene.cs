// Short "saving the Leaning Tower" (HDRP). Pure function of time: Evaluate(t) puts every object in its state at second t;
// beat times come from the voice (docs/scenes/pisa_words.json). Tower and ground are sliced like a cake along z = 0;
// the camera looks along +z, north is left, south is right.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class PisaScene : MonoBehaviour, IShortScene
{
    WellScene.Doc doc;
    public const float TAIL = 1.0f;
    public float Duration => doc.duration + TAIL;
    public Camera Cam => cam;
    public List<WellScene.Label> Labels => labels;
    readonly List<WellScene.Label> labels = new List<WellScene.Label>();

    static string Norm(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s.ToLowerInvariant()) if (char.IsLetterOrDigit(ch) || ch == '\'') sb.Append(ch);
        return sb.ToString();
    }

    float Tw(int sent, string word, int occ = 0, bool end = false)
    {
        var s = doc.sentences[sent - 1];
        string key = Norm(word);
        int n = 0;
        foreach (var w in s.words)
            if (Norm(w.w) == key) { if (n == occ) return end ? w.end : w.start; n++; }
        Debug.LogError("Tw: '" + word + "' not found in sentence " + sent);
        return s.start;
    }
    float S(int i) => doc.sentences[i - 1].start;
    float En(int i) => doc.sentences[i - 1].end;

    static float P(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(1e-4f, b - a));
    static float E(float x) => x * x * (3f - 2f * x);
    static float PE(float t, float a, float b) => E(P(t, a, b));
    static float Back(float x) { const float c = 1.7f; x = Mathf.Clamp01(x); return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }
    static float Frac(float x) => x - Mathf.Floor(x);
    static float Hash(int i, int k) => Frac(Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f);
    static Color Hex(string h) => WellLook.Hex(h);
    static void Show(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }
    static void Keys(Material m)
    {
#if UNITY_EDITOR
        UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(m);
#endif
    }
    static void TwoSided(Material m)
    {
        m.SetFloat("_DoubleSidedEnable", 1f);
        m.SetFloat("_DoubleSidedNormalMode", 1f);
        m.SetFloat("_CullMode", 0f);
        Keys(m);
    }

    Transform stage;

    GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material m, Transform parent = null)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        var col = g.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
        g.transform.SetParent(parent ?? stage, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    GameObject MeshObj(string name, Mesh mesh, Material m)
    {
        var g = new GameObject(name);
        g.transform.SetParent(stage, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    Material Mat(string key)
    {
        Material m;
        if (matCache.TryGetValue(key, out m)) return m;
        switch (key)
        {
            case "pisa_marble":
                m = WellLook.Textured("white_stucco", Hex("#F4EDDD"), 4.8f, 0.3f, true, 0.45f);
                m.SetTexture("_BaseColorMap", StoneTex(10, 7, 0.035f, 0.26f, false, 3));
                TwoSided(m);
                break;
            case "pisa_stripe":
                m = WellLook.Textured("white_stucco", Hex("#EFE8D8"), 7f, 0.3f, true, 0.4f);
                m.SetTexture("_BaseColorMap", StoneTex(10, 5, 0.03f, 0.2f, true, 5));
                TwoSided(m);
                break;
            case "pisa_tile": m = WellLook.Textured("grey_roof_tiles", Hex("#C07A55"), 3f, 0.2f, true); TwoSided(m); break;
            case "pisa_cypress": m = WellLook.Metal(key, Hex("#2A4A30"), 0.12f, 0f, "rock_wall_02", 2.2f, 1.1f); TwoSided(m); break;
            case "pisa_section":
                m = WellLook.Metal(key, Hex("#B6AE9F"), 0.12f, 0f, "rock_wall_02", 5f, 0.7f);
                m.SetTexture("_BaseColorMap", StoneTex(6, 5, 0.05f, 0.34f, false, 8));
                m.SetFloat("_UVBase", 5f); m.SetFloat("_TexWorldScale", 1f / 5f); m.SetFloat("_ObjectSpaceUVMapping", 1f);
                TwoSided(m);
                break;
            default: m = WellLook.Lit(key, Hex("#B0B0B0"), 0.4f); break;
        }
        matCache[key] = m;
        return m;
    }

    GameObject Model(string file, Transform parent, Vector3 pos)
    {
        var src = Resources.Load<GameObject>("Models/" + file);
        if (src == null) { Debug.LogError("model missing: " + file); return new GameObject(file); }
        var g = Instantiate(src);
        g.name = file;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = Mat(ms[i] != null ? ms[i].name.Replace(" (Instance)", "") : "");
            r.sharedMaterials = ms;
        }
        return g;
    }

    readonly Dictionary<string, GameObject> phSrc = new Dictionary<string, GameObject>();
    GameObject PH(string id, int variant, Vector3 pos, float yaw, float scale)
    {
        GameObject src;
        if (!phSrc.TryGetValue(id, out src)) { src = Resources.Load<GameObject>($"PH/model/{id}/{id}_1k"); phSrc[id] = src; }
        if (src == null) { Debug.LogError("PH model missing: " + id); return new GameObject(id); }
        var holder = new GameObject(id + "_" + variant).transform;
        holder.SetParent(stage, false);
        var rends = src.GetComponentsInChildren<Renderer>();
        var g = (variant >= 0 && rends.Length > 1) ? Instantiate(rends[variant % rends.Length].gameObject) : Instantiate(src);
        g.transform.SetParent(holder, false);
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        foreach (var r in rs)
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = WellLook.PHMaterial(id, ms[i] != null ? ms[i].name.Replace(" (Instance)", "") : id);
            r.sharedMaterials = ms;
        }
        holder.localPosition = pos;
        holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
        holder.localScale = Vector3.one * scale;
        return holder.gameObject;
    }

    // a vertical sheet on the cut plane between two curves
    class Strip
    {
        readonly Mesh mesh; readonly Vector3[] v; readonly int n;
        public Strip(PisaScene s, string name, int columns, Material m)
        {
            n = columns;
            v = new Vector3[n * 2];
            var nrm = new Vector3[n * 2]; var tan = new Vector4[n * 2]; var uv = new Vector2[n * 2];
            for (int i = 0; i < n * 2; i++) { nrm[i] = Vector3.back; tan[i] = new Vector4(1, 0, 0, 1); }
            var tr = new int[(n - 1) * 6];
            for (int i = 0, q = 0; i < n - 1; i++)
            {
                int t0 = i * 2, b0 = i * 2 + 1, t1 = i * 2 + 2, b1 = i * 2 + 3;
                tr[q++] = b0; tr[q++] = t0; tr[q++] = b1;
                tr[q++] = t0; tr[q++] = t1; tr[q++] = b1;
            }
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = v; mesh.normals = nrm; mesh.tangents = tan; mesh.uv = uv; mesh.triangles = tr;
            s.MeshObj(name, mesh, m);
        }
        public void Set(int i, float x, float top, float bot, float z) { v[i * 2] = new Vector3(x, top, z); v[i * 2 + 1] = new Vector3(x, bot, z); }
        public void Commit() { mesh.vertices = v; mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8000f); }
    }

    // a flat arrow facing the camera, tail at the origin, pointing up its local Y
    class Arrow
    {
        public readonly GameObject go;
        public Arrow(PisaScene s, string name, float L, float W, Material m)
        {
            float sw = W * 0.42f, hl = W * 1.1f;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-sw / 2, 0, 0), new Vector3(-sw / 2, L - hl, 0), new Vector3(-W / 2, L - hl, 0), new Vector3(0, L, 0), new Vector3(W / 2, L - hl, 0), new Vector3(sw / 2, L - hl, 0), new Vector3(sw / 2, 0, 0) };
            mesh.triangles = new[] { 0, 1, 5, 0, 5, 6, 2, 3, 4 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = new Vector2[7];
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 500f);
            go = s.MeshObj(name, mesh, m);
            go.SetActive(false);
        }
        public void Set(bool on, Vector3 pos, float ang, float k)
        {
            Show(go, on && k > 0.01f);
            if (!on) return;
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
            go.transform.localScale = new Vector3(k, k, 1f);
        }
    }

    // ------------------------------------------------------------------ objects
    Camera cam;
    Light sun; HDRISky sky;
    Transform pivot;
    GameObject frontHalf, lawnFront, paveFront;
    const float DUOMO_YAW = 105f;
    Strip layerSand, layerClay, layerDeep;
    float[] fx;
    Material frostMat, dustMat; Color frostBase, dustBase;
    GameObject[] blocks, frost, stones, dust, soil, cables, plates, ghost;
    Vector3[] blockPos;
    GameObject slab, tube, mast, rigBody, cavity, heap, barFound, barTop;
    Arrow arWeight, arSouth, arLead, arWrong, arLeft, arRight, arSettle;
    WellScene.Label labTons, lab10, labClay, labYear, labLead, labStop, labUgly, labFrozen, labWrong, lab900, labHigh, labSink, labDont, labDig, lab70, lab18, lab200, labFront;

    WellScene.Label Lab(string text, string style) { var l = new WellScene.Label { text = text, style = style }; labels.Add(l); return l; }

    void Pill(WellScene.Label l, float t, float a, float b, Vector3 pos)
    {
        l.pos = pos;
        l.k = (t < a || t >= b) ? 0f : Mathf.Min(Back(P(t, a, a + 0.22f)), 1f - P(t, b - 0.12f, b));
    }

    // ------------------------------------------------------------------ build
    const float FOV = 34f;

    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<WellScene.Doc>(File.ReadAllText(wordsJson));
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);
        WellLook.FoliageTint = new Color(0.7f, 0.85f, 0.55f);
        WellLook.GradeContrast = 9f; WellLook.GradeSaturation = 12f; WellLook.GradeWarm = 7f;
        sun = WellLook.Sun(stage);
        var vol = WellLook.Environment(stage);
        Fog fog;
        if (vol.sharedProfile.TryGet(out fog)) { fog.meanFreePath.value = 900f; fog.maximumHeight.value = 80f; }
        vol.sharedProfile.TryGet(out sky);
        cam = WellLook.Camera(stage, FOV);

        BuildGround();
        BuildTower();
        BuildProps();
        BuildFx();

        labTons = Lab("15,000 TONS", "y");
        lab10 = Lab("10 FT", "y");
        labClay = Lab("SOFT WET CLAY", "b");
        labYear = Lab("1178", "y");
        labLead = Lab("600 TONS OF LEAD", "o");
        labStop = Lab("IT STOPS", "y");
        labUgly = Lab("UGLY", "w");
        labFrozen = Lab("FROZEN", "b");
        labWrong = Lab("WRONG WAY", "o");
        lab900 = Lab("900 TONS", "o");
        labHigh = Lab("HIGH SIDE", "w");
        labSink = Lab("SINKING", "o");
        labDont = Lab("LEAVE IT", "o");
        labDig = Lab("DIG HERE", "y");
        lab70 = Lab("70 TONS", "y");
        lab18 = Lab("18 INCHES", "y");
        lab200 = Lab("200+ YEARS", "y");
    }

    static Quaternion FlatUp => Quaternion.Euler(90f, 0f, 0f);

    void BuildGround()
    {
        var grass = WellLook.Textured("leafy_grass", new Color(0.46f, 0.6f, 0.33f), 3.5f, 0.1f);
        var q = Prim(PrimitiveType.Quad, "lawnBack", new Vector3(0f, 0f, 1000f), new Vector3(4000f, 2000f, 1f), grass);
        q.transform.localRotation = FlatUp;
        lawnFront = Prim(PrimitiveType.Quad, "lawnFront", new Vector3(0f, 0f, -1000f), new Vector3(4000f, 2000f, 1f), grass);
        lawnFront.transform.localRotation = FlatUp;

        var l = new List<float>();
        foreach (var v in new[] { -1500f, -800f, -400f, -200f, -100f, -70f }) l.Add(v);
        for (float x = -60f; x <= 60f; x += 0.5f) l.Add(x);
        foreach (var v in new[] { 70f, 100f, 200f, 400f, 800f, 1500f }) l.Add(v);
        fx = l.ToArray();
        var nrm = Resources.Load<Texture2D>("PH/tex/excavated_soil_wall/excavated_soil_wall_nor_gl");
        Material Face(string name, string hex, float tile, Texture2D tex, float normalScale)
        {
            var m = WellLook.Lit(name, Hex(hex), 0.08f);
            m.SetTexture("_BaseColorMap", tex);
            if (nrm != null) { m.SetTexture("_NormalMap", nrm); m.SetFloat("_NormalScale", normalScale); }
            m.SetFloat("_UVBase", 5f);
            m.SetFloat("_TexWorldScale", 1f / tile);
            m.SetFloat("_ObjectSpaceUVMapping", 0f);
            Keys(m);
            return m;
        }
        var sand = Face("faceSand", "#C2A074", 7f, EarthTex(0.045f, 900, 3f, 0.07f, 0.14f, 1), 0f);
        var clay = Face("faceClay", "#7E8791", 12f, EarthTex(0.05f, 160, 2.2f, 0.035f, 0.18f, 2), 0f);
        var deep = Face("faceDeep", "#8F7656", 18f, EarthTex(0.08f, 1500, 6f, 0.07f, 0.16f, 3), 0f);
        layerSand = new Strip(this, "LayerSand", fx.Length, sand);
        layerClay = new Strip(this, "LayerClay", fx.Length, clay);
        layerDeep = new Strip(this, "LayerDeep", fx.Length, deep);
    }

    // noise that repeats seamlessly: four shifted copies, blended
    static float Fbm(float u, float v, float f, int oct, float seed)
    {
        float Tile(float a, float b, float fr)
        {
            float n(float x, float y) => Mathf.PerlinNoise((x + seed) * fr, (y + seed * 1.7f) * fr);
            return n(a, b) * (1f - a) * (1f - b) + n(a - 1f, b) * a * (1f - b) + n(a, b - 1f) * (1f - a) * b + n(a - 1f, b - 1f) * a * b;
        }
        float sum = 0f, amp = 1f, tot = 0f;
        for (int i = 0; i < oct; i++) { sum += Tile(u, v, f) * amp; tot += amp; amp *= 0.5f; f *= 2f; }
        return sum / tot;
    }

    // courses of cut marble: every block its own shade, thin joints, rain streaks and old stains
    static Texture2D StoneTex(int rows, int blocks, float joint, float stain, bool stripes, int seed)
    {
        const int N = 1024;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var c = new Color[N * N];
        var rnd = new System.Random(seed);
        var tint = new float[rows, blocks];
        for (int r = 0; r < rows; r++) for (int b = 0; b < blocks; b++) tint[r, b] = (float)rnd.NextDouble();
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float ry = v * rows; int r = Mathf.FloorToInt(ry) % rows; float fy = ry - Mathf.Floor(ry);
                float rx = u * blocks + (r % 2) * 0.5f; int b = Mathf.FloorToInt(rx) % blocks; float fx = rx - Mathf.Floor(rx);
                float k = tint[r, b];
                float g = 0.93f + 0.09f * k;
                g *= 0.9f + 0.2f * Fbm(u, v, 18f, 3, 3f);
                float streak = Fbm(u, v * 0.18f, 26f, 2, 9f);
                g *= 1f - stain * 0.7f * Mathf.Clamp01((streak - 0.55f) * 4f);
                g *= 1f - stain * 0.8f * Mathf.Clamp01((Fbm(u, v, 5f, 3, 21f) - 0.52f) * 5f);
                float jy = Mathf.Min(fy, 1f - fy) * rows / (float)rows, jx = Mathf.Min(fx, 1f - fx) / blocks * rows;
                float d = Mathf.Min(Mathf.Min(fy, 1f - fy), Mathf.Min(fx, 1f - fx) * rows / (float)blocks * 1f);
                float line = Mathf.Clamp01(d / joint);
                g *= Mathf.Lerp(0.66f, 1f, line);
                var col = new Color(g, g * 0.985f, g * 0.95f, 1f);
                if (stripes && r % 2 == 1) col *= new Color(0.62f, 0.63f, 0.66f, 1f);
                col.a = 1f;
                c[y * N + x] = col;
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    // earth seen on a cut face: mottling, thin beds, grains and stones
    static Texture2D EarthTex(float beds, int stones, float stoneMax, float grain, float mottle, int seed)
    {
        const int N = 1024;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var g = new float[N * N];
        var rnd = new System.Random(seed);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float wv = v + 0.012f * Mathf.Sin(2f * Mathf.PI * (u * 2f + 0.3f)) + (Fbm(u, v, 8f, 2, 5f) - 0.5f) * 0.03f;
                float b = 0.5f + 0.5f * (0.45f * Mathf.Sin(2f * Mathf.PI * wv * 7f) + 0.35f * Mathf.Sin(2f * Mathf.PI * (wv * 19f + 0.2f)) + 0.2f * Mathf.Sin(2f * Mathf.PI * (wv * 43f + 0.6f)));
                float m = Fbm(u, v, 6f, 4, 13f);
                g[y * N + x] = (1f - beds * b) * (1f - mottle + 2f * mottle * m) * (1f - grain + 2f * grain * (float)rnd.NextDouble());
            }
        for (int s = 0; s < stones; s++)
        {
            int cx = rnd.Next(N), cy = rnd.Next(N);
            float rx = 2f + (float)rnd.NextDouble() * stoneMax, ry = rx * (0.55f + 0.4f * (float)rnd.NextDouble());
            float shade = rnd.NextDouble() < 0.55 ? 0.62f + 0.2f * (float)rnd.NextDouble() : 1.15f + 0.15f * (float)rnd.NextDouble();
            int R = Mathf.CeilToInt(rx) + 2;
            for (int dy = -R; dy <= R; dy++)
                for (int dx = -R; dx <= R; dx++)
                {
                    float e = Mathf.Sqrt(dx * dx / (rx * rx) + dy * dy / (ry * ry));
                    if (e > 1.15f) continue;
                    int px = (cx + dx + N) % N, py = (cy + dy + N) % N;
                    float inner = Mathf.Clamp01((1.15f - e) / 0.2f);
                    float rim = (dy < 0 ? 0.92f : 1.05f);
                    g[py * N + px] *= Mathf.Lerp(1f, shade * rim, inner);
                }
        }
        var c = new Color[N * N];
        for (int i = 0; i < N * N; i++) c[i] = new Color(g[i], g[i], g[i], 1f);
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    // calm, fine-grained earth: faint beds and grain, tinted by the material
    static Texture2D GrainTex()
    {
        const int N = 1024;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var c = new Color[N * N];
        var rnd = new System.Random(11);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float wv = v + 0.012f * Mathf.Sin(2f * Mathf.PI * (u * 2f + 0.3f)) + 0.006f * Mathf.Sin(2f * Mathf.PI * (u * 5f + v * 2f));
                float beds = 0.5f + 0.5f * (0.5f * Mathf.Sin(2f * Mathf.PI * wv * 5f) + 0.3f * Mathf.Sin(2f * Mathf.PI * (wv * 13f + 0.2f)) + 0.2f * Mathf.Sin(2f * Mathf.PI * (wv * 31f + 0.6f)));
                float g = (1f - 0.05f * beds) * (0.95f + 0.1f * (float)rnd.NextDouble());
                c[y * N + x] = new Color(g, g, g, 1f);
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    void SetGround(float sink)
    {
        for (int i = 0; i < fx.Length; i++)
        {
            float x = fx[i];
            float d1 = (x - 2f) / 16f, d2 = (x - 2f) / 22f;
            float yClay = -11f - sink * 3.4f * Mathf.Exp(-d1 * d1);
            float yDeep = -40f - sink * 1.0f * Mathf.Exp(-d2 * d2);
            layerSand.Set(i, x, 0f, yClay, 0.03f);
            layerClay.Set(i, x, yClay, yDeep, 0.03f);
            layerDeep.Set(i, x, yDeep, -500f, 0.03f);
        }
        layerSand.Commit(); layerClay.Commit(); layerDeep.Commit();
    }

    void BuildTower()
    {
        pivot = new GameObject("TowerPivot").transform;
        pivot.SetParent(stage, false);
        pivot.localPosition = new Vector3(0f, -1.5f, 0f);
        var a = Model("pisa_a", pivot, new Vector3(0f, 1.5f, 0f));
        var b = Model("pisa_b", pivot, new Vector3(0f, 1.5f, 0f));
        float az = 0f;
        foreach (var r in a.GetComponentsInChildren<Renderer>()) { az = r.bounds.center.z; break; }
        frontHalf = az < 0f ? a : b;
    }

    GameObject Bar(string name, Material m) { var g = Prim(PrimitiveType.Quad, name, Vector3.zero, Vector3.one, m); g.SetActive(false); return g; }

    void PlaceBar(GameObject g, Vector3 a, Vector3 b, float w)
    {
        var d = b - a;
        g.transform.localPosition = (a + b) * 0.5f;
        g.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        g.transform.localScale = new Vector3(d.magnitude, w, 1f);
    }

    void BuildProps()
    {
        var pave = WellLook.Textured("concrete_floor_02", Hex("#CFC8B8"), 3f, 0.2f);
        var pv = Prim(PrimitiveType.Quad, "paveBack", new Vector3(0f, 0.02f, 12f), new Vector3(44f, 24f, 1f), pave);
        pv.transform.localRotation = FlatUp;
        paveFront = Prim(PrimitiveType.Quad, "paveFront", new Vector3(0f, 0.02f, -12f), new Vector3(44f, 24f, 1f), pave);
        paveFront.transform.localRotation = FlatUp;

        var du = new GameObject("DuomoHolder").transform;
        du.SetParent(stage, false);
        du.localPosition = new Vector3(64f, 0f, 195f);
        du.localRotation = Quaternion.Euler(0f, DUOMO_YAW, 0f);
        Model("pisa_duomo", du, Vector3.zero);
        for (int i = 0; i < 16; i++)
        {
            var h = new GameObject("cypress" + i).transform;
            h.SetParent(stage, false);
            h.localPosition = new Vector3(-30f + i * 13.5f + Hash(i, 7) * 4f, 0f, 150f + Hash(i, 8) * 40f);
            h.localScale = Vector3.one * (0.85f + Hash(i, 9) * 0.5f);
            Model("pisa_cypress", h, Vector3.zero);
        }

        // the walls and trees at the far side of the square
        var wall = WellLook.Textured("white_stucco", Hex("#D4C3A2"), 4f, 0.2f);
        Prim(PrimitiveType.Cube, "cityWall", new Vector3(0f, 6f, 210f), new Vector3(1400f, 12f, 3f), wall);
        for (int i = 0; i < 34; i++)
        {
            float x = -190f + i * 11.5f + Hash(i, 1) * 5f, z = 120f + Hash(i, 2) * 70f;
            PH("tree_small_02", -1, new Vector3(x, 0f, z), Hash(i, 3) * 360f, 2.6f + Hash(i, 4) * 1.2f);
        }

        var lead = WellLook.Metal("lead", Hex("#6C7178"), 0.34f, 0.55f, "metal_plate", 0.8f, 0.3f);
        var conc = WellLook.Textured("concrete_floor_02", Hex("#BEBAB0"), 2f, 0.2f);
        slab = Prim(PrimitiveType.Cube, "leadSlab", new Vector3(-11.3f, 0.2f, 1.3f), new Vector3(7.2f, 0.4f, 2.7f), conc);
        blocks = new GameObject[72]; blockPos = new Vector3[72];
        int n = 0;
        for (int ly = 0; ly < 6; ly++)
            for (int rz = 0; rz < 2; rz++)
                for (int cx = 0; cx < 6; cx++)
                {
                    int k = n++;
                    blockPos[k] = new Vector3(-14.05f + cx * 1.1f, 0.4f + 0.33f + ly * 0.66f, 0.62f + rz * 1.25f);
                    blocks[k] = Prim(PrimitiveType.Cube, "lead" + k, blockPos[k], new Vector3(1.06f, 0.62f, 1.2f), lead);
                    blocks[k].SetActive(false);
                }

        var white = WellLook.Unlit("arrowW", Color.white * 2.2f);
        var orange = WellLook.Unlit("arrowO", Hex("#FF6A10") * 1.3f);
        var yellow = WellLook.Unlit("barY", Hex("#FFD23F") * 1.6f);
        arWeight = new Arrow(this, "arWeight", 30f, 7f, white);
        arSouth = new Arrow(this, "arSouth", 13f, 6f, orange);
        arLead = new Arrow(this, "arLead", 7f, 3f, white);
        arWrong = new Arrow(this, "arWrong", 15f, 7f, orange);
        arLeft = new Arrow(this, "arLeft", 13f, 6f, white);
        arRight = new Arrow(this, "arRight", 13f, 6f, orange);
        arSettle = new Arrow(this, "arSettle", 5f, 2.4f, white);
        barFound = Bar("bar10", yellow);
        barTop = Bar("bar18", yellow);
        ghost = new[] { Bar("ghostR", yellow), Bar("ghostT", yellow), Bar("ghostL", yellow) };

        var steel = WellLook.Lit("cable", Hex("#E2E6EB"), 0.7f, 0.2f);
        cables = new GameObject[4]; plates = new GameObject[4];
        for (int i = 0; i < 4; i++)
        {
            cables[i] = Prim(PrimitiveType.Cylinder, "cable" + i, Vector3.zero, Vector3.one, steel); cables[i].SetActive(false);
            plates[i] = Prim(PrimitiveType.Cube, "plate" + i, Vector3.zero, new Vector3(1.6f, 0.55f, 1f), steel); plates[i].SetActive(false);
        }

        // the drill
        var paint = WellLook.Metal("rig", Hex("#E2A21E"), 0.5f, 0.15f, "metal_plate", 0.8f, 0.3f);
        var dark = WellLook.Metal("rigDark", Hex("#3A4047"), 0.45f, 0.7f, "metal_plate", 0.6f, 0.4f);
        rigBody = Prim(PrimitiveType.Cube, "rigBody", new Vector3(-31.2f, 1.25f, 1.4f), new Vector3(6.4f, 2.5f, 2.8f), paint);
        mast = Prim(PrimitiveType.Cube, "mast", Vector3.zero, Vector3.one, dark);
        tube = Prim(PrimitiveType.Cylinder, "tube", Vector3.zero, Vector3.one, WellLook.Metal("tube", Hex("#C7CCD2"), 0.65f, 0.9f, "metal_plate", 0.5f, 0.3f));
        var cav = WellLook.Unlit("cavity", new Color(0.03f, 0.025f, 0.02f, 1f), WellLook.SoftDot(), true);
        cavity = Prim(PrimitiveType.Quad, "cavity", new Vector3(TIP.x, TIP.y, 0.01f), Vector3.one, cav);
        cavity.SetActive(false);
        heap = Prim(PrimitiveType.Sphere, "heap", new Vector3(-20f, 0f, 1.6f), Vector3.one, WellLook.Textured("brown_mud_03", new Color(0.62f, 0.6f, 0.56f), 2f, 0.08f));
        heap.SetActive(false);
    }

    static readonly Vector3 ENTRY = new Vector3(-25f, 0f, 0f), TIP = new Vector3(-5.5f, -8f, 0f);

    void BuildFx()
    {
        var dot = WellLook.SoftDot();
        var frC = new Color(0.72f, 0.9f, 1.25f, 1f); frC.a = 0.3f;
        var fr = WellLook.Unlit("frost", frC, dot, true); frostMat = fr; frostBase = frC;
        frost = new GameObject[14];
        for (int i = 0; i < frost.Length; i++) { frost[i] = Prim(PrimitiveType.Quad, "frost" + i, Vector3.zero, Vector3.one, fr); frost[i].SetActive(false); }

        dustBase = new Color(1.05f, 0.96f, 0.8f, 0.34f);
        var dm = WellLook.Unlit("dust", dustBase, dot, true); dustMat = dm;
        dust = new GameObject[30];
        for (int i = 0; i < dust.Length; i++) { dust[i] = Prim(PrimitiveType.Quad, "dust" + i, Vector3.zero, Vector3.one, dm); dust[i].SetActive(false); }

        var st = Mat("pisa_marble");
        stones = new GameObject[18];
        for (int i = 0; i < stones.Length; i++) { stones[i] = Prim(PrimitiveType.Sphere, "stone" + i, Vector3.zero, Vector3.one, st); stones[i].SetActive(false); }

        var sm = WellLook.Lit("spoil", Hex("#6A5C49"), 0.1f);
        soil = new GameObject[26];
        for (int i = 0; i < soil.Length; i++) { soil[i] = Prim(PrimitiveType.Sphere, "soil" + i, Vector3.zero, Vector3.one * 0.3f, sm); soil[i].SetActive(false); }
    }

    // ------------------------------------------------------------------ the camera
    class Shot { public float t; public Vector3 pos, look; public Shot(float t, Vector3 p, Vector3 l) { this.t = t; pos = p; look = l; } }
    static Vector3 Cp(float x, float y, float height) => new Vector3(x, y, -height * 0.5f / Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad));
    static Vector3 Lk(float x, float y) => new Vector3(x, y, 0f);
    Shot W(float t, float x, float y, float h) => new Shot(t, Cp(x, y, h), Lk(x, y));
    Shot[] shots;

    void MakeShots()
    {
        const float C = 0.02f;
        float found = Tw(2, "foundation") - 0.15f, soft = Tw(2, "soft") - 0.15f;
        float so5 = Tw(5, "so") - 0.05f;
        float dig10 = Tw(10, "dig") - 0.25f, high = Tw(12, "high") - 0.25f, whole = Tw(12, "whole") - 0.05f;
        float top13 = Tw(13, "top") - 0.2f;
        shots = new[]
        {
            new Shot(0f, new Vector3(-27f, 1.7f, -106f), new Vector3(3f, 31f, 0f)),
            new Shot(S(2) - C, new Vector3(-21f, 1.7f, -99f), new Vector3(3f, 31f, 0f)),
            W(S(2), 1f, 9f, 114f),
            W(found, 1f, 9f, 110f),
            W(found + 0.7f, 0f, -3f, 30f),
            W(soft, 0f, -3f, 28f),
            W(soft + 0.7f, 0f, -16f, 58f),
            W(S(3) - C, 0f, -16f, 56f),
            W(S(3), 1f, 9f, 114f),
            W(S(4) - C, 1f, 9f, 110f),
            W(S(4), -5f, 10f, 44f),
            W(S(5) - C, -5f, 10f, 41f),
            W(S(5), -11f, 4f, 15f),
            W(so5, -11f, 4f, 14f),
            W(so5 + 0.7f, -8f, -19f, 62f),
            W(S(7) - C, -8f, -19f, 60f),
            W(S(7), 1f, 9f, 114f),
            W(S(8) - C, 1f, 9f, 110f),
            W(S(8), -5f, 9f, 48f),
            W(S(9) - C, -5f, 9f, 45f),
            W(S(9), 1f, 9f, 114f),
            W(S(10) - C, 1f, 6f, 96f),
            W(S(10), 8f, -3.5f, 26f),
            W(dig10, 8f, -3.5f, 26f),
            W(dig10 + 0.7f, -8f, -3.5f, 26f),
            W(S(11) - C, -8f, -3.5f, 25f),
            W(S(11), -15.5f, -1f, 44f),
            W(high, -15.5f, -1f, 43f),
            W(high + 0.6f, -6f, -3f, 22f),
            W(whole - C, -6f, -3f, 22f),
            W(whole, 5.5f, 52f, 25f),
            W(S(13) - C, 5.5f, 52f, 24f),
            W(S(13), -20f, 3.2f, 18f),
            W(top13 - C, -20f, 3.2f, 17f),
            W(top13, 5.3f, 52f, 24f),
            W(S(14) - C, 5.3f, 52f, 23f),
            new Shot(S(14), new Vector3(-30f, 2.2f, -112f), new Vector3(3f, 31f, 0f)),
            new Shot(Duration + 1f, new Vector3(-37f, 3.5f, -126f), new Vector3(3f, 31f, 0f)),
        };
    }

    void UpdateCamera(float t, float shake)
    {
        if (shots == null) MakeShots();
        int i = 0;
        while (i < shots.Length - 2 && t >= shots[i + 1].t) i++;
        float u = PE(t, shots[i].t, shots[i + 1].t);
        var pos = Vector3.Lerp(shots[i].pos, shots[i + 1].pos, u);
        var look = Vector3.Lerp(shots[i].look, shots[i + 1].look, u);
        if (shake > 0f)
        {
            pos += new Vector3(Mathf.Sin(t * 61f) + Mathf.Sin(t * 37f) * 0.6f, Mathf.Sin(t * 53f + 1f) + Mathf.Sin(t * 29f) * 0.6f, 0f) * shake;
            look += new Vector3(Mathf.Sin(t * 47f + 2f), Mathf.Sin(t * 67f), 0f) * shake * 0.6f;
        }
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
    }

    // ------------------------------------------------------------------ the story
    const float LEAN = 5.5f, LEAN_END = 5.04f;

    Vector3 OnTower(float angle, float lx, float ly)
    {
        var q = Quaternion.Euler(0f, 0f, -angle);
        return new Vector3(0f, -1.5f, 0f) + q * new Vector3(lx, ly + 1.5f, 0f);
    }

    public void Evaluate(float t)
    {
        foreach (var l in labels) l.k = 0f;
        float s2 = S(2), s3 = S(3), s4 = S(4), s5 = S(5), s7 = S(7), s8 = S(8), s9 = S(9), s10 = S(10), s11 = S(11), s12 = S(12), s13 = S(13), s14 = S(14);
        bool cut = t >= s2 && t < s14;
        bool topShot = (t >= Tw(12, "whole") - 0.05f && t < s13) || (t >= Tw(13, "top") - 0.2f && t < s14);
        Show(frontHalf, !cut || topShot); Show(lawnFront, !cut); Show(paveFront, !cut);

        // ------ the lean
        float lean = LEAN, sink = 1f, shake = 0f;
        float lurch = Tw(7, "lurches");
        if (t < s2)
        {
            float f = P(t, Tw(1, "fall") - 0.2f, s2);
            lean = LEAN + 0.12f * Mathf.Sin(t * 5f) + 5.5f * f * f;
            shake = 0.06f + 0.55f * f;
        }
        else if (t >= s3 && t < s4)
        {
            float back = P(t, s3, s3 + 0.28f), g = P(t, s3 + 0.3f, En(3));
            lean = back < 1f ? Mathf.Lerp(LEAN, 0f, back) : LEAN * g;
            sink = back < 1f ? 1f - back : g;
        }
        else if (t >= s7 && t < s8 + 1.8f)
        {
            float kick = 1f - Mathf.Pow(1f - P(t, lurch, lurch + 0.32f), 3f);
            lean = LEAN + 0.8f * kick - 0.8f * PE(t, s8 + 0.2f, s8 + 1.8f);
            shake = t > lurch ? 0.5f * (1f - P(t, lurch, lurch + 0.9f)) : 0f;
        }
        else if (t >= s12)
        {
            lean = Mathf.Lerp(LEAN, LEAN_END, PE(t, Tw(12, "whole") - 0.05f, En(12) + 0.1f));
        }
        pivot.localRotation = Quaternion.Euler(0f, 0f, -lean);
        SetGround(sink);

        // ------ the lead
        float a1 = Tw(4, "stack"), b1 = Tw(4, "and") - 0.1f, a2 = Tw(8, "pile"), b2 = Tw(8, "fast", 0, true);
        bool leadOn = t >= s4 && t < s14;
        Show(slab, leadOn);
        for (int i = 0; i < 72; i++)
        {
            float st = i < 48 ? a1 + (b1 - a1) * i / 48f : a2 + (b2 - a2) * (i - 48) / 24f;
            bool on = leadOn && t >= st;
            Show(blocks[i], on);
            if (on) blocks[i].transform.localPosition = blockPos[i] + Vector3.up * 9f * Mathf.Pow(1f - P(t, st, st + 0.18f), 2f);
        }
        if (t >= a2 && t < b2) shake = Mathf.Max(shake, 0.05f);

        // ------ cables going down, then the frozen ground
        float ca = Tw(5, "cables"), cb = Tw(5, "underground", 0, true), fz = Tw(6, "freeze");
        bool cablesOn = t >= ca && t < s8;
        for (int i = 0; i < 4; i++)
        {
            float x0 = -6.9f + i * 1.9f, x1 = x0 - 2.2f + i * 0.5f, y0 = -2.4f, y1 = -42f;
            float u = PE(t, ca + i * 0.12f, cb);
            Show(cables[i], cablesOn && u > 0.001f); Show(plates[i], cablesOn && u > 0.97f);
            if (cablesOn && u > 0.001f)
            {
                var a = new Vector3(x0, y0, -0.2f); var b = Vector3.Lerp(a, new Vector3(x1, y1, -0.2f), u);
                cables[i].transform.localPosition = (a + b) * 0.5f;
                cables[i].transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                cables[i].transform.localScale = new Vector3(0.34f, (b - a).magnitude * 0.5f, 0.34f);
                plates[i].transform.localPosition = new Vector3(x1, y1, -0.2f);
            }
        }
        float frostK = PE(t, fz, En(6) + 0.2f) * (1f - PE(t, lurch, lurch + 0.4f));
        for (int i = 0; i < frost.Length; i++)
        {
            bool on = frostK > 0.01f;
            Show(frost[i], on);
            if (!on) continue;
            float px = -8f + (Hash(i, 1) - 0.5f) * 12f, py = -9f - Hash(i, 2) * 8f;
            float sz = (30f + Hash(i, 3) * 12f) * frostK;
            frost[i].transform.localPosition = new Vector3(px, py, 0.015f + i * 0.0004f);
            frost[i].transform.localScale = new Vector3(sz, sz * 0.8f, 1f);
        }

        // ------ falling stones (hook) and dust
        float hookF = Tw(1, "about");
        for (int i = 0; i < stones.Length; i++)
        {
            float st = hookF + i * 0.13f, u = t - st;
            bool on = t < s2 && u > 0f;
            Show(stones[i], on);
            if (!on) continue;
            float lx = (Hash(i, 1) - 0.5f) * 12f, ly = 34f + Hash(i, 2) * 22f;
            var p0 = OnTower(lean, lx, ly);
            float vx = 3f + Hash(i, 3) * 5f;
            stones[i].transform.localPosition = p0 + new Vector3(vx * u, -4.9f * u * u, -8f * Hash(i, 4));
            float sz = 0.28f + Hash(i, 5) * 0.5f;
            stones[i].transform.localScale = Vector3.one * sz;
        }
        for (int i = 0; i < dust.Length; i++)
        {
            float st, u; Vector3 p0; bool on;
            if (t < s2) { st = hookF + i * 0.07f; u = t - st; on = u > 0f; p0 = OnTower(lean, (Hash(i, 1) - 0.5f) * 15f, 12f + Hash(i, 2) * 40f); }
            else { st = lurch + i * 0.012f; u = t - st; on = u > 0f && u < 1.4f && t < s8; p0 = new Vector3(8f + Hash(i, 1) * 10f, 0.5f, -1f); }
            Show(dust[i], on);
            if (!on) continue;
            float g = Mathf.Clamp01(u / 1.4f);
            dust[i].transform.localPosition = p0 + new Vector3(Hash(i, 3) * 3f * g, 2.2f * g - (t < s2 ? 7f * g : 0f), -0.5f - i * 0.01f);
            float sz = (t < s2 ? 7f : 5f) * (0.5f + g);
            dust[i].transform.localScale = new Vector3(sz, sz, 1f);
        }

        // ------ the drill
        float da = Tw(11, "slides"), db = Tw(11, "angle", 0, true), pa = Tw(11, "pulls"), pb = En(11), hc = Tw(12, "hole"), hd = Tw(12, "high");
        bool rig = t >= s11 && t < s14;
        Show(rigBody, rig); Show(mast, rig);
        var dir = (TIP - ENTRY).normalized;
        float out_ = PE(t, hc - 0.5f, hc + 0.1f);
        float reach = PE(t, da, db) * (1f - out_);
        var tp = Vector3.Lerp(ENTRY, TIP, reach);
        var tail = ENTRY - dir * 7.5f;
        Show(tube, rig);
        if (rig)
        {
            tube.transform.localPosition = new Vector3((tail.x + tp.x) * 0.5f, (tail.y + tp.y) * 0.5f, -0.15f);
            tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            tube.transform.localScale = new Vector3(0.62f, (tp - tail).magnitude * 0.5f, 0.62f);
            var mm = ENTRY - dir * 4.2f;
            mast.transform.localPosition = new Vector3(mm.x, mm.y + 0.7f, 0.5f);
            mast.transform.localRotation = Quaternion.FromToRotation(Vector3.right, dir);
            mast.transform.localScale = new Vector3(9f, 0.5f, 0.6f);
        }
        float cav = PE(t, pa, pb) * (1f - PE(t, hc, hd));
        Show(cavity, cav > 0.01f && t < s13);
        if (cav > 0.01f) cavity.transform.localScale = new Vector3(5.2f * cav, 3.4f * cav, 1f);
        for (int i = 0; i < soil.Length; i++)
        {
            bool on = t >= pa && t < hc;
            Show(soil[i], on);
            if (!on) continue;
            float u = Frac((t - pa) * 0.5f + i / (float)soil.Length);
            var p = Vector3.Lerp(TIP, ENTRY, u);
            soil[i].transform.localPosition = new Vector3(p.x, p.y, -0.55f) + Vector3.up * 0.14f * Mathf.Sin(i * 2.4f + t * 9f);
        }
        float hp = 0.3f + 0.7f * PE(t, s13, Tw(13, "dirt", 0, true));
        hp *= PE(t, pa, pb);
        Show(heap, rig && hp > 0.01f);
        if (hp > 0.01f) heap.transform.localScale = new Vector3(13f * hp, 7f * hp, 3f * hp);

        // ------ arrows, bars, pills
        float tons = Tw(2, "fifteen"), found2 = Tw(2, "foundation"), soft2 = Tw(2, "soft");
        arWeight.Set(t >= tons && t < found2 - 0.1f, new Vector3(0.6f, 55f, -0.5f), 180f, Back(P(t, tons, tons + 0.25f)));
        Pill(labTons, t, tons + 0.1f, found2 - 0.1f, new Vector3(-16f, 34f, 0f));
        bool b10 = t >= found2 + 0.5f && t < soft2 - 0.05f;
        Show(barFound, b10);
        if (b10) PlaceBar(barFound, new Vector3(9.4f, 0f, -0.3f), new Vector3(9.4f, -3f, -0.3f), 0.2f);
        Pill(lab10, t, found2 + 0.55f, soft2 - 0.05f, new Vector3(9.8f, -1.5f, 0f));
        Pill(labClay, t, soft2 + 0.5f, s3, new Vector3(0f, -24f, 0f));

        arSouth.Set(t >= s3 + 0.4f && t < s4, new Vector3(12f, -4f, -0.5f), 180f, Back(P(t, s3 + 0.4f, s3 + 0.7f)));
        if (t >= s3 && t < s4) labYear.text = Mathf.RoundToInt(Mathf.Lerp(1178f, 1990f, P(t, s3 + 0.3f, En(3)))).ToString();
        Pill(labYear, t, s3 + 0.1f, s4 - 0.02f, new Vector3(-17f, 50f, 0f));

        arLead.Set(t >= a1 + 0.6f && t < s5, new Vector3(-11f, 13.2f, -0.5f), 180f, Back(P(t, a1 + 0.6f, a1 + 0.9f)));
        Pill(labLead, t, a1 + 0.3f, s5 - 0.02f, new Vector3(-11f, 15f, 0f));
        float stops = Tw(4, "stops");
        Pill(labStop, t, stops, s5 - 0.02f, new Vector3(0f, 22f, 0f));
        Pill(labUgly, t, Tw(5, "ugly"), Tw(5, "so"), new Vector3(-11f, 7.2f, 0f));
        Pill(labFrozen, t, fz + 0.2f, s7 - 0.02f, new Vector3(-8f, -12f, 0f));

        arWrong.Set(t >= lurch && t < s8, new Vector3(16f, 56f, -0.5f), -90f, Back(P(t, lurch, lurch + 0.25f)));
        Pill(labWrong, t, lurch + 0.1f, s8 - 0.02f, new Vector3(-10f, 44f, 0f));
        Pill(lab900, t, a2 + 0.25f, s9 - 0.02f, new Vector3(-11f, 15f, 0f));

        float opp = Tw(9, "opposite");
        arRight.Set(t >= s9 + 0.05f && t < s10, new Vector3(13f, 8f, -0.5f), 180f, Back(P(t, s9 + 0.05f, s9 + 0.35f)) * (1f - P(t, opp, opp + 0.3f)));
        arLeft.Set(t >= opp && t < s10, new Vector3(-15f, 8f, -0.5f), 180f, Back(P(t, opp, opp + 0.3f)));
        Pill(labSink, t, s9 + 0.1f, opp + 0.15f, new Vector3(13f, 12f, 0f));
        Pill(labHigh, t, opp + 0.05f, s10 - 0.02f, new Vector3(-15f, 12f, 0f));

        float dig10b = Tw(10, "dig");
        Pill(labDont, t, s10 + 0.3f, dig10b - 0.1f, new Vector3(8f, -8f, 0f));
        Pill(labDig, t, dig10b + 0.45f, s11 - 0.02f, new Vector3(-8f, -8f, 0f));

        arSettle.Set(t >= hd && t < Tw(12, "whole") - 0.05f, new Vector3(-6f, 2.6f, -0.5f), 180f, Back(P(t, hd, hd + 0.25f)));

        Pill(lab18, t, Tw(13, "eighteen") - 0.1f, s14 - 0.02f, new Vector3(5.2f, 44.5f, -8f));
        Pill(lab70, t, Tw(13, "Seventy"), Tw(13, "top") - 0.25f, new Vector3(-20f, 8.6f, 0f));
        Pill(lab200, t, Tw(14, "two"), Duration + 1f, new Vector3(3f, 68f, 0f));

        // ------ one night, then day again
        float night = PE(t, s7 - 0.25f, s7 + 0.1f) * (1f - PE(t, s8 - 0.1f, s8 + 0.25f));
        sun.intensity = WellLook.SUN_LUX * Mathf.Lerp(1f, 0.1f, night);
        sky.multiplier.value = WellLook.SKY_MULT * Mathf.Lerp(1f, 0.4f, night);
        var fc = frostBase * Mathf.Lerp(1f, 0.45f, night); fc.a = frostBase.a; frostMat.SetColor("_UnlitColor", fc);
        var dc = dustBase * Mathf.Lerp(1f, 0.3f, night); dc.a = dustBase.a; dustMat.SetColor("_UnlitColor", dc);

        UpdateCamera(t, shake);

        // where the top used to be: drawn in front of the tower, projected so it lines up with the tower's outline
        foreach (var g in ghost) Show(g, topShot);
        bool b18 = t >= Tw(13, "eighteen") - 0.3f && t < s14;
        Show(barTop, b18);
        if (topShot)
        {
            var c = cam.transform.position;
            Vector3 F(Vector3 w) { float k = (-9.5f - c.z) / (0f - c.z); return c + (w - c) * k; }
            var bR = F(OnTower(LEAN, 5.45f, 50f)); var tR = F(OnTower(LEAN, 5.45f, 58.4f)); var tL = F(OnTower(LEAN, -5.45f, 58.4f)); var bL = F(OnTower(LEAN, -5.45f, 50f));
            PlaceBar(ghost[0], bR, tR, 0.1f); PlaceBar(ghost[1], tL, tR, 0.1f); PlaceBar(ghost[2], bL, tL, 0.1f);
            if (b18) PlaceBar(barTop, F(OnTower(LEAN, 5.45f, 54.5f)), F(OnTower(lean, 5.45f, 54.5f)), 0.42f);
        }
    }
}
