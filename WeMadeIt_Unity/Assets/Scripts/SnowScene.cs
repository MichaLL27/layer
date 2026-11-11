// Short "a snow cave in a blizzard" (HDRP). Pure function of time; beat times from docs/scenes/snowcave_words.json.
// The drift is sliced like a cake along z = 0, the camera looks along +z. The hollow is two curves, ceiling and floor, per column.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class SnowScene : MonoBehaviour, IShortScene
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

    Transform stage;

    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    Material Mat(string key)
    {
        Material m;
        if (matCache.TryGetValue(key, out m)) return m;
        Material G(string hex, float smooth, float nits = 0f) { var q = WellLook.Lit(key, Hex(hex), smooth); return nits > 0f ? WellLook.Emissive(q, Hex(hex), nits) : q; }
        switch (key)
        {
            case "snow_parka": m = G("#B83A18", 0.42f, 140f); break;
            case "snow_pants": m = G("#2A3549", 0.3f, 90f); break;
            case "snow_boot": m = G("#2B2622", 0.3f); break;
            case "snow_fur": m = G("#D8CDB8", 0.1f); break;
            case "snow_pack": m = G("#2F6A75", 0.35f, 80f); break;
            case "snow_mat": m = G("#B9A560", 0.3f); break;
            case "snow_face": m = G("#C98F6E", 0.35f, 220f); break;
            case "bag_outer": m = G("#C23C18", 0.58f, 260f); break;
            case "bag_inner": m = G("#6E2A12", 0.3f, 120f); break;
            case "pine_needle": m = G("#1B3527", 0.12f); m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_DoubleSidedNormalMode", 1f); m.SetFloat("_CullMode", 0f); Keys(m); break;
            case "pine_snow": m = G("#EEF3F9", 0.2f); m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_DoubleSidedNormalMode", 1f); m.SetFloat("_CullMode", 0f); Keys(m); break;
            case "pine_trunk": m = G("#3A2C22", 0.2f); break;
            default: m = G("#B0B0B0", 0.4f); break;
        }
        matCache[key] = m;
        return m;
    }

    GameObject Model(string file, Vector3 pos, float yaw, float scale)
    {
        var src = Resources.Load<GameObject>("Models/" + file);
        if (src == null) { Debug.LogError("model missing: " + file); return new GameObject(file); }
        var holder = new GameObject(file + "_holder");
        holder.transform.SetParent(stage, false);
        holder.transform.localPosition = pos;
        holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        holder.transform.localScale = Vector3.one * scale;
        var g = Instantiate(src);
        g.name = file;
        g.transform.SetParent(holder.transform, false);
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = Mat(ms[i] != null ? ms[i].name.Replace(" (Instance)", "") : "");
            r.sharedMaterials = ms;
        }
        return holder;
    }

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

    // the moving storm behind everything
    static Texture2D StormTex()
    {
        const int N = 512;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        var c = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float g = 0.78f + 0.44f * Fbm(x / (float)N, y / (float)N, 5f, 4, 2f);
                c[y * N + x] = new Color(g, g, g, 1f);
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    // hollow walls scraped by hand: shallow scoops, each lit a little differently
    static Texture2D IceTex()
    {
        const int N = 512, CELLS = 14;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        var rnd = new System.Random(9);
        var px = new float[CELLS, CELLS]; var py = new float[CELLS, CELLS];
        for (int j = 0; j < CELLS; j++) for (int i = 0; i < CELLS; i++) { px[i, j] = (float)rnd.NextDouble(); py[i, j] = (float)rnd.NextDouble(); }
        var c = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N * CELLS, v = y / (float)N * CELLS, best = 9f;
                int iu = Mathf.FloorToInt(u), iv = Mathf.FloorToInt(v);
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        int a = (iu + di + CELLS) % CELLS, b = (iv + dj + CELLS) % CELLS;
                        float dx = iu + di + px[a, b] - u, dy = iv + dj + py[a, b] - v;
                        best = Mathf.Min(best, dx * dx + dy * dy);
                    }
                float g = 0.88f + 0.2f * Mathf.Clamp01(1f - Mathf.Sqrt(best) * 0.95f) + 0.02f * (float)rnd.NextDouble();
                c[y * N + x] = new Color(g, g, g, 1f);
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    // alpha falling away from the hollow's edge
    static Texture2D RimTex()
    {
        var t = new Texture2D(4, 64, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 64; y++) { float a = Mathf.Pow(1f - y / 63f, 2f); for (int x = 0; x < 4; x++) t.SetPixel(x, y, new Color(1f, 1f, 1f, a)); }
        t.Apply();
        return t;
    }

    GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        var col = g.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
        g.transform.SetParent(stage, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }
    GameObject[] Many(int n, PrimitiveType p, string name, Material m)
    {
        var a = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = Prim(p, name + i, Vector3.zero, Vector3.one, m);
            if (p == PrimitiveType.Quad) a[i].GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            a[i].SetActive(false);
        }
        return a;
    }
    GameObject MeshObj(string name, Mesh mesh, Material m)
    {
        var g = new GameObject(name);
        g.transform.SetParent(stage, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    // a two-column-per-x sheet: either vertical on the cut plane, or running back from it
    class Strip
    {
        readonly Mesh mesh; readonly Vector3[] v; readonly Vector2[] uvs;
        public Strip(SnowScene s, string name, int columns, Material m, Vector3? facing = null)
        {
            v = new Vector3[columns * 2]; uvs = new Vector2[columns * 2];
            var nrm = new Vector3[columns * 2]; var tan = new Vector4[columns * 2];
            for (int i = 0; i < columns * 2; i++) { nrm[i] = facing ?? Vector3.back; tan[i] = new Vector4(1, 0, 0, 1); }
            var tr = new int[(columns - 1) * 6];
            for (int i = 0, q = 0; i < columns - 1; i++)
            {
                int t0 = i * 2, b0 = i * 2 + 1, t1 = i * 2 + 2, b1 = i * 2 + 3;
                tr[q++] = b0; tr[q++] = t0; tr[q++] = b1;
                tr[q++] = t0; tr[q++] = t1; tr[q++] = b1;
            }
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = v; mesh.normals = nrm; mesh.tangents = tan; mesh.uv = new Vector2[columns * 2]; mesh.triangles = tr;
            s.MeshObj(name, mesh, m);
        }
        public void Set(int i, Vector3 a, Vector3 b, Vector2 ua = default, Vector2 ub = default) { v[i * 2] = a; v[i * 2 + 1] = b; uvs[i * 2] = ua; uvs[i * 2 + 1] = ub; }
        public void Commit() { mesh.vertices = v; mesh.uv = uvs; mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8000f); }
    }

    class Arrow
    {
        public readonly GameObject go;
        public Arrow(SnowScene s, string name, float L, float W, Material m)
        {
            float sw = W * 0.42f, hl = W * 1.1f;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-sw / 2, 0, 0), new Vector3(-sw / 2, L - hl, 0), new Vector3(-W / 2, L - hl, 0), new Vector3(0, L, 0), new Vector3(W / 2, L - hl, 0), new Vector3(sw / 2, L - hl, 0), new Vector3(sw / 2, 0, 0) };
            mesh.triangles = new[] { 0, 1, 5, 0, 5, 6, 2, 3, 4 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.uv = new Vector2[7];
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 500f);
            go = s.MeshObj(name, mesh, m);
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
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

    // ------------------------------------------------------------------ the drift and the hollow
    const float X0 = -0.6f, H0 = 5.6f, VENT = -0.6f, DEPTH = 2.4f;
    static float Base(float x) { float sg = x < X0 ? 6f : 3.3f, d = x - X0; return H0 * Mathf.Exp(-d * d / (2f * sg * sg)); }
    static float Wind(float x, float z)
    {
        float a = Mathf.PerlinNoise(x * 0.22f + 11.3f, z * 0.22f + 4.1f) - 0.5f;
        float b = Mathf.PerlinNoise(x * 0.9f + 3.7f, z * 2.4f + 8.8f) - 0.5f;
        float c = Mathf.PerlinNoise(x * 3.1f + 1.2f, z * 7f + 2.2f) - 0.5f;
        return a * 0.5f + b * 0.16f + c * 0.035f;
    }
    static float Surf(float x) => Base(x) + Wind(x, 0f);
    static float SurfZ(float x, float z) => Base(x) * Mathf.Exp(-z * z / (2f * 5.5f * 5.5f)) + Wind(x, z);

    float digK, morphK, domeK, ventK;

    static void Stage1(float x, float tipX, out float c, out float f)
    {
        f = 0.15f;
        if (x < tipX) { c = f; return; }
        c = f + 1.15f * Mathf.Sqrt(Mathf.Clamp01((x - tipX) / 0.7f));
    }

    static float Arch(float x) { float u = (x + 0.5f) / 2.6f; return 1.1f + 2.6f * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)); }
    static float Rough(float x)
    {
        float e = Mathf.Min(Mathf.Clamp01((x + 3.1f) / 0.5f), Mathf.Clamp01((2.1f - x) / 0.5f));
        return 1.1f + (1.9f + 0.42f * (Mathf.PerlinNoise(x * 1.7f + 2f, 3.3f) - 0.5f) + 0.2f * (Mathf.PerlinNoise(x * 5.3f, 7.7f) - 0.5f)) * Mathf.Sqrt(e);
    }

    void Stage2(float x, out float c, out float f)
    {
        if (x < -3.1f) { f = 1.1f; c = f; return; }
        if (x < 1f) f = 1.1f;
        else if (x < 1.7f) f = Mathf.Lerp(1.1f, -0.7f, E((x - 1f) / 0.7f));
        else if (x < 5.6f) f = -0.7f;
        else if (x < 7.4f) f = Mathf.Lerp(-0.7f, 0f, E((x - 5.6f) / 1.8f));
        else f = 0f;
        if (x < 2.1f) c = Mathf.Lerp(Rough(x), Arch(x), domeK);
        else if (x < 2.6f) c = Mathf.Lerp(1.1f, 0.45f, E((x - 2.1f) / 0.5f));
        else if (x < 7.4f) c = 0.45f;
        else c = 0f;
    }

    void Cave(float x, out float c, out float f)
    {
        float c1, f1, c2, f2;
        Stage1(x, Mathf.Lerp(7.5f, -1f, digK), out c1, out f1);
        Stage2(x, out c2, out f2);
        c = Mathf.Lerp(c1, c2, morphK); f = Mathf.Lerp(f1, f2, morphK);
        if (ventK > 0f && Mathf.Abs(x - VENT) < 0.13f) c = Mathf.Lerp(c, Surf(x), ventK);
    }

    float FloorAt(float x) { float c, f; Cave(x, out c, out f); return f; }
    float CeilAt(float x) { float c, f; Cave(x, out c, out f); return c; }

    // ------------------------------------------------------------------ objects
    Camera cam;
    Light sun; HDRISky sky; Fog fog;
    GameObject frontSnow, backdrop;
    Strip above, below, backWall, ceilRib, floorRib, rimC, rimF;
    Material rimMat, glowMat, stormMat;
    GameObject candle, flame, sitting;
    Light candleL, coldL;
    const float CANDLE_LM = 3000000f, COLD_LM = 1700000f;
    GameObject[] spin;

    Light PointL(string name, Vector3 pos, Color c, float range, bool shadows)
    {
        var g = new GameObject(name);
        g.transform.SetParent(stage, false);
        g.transform.localPosition = pos;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        g.AddComponent<HDAdditionalLightData>();
        l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        l.color = c;
        l.range = range;
        l.intensity = 0f;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return l;
    }

    // a ragged puff of mist
    static Texture2D WispTex()
    {
        const int N = 256;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var c = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (N - 1f), v = y / (N - 1f), dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                float n = Fbm(u, v, 4f, 4, 6f);
                float a = Mathf.Clamp01(r * r * (3f - 2f * r) * Mathf.Clamp01((n - 0.34f) * 3.2f));
                c[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    // the far mountains, seen when the storm has gone
    void BuildRange(Material m)
    {
        const int NX = 400, NZ = 80;
        var v = new Vector3[NX * NZ];
        var tr = new int[(NX - 1) * (NZ - 1) * 6];
        for (int i = 0; i < NX; i++)
            for (int j = 0; j < NZ; j++)
            {
                float x = -1100f + 2200f * i / (NX - 1f), z = 560f + 500f * j / (NZ - 1f);
                float a = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.0042f + 3f, z * 0.0042f + 9f) * 2f - 1f);
                float b = 1f - Mathf.Abs(Mathf.PerlinNoise(x * 0.013f + 7f, z * 0.013f + 1f) * 2f - 1f);
                float f = Mathf.Clamp01((z - 560f) / 180f);
                v[i * NZ + j] = new Vector3(x, 118f * f * Mathf.Pow(a, 2.2f) + 26f * f * Mathf.Pow(b, 2f), z);
            }
        for (int i = 0, q = 0; i < NX - 1; i++)
            for (int j = 0; j < NZ - 1; j++)
            {
                int a = i * NZ + j, b = a + 1, c = a + NZ, d = c + 1;
                tr[q++] = a; tr[q++] = b; tr[q++] = c; tr[q++] = b; tr[q++] = d; tr[q++] = c;
            }
        var mesh = new Mesh { name = "Range", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = v; mesh.triangles = tr; mesh.RecalculateNormals(); mesh.RecalculateTangents();
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8000f);
        MeshObj("Range", mesh, m);
    }
    float[] fx;
    Material caveMat;
    GameObject[] snow, chunks, cold, warm, co2, drops;
    GameObject bag, person, rod, glow, gearObj;
    Arrow arW1, arW2, arW3, arDown;
    Material coldMat, co2Mat;
    WellScene.Label lab40, labWind, labCold, labBed, labCold2, labWarm, labCO2, labOut, labIn, labYou;

    WellScene.Label Lab(string text, string style) { var l = new WellScene.Label { text = text, style = style }; labels.Add(l); return l; }
    void Pill(WellScene.Label l, float t, float a, float b, Vector3 pos)
    {
        l.pos = pos;
        l.k = (t < a || t >= b) ? 0f : Mathf.Min(Back(P(t, a, a + 0.22f)), 1f - P(t, b - 0.12f, b));
    }

    const float FOV = 34f;

    static Texture2D GrainTex()
    {
        const int N = 1024;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var c = new Color[N * N];
        var rnd = new System.Random(5);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float wv = v + 0.014f * Mathf.Sin(2f * Mathf.PI * (u * 2f + 0.3f));
                float beds = 0.5f + 0.5f * (0.6f * Mathf.Sin(2f * Mathf.PI * wv * 6f) + 0.4f * Mathf.Sin(2f * Mathf.PI * (wv * 17f + 0.2f)));
                float crust = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Frac(wv * 5f + 0.5f * Fbm(u, v, 6f, 2, 3f)) - 0.5f) * 40f), 2f);
                float g = (1f - 0.03f * beds - 0.04f * crust) * (0.98f + 0.04f * (float)rnd.NextDouble()) * (0.97f + 0.06f * Fbm(u, v, 9f, 3, 7f));
                c[y * N + x] = new Color(g, g, g, 1f);
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<WellScene.Doc>(File.ReadAllText(wordsJson));
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);
        WellLook.GradeContrast = 8f; WellLook.GradeSaturation = 10f; WellLook.GradeWarm = 0f;
        sun = WellLook.Sun(stage);
        var vol = WellLook.Environment(stage);
        vol.sharedProfile.TryGet(out fog);
        vol.sharedProfile.TryGet(out sky);
        fog.maximumHeight.value = 200f;
        cam = WellLook.Camera(stage, FOV);

        var top = WellLook.Metal("snowTop", Hex("#EEF3FA"), 0.32f, 0f, "sand_01", 2.6f, 0.55f);
        BuildField(top, "SnowBack", 0f, 90f);
        frontSnow = BuildField(top, "SnowFront", -90f, 0f);

        BuildRange(WellLook.Metal("range", Hex("#E8EDF4"), 0.2f, 0f, "rock_face", 40f, 1.3f));
        var grain = GrainTex();
        var section = WellLook.Lit("snowCut", Hex("#EEF2F8"), 0.1f);
        section.SetTexture("_BaseColorMap", grain);
        section.SetFloat("_UVBase", 5f); section.SetFloat("_TexWorldScale", 1f / 5f); section.SetFloat("_ObjectSpaceUVMapping", 0f);
        section.SetTexture("_NormalMap", Resources.Load<Texture2D>("PH/tex/sand_01/sand_01_nor_gl"));
        section.SetFloat("_NormalScale", 0.45f);
        Keys(section);
        var l = new List<float>();
        foreach (var v in new[] { -1500f, -800f, -400f, -200f, -100f, -60f, -30f, -15f, -8f }) l.Add(v);
        for (float x = -6f; x <= 12f; x += 0.03f) l.Add(x);
        foreach (var v in new[] { 15f, 30f, 60f, 100f, 200f, 400f, 800f, 1500f }) l.Add(v);
        fx = l.ToArray();
        above = new Strip(this, "Above", fx.Length, section);
        below = new Strip(this, "Below", fx.Length, section);
        caveMat = WellLook.Metal("cave", new Color(0.2f, 0.235f, 0.29f), 0.3f, 0f, "rock_wall_02", 1.3f, 1.25f);
        caveMat.SetFloat("_DoubleSidedNormalMode", 1f);
        caveMat.SetFloat("_DoubleSidedEnable", 1f); caveMat.SetFloat("_CullMode", 0f); Keys(caveMat);
        backWall = new Strip(this, "BackWall", fx.Length, caveMat);
        ceilRib = new Strip(this, "CeilRib", fx.Length, caveMat, Vector3.down);
        floorRib = new Strip(this, "FloorRib", fx.Length, caveMat, Vector3.up);
        rimMat = WellLook.Unlit("rim", new Color(0.5f, 0.68f, 0.95f, 0.75f), RimTex(), true);
        rimC = new Strip(this, "RimC", fx.Length, rimMat);
        rimF = new Strip(this, "RimF", fx.Length, rimMat);

        stormMat = WellLook.Unlit("backdrop", Hex("#AEBFD2"), StormTex());
        stormMat.SetTextureScale("_UnlitColorMap", new Vector2(22f, 10f));
        backdrop = Prim(PrimitiveType.Quad, "backdrop", new Vector3(0f, 200f, 170f), new Vector3(3000f, 1400f, 1f), stormMat);
        for (int i = 0; i < 46; i++)
            Model(i % 2 == 0 ? "pine_a" : "pine_b", new Vector3(-85f + Hash(i, 11) * 170f, 0f, 30f + Hash(i, 12) * 120f), Hash(i, 13) * 360f, 0.8f + Hash(i, 14) * 0.7f);
        var dot = WellLook.SoftDot();
        var flake = WellLook.Unlit("flake", new Color(1.3f, 1.3f, 1.35f, 0.6f), dot, true);
        snow = Many(760, PrimitiveType.Quad, "flake", flake);
        chunks = Many(20, PrimitiveType.Sphere, "chunk", WellLook.Lit("chunk", Hex("#EEF3FA"), 0.2f));
        var wisp = WispTex();
        var c0 = new Color(0.5f, 0.74f, 1.2f, 0.32f);
        coldMat = WellLook.Unlit("cold", c0, wisp, true);
        cold = Many(90, PrimitiveType.Quad, "cold", coldMat);
        var w0 = new Color(1.15f, 0.72f, 0.36f, 0.12f);
        warm = Many(20, PrimitiveType.Quad, "warm", WellLook.Unlit("warm", w0, wisp, true));
        var cc = new Color(0.72f, 0.72f, 0.76f, 0.5f);
        co2Mat = WellLook.Unlit("co2", cc, wisp, true);
        co2 = Many(54, PrimitiveType.Quad, "co2", co2Mat);
        spin = Many(70, PrimitiveType.Quad, "spin", WellLook.Unlit("spin", new Color(1.25f, 1.27f, 1.32f, 0.36f), wisp, true));
        candleL = PointL("candleLight", new Vector3(-2.68f, 1.62f, 1.4f), new Color(1f, 0.62f, 0.3f), 9f, true);
        coldL = PointL("coldLight", new Vector3(5.6f, 0.2f, 1.2f), new Color(0.46f, 0.66f, 1f), 11f, false);
        drops = Many(16, PrimitiveType.Sphere, "drop", WellLook.Emissive(WellLook.Lit("drop", Hex("#BFE6FF"), 0.9f), Hex("#BFE6FF"), 1800f));

        bag = Model("snow_bag", Vector3.zero, 180f, 1f);
        person = Model("snow_person", Vector3.zero, 0f, 1f);
        sitting = Model("snow_person_sit", Vector3.zero, -90f, 1f);
        gearObj = Model("snow_gear", new Vector3(0.62f, 1.1f, 1.25f), 200f, 1f);
        rod = Prim(PrimitiveType.Cylinder, "rod", Vector3.zero, new Vector3(0.05f, 0.9f, 0.05f), WellLook.Lit("rod", Hex("#7A5A36"), 0.3f));
        rod.SetActive(false);
        glowMat = WellLook.Unlit("glow", new Color(1.4f, 0.75f, 0.3f, 0.6f), dot, true);
        glow = Prim(PrimitiveType.Quad, "glow", new Vector3(-1.6f, 2.0f, DEPTH - 0.04f), new Vector3(7.5f, 6f, 1f), glowMat);
        glow.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        glow.SetActive(false);
        candle = Prim(PrimitiveType.Cylinder, "candle", new Vector3(-2.68f, 1.22f, 1.4f), new Vector3(0.1f, 0.12f, 0.1f), WellLook.Emissive(WellLook.Lit("candle", Hex("#F2E7C9"), 0.4f), Hex("#F2E7C9"), 3800f));
        var flameC = Hex("#FFC864") * 3f; flameC.a = 0.95f;
        flame = Prim(PrimitiveType.Quad, "flame", Vector3.zero, Vector3.one, WellLook.Unlit("flame", flameC, dot, true));
        flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        candle.SetActive(false); flame.SetActive(false);

        var white = WellLook.Unlit("arrowW", Color.white * 2f);
        var blue = WellLook.Unlit("arrowB", Hex("#3C9BFF") * 1.3f);
        arW1 = new Arrow(this, "w1", 7f, 1.9f, white);
        arW2 = new Arrow(this, "w2", 6f, 1.6f, white);
        arW3 = new Arrow(this, "w3", 7f, 1.9f, white);
        arDown = new Arrow(this, "down", 2.2f, 1.1f, blue);

        lab40 = Lab("-40°F", "o");
        labWind = Lab("WIND", "w");
        labCold = Lab("COLD AIR", "b");
        labBed = Lab("YOUR BED", "y");
        labCold2 = Lab("COLD", "b");
        labWarm = Lab("WARM", "o");
        labCO2 = Lab("CO2", "w");
        labOut = Lab("-40°F", "b");
        labIn = Lab("32°F", "o");
        labYou = Lab("YOU", "w");
    }

    GameObject BuildField(Material m, string name, float z0, float z1)
    {
        const int NX = 560, NZ = 190;
        var v = new Vector3[NX * NZ];
        var tr = new int[(NX - 1) * (NZ - 1) * 6];
        float XS(int i) { float u = i / (NX - 1f) * 2f - 1f; return 2f + Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), 2.4f) * 400f; }
        float ZS(int j) { float u = j / (NZ - 1f); u = Mathf.Pow(u, 2.4f); return z0 < 0f ? z1 + (z0 * 6f - z1) * u : z0 + z1 * 6f * u; }
        for (int i = 0; i < NX; i++)
            for (int j = 0; j < NZ; j++)
            {
                float x = XS(i), z = ZS(j);
                v[i * NZ + j] = new Vector3(x, SurfZ(x, z), z);
            }
        for (int i = 0, q = 0; i < NX - 1; i++)
            for (int j = 0; j < NZ - 1; j++)
            {
                int a = i * NZ + j, b = a + 1, c = a + NZ, d = c + 1;
                if (z0 < 0f) { tr[q++] = a; tr[q++] = c; tr[q++] = b; tr[q++] = b; tr[q++] = c; tr[q++] = d; }
                else { tr[q++] = a; tr[q++] = b; tr[q++] = c; tr[q++] = b; tr[q++] = d; tr[q++] = c; }
            }
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.vertices = v; mesh.triangles = tr; mesh.RecalculateNormals(); mesh.RecalculateTangents();
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8000f);
        return MeshObj(name, mesh, m);
    }

    void SetCave()
    {
        for (int i = 0; i < fx.Length; i++)
        {
            float x = fx[i], c, f, s = Surf(x);
            Cave(x, out c, out f);
            float cc = Mathf.Min(c, s);
            above.Set(i, new Vector3(x, s, 0.0f), new Vector3(x, cc, 0.0f));
            below.Set(i, new Vector3(x, Mathf.Min(f, s), 0.0f), new Vector3(x, -600f, 0.0f));
            float ff = Mathf.Min(f, s);
            float top = Mathf.Max(cc, ff), uu = x / 3.2f;
            backWall.Set(i, new Vector3(x, top, DEPTH), new Vector3(x, ff, DEPTH), new Vector2(uu, top / 3.2f), new Vector2(uu, ff / 3.2f));
            ceilRib.Set(i, new Vector3(x, top, 0f), new Vector3(x, top, DEPTH), new Vector2(uu, 0f), new Vector2(uu, 1.1f));
            floorRib.Set(i, new Vector3(x, ff, DEPTH), new Vector3(x, ff, 0f), new Vector2(uu, 1.1f), new Vector2(uu, 0f));
            bool hollow = top - ff > 0.03f && Mathf.Abs(x - VENT) > 0.15f + (ventK > 0f ? 0f : -1f);
            float wc = hollow && c < s - 0.05f ? 0.42f : 0f, wf = hollow ? 0.3f : 0f;
            rimC.Set(i, new Vector3(x, top, -0.006f), new Vector3(x, top + wc, -0.006f), new Vector2(uu, 0f), new Vector2(uu, 1f));
            rimF.Set(i, new Vector3(x, ff, -0.006f), new Vector3(x, ff - wf, -0.006f), new Vector2(uu, 0f), new Vector2(uu, 1f));
        }
        above.Commit(); below.Commit(); backWall.Commit(); ceilRib.Commit(); floorRib.Commit(); rimC.Commit(); rimF.Commit();
    }

    // ------------------------------------------------------------------ the camera
    class Shot { public float t; public Vector3 pos, look; public Shot(float t, Vector3 p, Vector3 l) { this.t = t; pos = p; look = l; } }
    static Vector3 Cp(float x, float y, float h) => new Vector3(x, y, -h * 0.5f / Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad));
    Shot W(float t, float x, float y, float h) => new Shot(t, Cp(x, y, h), new Vector3(x, y, 0f));
    Shot[] shots;

    void MakeShots()
    {
        const float C = 0.02f;
        shots = new[]
        {
            new Shot(0f, new Vector3(5.6f, 1.15f, -23f), new Vector3(3.2f, 2.5f, 0f)),
            new Shot(S(3) - C, new Vector3(5.5f, 1.1f, -19f), new Vector3(3.6f, 2.3f, 0f)),
            W(S(3), 2.6f, 2.3f, 19f),
            W(S(4) - C, 2.6f, 2.3f, 18f),
            W(S(4), 2.4f, 1.0f, 14f),
            W(S(5) - C, 2.4f, 1.0f, 13.5f),
            W(S(5), 2.0f, 1.9f, 17f),
            W(S(6) - C, 2.0f, 1.9f, 16.5f),
            W(S(6), 2.0f, 1.3f, 15.5f),
            W(S(7) - C, 2.0f, 1.3f, 15f),
            W(S(7), -0.7f, 2.3f, 8.8f),
            W(S(9) - C, -0.7f, 2.3f, 8.4f),
            W(S(9), -0.9f, 2.0f, 7.4f),
            W(S(10) - C, -0.9f, 2.0f, 5.8f),
            W(S(10), -0.7f, 2.4f, 8.8f),
            W(S(11) - C, -0.7f, 2.4f, 8.4f),
            W(S(11), -0.5f, 4.7f, 11f),
            W(S(12) - C, -0.5f, 4.7f, 10.5f),
            W(S(12), 2.4f, 3.6f, 21f),
            W(S(13) - C, 2.4f, 3.6f, 20f),
            new Shot(S(13), new Vector3(-5f, 2f, -21f), new Vector3(0f, 2.8f, 0f)),
            new Shot(Duration + 1f, new Vector3(-9f, 3.4f, -30f), new Vector3(0f, 3.4f, 0f)),
        };
    }

    void UpdateCamera(float t)
    {
        if (shots == null) MakeShots();
        int i = 0;
        while (i < shots.Length - 2 && t >= shots[i + 1].t) i++;
        float u = PE(t, shots[i].t, shots[i + 1].t);
        var pos = Vector3.Lerp(shots[i].pos, shots[i + 1].pos, u);
        var look = Vector3.Lerp(shots[i].look, shots[i + 1].look, u);
        if (t < S(3))
        {
            pos += new Vector3(Mathf.Sin(t * 13f) + Mathf.Sin(t * 7f) * 0.6f, Mathf.Sin(t * 11f + 1f), 0f) * 0.035f;
        }
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
    }

    // ------------------------------------------------------------------ the story
    void Sprite(GameObject g, bool on, Vector3 p, float sx, float sy)
    {
        Show(g, on);
        if (!on) return;
        g.transform.localPosition = p;
        g.transform.localScale = new Vector3(sx, sy, 1f);
    }

    public void Evaluate(float t)
    {
        foreach (var l in labels) l.k = 0f;
        float s3 = S(3), s4 = S(4), s5 = S(5), s6 = S(6), s7 = S(7), s8 = S(8), s9 = S(9), s10 = S(10), s11 = S(11), s12 = S(12), s13 = S(13);
        bool cut = t >= s3 && t < s13;
        Show(frontSnow, !cut);

        // ------ weather: a whiteout outside, thin haze for the cut shots, a clear morning at the end
        float dawn = PE(t, s13 - 0.1f, s13 + 0.25f);
        fog.meanFreePath.value = dawn > 0.5f ? 1400f : (cut ? 190f : 55f);
        stormMat.SetTextureOffset("_UnlitColorMap", new Vector2(t * 0.55f, t * 0.05f));
        // overcast in the storm: no hard sun; a low golden sun at dawn
        sun.intensity = WellLook.SUN_LUX * Mathf.Lerp(0.12f, 0.62f, dawn);
        sun.colorTemperature = Mathf.Lerp(6500f, 3300f, dawn);
        sun.transform.rotation = dawn > 0.5f ? Quaternion.Euler(13f, 58f, 0f) : Quaternion.Euler(WellLook.SUN_ELEV, WellLook.SUN_YAW, 0f);
        sky.multiplier.value = WellLook.SKY_MULT * Mathf.Lerp(2.0f, 0.55f, dawn);
        Show(backdrop, dawn < 0.5f);

        // ------ the hollow
        float dg0 = Tw(3, "dig"), dg1 = Tw(3, "get") - 0.05f;
        digK = PE(t, dg0, dg1);
        morphK = PE(t, Tw(5, "dig"), Tw(5, "high", 0, true));
        domeK = PE(t, Tw(8, "carve"), Tw(8, "dome", 0, true));
        float vA = Tw(11, "poke"), vB = Tw(11, "hole", 0, true);
        ventK = P(t, vA, vB); ventK = ventK * ventK;
        SetCave();

        // the inside: blue and cold until the bed is up, then warm; dim when the air goes bad
        float warmK = PE(t, s6 + 0.8f, s7);
        float cool = PE(t, s4, s4 + 0.4f) * (1f - warmK);
        float bad = PE(t, Tw(9, "sleepy"), s10 + 0.8f) * (1f - PE(t, vB, vB + 1.2f));
        var cCold = Hex("#6F8DB8"); var cIce = Hex("#5B86C4"); var cWarm = Hex("#7B6865");
        var col = Color.Lerp(Color.Lerp(cCold, cIce, cool), cWarm, warmK) * Mathf.Lerp(1.1f, 0.5f, bad);
        col.a = 1f;
        float flick = 1f + 0.09f * Mathf.Sin(t * 19f) + 0.06f * Mathf.Sin(t * 31f + 1f);
        candleL.intensity = CANDLE_LM * warmK * flick * Mathf.Lerp(1f, 0.32f, bad);
        candleL.enabled = warmK > 0.01f && t < s13;
        coldL.intensity = COLD_LM * (0.25f + 0.75f * cool) * (1f - warmK);
        coldL.enabled = t >= s3 && t < s7 + 0.3f;
        var rc = Color.Lerp(new Color(0.5f, 0.68f, 0.95f, 0.75f), new Color(1.0f, 0.7f, 0.42f, 0.6f), warmK); rimMat.SetColor("_UnlitColor", rc);
        float gl = 0.3f * warmK * (1f - 0.6f * bad);
        Show(glow, gl > 0.01f && t < s13);
        glowMat.SetColor("_UnlitColor", new Color(1.4f, 0.75f, 0.3f, 0.36f * gl));
        bool lit = warmK > 0.01f && t < s13;
        Show(candle, lit); Show(flame, lit);
        if (lit)
        {
            float fl = 1f + 0.12f * Mathf.Sin(t * 23f) + 0.08f * Mathf.Sin(t * 37f + 1f);
            flame.transform.localPosition = new Vector3(-2.68f, 1.48f, 1.39f);
            flame.transform.localScale = new Vector3(0.16f * fl, 0.32f * fl, 1f);
        }

        // ------ the person
        bool standing = t < s3 || dawn > 0.5f;
        Show(person, standing);
        if (standing)
        {
            float lean = t < s3 ? 8f + 3f * Mathf.Sin(t * 3.1f) : 0f;
            float bx = t < s3 ? 5f : -2.6f, bz = t < s3 ? -11f : -1.2f;
            person.transform.localPosition = new Vector3(bx, SurfZ(bx, bz), bz);
            person.transform.localRotation = Quaternion.Euler(0f, t < s3 ? 25f : -20f, lean);
        }
        float build = Tw(5, "build");
        float sitA = Tw(3, "get") - 0.05f;
        bool sits = t >= sitA && t < build;
        Show(sitting, sits);
        if (sits)
        {
            float slide = PE(t, sitA, sitA + 0.45f);
            float shiver = Mathf.Sin(t * 41f) * 0.006f;
            sitting.transform.localPosition = new Vector3(Mathf.Lerp(2.4f, 0.1f, slide) + shiver, FloorAt(0.1f), 1.2f);
        }
        bool inside = t >= build && t < s13;
        Show(bag, inside); Show(gearObj, inside);
        if (inside)
        {
            float pop = Back(P(t, build, build + 0.25f));
            bag.transform.localPosition = new Vector3(-1.1f, 1.38f, 1.1f);
            bag.transform.localScale = new Vector3(pop, pop * (1f + 0.03f * Mathf.Sin(t * 2.2f)), pop);
            gearObj.transform.localScale = Vector3.one * pop;
        }
        Pill(labYou, t, Tw(5, "build"), s6 - 0.02f, new Vector3(-1.1f, 2.5f, 0f));

        // ------ flying snow
        float windOn = 1f - dawn;
        for (int i = 0; i < snow.Length; i++)
        {
            float speed = 15f + Hash(i, 1) * 12f;
            float xr = t < s3 ? 7f : 30f;
            float cx = cam.transform.position.x;
            float x = cx + xr - Frac(t * speed / (2f * xr) + Hash(i, 2)) * 2f * xr;
            float y = (t < s3 ? -1f + Hash(i, 3) * 10f : -2f + Hash(i, 3) * 18f) + 0.4f * Mathf.Sin(t * 3f + i);
            float z = cut ? -0.6f - Hash(i, 4) * 4f : -3f - Hash(i, 4) * 12f;
            if (t < s3) z = -17f + Hash(i, 4) * 9f;
            if (i % 3 == 0) y = SurfZ(x, cut ? 0f : Mathf.Min(z, 0f)) + 0.08f + Hash(i, 6) * 0.5f;
            bool on = windOn > 0.5f && y > SurfZ(x, cut ? 0f : Mathf.Max(z, 0f)) + 0.05f;
            float w = cut ? 0.9f : 0.6f + Hash(i, 5) * 0.55f;
            Sprite(snow[i], on, new Vector3(x, y, z), w, cut ? 0.04f : 0.022f);
        }

        // ------ snow torn off the crest of the drift
        for (int i = 0; i < spin.Length; i++)
        {
            float u = Frac(t * (0.8f + Hash(i, 1) * 0.7f) + Hash(i, 2));
            float x0 = -2.5f + Hash(i, 3) * 9f;
            float x = x0 - u * 8f;
            float y = Mathf.Max(Surf(x0), 0.1f) + 0.15f + u * u * (1.2f + Hash(i, 4) * 2f);
            float z = cut ? -0.3f - i * 0.002f : -2f - Hash(i, 5) * 5f;
            float a0 = u * (1f - u) * 4f;
            float sz = (0.9f + u * 2.6f) * Mathf.Min(1f, a0 * 1.6f);
            bool spinOn = windOn > 0.5f && (!cut || (y > Surf(x) + 0.35f && i % 2 == 0));
            Sprite(spin[i], spinOn, new Vector3(x, y, z), sz * 1.5f, sz * 0.7f);
            if (spinOn) spin[i].transform.localRotation = Quaternion.Euler(0f, 0f, Hash(i, 6) * 360f);
        }

        // ------ snow thrown out of the hole
        for (int i = 0; i < chunks.Length; i++)
        {
            float st = dg0 + i * (dg1 - dg0) / chunks.Length, u = (t - st);
            bool on = u > 0f && u < 0.7f;
            Show(chunks[i], on);
            if (!on) continue;
            float tip = Mathf.Lerp(7.5f, -1f, digK);
            float vx = 5f + Hash(i, 1) * 5f;
            chunks[i].transform.localPosition = new Vector3(Mathf.Min(tip + 0.5f + vx * u, 12f), 0.8f + (4.5f + Hash(i, 2) * 2f) * u - 9f * u * u, -0.4f);
            chunks[i].transform.localScale = Vector3.one * (0.12f + Hash(i, 3) * 0.2f);
        }

        // ------ cold air: pours along the floor, then lies in the tunnel
        float cOn = PE(t, Tw(4, "cold"), Tw(4, "door", 0, true));
        float settle = PE(t, Tw(5, "dig"), Tw(6, "stays", 0, true));
        bool coldVisible = t >= Tw(4, "cold") && t < s7;
        for (int i = 0; i < cold.Length; i++)
        {
            bool on = coldVisible && i / (float)cold.Length < cOn + settle;
            Vector3 p = Vector3.zero;
            if (on)
            {
                float u = Frac(t * 0.42f + Hash(i, 1));
                var run = new Vector3(Mathf.Lerp(8f, -0.8f, u), FloorAt(Mathf.Lerp(8f, -0.8f, u)) + 0.3f + Hash(i, 2) * 0.55f, 1.0f);
                float tx = 2.1f + Hash(i, 3) * 4.6f + 0.25f * Mathf.Sin(t * 1.3f + i);
                var lie = new Vector3(tx, -0.62f + Hash(i, 4) * 0.8f, 1.0f);
                p = Vector3.Lerp(run, lie, settle);
                p.z = -0.03f - i * 0.0008f;
            }
            float sz = 1.3f + Hash(i, 5) * 1.0f;
            Sprite(cold[i], on, p, sz, sz * 0.7f);
            if (on) cold[i].transform.localRotation = Quaternion.Euler(0f, 0f, Hash(i, 6) * 360f + t * 18f * (Hash(i, 7) - 0.5f));
        }
        float wA = Tw(6, "sinks"), wB = s7;
        for (int i = 0; i < warm.Length; i++)
        {
            bool on = t >= wA && t < s13;
            float a = t * 0.25f + Hash(i, 1) * 6.28f;
            var p = new Vector3(-0.7f + Mathf.Cos(a) * (0.6f + Hash(i, 2) * 1.2f), 2.4f + Mathf.Sin(a * 1.3f) * 0.55f, DEPTH - 0.1f - i * 0.0008f);
            float sz = (1.4f + Hash(i, 3)) * PE(t, wA, wB) * (1f - bad * 0.6f);
            Sprite(warm[i], on && sz > 0.05f, p, sz, sz * 0.8f);
        }

        // ------ drops: fall on you, then run down the dome
        float dA = Tw(7, "melts"), dB = En(8) + 0.3f, cv = Tw(8, "carve");
        for (int i = 0; i < drops.Length; i++)
        {
            bool on = t >= dA + i * 0.09f && t < dB;
            Show(drops[i], on);
            if (!on) continue;
            float x0 = -2.2f + Hash(i, 1) * 3.2f;
            float y;
            float x = x0;
            if (t < cv)
            {
                float top = CeilAt(x0), bot = 1.75f;
                float u = Frac((t - dA) * (0.55f + Hash(i, 2) * 0.5f) + Hash(i, 3));
                y = Mathf.Lerp(top - 0.06f, bot, u * u);
            }
            else
            {
                float u = Frac((t - cv) * 0.5f + Hash(i, 3));
                float wall = x0 < -0.5f ? -2.98f : 1.9f;
                x = Mathf.Lerp(x0, wall, u * u);
                y = Mathf.Max(CeilAt(x) - 0.07f, 1.15f);
            }
            drops[i].transform.localPosition = new Vector3(x, y, -0.05f);
            drops[i].transform.localScale = new Vector3(0.09f, 0.13f, 0.09f);
        }

        // ------ the bad air, then the vent
        float exh = PE(t, Tw(10, "carbon"), s11 - 0.1f);
        float outK = PE(t, vB - 0.05f, vB + 1.6f);
        for (int i = 0; i < co2.Length; i++)
        {
            bool on = t >= Tw(10, "carbon") && t < s12 && i / (float)co2.Length < exh;
            Vector3 p = Vector3.zero;
            float sz = 1.1f + Hash(i, 2) * 0.8f;
            if (on)
            {
                float a = t * 0.3f + Hash(i, 1) * 6.28f;
                p = new Vector3(-0.8f + Mathf.Cos(a) * 1.8f * Hash(i, 3), 2.1f + Mathf.Sin(a * 0.8f) * 0.8f * Hash(i, 4), -0.06f - i * 0.0009f);
                float u = Frac((t - vB) * 0.8f + Hash(i, 5));
                if (outK > 0f) { var up = new Vector3(VENT, Mathf.Lerp(3.4f, 7.6f, u), -0.06f - i * 0.0009f); p = Vector3.Lerp(p, up, outK); sz = Mathf.Lerp(sz, 0.55f, outK); }
            }
            Sprite(co2[i], on, p, sz * 1.4f, sz * 1.4f);
            if (on) co2[i].transform.localRotation = Quaternion.Euler(0f, 0f, Hash(i, 6) * 360f + t * 22f * (Hash(i, 7) - 0.5f));
        }
        bool rodOn = t >= vA - 0.2f && t < vB + 0.4f;
        Show(rod, rodOn);
        if (rodOn)
        {
            float u = P(t, vA - 0.2f, vB) ;
            float tipY = Mathf.Lerp(2.6f, Surf(VENT) + 0.3f, u * u) - 1.8f * P(t, vB, vB + 0.4f);
            rod.transform.localPosition = new Vector3(VENT, tipY - 0.9f, -0.08f);
        }

        // ------ wind arrows, pills
        float wnd = Tw(2, "wind");
        for (int k = 0; k < 3; k++)
        {
            var ar = k == 0 ? arW1 : k == 1 ? arW2 : arW3;
            float x = 7.6f - 3f * P(t, wnd + k * 0.08f, wnd + k * 0.08f + 1.2f);
            ar.Set(t >= wnd + k * 0.08f && t < s3, new Vector3(x, 1.4f + k * 1.25f, -9f), 90f, 0.36f * Back(P(t, wnd + k * 0.08f, wnd + k * 0.08f + 0.2f)));
        }
        Pill(lab40, t, -1f, s2Hold(), new Vector3(3.9f, 4.2f, -9f));
        Pill(labWind, t, wnd + 0.15f, s3 - 0.02f, new Vector3(4.4f, 4.5f, -9f));
        Pill(labCold, t, Tw(4, "cold") + 0.1f, s5 - 0.02f, new Vector3(3.5f, 3.0f, 0f));
        Pill(labBed, t, Tw(5, "bed"), s6 - 0.02f, new Vector3(-1.1f, 3.6f, 0f));
        Pill(labCold2, t, Tw(6, "sinks"), s7 - 0.02f, new Vector3(4.2f, -1.25f, 0f));
        Pill(labWarm, t, Tw(6, "stays"), s7 - 0.02f, new Vector3(-0.7f, 4.4f, 0f));
        arDown.Set(t >= Tw(6, "sinks") && t < s7, new Vector3(3.0f, 3.1f, -0.3f), 180f, Back(P(t, Tw(6, "sinks"), Tw(6, "sinks") + 0.25f)));
        Pill(labCO2, t, Tw(10, "carbon") + 0.1f, Tw(11, "hole"), new Vector3(-0.7f, 4.35f, 0f));
        Pill(labOut, t, s12 + 0.05f, s13 - 0.02f, new Vector3(3f, 8.8f, 0f));
        Pill(labIn, t, Tw(12, "In"), s13 - 0.02f, new Vector3(-0.8f, 2.6f, -0.1f));

        UpdateCamera(t);
    }

    float s2Hold() => Tw(2, "wind") + 0.1f;
}
