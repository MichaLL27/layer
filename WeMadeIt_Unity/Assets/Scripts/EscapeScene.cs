// Short "the Great Escape tunnel" (HDRP). Pure function of time; beat times from docs/scenes/escape_words.json.
// The camp is sliced along z = 0, the camera looks along +z. Hut 104 stands at x -6..6 (front open), the shaft drops
// from under its stove (x = 2) to y = -9, the tunnel runs along +x toward the wire (x = 24 / 27).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class EscapeScene : MonoBehaviour, IShortScene
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

    // ------------------------------------------------------------------ materials
    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    Material Mat(string key)
    {
        Material m;
        if (matCache.TryGetValue(key, out m)) return m;
        Material G(string hex, float smooth) => WellLook.Lit(key, Hex(hex), smooth);
        switch (key)
        {
            case "hut_wall": m = WellLook.Textured("brown_planks_03", Hex("#8C7258"), 1.6f, 0.3f, true); break;
            case "hut_floor": m = WellLook.Textured("brown_planks_03", Hex("#A88A68"), 1.3f, 0.3f, true); break;
            case "hut_batten": m = WellLook.Textured("brown_planks_03", Hex("#6E5844"), 1.0f, 0.3f, true); break;
            case "hut_stilt": m = WellLook.Textured("brown_planks_03", Hex("#5E4A39"), 1.0f, 0.25f, true); break;
            case "hut_trim": m = WellLook.Textured("brown_planks_03", Hex("#7A634C"), 1.0f, 0.3f, true); break;
            case "hut_roof": m = WellLook.Metal(key, Hex("#2C2D2F"), 0.32f, 0f, "rock_wall_02", 1.4f, 0.35f); break;
            case "hut_lap": m = G("#232426", 0.35f); break;
            case "hut_glass": m = WellLook.Emissive(G("#5F6E78", 0.92f), Hex("#C9D6DE"), 900f); break;
            case "brick": m = WellLook.Metal(key, Hex("#8E4A36"), 0.25f, 0f, "rock_wall_02", 0.6f, 0.4f); break;
            case "mortar": m = G("#5E5048", 0.2f); break;
            case "tile": m = G("#B8B0A2", 0.55f); break;
            case "iron": m = WellLook.Metal(key, Hex("#2B2B2D"), 0.42f, 0.7f, "rusty_metal_02", 0.5f, 0.4f); break;
            case "iron_dark": m = G("#161616", 0.3f); break;
            case "bunk_wood": m = WellLook.Textured("brown_planks_03", Hex("#C29A6E"), 1.0f, 0.3f, true); break;
            case "tower_wood": m = WellLook.Textured("brown_planks_03", Hex("#6B5642"), 1.2f, 0.25f, true); break;
            case "tower_floor": m = WellLook.Textured("brown_planks_03", Hex("#5A4836"), 1.2f, 0.25f, true); break;
            case "tower_roof": m = WellLook.Metal(key, Hex("#2E3030"), 0.3f, 0f, "rock_wall_02", 1.2f, 0.3f); break;
            case "lamp_metal": m = WellLook.Metal(key, Hex("#3A3D40"), 0.5f, 0.7f); break;
            case "lamp_glass": m = WellLook.Emissive(G("#E8E4D6", 0.9f), Hex("#FFF1D0"), 2500f); break;
            case "post_wood": m = WellLook.Textured("brown_planks_03", Hex("#6A5946"), 1.0f, 0.25f, true); break;
            case "wire": m = WellLook.Metal(key, Hex("#6E6A64"), 0.45f, 0.8f); break;
            case "pine_needle": m = G("#1E3324", 0.12f); m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_DoubleSidedNormalMode", 1f); m.SetFloat("_CullMode", 0f); Keys(m); break;
            case "pine_snow": m = G("#26402C", 0.12f); m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_DoubleSidedNormalMode", 1f); m.SetFloat("_CullMode", 0f); Keys(m); break;
            case "pine_trunk": m = G("#4A3528", 0.2f); break;
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

    readonly Dictionary<string, GameObject> phSrc = new Dictionary<string, GameObject>();
    GameObject PH(string id, Vector3 pos, Vector3 euler, float scale)
    {
        GameObject src;
        if (!phSrc.TryGetValue(id, out src)) { src = Resources.Load<GameObject>($"PH/model/{id}/{id}_1k"); phSrc[id] = src; }
        if (src == null) { Debug.LogError("PH model missing: " + id); return new GameObject(id); }
        var holder = new GameObject(id).transform;
        holder.SetParent(stage, false);
        var g = Instantiate(src);
        g.transform.SetParent(holder, false);
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = WellLook.PHMaterial(id, ms[i] != null ? ms[i].name.Replace(" (Instance)", "") : "");
            r.sharedMaterials = ms;
        }
        holder.localPosition = pos; holder.localRotation = Quaternion.Euler(euler); holder.localScale = Vector3.one * scale;
        return holder.gameObject;
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
    void Sprite(GameObject g, bool on, Vector3 p, float sx, float sy)
    {
        Show(g, on);
        if (!on) return;
        g.transform.localPosition = p;
        g.transform.localScale = new Vector3(sx, sy, 1f);
    }

    class Arrow
    {
        public readonly GameObject go;
        public Arrow(EscapeScene s, string name, float L, float W, Material m)
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

    // bright yellow sand seen in section: fine grain, faint wavy beds, no big blotches
    static Texture2D SandFaceTex()
    {
        const int N = 1024;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var c = new Color[N * N];
        Color pale = Hex("#F0CB6A"), deep = Hex("#C98F3A");
        var rnd = new System.Random(11);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float wv = v + 0.010f * Mathf.Sin(2f * Mathf.PI * (u * 2f + 0.3f)) + 0.005f * Mathf.Sin(2f * Mathf.PI * (u * 5f + v * 2f));
                float beds = 0.5f + 0.5f * (0.5f * Mathf.Sin(2f * Mathf.PI * wv * 4f) + 0.3f * Mathf.Sin(2f * Mathf.PI * (wv * 11f + 0.2f)) + 0.2f * Mathf.Sin(2f * Mathf.PI * (wv * 29f + 0.6f)));
                float grain = (float)rnd.NextDouble();
                var col = Color.Lerp(pale, deep, 0.6f * beds * beds);
                col *= 0.9f + 0.2f * grain;
                col.a = 1f;
                c[y * N + x] = col;
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    static Texture2D RingTex()
    {
        const int N = 256;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var c = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.93f) / 0.05f);
                c[y * N + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    WellScene.Label Lab(string text, string style) { var l = new WellScene.Label { text = text, style = style }; labels.Add(l); return l; }
    void Pill(WellScene.Label l, float t, float a, float b, Vector3 pos)
    {
        if (t < a || t >= b) return;
        l.pos = pos;
        l.k = Mathf.Min(Back(P(t, a, a + 0.22f)), 1f - P(t, b - 0.12f, b));
    }

    // ------------------------------------------------------------------ layout
    const float FLOOR = 0.62f;                       // hut floor top
    const float SX = 2f, SH0 = 1.62f, SH1 = 2.38f;   // the shaft under the stove
    const float SZ = 0.88f;                          // shaft and tunnel run this far into the picture
    const float DEEP = -9.0f, TH = 0.85f;            // tunnel floor and height
    const float FENCE_A = 24f, FENCE_B = 27f, TOWER_X = 22.4f, TOWER_Z = 7f;
    const float HOLE_X = 17.6f, SHALLOW = -1.7f;     // the first, shallow try (heard by the microphones)
    static readonly float[] MIC_X = { 23.2f, 25.6f };
    const float MIC_Y = -1.6f;

    Camera cam;
    Light sun;
    CutGround ground;
    readonly List<Bounds> cuts = new List<Bounds>();
    GameObject frontGround, stove, trapdoor;
    readonly List<GameObject> frontOnly = new List<GameObject>();
    GameObject[] slats, shores, grains, rings, mics, micGlow;
    GameObject pile1, pile2, ladder, lamp, flame, lampFlat;
    Light lampL, shaftL, hutL, searchL;
    HDRISky sky;
    const float SUN_K = 0.55f, SKY_K = 0.5f;          // evening: a low warm sun from the front-left, a dimmer sky
    Material ringMat, micGlowMat;
    Human you;
    Arrow arUnder;
    WellScene.Label lab1944, labMic, lab30, labSand, labNoAir;

    const float FOV = 34f;

    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<WellScene.Doc>(File.ReadAllText(wordsJson));
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);
        WellLook.GradeContrast = 10f; WellLook.GradeSaturation = 6f; WellLook.GradeWarm = 6f;
        sun = WellLook.Sun(stage);
        var vol = WellLook.Environment(stage);
        cam = WellLook.Camera(stage, FOV);
        // v3: late evening. A low warm sun from behind the camp (the cut face is not sunlit), a dim sky:
        // the lamps, the hut's light and the searchlight carry the picture
        sun.transform.rotation = Quaternion.Euler(15f, 35f, 0f);
        sun.intensity = WellLook.SUN_LUX * SUN_K;
        sun.colorTemperature = 3400f;
        if (!vol.sharedProfile.TryGet(out sky)) Debug.LogError("no HDRISky");
        sky.multiplier.value = WellLook.SKY_MULT * SKY_K;

        // ground: grey topsoil over bright yellow sand
        var topsoil = WellLook.Textured("brown_mud_03", Hex("#8B8780"), 1.6f, 0.2f);
        // the sand in section: calm fine grain and faint beds (the Poly Haven grain only as a soft normal)
        var sandA = WellLook.Textured("sand_01", Color.white, 2.6f, 0.15f, false, 0.35f);
        sandA.SetTexture("_BaseColorMap", SandFaceTex());
        Keys(sandA);
        var yard = WellLook.Textured("brown_mud_03", Hex("#9C988F"), 2.4f, 0.22f);
        ground = new CutGround(stage, new[] { 0f, -0.38f, -60f }, new[] { topsoil, sandA }, yard);
        frontGround = Prim(PrimitiveType.Cube, "frontGround", new Vector3(0f, -0.05f, -150f), new Vector3(500f, 0.1f, 300f), yard);

        // hut 104 (cutaway) and two more huts behind (closed fronts)
        Model("esc_barrack", Vector3.zero, 180f, 1f);
        foreach (var hx in new[] { -15f, 12f, 1f })
        {
            float hz = hx == 1f ? 14f : 6.5f;
            Model("esc_barrack", new Vector3(hx, 0f, hz), 180f, 1f);
            Prim(PrimitiveType.Cube, "hutFront", new Vector3(hx, FLOOR + 1.27f, hz + 0.06f), new Vector3(12f, 2.55f, 0.12f), Mat("hut_wall"));
        }
        Model("esc_stove_base", new Vector3(SX, 0f, 0f), 180f, 1f);
        trapdoor = Model("esc_trapdoor", new Vector3(SX, FLOOR, 0.12f), 180f, 1f);
        stove = Model("esc_stove", new Vector3(SX, FLOOR + 0.03f, 0.12f), 180f, 1f);
        Model("esc_bunk", new Vector3(-1.6f, FLOOR, 1.6f), 180f, 1f);
        Model("esc_bunk", new Vector3(-3.6f, FLOOR, 3.85f), 180f, 1f);
        Model("esc_table", new Vector3(4.4f, FLOOR, 2.8f), 180f, 1f);
        var blanket = WellLook.Lit("blanket", Hex("#5D5A52"), 0.15f);
        Prim(PrimitiveType.Cube, "blanketTop", new Vector3(-1.6f, FLOOR + 1.31f, 2.05f), new Vector3(1.95f, 0.12f, 0.86f), blanket);
        Prim(PrimitiveType.Cube, "blanketTop2", new Vector3(-3.6f, FLOOR + 1.31f, 4.3f), new Vector3(1.95f, 0.12f, 0.86f), blanket);
        Prim(PrimitiveType.Cube, "blanketLow2", new Vector3(-3.6f, FLOOR + 0.36f, 4.3f), new Vector3(1.95f, 0.12f, 0.86f), blanket);
        // the lower bunk's slats: these become the tunnel's boards
        var board = WellLook.Textured("brown_planks_03", Hex("#B08D66"), 0.8f, 0.3f, true);
        slats = new GameObject[9];
        for (int i = 0; i < slats.Length; i++)
            slats[i] = Prim(PrimitiveType.Cube, "slat" + i, Vector3.zero, new Vector3(0.15f, 0.028f, 0.86f), board);

        // the wire: two fences of posts and strands running into the picture, concertina between
        for (float z = -70f; z < 140f; z += 2.8f)
            foreach (var fx in new[] { FENCE_A, FENCE_B })
            {
                var p = Model("esc_post", new Vector3(fx, 0f, z), 180f, 1f);
                if (z < 0f) frontOnly.Add(p);
            }
        var wire = Mat("wire");
        foreach (var fx in new[] { FENCE_A, FENCE_B })
            for (int k = 0; k < 8; k++)
            {
                float y = 0.3f + k * 0.34f;
                for (int half = 0; half < 2; half++)
                {
                    // strands stop at the cut: the half in front of it only shows in the hook
                    var w = Prim(PrimitiveType.Cylinder, "strand", new Vector3(fx - 0.08f, y, half == 0 ? 70f : -35f), new Vector3(0.014f, half == 0 ? 70f : 35f, 0.014f), wire);
                    w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    w.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    if (half == 1) frontOnly.Add(w);
                }
            }
        for (float z = -70f; z < 140f; z += 10f)
        {
            var c = Model("esc_coil", new Vector3((FENCE_A + FENCE_B) * 0.5f, 0f, z + 5f), 180f, 1f);
            if (z < 0f) frontOnly.Add(c);
        }
        Model("esc_tower", new Vector3(TOWER_X, 0f, TOWER_Z), 180f, 1f);
        // the tower's searchlight: a visible beam sweeping the wire (volumetric fog)
        var sg = new GameObject("searchlight"); sg.transform.SetParent(stage, false);
        searchL = sg.AddComponent<Light>();
        searchL.type = LightType.Spot;
        sg.AddComponent<HDAdditionalLightData>();
        searchL.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
        searchL.color = new Color(1f, 0.95f, 0.85f);
        searchL.spotAngle = 9f; searchL.innerSpotAngle = 5f;
        searchL.range = 80f;
        searchL.intensity = 2500000f;
        searchL.shadows = LightShadows.None;
        sg.transform.localPosition = new Vector3(TOWER_X, 5.35f, TOWER_Z - 0.6f);

        // the pine forest outside the wire, and behind the camp
        int ti = 0;
        for (int i = 0; i < 140; i++)
        {
            float x = 36f + Hash(i, 1) * 85f, z = -40f + Hash(i, 2) * 140f;
            if (z > -3f && z < 1.5f) continue;
            var tr = Model(i % 2 == 0 ? "pine_a" : "pine_b", new Vector3(x, 0f, z), Hash(i, 3) * 360f, 1.15f + Hash(i, 4) * 0.7f);
            if (z < 0f) frontOnly.Add(tr);
            ti++;
        }
        for (int i = 0; i < 70; i++)
        {
            float x = -60f + Hash(i, 5) * 95f, z = 34f + Hash(i, 6) * 50f;
            Model(i % 2 == 0 ? "pine_a" : "pine_b", new Vector3(x, 0f, z), Hash(i, 7) * 360f, 1.5f + Hash(i, 8) * 0.8f);
        }

        // microphones in the soil under the wire
        var micMat = WellLook.Lit("mic", Hex("#8C939B"), 0.45f, 0.15f);
        micGlowMat = WellLook.Emissive(WellLook.Lit("micGlow", Hex("#FF3B2A"), 0.6f), Hex("#FF3B2A"), 0f);
        mics = new GameObject[MIC_X.Length]; micGlow = new GameObject[MIC_X.Length];
        for (int i = 0; i < MIC_X.Length; i++)
        {
            var mic = new GameObject("mic" + i);
            mic.transform.SetParent(stage, false);
            var body = Prim(PrimitiveType.Cube, "micBody", Vector3.zero, new Vector3(0.6f, 0.44f, 0.6f), micMat); body.transform.SetParent(mic.transform, false);
            var face = Prim(PrimitiveType.Cylinder, "micFace", new Vector3(0f, 0.26f, 0f), new Vector3(0.44f, 0.05f, 0.44f), micGlowMat); face.transform.SetParent(mic.transform, false);
            var cable = Prim(PrimitiveType.Cylinder, "micCable", new Vector3(0f, -MIC_Y * 0.5f, 0f), new Vector3(0.035f, -MIC_Y * 0.5f, 0.035f), Mat("iron_dark")); cable.transform.SetParent(mic.transform, false);
            mic.transform.localPosition = new Vector3(MIC_X[i], MIC_Y, 0.08f);
            mics[i] = mic; micGlow[i] = face;
            mic.SetActive(false);
        }
        ringMat = WellLook.Unlit("ring", new Color(2.2f, 0.55f, 0.3f, 0.9f), RingTex(), true);
        rings = Many(14, PrimitiveType.Quad, "ring", ringMat);

        // the shaft's ladder, the tunnel's boards, collapses, the lamp
        ladder = new GameObject("ladder"); ladder.transform.SetParent(stage, false);
        var lw = Mat("post_wood");
        foreach (var dx in new[] { -0.2f, 0.2f })
        {
            var r = Prim(PrimitiveType.Cube, "rail", new Vector3(dx, -4.5f, 0f), new Vector3(0.05f, 9.2f, 0.05f), lw); r.transform.SetParent(ladder.transform, false);
        }
        for (int i = 0; i < 30; i++)
        {
            var r = Prim(PrimitiveType.Cube, "rung", new Vector3(0f, -0.15f - i * 0.3f, 0f), new Vector3(0.4f, 0.035f, 0.04f), lw); r.transform.SetParent(ladder.transform, false);
        }
        ladder.transform.localPosition = new Vector3(SX - 0.12f, 0.35f, SZ - 0.08f);
        ladder.SetActive(false);

        shores = new GameObject[80];
        for (int i = 0; i < shores.Length; i++)
        {
            var f = new GameObject("shore" + i); f.transform.SetParent(stage, false);
            var roofB = Prim(PrimitiveType.Cube, "roofBoard", new Vector3(0f, TH - 0.025f, SZ * 0.5f), new Vector3(0.27f, 0.05f, SZ), board); roofB.transform.SetParent(f.transform, false);
            var post = Prim(PrimitiveType.Cube, "post", new Vector3(0f, TH * 0.5f, SZ - 0.05f), new Vector3(0.09f, TH, 0.06f), board); post.transform.SetParent(f.transform, false);
            var wall = Prim(PrimitiveType.Cube, "wallBoard", new Vector3(0f, TH * 0.5f, SZ - 0.01f), new Vector3(0.29f, TH, 0.02f), Mat("hut_batten")); wall.transform.SetParent(f.transform, false);
            shores[i] = f; f.SetActive(false);
        }
        pile1 = Prim(PrimitiveType.Sphere, "pile1", Vector3.zero, Vector3.one, sandA); pile1.SetActive(false);
        pile2 = Prim(PrimitiveType.Sphere, "pile2", Vector3.zero, Vector3.one, sandA); pile2.SetActive(false);
        var grainMat = WellLook.Lit("grain", Hex("#E6BD66"), 0.15f);
        grains = Many(150, PrimitiveType.Sphere, "grain", grainMat);
        foreach (var g in grains) g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        lamp = Prim(PrimitiveType.Cylinder, "lamp", Vector3.zero, new Vector3(0.11f, 0.05f, 0.11f), Mat("iron"));
        lampFlat = Prim(PrimitiveType.Cylinder, "lampFat", Vector3.zero, new Vector3(0.09f, 0.004f, 0.09f), WellLook.Lit("fat", Hex("#E8DFC4"), 0.6f));
        var flameC = Hex("#FFC864") * 3f; flameC.a = 0.95f;
        flame = Prim(PrimitiveType.Quad, "flame", Vector3.zero, Vector3.one, WellLook.Unlit("flame", flameC, WellLook.SoftDot(), true));
        flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        lampL = PointL("lampLight", Vector3.zero, new Color(1f, 0.64f, 0.32f), 6f, true);
        hutL = PointL("hutLight", new Vector3(0.6f, FLOOR + 2.1f, 1.0f), new Color(1f, 0.86f, 0.7f), 10f, false);
        hutL.intensity = 1500000f;
        shaftL = PointL("shaftLight", new Vector3(SX, DEEP + 0.7f, 0.5f), new Color(1f, 0.66f, 0.34f), 5f, false);

        you = new Human(stage, "Mixamo/remy", 1.78f);

        arUnder = new Arrow(this, "under", 8.2f, 1.1f, WellLook.Unlit("arrowW", Color.white * 2f));
        lab1944 = Lab("1944", "w");
        labMic = Lab("MICROPHONES", "o");
        lab30 = Lab("30 FT", "y");
        labSand = Lab("SAND", "y");
        labNoAir = Lab("NO AIR", "w");
    }

    // ------------------------------------------------------------------ the camera
    // a camera key: position + look target (+ optional anchor that moves with the story, e.g. the tunnel face) and lens
    class Shot
    {
        public float t, fov = FOV; public Vector3 pos, look; public System.Func<float, Vector3> anchor;
        public Shot(float t, Vector3 p, Vector3 l) { this.t = t; pos = p; look = l; }
        public Vector3 P(float now) => anchor == null ? pos : pos + anchor(now);
        public Vector3 L(float now) => anchor == null ? look : look + anchor(now);
    }
    static Shot K(float t, Vector3 p, Vector3 l, float fov = FOV, System.Func<float, Vector3> anchor = null) => new Shot(t, p, l) { fov = fov, anchor = anchor };
    static Vector3 Cp(float x, float y, float h) => new Vector3(x, y, -h * 0.5f / Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad));
    Shot W(float t, float x, float y, float h) => new Shot(t, Cp(x, y, h), new Vector3(x, y, 0f));
    Shot[] shots;
    const float C = 0.02f;

    void MakeShots()
    {
        float under = Tw(1, "under"), dig = Tw(3, "dig"), box = Tw(5, "box");
        if (SHOTS3) { MakeShots3(); return; }
        shots = new[]
        {
            new Shot(0f, new Vector3(3.0f, 1.9f, -2.6f), new Vector3(24f, 1.7f, -1.0f)),
            new Shot(under - C, new Vector3(4.4f, 1.85f, -2.45f), new Vector3(24f, 1.7f, -1.0f)),
            W(under, 21.4f, 0.4f, 15.5f),
            W(S(2) - C, 21.4f, 0.3f, 15f),
            W(S(2), 21.6f, 0.2f, 15.5f),
            W(S(3) - C, 21.6f, -0.1f, 14.8f),
            W(S(3), 1.3f, 1.55f, 6.6f),
            W(dig + 0.25f, 1.4f, 1.3f, 7.2f),
            W(En(3) - 0.2f, 2.2f, -4.4f, 14.5f),
            W(S(4) - C, 2.2f, -4.4f, 14.5f),
            W(S(4), 3.3f, -8.3f, 5.4f),
            W(Tw(4, "roof"), 5.2f, -8.3f, 6.0f),
            W(S(5) - C, 5.8f, -8.35f, 5.2f),
            W(S(5), -0.3f, 1.6f, 6.8f),
            W(Tw(5, "bed") , 0.6f, 1.2f, 7.2f),
            W(box + 0.15f, 4.2f, -8.35f, 6.0f),
            W(S(6) - C, 6.0f, -8.35f, 6.0f),
            W(S(6), 12.4f, -8.4f, 5.0f),
            W(Duration + 1f, 12.9f, -8.45f, 4.2f),
        };
    }

    // v3 (Lazar 2026-11-12, after the Alcatraz reference): 3/4 perspective angles, close-ups of the things, a new
    // picture every 2-3 s, the camera always drifting. Each shot = two keys (start, end - C); a cut between shots.
    const bool SHOTS3 = true;
    Vector3 TipA(float t) => new Vector3(TipAt(t), 0f, 0f);
    void MakeShots3()
    {
        float under = Tw(1, "under"), s2 = S(2), s3 = S(3), s4 = S(4), s5 = S(5), s6 = S(6);
        float dig = Tw(3, "dig"), too = Tw(3, "too"), mic = Tw(2, "microphones"), hear = Tw(2, "hear");
        float roofThe = Tw(4, "the"), box = Tw(5, "box"), lampW = Tw(6, "lamp");
        float D = DEEP;
        var L = new List<Shot>();
        void Sh(float a, float b, Vector3 p0, Vector3 p1, Vector3 l0, Vector3 l1, float fov = FOV, System.Func<float, Vector3> anc = null)
        { L.Add(K(a, p0, l0, fov, anc)); L.Add(K(b - C, p1, l1, fov, anc)); }
        // 1. the hook: low behind the prisoner, the tower's searchlight over the wire, a slow push
        Sh(0f, under, new Vector3(2.6f, 1.3f, -2.9f), new Vector3(4.8f, 1.25f, -2.7f), new Vector3(24f, 3.0f, 2f), new Vector3(24f, 3.0f, 2f));
        // 2. "under the fence": high 3/4, the camp sliced open
        Sh(under, s2, new Vector3(10.5f, 6.2f, -17.5f), new Vector3(12.0f, 5.4f, -16.5f), new Vector3(21.5f, 0.2f, 2.0f), new Vector3(21.7f, 0.0f, 2.0f));
        // 3. the shallow try, close and low
        Sh(s2, mic - 0.1f, new Vector3(20.2f, -0.75f, -2.3f), new Vector3(20.0f, -0.85f, -2.1f), new Vector3(18.2f, -1.4f, 0.3f), new Vector3(18.4f, -1.4f, 0.3f), 32f);
        // 4. the microphones pop up, close
        Sh(mic - 0.1f, hear - 0.15f, new Vector3(21.9f, -0.65f, -2.6f), new Vector3(22.05f, -0.7f, -2.4f), new Vector3(23.3f, -1.4f, 0.1f), new Vector3(23.3f, -1.4f, 0.1f), 30f);
        // 5. every shovel is heard: 3/4, rings reaching the wire
        Sh(hear - 0.15f, s3, new Vector3(15.2f, 1.7f, -10.6f), new Vector3(16.4f, 1.3f, -10.0f), new Vector3(21.5f, -1.2f, 0.5f), new Vector3(21.6f, -1.3f, 0.5f));
        // 6. in the hut: the stove slides off the trap door, close
        Sh(s3, dig + 0.45f, new Vector3(4.1f, 2.1f, -2.7f), new Vector3(3.9f, 2.0f, -2.6f), new Vector3(1.7f, 1.05f, 0.5f), new Vector3(1.8f, 1.0f, 0.5f), 32f);
        // 7. straight down: high, looking down the shaft as it deepens
        Sh(dig + 0.45f, too, new Vector3(3.9f, 2.5f, -3.4f), new Vector3(3.7f, 1.7f, -3.7f), new Vector3(2.0f, -2.2f, 0.4f), new Vector3(2.0f, -4.6f, 0.4f));
        // 8. too deep to be heard: wide 3/4 low, the hut on top, the whole shaft
        Sh(too, s4, new Vector3(-3.6f, -2.6f, -17.5f), new Vector3(-3.0f, -3.1f, -16.8f), new Vector3(2.6f, -4.3f, 0.5f), new Vector3(2.6f, -4.4f, 0.5f));
        // 9. pure sand: at the face, ahead of the digger, tracking the tunnel's tip
        Sh(s4, roofThe - 0.1f, new Vector3(0.6f, D + 0.6f, -1.35f), new Vector3(0.55f, D + 0.58f, -1.3f), new Vector3(-0.75f, D + 0.38f, 0.45f), new Vector3(-0.75f, D + 0.38f, 0.45f), FOV, TipA);
        // 10. the roof keeps falling in: 3/4 from the front-left
        Sh(roofThe - 0.1f, s5, new Vector3(1.6f, D + 1.5f, -3.6f), new Vector3(2.0f, D + 1.3f, -3.4f), new Vector3(4.9f, D + 0.45f, 0.4f), new Vector3(5.0f, D + 0.45f, 0.4f));
        // 11. the bed's boards, close
        Sh(s5, box - 0.15f, new Vector3(-0.9f, FLOOR + 1.55f, -1.6f), new Vector3(-0.8f, FLOOR + 1.5f, -1.45f), new Vector3(-0.6f, FLOOR + 0.55f, 2.0f), new Vector3(-0.6f, FLOOR + 0.55f, 2.0f));
        // 12. box in every foot: down the length of the tunnel at a steep angle, boards popping in one after another
        Sh(box - 0.15f, s6, new Vector3(SH1 - 1.0f, D + 0.45f, -1.8f), new Vector3(SH1 - 0.4f, D + 0.45f, -1.7f), new Vector3(SH1 + 4.5f, D + 0.42f, 0.45f), new Vector3(SH1 + 5.5f, D + 0.42f, 0.45f), 30f);
        // 13. no air, the lamp goes out: close on his face and the lamp, pushing in
        Sh(s6, Duration + 1f, new Vector3(0.6f, D + 0.55f, -1.3f), new Vector3(0.3f, D + 0.4f, -0.9f), new Vector3(-0.8f, D + 0.33f, 0.45f), new Vector3(-0.6f, D + 0.27f, 0.35f), FOV, TipA);
        shots = L.ToArray();
    }

    void UpdateCamera(float t)
    {
        if (shots == null) MakeShots();
        int i = 0;
        while (i < shots.Length - 2 && t >= shots[i + 1].t) i++;
        float u = PE(t, shots[i].t, shots[i + 1].t);
        var pos = Vector3.Lerp(shots[i].P(t), shots[i + 1].P(t), u);
        var look = Vector3.Lerp(shots[i].L(t), shots[i + 1].L(t), u);
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        float fov = Mathf.Lerp(shots[i].fov, shots[i + 1].fov, u);
        if (Mathf.Abs(cam.fieldOfView - fov) > 1e-3f) { cam.fieldOfView = fov; cam.focalLength = 18f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad); }
    }

    // ------------------------------------------------------------------ the story
    float TipAt(float t)
    {
        // the tunnel face: starts at the shaft, gets to 7 m while the roof falls, 14 m for the lamp beat
        float tip = SH1;
        tip = Mathf.Lerp(tip, 3.6f, PE(t, S(4) - 0.6f, S(4)));
        tip = Mathf.Lerp(tip, 7.0f, PE(t, S(4), Tw(4, "roof")));
        tip = Mathf.Lerp(tip, 14.2f, PE(t, En(5), S(6)));
        return tip;
    }

    public void Evaluate(float t)
    {
        foreach (var l in labels) l.k = 0f;
        float under = Tw(1, "under"), s2 = S(2), s3 = S(3), s4 = S(4), s5 = S(5), s6 = S(6), s7 = S(7);
        bool hook = t < under;
        Show(frontGround, hook);
        foreach (var g in frontOnly) Show(g, hook);

        // ------ the cuts
        cuts.Clear();
        float dig = Tw(3, "dig"), down = Tw(3, "down", 0, true);
        float depth = t < dig ? 0f : -DEEP * PE(t, dig + 0.15f, down + 0.5f);
        if (t >= s4) depth = -DEEP;
        if (depth > 0.01f) cuts.Add(new Bounds(new Vector3(SX, -depth * 0.5f + 0.005f, -50f), new Vector3(SH1 - SH0, depth + 0.01f, 100f + 2f * SZ)));
        float tip = TipAt(t);
        if (t >= s4 - 0.6f && tip > SH1 + 0.01f)
        {
            float x0 = SH0;
            cuts.Add(new Bounds(new Vector3((x0 + tip) * 0.5f, DEEP + TH * 0.5f, -50f), new Vector3(tip - x0, TH, 100f + 2f * SZ)));
        }
        // the shallow try in sentence 2
        bool shallowOn = t >= s2 && t < s3;
        float shTip = HOLE_X + 0.8f + 2.2f * PE(t, s2, Tw(2, "hear"));
        if (shallowOn)
        {
            cuts.Add(new Bounds(new Vector3(HOLE_X + 0.4f, SHALLOW * 0.5f, -50f), new Vector3(0.8f, -SHALLOW + 0.02f, 100f + 2f * SZ)));
            cuts.Add(new Bounds(new Vector3((HOLE_X + shTip) * 0.5f, SHALLOW + 0.4f, -50f), new Vector3(shTip - HOLE_X, 0.8f, 100f + 2f * SZ)));
        }
        // roof falls (sentence 4): the void left above the fallen sand
        float fall1 = Tw(4, "roof"), fall2 = Tw(4, "falling");
        bool collapseOn = t >= fall1 && t < s5;
        float v1 = PE(t, fall1, fall1 + 0.5f), v2 = PE(t, fall2, fall2 + 0.6f);
        if (collapseOn)
        {
            if (v1 > 0f) cuts.Add(new Bounds(new Vector3(3.5f, DEEP + TH + 0.2f * v1, -50f), new Vector3(0.8f, 0.4f * v1 + 0.01f, 100f + 2f * SZ)));
            if (v2 > 0f) cuts.Add(new Bounds(new Vector3(5.3f, DEEP + TH + 0.28f * v2, -50f), new Vector3(1.2f, 0.56f * v2 + 0.01f, 100f + 2f * SZ)));
        }
        ground.Rebuild(cuts);

        // ------ the stove slides off the trap door
        float slide = PE(t, dig - 0.25f, dig + 0.2f);
        stove.transform.localPosition = new Vector3(SX + 1.0f * slide, FLOOR + 0.03f, 0.12f);
        trapdoor.transform.localPosition = new Vector3(SX - 0.9f * slide, FLOOR + 0.0f, 0.12f);
        Show(ladder, depth > 3f);

        // ------ piles of fallen sand
        Show(pile1, collapseOn && v1 > 0.01f);
        if (pile1.activeSelf) { pile1.transform.localPosition = new Vector3(3.5f, DEEP, SZ * 0.5f); pile1.transform.localScale = new Vector3(1.3f, 0.95f * v1, SZ * 1.02f); }
        Show(pile2, collapseOn && v2 > 0.01f);
        if (pile2.activeSelf) { pile2.transform.localPosition = new Vector3(5.3f, DEEP, SZ * 0.5f); pile2.transform.localScale = new Vector3(1.9f, 1.7f * v2, SZ * 1.02f); }
        for (int i = 0; i < grains.Length; i++)
        {
            bool second = i % 3 != 0;
            float ft = second ? fall2 : fall1;
            float u = (t - ft) * 1.6f - Hash(i, 1) * 0.6f;
            bool on = t < s5 && u > 0f && u < 1f;
            Show(grains[i], on);
            if (!on) continue;
            float cx = second ? 5.3f : 3.5f, w = second ? 1.1f : 0.7f;
            float y = DEEP + TH + (second ? 0.5f : 0.35f) - u * u * 1.4f;
            grains[i].transform.localPosition = new Vector3(cx + (Hash(i, 2) - 0.5f) * w, Mathf.Max(y, DEEP + 0.05f), 0.04f + Hash(i, 3) * 0.6f);
            grains[i].transform.localScale = Vector3.one * (0.03f + Hash(i, 4) * 0.05f);
        }

        // ------ the bed's boards: lifted out, thrown down the shaft
        float pull = Tw(5, "pull"), box = Tw(5, "box");
        for (int i = 0; i < slats.Length; i++)
        {
            var home = new Vector3(-1.6f + 0.84f - i * 0.21f, FLOOR + 0.31f, 2.05f);
            float a = pull + i * 0.11f, u = P(t, a, a + 0.9f);
            bool gone = t >= s5 && u >= 1f || t >= s6;
            bool flying = t >= s5 && t < s6 && u > 0f && u < 1f;
            Show(slats[i], !gone);
            if (gone) continue;
            if (!flying) { slats[i].transform.localPosition = home; slats[i].transform.localRotation = Quaternion.identity; continue; }
            var lift = home + Vector3.up * 0.35f;
            var top = new Vector3(SX, FLOOR + 0.8f, 0.45f);
            var bot = new Vector3(SX, FLOOR - 4f, 0.45f);
            Vector3 p;
            if (u < 0.25f) p = Vector3.Lerp(home, lift, E(u / 0.25f));
            else if (u < 0.7f) { float k = E((u - 0.25f) / 0.45f); p = Vector3.Lerp(lift, top, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f; }
            else p = Vector3.Lerp(top, bot, (u - 0.7f) / 0.3f);
            slats[i].transform.localPosition = p;
            slats[i].transform.localRotation = Quaternion.Euler(0f, u * 200f, u > 0.7f ? 90f : u * 60f);
        }
        // boards along the tunnel, one by one from the shaft
        int nShore = 0;
        for (int i = 0; i < shores.Length; i++)
        {
            float x = SH1 + 0.15f + i * 0.3f;
            float appear = Mathf.Min(box - 0.1f + i * 0.085f, S(6) - 0.05f);
            bool on = t >= appear && x < tip - 0.55f && t >= s5;
            Show(shores[i], on);
            if (!on) continue;
            nShore++;
            float k = Back(P(t, appear, appear + 0.25f));
            shores[i].transform.localPosition = new Vector3(x, DEEP, 0f);
            shores[i].transform.localScale = new Vector3(1f, Mathf.Max(0.01f, k), 1f);
        }

        // ------ the person
        UpdateYou(t, tip, shTip);

        // ------ sound rings: loud near the surface, dying in the deep
        float hear = Tw(2, "hear");
        float[] hits = { hear - 0.1f, Tw(2, "every"), Tw(2, "shovel"), Tw(2, "shovel") + 0.45f, Tw(2, "shovel") + 0.9f };
        int ri = 0;
        foreach (var h in hits)
            for (int k = 0; k < 2; k++, ri++)
            {
                float u = (t - h - k * 0.18f) / 1.1f;
                bool on = shallowOn && u > 0f && u < 1f;
                var src = new Vector3(shTip - 0.15f, SHALLOW + 0.35f, -0.05f - ri * 0.001f);
                float r = Mathf.Lerp(0.3f, SHOTS3 ? 4.2f : 5.5f, u);
                Sprite(rings[ri], on, src, r * 2f, r * 2f);
            }
        float[] deep = { Tw(3, "too"), Tw(3, "deep") + 0.25f };
        for (int k = 0; k < 2; k++, ri++)
        {
            float u = (t - deep[k]) / 0.8f;
            bool on = t < s4 && u > 0f && u < 1f;
            float r = Mathf.Lerp(0.2f, 1.6f, u);
            Sprite(rings[ri], on, new Vector3(SX, -depth + 0.3f, -0.05f - ri * 0.001f), r * 2f, r * 2f);
        }
        ringMat.SetColor("_UnlitColor", new Color(2.2f, 0.55f, 0.3f, t < s3 ? 0.9f : 0.55f));
        // microphones: pop in, then light up when the sound reaches them
        float micIn = Tw(2, "microphones");
        float alarm = 0f;
        foreach (var h in hits) { float d = t - h - 0.35f; if (d > 0f && d < 0.5f) alarm = Mathf.Max(alarm, 1f - d / 0.5f); }
        for (int i = 0; i < mics.Length; i++)
        {
            bool on = t >= micIn - 0.05f && t < s3;
            Show(mics[i], on);
            if (!on) continue;
            float k = Back(P(t, micIn - 0.05f + i * 0.1f, micIn + 0.2f + i * 0.1f));
            mics[i].transform.localScale = Vector3.one * Mathf.Max(0.01f, k);
        }
        micGlowMat.SetColor("_EmissiveColor", Hex("#FF3B2A") * (alarm * 9000f + 150f));

        // ------ the lamp: lit while you dig, dies when the air goes
        bool lampOn = t >= s4 - 0.6f && t < s7 + 2f;
        float air = PE(t, Tw(6, "no"), Tw(6, "out") + 0.35f);
        var lampP = new Vector3(Mathf.Max(SH1 + 0.6f, tip - 0.35f), DEEP + 0.03f, 0.18f);
        if (t >= s5 && t < s6) lampP.x = Mathf.Min(lampP.x, SH1 + 3.2f);
        Show(lamp, lampOn); Show(lampFlat, lampOn);
        float fk = (1f - air) * (1f + 0.1f * Mathf.Sin(t * 23f) + 0.07f * Mathf.Sin(t * 37f + 1f));
        Show(flame, lampOn && fk > 0.03f);
        if (lampOn)
        {
            lamp.transform.localPosition = lampP + Vector3.up * 0.05f;
            lampFlat.transform.localPosition = lampP + Vector3.up * 0.1f;
            flame.transform.localPosition = lampP + new Vector3(0f, 0.2f, -0.12f);
            flame.transform.localScale = new Vector3(0.09f * fk, 0.17f * fk, 1f);
        }
        lampL.transform.localPosition = lampP + new Vector3(0f, 0.3f, -0.15f);
        lampL.intensity = lampOn ? 150000f * fk : 0f;
        lampL.enabled = lampOn && fk > 0.01f;
        shaftL.intensity = t >= s4 - 0.6f && t < s6 ? 160000f : 0f;
        shaftL.enabled = shaftL.intensity > 0f;

        // no air: the world goes dark around the dying lamp
        float dark = PE(t, Tw(6, "no"), Tw(6, "out") + 0.4f);
        sun.intensity = WellLook.SUN_LUX * SUN_K * (1f - 0.85f * dark);
        sky.multiplier.value = WellLook.SKY_MULT * SKY_K * (1f - 0.8f * dark);

        // ------ the searchlight sweeps along the wire
        float sw = Mathf.Sin(t * 0.55f + 0.4f);
        var aim = new Vector3(FENCE_A + 1.5f + 2.5f * sw, 0f, -2f + 9f * sw);
        searchL.transform.localRotation = Quaternion.LookRotation(aim - searchL.transform.localPosition, Vector3.up);

        // ------ arrows, pills
        arUnder.Set(t >= under && t < s2, new Vector3(16.6f, -2.3f, -0.2f), -90f, Back(P(t, under + 0.1f, under + 0.4f)));
        Pill(lab1944, t, -1f, under, new Vector3(24f, 5.2f, -1.2f));
        Pill(labMic, t, micIn + 0.1f, s3 - 0.02f, new Vector3(SHOTS3 ? MIC_X[0] + 0.1f : (MIC_X[0] + MIC_X[1]) * 0.5f, MIC_Y + 0.8f, 0f));
        Pill(lab30, t, Tw(3, "thirty"), s4 - 0.02f, new Vector3(SH1 + 1.6f, -4.5f, 0f));
        Pill(labSand, t, Tw(4, "sand"), s5 - 0.02f, new Vector3(Mathf.Min(tip, 7f) - 0.4f, DEEP + 1.35f, 0f));
        Pill(labNoAir, t, Tw(6, "air"), s7 - 0.02f, new Vector3(tip - 0.7f, DEEP + 1.0f, 0f));

        UpdateCamera(t);
    }

    // ------------------------------------------------------------------ the person (one figure, posed per shot)
    void UpdateYou(float t, float tip, float shTip)
    {
        float under = Tw(1, "under"), s2 = S(2), s3 = S(3), s4 = S(4), s5 = S(5), s6 = S(6), s7 = S(7);
        float dig = Tw(3, "dig");
        you.Show(true);
        if (t < under)
        {
            // walking toward the wire
            float m = t * 1.05f;
            you.Walk(new Vector3(8.6f + m, 0f, -1.6f), 90f, m);
        }
        else if (t < s2)
        {
            you.Stand(new Vector3(17.6f, 0f, 1.3f), 90f, new Vector3(FENCE_A, 1.6f, 1.3f), 1f);
        }
        else if (t < s3)
        {
            // crawling in the shallow tunnel, scraping at its face
            float hx = shTip - 1.05f;
            you.Crawl(new Vector3(hx, SHALLOW, 0.42f), 90f, (t - s2) * 1.3f);
        }
        else if (t < dig)
        {
            var c = new Vector3(SX - 0.75f, FLOOR, 0.5f);
            you.Kneel(c, 90f, 18f, new Vector3(SX - 0.2f, FLOOR + 0.55f, 0.3f), new Vector3(SX - 0.2f, FLOOR + 0.5f, 0.7f), new Vector3(SX, FLOOR + 0.4f, 0.5f), 1f);
        }
        else if (t < s4 - 0.6f)
        {
            // in the shaft, going down with its floor
            float depth = -DEEP * PE(t, dig + 0.15f, Tw(3, "down", 0, true) + 0.5f);
            float bob = Mathf.Sin(t * 9f) * 0.5f + 0.5f;
            var f = new Vector3(SX, -depth, 0.45f);
            if (depth < 0.9f) f.y = Mathf.Min(FLOOR, -depth + 0.0f);
            you.Kneel(f, 90f, 30f + 18f * bob, f + new Vector3(0.18f, 0.12f + 0.25f * bob, -0.15f), f + new Vector3(0.18f, 0.12f + 0.25f * bob, 0.15f), f + new Vector3(0.4f, 0f, 0f), 0.8f);
        }
        else if (t < s5)
        {
            // at the face of the tunnel; backs off when the roof comes down
            float fall2 = Tw(4, "falling");
            float back = PE(t, fall2 - 0.1f, fall2 + 0.5f) * 0f;
            float hx = Mathf.Max(SH1 + 0.9f, tip - 1.05f - back);
            float speed = t < Tw(4, "roof") ? 1.4f : 0.25f;
            you.Crawl(new Vector3(hx, DEEP, 0.44f), 90f, (t - s4) * speed);
        }
        else if (t < box0())
        {
            // pulling the boards out of the bunk
            var f = new Vector3(0.0f, FLOOR, 2.05f);
            float reach = Mathf.Sin(t * 7f) * 0.5f + 0.5f;
            var hand = new Vector3(-0.5f - 0.1f * reach, FLOOR + 0.5f + 0.25f * reach, 2.05f);
            you.Kneel(f, -90f, 25f, hand + Vector3.forward * 0.15f, hand - Vector3.forward * 0.15f, new Vector3(-1.1f, FLOOR + 0.35f, 2.05f), 1f);
        }
        else if (t < s6)
        {
            you.Crawl(new Vector3(Mathf.Max(SH1 + 0.9f, tip - 1.05f), DEEP, 0.44f), 90f, (t - s5) * 1.2f);
        }
        else
        {
            // no air: the crawl slows to a stop
            float a = Tw(6, "no");
            float slow = t < a ? (t - s6) * 1.3f : (a - s6) * 1.3f + 1.3f * (1f - Mathf.Exp(-(t - a) * 1.2f)) / 1.2f;
            you.Crawl(new Vector3(tip - 1.05f, DEEP, 0.44f), 90f, slow);
        }
    }
    float box0() => Tw(5, "box") - 0.05f;

    // ------------------------------------------------------------------ the character: Mixamo figure, clips retargeted, IK poses
    class Human
    {
        readonly GameObject holder, inst;
        readonly Transform hips, head, neck, lArm, lFore, lHand, rArm, rFore, rHand, lUp, lLow, lFoot, rUp, rLow, rFoot;
        readonly Transform[] spine;
        readonly Transform[] bones; readonly Vector3[] bindP; readonly Quaternion[] bindR;
        readonly Quaternion lFootBind, rFootBind, lHandBind, rHandBind;
        Vector3 faceLocal;
        readonly float hipHeight;

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        public Human(Transform stage, string path, float height)
        {
            holder = new GameObject(path.Replace('/', '_'));
            holder.transform.SetParent(stage, false);
            var src = Resources.Load<GameObject>(path);
            inst = Instantiate(src);
            inst.transform.SetParent(holder.transform, false);
            Transform B(string n) => FindDeep(inst.transform, "mixamorig:" + n);
            hips = B("Hips"); head = B("Head"); neck = B("Neck");
            lArm = B("LeftArm"); lFore = B("LeftForeArm"); lHand = B("LeftHand");
            rArm = B("RightArm"); rFore = B("RightForeArm"); rHand = B("RightHand");
            lUp = B("LeftUpLeg"); lLow = B("LeftLeg"); lFoot = B("LeftFoot");
            rUp = B("RightUpLeg"); rLow = B("RightLeg"); rFoot = B("RightFoot");
            spine = new[] { B("Spine"), B("Spine1"), B("Spine2") };
            bones = hips.GetComponentsInChildren<Transform>();
            bindP = new Vector3[bones.Length]; bindR = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) { bindP[i] = bones[i].localPosition; bindR[i] = bones[i].localRotation; }
            lHandBind = lHand.localRotation; rHandBind = rHand.localRotation;

            float lo = float.MaxValue, hi = float.MinValue;
            var baked0 = new Mesh();
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(baked0, true);
                var vs = baked0.vertices;
                var mtx = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int i = 0; i < vs.Length; i += 7) { float y = mtx.MultiplyPoint3x4(vs[i]).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            }
            DestroyImmediate(baked0);
            holder.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, hi - lo));
            hipHeight = hips.position.y - lo * holder.transform.localScale.x;
            faceLocal = head.InverseTransformDirection(holder.transform.forward);
            lFootBind = Quaternion.Inverse(holder.transform.rotation) * lFoot.rotation;
            rFootBind = Quaternion.Inverse(holder.transform.rotation) * rFoot.rotation;
            Stylize();
            SetupBake();
        }

        // the figure's own textures (skin, face, knit pullover), clothes tinted: RAF blue-grey knit, dark trousers,
        // dark brown boots; a dark wool cap over the painted scalp
        void Stylize()
        {
            var scalp = Resources.Load<Texture2D>("Mixamo/Textures/remy/Remy_Body_DiffuseH");     // tools/remy_scalp.py
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                string n = r.name;
                if (n.Contains("Hair") || n.Contains("Eyelash") || n == "Eyes") { r.enabled = false; continue; }
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    var m = new Material(ms[i]);
                    float sm = 0.25f;
                    if (n == "Body") { if (scalp != null) m.SetTexture("_BaseColorMap", scalp); sm = 0.38f; }
                    // the clothes' own maps are dark grey: plain colour, the knit/cloth kept in the normal map
                    else if (n.Contains("Top")) { m.SetTexture("_BaseColorMap", null); m.SetColor("_BaseColor", Hex("#647A9C")); }
                    else if (n.Contains("Bottom")) { m.SetTexture("_BaseColorMap", null); m.SetColor("_BaseColor", Hex("#5C5850")); }
                    else if (n.Contains("Shoe") || n.Contains("Sneaker")) { m.SetColor("_BaseColor", Hex("#4A3426")); sm = 0.3f; }
                    m.SetFloat("_Smoothness", sm);
                    Keys(m);
                    ms[i] = m;
                }
                r.sharedMaterials = ms;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
                Debug.Log("REMY part " + n + " mats " + ms.Length);
            }
            var top = FindDeep(inst.transform, "mixamorig:HeadTop_End");
            float hr = Vector3.Distance(head.position, top.position) * 0.5f;
            var up = (top.position - head.position).normalized;
            var fwd = Vector3.ProjectOnPlane(head.TransformDirection(faceLocal), up).normalized;
            var hm = new GameObject("cap");
            hm.AddComponent<MeshFilter>().sharedMesh = HemiMesh();
            var wool = WellLook.Lit("cap", Hex("#2B2F36"), 0.12f);
            wool.SetFloat("_DoubleSidedEnable", 1f); wool.SetFloat("_CullMode", 0f);
            Keys(wool);
            hm.AddComponent<MeshRenderer>().sharedMaterial = wool;
            hm.transform.position = Vector3.Lerp(head.position, top.position, 0.62f) - fwd * hr * 0.12f;
            hm.transform.rotation = Quaternion.LookRotation(fwd, up) * Quaternion.Euler(-14f, 0f, 0f);
            hm.transform.localScale = new Vector3(hr * 1.06f, hr * 0.95f, hr * 1.1f);
            hm.transform.SetParent(head, true);
        }

        static Mesh HemiMesh()
        {
            const int SEG = 40, RING = 18;
            var v = new List<Vector3>(); var tr = new List<int>();
            for (int j = 0; j <= RING; j++)
            {
                float th = Mathf.Deg2Rad * 100f * j / RING;
                for (int i = 0; i <= SEG; i++)
                {
                    float ph = 2f * Mathf.PI * i / SEG;
                    v.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)));
                }
            }
            for (int j = 0; j < RING; j++)
                for (int i = 0; i < SEG; i++)
                {
                    int a = j * (SEG + 1) + i, b = a + SEG + 1;
                    tr.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var m = new Mesh { name = "cap" };
            m.SetVertices(v); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        readonly List<(SkinnedMeshRenderer smr, Mesh mesh)> baked = new List<(SkinnedMeshRenderer, Mesh)>();
        void SetupBake()
        {
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || smr.forceRenderingOff) continue;
                var go = new GameObject(smr.name + "_baked");
                go.transform.SetParent(smr.transform, false);
                go.transform.localScale = Vector3.one / smr.transform.lossyScale.x;
                var mesh = new Mesh { name = smr.name + "_baked" }; mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = smr.sharedMaterials;
                mr.shadowCastingMode = smr.shadowCastingMode;
                mr.receiveShadows = smr.receiveShadows;
                mr.allowOcclusionWhenDynamic = false;
                smr.forceRenderingOff = true;
                baked.Add((smr, mesh));
            }
        }
        void Bake() { if (!holder.activeInHierarchy) return; foreach (var (smr, mesh) in baked) { smr.BakeMesh(mesh, false); mesh.RecalculateBounds(); } }

        void ResetPose() { for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = bindP[i]; bones[i].localRotation = bindR[i]; } }

        void Place(Vector3 hipsAt, Quaternion rot)
        {
            holder.transform.rotation = rot;
            holder.transform.position = hipsAt;
            holder.transform.position += hipsAt - hips.position;
        }

        void LeanFwd(float deg)
        {
            foreach (var b in spine) b.rotation = Quaternion.AngleAxis(deg / spine.Length, holder.transform.right) * b.rotation;
        }

        static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
        {
            float la = Vector3.Distance(a.position, b.position), lb = Vector3.Distance(b.position, c.position);
            var toT = target - a.position;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, (la + lb) * 0.999f);
            var n = Vector3.Cross(toT, pole).normalized;
            float cosA = (la * la + d * d - lb * lb) / (2f * la * d);
            float angA = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
            var dirUpper = Quaternion.AngleAxis(angA, n) * toT.normalized;
            a.rotation = Quaternion.FromToRotation(b.position - a.position, dirUpper) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
        }

        void Leg(bool left, Vector3 ankle, Vector3 kneeDir, float toeDownDeg)
        {
            Transform a = left ? lUp : rUp, b = left ? lLow : rLow, c = left ? lFoot : rFoot;
            Solve(a, b, c, ankle, kneeDir);
            c.rotation = Quaternion.AngleAxis(toeDownDeg, holder.transform.right) * holder.transform.rotation * (left ? lFootBind : rFootBind);
        }

        void Reach(bool left, Vector3 target)
        {
            Transform a = left ? lArm : rArm, b = left ? lFore : rFore, c = left ? lHand : rHand;
            var side = holder.transform.right * (left ? -1f : 1f);
            Solve(a, b, c, target, (side * 0.6f + Vector3.down).normalized);
        }

        void Look(Vector3 target, float w)
        {
            if (w <= 0.001f) return;
            var cur = head.TransformDirection(faceLocal); var want = target - head.position;
            var delta = Quaternion.FromToRotation(cur, want);
            float ang; Vector3 axis; delta.ToAngleAxis(out ang, out axis);
            if (ang > 180f) ang -= 360f;
            ang = Mathf.Clamp(ang, -40f, 40f) * 0.7f * w;
            neck.rotation = Quaternion.AngleAxis(ang, axis) * neck.rotation;
        }

        public void Show(bool on) { if (holder.activeSelf != on) holder.SetActive(on); }

        // ---- poses
        public void Stand(Vector3 feet, float yaw, Vector3 look, float lookW, Vector3? handL = null, Vector3? handR = null)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            ResetPose();
            var hp = feet + Vector3.up * (hipHeight - 0.02f);
            Place(hp, rot);
            foreach (float s in new[] { -1f, 1f })
            {
                var foot = new Vector3(hp.x, feet.y + 0.1f, hp.z) + r * s * 0.12f;
                Leg(s < 0f, foot, f, 0f);
                var hand = s < 0f ? handL : handR;
                Reach(s < 0f, hand ?? (hp + r * s * 0.29f + Vector3.up * -0.04f + f * 0.04f));
            }
            Look(look, lookW);
            Bake();
        }

        public void Kneel(Vector3 knees, float yaw, float lean, Vector3 handL, Vector3 handR, Vector3 look, float lookW)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            ResetPose();
            var hp = knees + Vector3.up * 0.5f;
            Place(hp, rot);
            LeanFwd(lean);
            foreach (float s in new[] { -1f, 1f })
            {
                var foot = hp - f * 0.5f + r * s * 0.13f;
                foot.y = knees.y + 0.1f;
                Leg(s < 0f, foot, f + Vector3.down * 0.3f, 72f);
            }
            Reach(true, handL);
            Reach(false, handR);
            Look(look, lookW);
            Bake();
        }

        public void Walk(Vector3 at, float yaw, float metres)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var c = Clip("walking");
            holder.transform.rotation = rot;
            float time = metres / Mathf.Max(0.1f, c.speed);
            float bob = PoseClip(c, time);
            Place(new Vector3(at.x, at.y + hipHeight * bob, at.z), rot);
            Ground(at.y, true);
            // the clip's arms swing wide on this skeleton: hands back by the sides, swinging with the step
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            float sw = Mathf.Sin(2f * Mathf.PI * time / c.clip.length);
            foreach (float s in new[] { -1f, 1f })
                Reach(s < 0f, hips.position + r * s * 0.2f + Vector3.down * 0.2f + f * (0.12f * sw * s));
            lHand.localRotation = lHandBind; rHand.localRotation = rHandBind;   // the clip twists the wrists
            Bake();
        }

        public void Crawl(Vector3 at, float yaw, float cycles)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var c = Clip("crawl_forward");
            holder.transform.rotation = rot;
            float h = PoseClip(c, cycles * c.clip.length);
            Place(new Vector3(at.x, at.y + hipHeight * h, at.z), rot);
            Ground(at.y, false);
            Bake();
        }

        // drop or lift the posed body so its lowest joint rests on the floor
        void Ground(float floorY, bool feetOnly)
        {
            float lo = Mathf.Min(lFoot.position.y, rFoot.position.y) - 0.08f;
            if (!feetOnly)
            {
                lo = Mathf.Min(lo, lHand.position.y - 0.04f, rHand.position.y - 0.04f, lLow.position.y - 0.07f, rLow.position.y - 0.07f, head.position.y - 0.12f);
            }
            holder.transform.position += Vector3.up * (floorY - lo);
        }

        // ---- clips: a hidden copy of the clip's file plays it; every bone's world rotation relative to its rest is copied onto ours
        public class ClipDrv
        {
            public GameObject driver; public AnimationClip clip; public Transform dHips; public float dHipRest, speed;
            public (Transform d, Transform t, Quaternion dRest, Quaternion tRest)[] map;
        }
        readonly Dictionary<string, ClipDrv> clips = new Dictionary<string, ClipDrv>();

        ClipDrv Clip(string name)
        {
            ClipDrv c;
            if (clips.TryGetValue(name, out c)) return c;
            c = new ClipDrv();
            clips[name] = c;
            var src = Resources.Load<GameObject>("Mixamo/" + name);
            foreach (var a in Resources.LoadAll<AnimationClip>("Mixamo/" + name))
                if (!a.name.StartsWith("__preview")) { c.clip = a; break; }
            if (src == null || c.clip == null) { Debug.LogError("clip missing: " + name); return c; }
            var keepRot = holder.transform.rotation; var keepPos = holder.transform.position;
            holder.transform.rotation = Quaternion.identity;
            ResetPose();
            c.driver = Instantiate(src);
            c.driver.name = name + "Driver";
            c.driver.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var r in c.driver.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var names = new[] { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };
            var nextOf = new Dictionary<string, string> {
                { "Spine", "Spine1" }, { "Spine1", "Spine2" }, { "Spine2", "Neck" }, { "Neck", "Head" },
                { "LeftShoulder", "LeftArm" }, { "LeftArm", "LeftForeArm" }, { "LeftForeArm", "LeftHand" },
                { "RightShoulder", "RightArm" }, { "RightArm", "RightForeArm" }, { "RightForeArm", "RightHand" },
                { "LeftUpLeg", "LeftLeg" }, { "LeftLeg", "LeftFoot" }, { "RightUpLeg", "RightLeg" }, { "RightLeg", "RightFoot" } };
            var all = c.driver.GetComponentsInChildren<Transform>();
            Transform DF(string n) => System.Array.Find(all, x => x.name == n || x.name.EndsWith(":" + n));
            var list = new List<(Transform, Transform, Quaternion, Quaternion)>();
            foreach (var n in names)
            {
                var d = DF(n);
                var t = FindDeep(inst.transform, "mixamorig:" + n);
                if (d == null || t == null) { Debug.LogError(name + " bone missing: " + n); continue; }
                var dr = d.rotation;
                string nx;
                if (nextOf.TryGetValue(n, out nx))
                {
                    var dn = DF(nx); var tn = FindDeep(inst.transform, "mixamorig:" + nx);
                    dr = Quaternion.FromToRotation(dn.position - d.position, tn.position - t.position) * dr;
                }
                list.Add((d, t, dr, t.rotation));
            }
            c.map = list.ToArray();
            c.dHips = c.map[0].d;
            c.dHipRest = c.dHips.position.y;
            // root motion per loop (in our metres), and the walk's ground speed from a planted foot
            c.clip.SampleAnimation(c.driver, 0f); var z0 = c.dHips.position;
            c.clip.SampleAnimation(c.driver, c.clip.length * 0.999f); var z1 = c.dHips.position;
            float k = hipHeight / Mathf.Max(1e-4f, c.dHipRest);
            float loopDist = (z1 - z0).magnitude * k;
            c.speed = 1f;
            if (name == "walking")
            {
                const int N = 120;
                var zs = new float[N];
                for (int i = 0; i < N; i++) { PoseClip(c, c.clip.length * i / N); zs[i] = lFoot.position.z - hips.position.z; }
                var v = new float[N];
                float dt = c.clip.length / N;
                for (int i = 0; i < N; i++) v[i] = (zs[(i + 1) % N] - zs[i]) / dt;
                System.Array.Sort(v);
                c.speed = -v[N / 5];
            }
            Debug.Log($"CLIP {name}: {c.clip.length:F2}s, root moves {loopDist:F2} m/loop, speed {c.speed:F2}, driver hips {c.dHipRest:F3}");
            ResetPose();
            holder.transform.rotation = keepRot; holder.transform.position = keepPos;
            return c;
        }

        // poses the clip at `time`; returns the hips' height as a fraction of the standing hip height
        float PoseClip(ClipDrv c, float time)
        {
            if (c.clip == null) { ResetPose(); return 1f; }
            c.clip.SampleAnimation(c.driver, Mathf.Repeat(time, c.clip.length));
            ResetPose();
            foreach (var m in c.map)
                m.t.rotation = holder.transform.rotation * (m.d.rotation * Quaternion.Inverse(m.dRest)) * m.tRest;
            return c.dHips.position.y / Mathf.Max(1e-4f, c.dHipRest);
        }
    }
}
