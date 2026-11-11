// "How a Well Gets Dug (and Stays Clean)": the whole video as a pure function of time.
// Evaluate(t) puts every object in its state at second t, so any frame renders on its own and
// the scene follows the voice: beat times come from docs/scenes/well_words.json (Tw(sentence, word)).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class WellScene : MonoBehaviour
{
    // ------------------------------------------------------------------ timing data
    [Serializable] public class WordRec { public string w; public float start; public float end; }
    [Serializable] public class SentRec { public int i; public string text; public float start; public float end; public WordRec[] words; }
    [Serializable] public class Doc { public float duration; public float speech_end; public SentRec[] sentences; }

    public Doc doc;
    public float Duration { get { return doc.duration; } }

    static string Norm(string s)
    {
        var sb = new StringBuilder();
        foreach (char ch in s.ToLowerInvariant()) if (char.IsLetterOrDigit(ch) || ch == '\'') sb.Append(ch);
        return sb.ToString();
    }

    // start (or end) time of the occ-th occurrence of `word` in sentence `sent` (1-based)
    float Tw(int sent, string word, int occ = 0, bool end = false)
    {
        var s = doc.sentences[sent - 1];
        string key = Norm(word);
        int n = 0;
        foreach (var w in s.words)
        {
            if (Norm(w.w) == key)
            {
                if (n == occ) return end ? w.end : w.start;
                n++;
            }
        }
        Debug.LogError("Tw: '" + word + "' not found in sentence " + sent);
        return s.start;
    }

    // ------------------------------------------------------------------ models (set by the editor script)
    public GameObject mHouse, mTree, mRig, mPump, mCap, mCasing, mScreen;

    // ------------------------------------------------------------------ palette
    static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }
    static readonly Dictionary<string, string> Pal = new Dictionary<string, string> {
        {"wall","#FFF3DA"},{"roof","#1B7F74"},{"roof_trim","#2AA396"},{"door","#F08A3C"},{"window","#8FD9FF"},
        {"chimney","#B8B2A7"},{"trunk","#8E5530"},{"leaves","#4DB04A"},{"leaves2","#6CC04A"},
        {"rig_yellow","#FFB02E"},{"rig_dark","#35506B"},{"rig_teal","#2AA396"},{"steel","#AEB8C4"},
        {"steel_dark","#6E7B8F"},{"pump_body","#35506B"},{"pump_band","#F08A3C"},{"cap","#8FA0B3"},
    };

    Shader shStd, shGround, shUnlit, shUnlitTex;
    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();

    Material Std(string hex, float smooth = 0.12f)
    {
        string k = hex + smooth;
        Material m;
        if (!matCache.TryGetValue(k, out m))
        {
            m = new Material(shStd);
            m.color = C(hex);
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", 0f);
            matCache[k] = m;
        }
        return m;
    }

    Material Flat(string hex)
    {
        var m = new Material(shUnlit);
        m.color = C(hex);
        return m;
    }

    Material Alpha(string hex, float a)
    {
        var m = new Material(shStd);
        m.SetFloat("_Mode", 3);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        var c = C(hex); c.a = a;
        m.color = c;
        m.SetFloat("_Glossiness", 0.6f);
        return m;
    }

    // ------------------------------------------------------------------ small helpers
    static float P(float t, float a, float b) { return Mathf.Clamp01((t - a) / Mathf.Max(1e-4f, b - a)); }
    static float E(float x) { return x * x * (3f - 2f * x); }
    static float PE(float t, float a, float b) { return E(P(t, a, b)); }
    static float Back(float x) { float c = 1.9f; x = Mathf.Clamp01(x); return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }
    static float Frac(float x) { return x - Mathf.Floor(x); }
    static float Hash(int i, int k) { return Frac(Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f); }

    Transform stage;

    GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        var col = g.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        g.transform.SetParent(parent == null ? stage : parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    static void Show(GameObject g, bool on) { if (g.activeSelf != on) g.SetActive(on); }

    GameObject Place(GameObject model, string name, Vector3 pos)
    {
        var g = Instantiate(model);
        g.name = name;
        g.transform.SetParent(stage, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // FBX front (-Y in Blender) lands on +Z: face the camera
        ApplyPalette(g);
        return g;
    }

    void ApplyPalette(GameObject g)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                string key = ms[i] != null ? ms[i].name.Replace(" (Instance)", "") : "";
                string hex;
                if (Pal.TryGetValue(key, out hex)) ms[i] = Std(hex, 0.15f);
            }
            r.sharedMaterials = ms;
        }
    }

    static Mesh ConeMesh(float r, float h, int seg)
    {
        var v = new List<Vector3>(); var tri = new List<int>();
        v.Add(new Vector3(0, h, 0));
        for (int i = 0; i <= seg; i++) { float a = i * Mathf.PI * 2f / seg; v.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
        v.Add(Vector3.zero);
        for (int i = 1; i <= seg; i++) { tri.Add(0); tri.Add(i + 1); tri.Add(i); }
        int c = v.Count - 1;
        for (int i = 1; i <= seg; i++) { tri.Add(c); tri.Add(i); tri.Add(i + 1); }
        var m = new Mesh { vertices = v.ToArray(), triangles = tri.ToArray() };
        m.RecalculateNormals();
        return m;
    }

    // convex polygon profile in XY extruded along Z (flat shaded)
    static Mesh PrismMesh(Vector2[] prof, float z0, float z1)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        int n = prof.Length;
        int b = v.Count;
        for (int i = 0; i < n; i++) v.Add(new Vector3(prof[i].x, prof[i].y, z0));
        for (int i = 1; i < n - 1; i++) { t.Add(b); t.Add(b + i + 1); t.Add(b + i); }
        b = v.Count;
        for (int i = 0; i < n; i++) v.Add(new Vector3(prof[i].x, prof[i].y, z1));
        for (int i = 1; i < n - 1; i++) { t.Add(b); t.Add(b + i); t.Add(b + i + 1); }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            b = v.Count;
            v.Add(new Vector3(prof[i].x, prof[i].y, z0)); v.Add(new Vector3(prof[j].x, prof[j].y, z0));
            v.Add(new Vector3(prof[j].x, prof[j].y, z1)); v.Add(new Vector3(prof[i].x, prof[i].y, z1));
            t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
        }
        var m = new Mesh { vertices = v.ToArray(), triangles = t.ToArray() };
        m.RecalculateNormals();
        return m;
    }

    // ------------------------------------------------------------------ layout constants
    const float SL = -3.5f, SR = 3.5f, SD = 3f;          // slab x range and depth
    const float HX = -1.0f, HW = 0.45f, HZ = 0.225f;     // hole centre x, half width, centre z (notch is z 0..0.45)
    const float NOTCH = 0.45f;
    const float SEAL_CX = (0.22f + 0.45f) / 2f, SEAL_W = 0.45f - 0.22f - 0.02f;   // seal strip centre offset / width: the gap between casing (r 0.22) and hole wall
    static readonly float[] LayerY = { 0f, -0.3f, -1.6f, -3.0f, -3.9f, -5.2f, -7.2f };
    static readonly string[] LayerCol = { "#6CC24A", "#8E5530", "#C0692B", "#E7C777", "#86C5D6", "#8693A8" };
    const float HOLE_BOTTOM = -5.6f;
    const float CASING_LEN = 4.35f, SCREEN_LEN = 1.4f, CASING_TOP_FINAL = 0.45f;
    const float SEAL_BOTTOM = -3.9f;
    const float TRENCH_Y = -0.95f, HOUSE_X = 2.25f, RISER_X = 2.1f;

    // ------------------------------------------------------------------ scene objects
    Camera cam;
    GameObject skyQuad;
    Transform[] clouds;
    float[] cloudX0;

    GameObject[] layerBox;
    GameObject voidBack, voidL, voidR, voidFloor, trenchBack, trenchFloor, riserBack;
    GameObject rig, rod, bit;
    readonly List<GameObject> flights = new List<GameObject>();
    GameObject casing, screen, pump, pumpPipe, cap, house, glass, glassWater, tapPipe, tapSpout;
    GameObject sealClayL, sealClayR, sealCemL, sealCemR, casingWater, chlorine;
    GameObject mound1, mound2, puddle, measure, measureTop, measureBot;
    GameObject trenchPipe, riserPipe, pipeInCasing;
    GameObject[] aqDrops, chips, caveBlocks, rainDrops, cemDrops, inDrops, sandGrains, flowDrops, germs, sparkles, tapDrops;
    GameObject[] cracks;
    Material crackDry, crackWet;
    GameObject[] arrows;
    Material screenMat, windowLit;
    readonly List<Renderer> houseWindows = new List<Renderer>();

    class Label { public GameObject root; public TextMesh tm; public GameObject[] parts; public float t0, t1; public Vector3 pos; public string text; }
    readonly List<Label> labels = new List<Label>();

    // ------------------------------------------------------------------ build
    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<Doc>(File.ReadAllText(wordsJson));
        shStd = Shader.Find("Standard");
        shGround = Shader.Find("WMI/CutawayGround");
        shUnlit = Shader.Find("Unlit/Color");
        shUnlitTex = Shader.Find("Unlit/Texture");
        if (shGround == null) Debug.LogError("shader WMI/CutawayGround not found");

        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = C("#8497B2");
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        QualitySettings.shadowDistance = 60f;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowCascades = 4;

        var lg = new GameObject("Sun");
        lg.transform.SetParent(stage, false);
        lg.transform.rotation = Quaternion.Euler(42f, 24f, 0f);
        var light = lg.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = C("#FFF1D6");
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.55f;

        var cg = new GameObject("Cam");
        cg.transform.SetParent(stage, false);
        cam = cg.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = C("#FFE873");
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 150f;

        BuildSky();
        BuildGround();
        BuildHole();
        BuildModels();
        BuildFx();
        BuildLabels();
    }

    public Camera Cam { get { return cam; } }

    void BuildSky()
    {
        var tex = new Texture2D(1, 256, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
        Color top = C("#FFEA7A"), bot = C("#FFB04A");
        for (int y = 0; y < 256; y++) tex.SetPixel(0, y, Color.Lerp(bot, top, y / 255f));
        tex.Apply();
        var m = new Material(shUnlitTex) { mainTexture = tex };
        skyQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        skyQuad.name = "Sky";
        DestroyImmediate(skyQuad.GetComponent<Collider>());
        skyQuad.transform.SetParent(cam.transform, false);
        skyQuad.GetComponent<Renderer>().sharedMaterial = m;
        skyQuad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var cm = Flat("#FFF8E6");
        clouds = new Transform[7];
        cloudX0 = new float[7];
        float[,] cp = { { -5.5f, 5.5f, 9f }, { 4.5f, 7.5f, 11f }, { -2f, 9.5f, 12f }, { 6f, 2.5f, 10f }, { -6.5f, -9.5f, 9f }, { 3.5f, -10f, 10f }, { -1f, -12f, 11f } };
        for (int i = 0; i < 7; i++)
        {
            var root = new GameObject("Cloud" + i).transform;
            root.SetParent(stage, false);
            root.position = new Vector3(cp[i, 0], cp[i, 1], cp[i, 2]);
            float s = 1.1f + 0.35f * (i % 3);
            Prim(PrimitiveType.Sphere, "a", root, new Vector3(0, 0, 0), new Vector3(2.4f, 1.5f, 1f) * s, cm).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Prim(PrimitiveType.Sphere, "b", root, new Vector3(1.3f * s, -0.2f * s, 0), new Vector3(1.8f, 1.2f, 1f) * s, cm).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Prim(PrimitiveType.Sphere, "c", root, new Vector3(-1.3f * s, -0.25f * s, 0), new Vector3(1.7f, 1.1f, 1f) * s, cm).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            clouds[i] = root;
            cloudX0[i] = root.position.x;
        }
    }

    void BuildGround()
    {
        layerBox = new GameObject[6];
        for (int i = 0; i < 6; i++)
        {
            var m = new Material(shGround);
            m.color = C(LayerCol[i]);
            float y0 = LayerY[i], y1 = LayerY[i + 1];
            layerBox[i] = Prim(PrimitiveType.Cube, "layer" + i, null,
                new Vector3(0, (y0 + y1) / 2f, SD / 2f), new Vector3(SR - SL, y0 - y1, SD), m);
        }

        // pebbles / gravel on the cut face (not over the hole, trench or riser)
        var rnd = new System.Random(7);
        string[] pcol = { "#6E4326", "#A9561F", "#CDB26A", "#B9DDE6", "#6A768C" };
        int[] pcount = { 0, 22, 16, 14, 26, 8 };
        for (int L = 1; L < 6; L++)
        {
            for (int k = 0; k < pcount[L]; k++)
            {
                float x = (float)(rnd.NextDouble() * 6.6 - 3.3);
                float y = Mathf.Lerp(LayerY[L] - 0.08f, LayerY[L + 1] + 0.08f, (float)rnd.NextDouble());
                if (x > HX - 0.55f && x < HX + 0.55f) continue;
                if (y < TRENCH_Y + 0.25f && y > TRENCH_Y - 0.25f && x > HX && x < 2.5f) continue;
                if (x > 1.85f && x < 2.4f && y > -1.3f) continue;
                float s = 0.05f + (float)rnd.NextDouble() * (L == 4 ? 0.12f : 0.07f);
                string col = L == 4 && k % 2 == 0 ? "#8896A8" : pcol[L - 1 < pcol.Length ? L - 1 : 0];
                Prim(PrimitiveType.Sphere, "pebble", null, new Vector3(x, y, -0.012f), new Vector3(s * 1.5f, s, 0.02f), Std(col, 0.05f));
            }
        }

        // cracks in the rock (dark when dry, turn blue at "rock")
        crackDry = Flat("#4B5668");
        crackWet = Flat("#59C9F5");
        var cr = new List<GameObject>();
        float[,] seeds = { { -3.0f, -5.4f }, { -2.2f, -6.6f }, { -0.4f, -5.5f }, { 0.4f, -6.4f }, { 1.4f, -5.6f }, { 2.4f, -6.7f }, { 3.0f, -5.5f }, { -1.0f, -6.9f } };
        for (int i = 0; i < seeds.GetLength(0); i++)
        {
            float x = seeds[i, 0], y = seeds[i, 1];
            for (int s = 0; s < 4; s++)
            {
                float ang = (Hash(i, s) - 0.5f) * 1.6f + (s % 2 == 0 ? -0.9f : 0.9f);
                float len = 0.28f + 0.12f * Hash(i, s + 9);
                var seg = Prim(PrimitiveType.Cube, "crack", null, new Vector3(x + Mathf.Sin(ang) * len / 2, y - Mathf.Cos(ang) * len / 2, -0.015f), new Vector3(0.035f, len, 0.02f), crackDry);
                seg.transform.rotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
                x += Mathf.Sin(ang) * len; y -= Mathf.Cos(ang) * len;
                cr.Add(seg);
            }
        }
        cracks = cr.ToArray();

        // slope wedges around the future well (grow at "slopes")
        var gm = new Material(shStd) { color = C("#7AD04F") };
        gm.SetFloat("_Glossiness", 0.05f);
        mound1 = new GameObject("moundL"); mound2 = new GameObject("moundR");
        foreach (var g in new[] { mound1, mound2 })
        {
            g.transform.SetParent(stage, false);
            g.AddComponent<MeshRenderer>().sharedMaterial = gm;
        }
        mound1.AddComponent<MeshFilter>().sharedMesh = PrismMesh(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1) }, 0f, 1.3f);   // left wedge: apex on its right end (next to the well)
        mound2.AddComponent<MeshFilter>().sharedMesh = PrismMesh(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) }, 0f, 1.3f);   // right wedge: apex on its left end
    }

    void BuildHole()
    {
        var back = Std("#2B1A12", 0f); var wall = Std("#7A4A2A", 0f); var floor = Std("#4A2E1B", 0f);
        voidBack = Prim(PrimitiveType.Cube, "voidBack", null, Vector3.zero, Vector3.one, back);
        voidL = Prim(PrimitiveType.Cube, "voidL", null, Vector3.zero, Vector3.one, wall);
        voidR = Prim(PrimitiveType.Cube, "voidR", null, Vector3.zero, Vector3.one, wall);
        voidFloor = Prim(PrimitiveType.Cube, "voidFloor", null, Vector3.zero, Vector3.one, floor);
        trenchBack = Prim(PrimitiveType.Cube, "trenchBack", null, Vector3.zero, Vector3.one, back);
        trenchFloor = Prim(PrimitiveType.Cube, "trenchFloor", null, Vector3.zero, Vector3.one, floor);
        riserBack = Prim(PrimitiveType.Cube, "riserBack", null, Vector3.zero, Vector3.one, back);
    }

    void BuildModels()
    {
        house = Place(mHouse, "house", new Vector3(HOUSE_X - 0.15f, 0f, 1.5f));
        foreach (var r in house.GetComponentsInChildren<Renderer>()) if (r.gameObject.name.StartsWith("window")) houseWindows.Add(r);
        windowLit = Std("#FFE873", 0.1f);
        Place(mTree, "treeA", new Vector3(3.05f, 0f, 2.1f));
        Place(mTree, "treeB", new Vector3(-3.0f, 0f, 2.3f)).transform.localScale = Vector3.one * 1.15f;
        Place(mTree, "treeC", new Vector3(0.55f, 0f, 2.5f)).transform.localScale = Vector3.one * 0.85f;

        rig = Place(mRig, "rig", new Vector3(HX, 0f, HZ));
        casing = Place(mCasing, "casing", new Vector3(HX, CASING_TOP_FINAL, HZ));
        screen = Place(mScreen, "screen", new Vector3(HX, CASING_TOP_FINAL - CASING_LEN, HZ));
        pump = Place(mPump, "pump", new Vector3(HX, 0f, HZ));
        cap = Place(mCap, "cap", new Vector3(HX, CASING_TOP_FINAL, HZ));
        screenMat = new Material(shStd) { color = C("#FFB02E") };
        foreach (var r in screen.GetComponentsInChildren<Renderer>()) if (r.gameObject.name.StartsWith("band")) r.sharedMaterial = screenMat;

        // drill rod + bit + spinning flights
        var steel = Std("#AEB8C4", 0.3f);
        rod = Prim(PrimitiveType.Cylinder, "rod", null, Vector3.zero, Vector3.one, steel);
        bit = Prim(PrimitiveType.Cylinder, "bit", null, Vector3.zero, Vector3.one, Std("#35506B", 0.2f));
        for (int i = 0; i < 12; i++) flights.Add(Prim(PrimitiveType.Cube, "flight", null, Vector3.zero, new Vector3(0.3f, 0.05f, 0.05f), Std("#FFB02E", 0.2f)));

        pumpPipe = Prim(PrimitiveType.Cylinder, "pumpPipe", null, Vector3.zero, Vector3.one, Std("#35506B", 0.3f));
        trenchPipe = Prim(PrimitiveType.Cylinder, "trenchPipe", null, Vector3.zero, Vector3.one, Std("#6E7B8F", 0.3f));
        riserPipe = Prim(PrimitiveType.Cylinder, "riserPipe", null, Vector3.zero, Vector3.one, Std("#6E7B8F", 0.3f));
        tapSpout = Prim(PrimitiveType.Cylinder, "spout", null, Vector3.zero, Vector3.one, Std("#6E7B8F", 0.3f));

        // clay + cement seal strips (grow from the bottom)
        sealClayL = Prim(PrimitiveType.Cube, "clayL", null, Vector3.zero, Vector3.one, Std("#D9B27A", 0.05f));
        sealClayR = Prim(PrimitiveType.Cube, "clayR", null, Vector3.zero, Vector3.one, Std("#D9B27A", 0.05f));
        sealCemL = Prim(PrimitiveType.Cube, "cemL", null, Vector3.zero, Vector3.one, Std("#B7BDC4", 0.05f));
        sealCemR = Prim(PrimitiveType.Cube, "cemR", null, Vector3.zero, Vector3.one, Std("#B7BDC4", 0.05f));

        casingWater = Prim(PrimitiveType.Cube, "casingWater", null, Vector3.zero, Vector3.one, Std("#59C9F5", 0.4f));
        chlorine = Prim(PrimitiveType.Cube, "chlorine", null, Vector3.zero, Vector3.one, Std("#B8F24B", 0.4f));

        puddle = Prim(PrimitiveType.Cylinder, "puddle", null, Vector3.zero, Vector3.one, Std("#59C9F5", 0.7f));

        glass = Prim(PrimitiveType.Cylinder, "glass", null, Vector3.zero, Vector3.one, Alpha("#DFF6FF", 0.35f));
        glassWater = Prim(PrimitiveType.Cylinder, "glassWater", null, Vector3.zero, Vector3.one, Std("#59C9F5", 0.6f));

        measure = Prim(PrimitiveType.Cube, "measure", null, Vector3.zero, Vector3.one, Flat("#FFD84D"));
        measureTop = Prim(PrimitiveType.Cube, "measureTop", null, Vector3.zero, Vector3.one, Flat("#FFD84D"));
        measureBot = Prim(PrimitiveType.Cube, "measureBot", null, Vector3.zero, Vector3.one, Flat("#FFD84D"));
    }

    GameObject[] Many(int n, PrimitiveType t, string name, Material m)
    {
        var a = new GameObject[n];
        for (int i = 0; i < n; i++) a[i] = Prim(t, name + i, null, Vector3.zero, Vector3.one, m);
        return a;
    }

    void BuildFx()
    {
        aqDrops = Many(22, PrimitiveType.Sphere, "aq", Std("#59C9F5", 0.7f));
        chips = Many(14, PrimitiveType.Cube, "chip", Std("#8E5530", 0.05f));
        caveBlocks = Many(8, PrimitiveType.Cube, "cave", Std("#8E5530", 0.05f));
        rainDrops = Many(14, PrimitiveType.Sphere, "rain", Std("#7A5A3A", 0.5f));
        cemDrops = Many(8, PrimitiveType.Cube, "cemdrop", Std("#B7BDC4", 0.05f));
        inDrops = Many(14, PrimitiveType.Sphere, "in", Std("#59C9F5", 0.7f));
        sandGrains = Many(9, PrimitiveType.Sphere, "grain", Std("#E0B85A", 0.05f));
        flowDrops = Many(12, PrimitiveType.Sphere, "flow", Std("#59C9F5", 0.7f));
        tapDrops = Many(3, PrimitiveType.Sphere, "tapdrop", Std("#59C9F5", 0.7f));
        germs = Many(6, PrimitiveType.Sphere, "germ", Std("#B5314F", 0.3f));
        sparkles = Many(10, PrimitiveType.Cube, "spark", Flat("#FFFFFF"));

        // arrows: shaft + cone, pointing DOWN (-Y) in local space
        arrows = new GameObject[5];
        var am = Std("#F08A3C", 0.2f);
        var cone = ConeMesh(0.2f, 0.34f, 16);
        for (int i = 0; i < arrows.Length; i++)
        {
            var root = new GameObject("arrow" + i);
            root.transform.SetParent(stage, false);
            var shaft = Prim(PrimitiveType.Cube, "shaft", root.transform, new Vector3(0, 0.34f, 0), new Vector3(0.12f, 0.5f, 0.12f), am);
            var head = new GameObject("head");
            head.transform.SetParent(root.transform, false);
            head.transform.localRotation = Quaternion.Euler(180, 0, 0);
            head.transform.localPosition = new Vector3(0, 0.34f, 0);
            head.AddComponent<MeshFilter>().sharedMesh = cone;
            head.AddComponent<MeshRenderer>().sharedMaterial = am;
            arrows[i] = root;
        }
        for (int i = 0; i < germs.Length; i++)
        {
            Prim(PrimitiveType.Sphere, "eye", germs[i].transform, new Vector3(-0.22f, 0.2f, -0.42f), new Vector3(0.28f, 0.28f, 0.28f), Flat("#FFFFFF"));
            Prim(PrimitiveType.Sphere, "eye", germs[i].transform, new Vector3(0.22f, 0.2f, -0.42f), new Vector3(0.28f, 0.28f, 0.28f), Flat("#FFFFFF"));
        }
    }

    // ------------------------------------------------------------------ labels
    void AddLabel(string text, string pillHex, string textHex, Vector3 pos, float t0, float t1)
    {
        var L = new Label { text = text, pos = pos, t0 = t0, t1 = t1 };
        L.root = new GameObject("label_" + text);
        L.root.transform.SetParent(stage, false);
        float w = 0.22f * text.Length + 0.55f, h = 0.5f;
        var pm = Flat(pillHex);
        var parts = new List<GameObject>();
        parts.Add(Prim(PrimitiveType.Cube, "mid", L.root.transform, Vector3.zero, new Vector3(w - h, h, 0.02f), pm));
        foreach (float sx in new[] { -1f, 1f })
        {
            var cyl = Prim(PrimitiveType.Cylinder, "end", L.root.transform, new Vector3(sx * (w - h) / 2f, 0, 0), new Vector3(h, 0.01f, h), pm);
            cyl.transform.localRotation = Quaternion.Euler(90, 0, 0);
            parts.Add(cyl);
        }
        L.parts = parts.ToArray();
        var tg = new GameObject("text");
        tg.transform.SetParent(L.root.transform, false);
        tg.transform.localPosition = new Vector3(0, 0, -0.03f);
        L.tm = tg.AddComponent<TextMesh>();
        L.tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tg.GetComponent<MeshRenderer>().sharedMaterial = L.tm.font.material;
        L.tm.text = text;
        L.tm.fontSize = 100;
        L.tm.characterSize = 0.03f;
        L.tm.fontStyle = FontStyle.Bold;
        L.tm.anchor = TextAnchor.MiddleCenter;
        L.tm.alignment = TextAlignment.Center;
        L.tm.color = C(textHex);
        foreach (var r in L.root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        labels.Add(L);
    }

    void BuildLabels()
    {
        string W = "#FFFFFF", N = "#0D1B2A", Y = "#FFD84D", G = "#B8F24B";
        AddLabel("SAND", W, N, new Vector3(2.45f, -3.45f, -0.3f), Tw(1, "sand"), Tw(1, "A"));
        AddLabel("GRAVEL", W, N, new Vector3(2.45f, -4.55f, -0.3f), Tw(1, "gravel"), Tw(1, "A"));
        AddLabel("ROCK", W, N, new Vector3(2.45f, -6.2f, -0.3f), Tw(1, "rock"), Tw(1, "A"));
        AddLabel("300 FT", Y, N, new Vector3(-2.7f, -2.8f, -0.3f), Tw(2, "hundreds"), Tw(3, "But") + 0.3f);
        AddLabel("CASING", W, N, new Vector3(-0.05f, -0.7f, -0.3f), Tw(3, "casing"), Tw(4, "Now") + 0.1f);
        AddLabel("SEAL", W, N, new Vector3(-0.1f, -1.9f, -0.3f), Tw(4, "cement"), Tw(4, "outside"));
        AddLabel("SEALED", G, N, new Vector3(-0.1f, -1.9f, -0.3f), Tw(4, "outside"), Tw(5, "At") + 0.2f);
        AddLabel("SCREEN", W, N, new Vector3(-0.05f, -4.7f, -0.3f), Tw(5, "screen"), Tw(6, "A"));
        AddLabel("PUMP", W, N, new Vector3(-0.05f, -3.0f, -0.3f), Tw(6, "pump"), Tw(6, "pushes") + 0.4f);
        AddLabel("CAP", W, N, new Vector3(0.2f, 1.05f, -0.3f), Tw(7, "cap"), Tw(8, "Last"));
        AddLabel("CHLORINE", G, N, new Vector3(0.2f, 1.2f, -0.3f), Tw(8, "chlorine"), Tw(8, "chlorine") + 1.9f);
        AddLabel("GERM-FREE", G, N, new Vector3(1.55f, 2.5f, -0.3f), Tw(8, "germ-free"), doc.duration + 5f);
    }

    // ------------------------------------------------------------------ camera
    struct Key { public float t, x, y, z; public Key(float t, float x, float y, float z) { this.t = t; this.x = x; this.y = y; this.z = z; } }
    Key[] keys;

    void BuildCamKeys()
    {
        keys = new[] {
            new Key(0f, 0f, -2.6f, 0.93f),
            new Key(Tw(1, "water") - 0.3f, 0f, -3.2f, 0.97f),
            new Key(Tw(1, "reach", 0, true), -0.6f, -2.2f, 1.08f),
            new Key(Tw(2, "drill"), -1.0f, -1.5f, 1.3f),
            new Key(Tw(2, "bores") + 0.4f, -1.0f, -2.3f, 1.4f),
            new Key(Tw(2, "feet", 0, true), -1.0f, -2.6f, 1.25f),
            new Key(Tw(3, "But"), -1.0f, -1.6f, 1.45f),
            new Key(Tw(3, "So"), -1.0f, -1.1f, 1.6f),
            new Key(Tw(3, "walls", 0, true), -1.0f, -1.8f, 1.5f),
            new Key(Tw(4, "Now") + 0.6f, -1.0f, -0.7f, 2.2f),
            new Key(Tw(4, "filled") - 0.2f, -1.0f, -1.5f, 1.9f),
            new Key(Tw(4, "so"), -1.0f, -0.9f, 2.1f),
            new Key(Tw(4, "outside", 0, true), -1.0f, -2.2f, 1.7f),
            new Key(Tw(5, "At"), -1.0f, -4.5f, 2.3f),
            new Key(Tw(5, "out", 0, true), -1.0f, -4.5f, 2.4f),
            new Key(Tw(6, "A") + 0.2f, 0.4f, -2.2f, 1.4f),
            new Key(Tw(6, "home", 0, true), 0.6f, -1.2f, 1.3f),
            new Key(Tw(7, "On"), -0.6f, 0.2f, 2.0f),
            new Key(Tw(7, "it", 0, true), -0.8f, 0.1f, 1.9f),
            new Key(Tw(8, "Last") + 0.2f, -0.6f, -0.8f, 1.7f),
            new Key(Tw(8, "glass"), 1.0f, 0.5f, 2.1f),
            new Key(doc.duration, 1.1f, 0.6f, 2.3f),
        };
    }

    float camZoom = 1f;

    void EvalCamera(float t)
    {
        if (keys == null) BuildCamKeys();
        Key a = keys[0], b = keys[keys.Length - 1];
        for (int i = 0; i < keys.Length - 1; i++) { if (t >= keys[i].t && t < keys[i + 1].t) { a = keys[i]; b = keys[i + 1]; break; } if (t >= keys[keys.Length - 1].t) { a = b = keys[keys.Length - 1]; } }
        float u = (a.t == b.t) ? 1f : E(Mathf.Clamp01((t - a.t) / (b.t - a.t)));
        float fx = Mathf.Lerp(a.x, b.x, u), fy = Mathf.Lerp(a.y, b.y, u), zm = Mathf.Lerp(a.z, b.z, u);
        camZoom = zm;
        float dist = 18f / zm;
        float cave = Tw(3, "cave");
        float sh = Mathf.Exp(-(t - cave) * 5f) * (t >= cave ? 1f : 0f);
        float shx = 0.06f * Mathf.Sin(t * 70f) * sh, shy = 0.05f * Mathf.Cos(t * 83f) * sh;
        Vector3 focus = new Vector3(fx + shx, fy + shy, 0.4f);
        cam.transform.position = focus + new Vector3(0.0f, dist * 0.3f, -dist);
        cam.transform.LookAt(focus);

        float depth = 60f;
        float hh = 2f * depth * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        skyQuad.transform.localPosition = new Vector3(0, 0, depth);
        skyQuad.transform.localScale = new Vector3(hh * cam.aspect * 1.4f, hh * 1.4f, 1f);
        for (int i = 0; i < clouds.Length; i++)
        {
            var p = clouds[i].position;
            p.x = cloudX0[i] + t * 0.12f * (1 + i % 3 * 0.3f);
            clouds[i].position = p;
        }
    }

    // ------------------------------------------------------------------ the video
    public void Evaluate(float t)
    {
        EvalCamera(t);
        EvalGroundAndHole(t);
        EvalDrill(t);
        EvalCasing(t);
        EvalSeal(t);
        EvalScreenAndWater(t);
        EvalPump(t);
        EvalSurface(t);
        EvalClean(t);
        EvalLabels(t);
    }

    // arrow helper: tip at `tip`, pointing along `dir` (unit), size s, bobbing
    void Arrow(int i, bool on, Vector3 tip, Vector3 dir, float s, float t)
    {
        var a = arrows[i];
        Show(a, on);
        if (!on) return;
        float bob = 0.12f * Mathf.Sin(t * 7f + i);
        a.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir);
        a.transform.localScale = Vector3.one * s;
        a.transform.position = tip - dir * bob;
    }

    void EvalGroundAndHole(float t)
    {
        // S1: aquifer drops + highlights
        float aq0 = Tw(1, "water"), aqEnd = Tw(3, "But");
        float aqShow = PE(t, aq0, aq0 + 0.5f) * (1f - PE(t, aqEnd, aqEnd + 0.4f));
        for (int i = 0; i < aqDrops.Length; i++)
        {
            var g = aqDrops[i];
            Show(g, aqShow > 0.01f);
            float x = Mathf.Lerp(-3.2f, 3.2f, Hash(i, 1)), y = Mathf.Lerp(-4.0f, -5.1f, Hash(i, 2));
            if (Mathf.Abs(x - HX) < 0.7f) x += 1.6f;
            y += 0.06f * Mathf.Sin(t * 3f + i * 1.7f);
            g.transform.position = new Vector3(x, y, -0.04f);
            g.transform.localScale = Vector3.one * 0.11f * aqShow * (0.8f + 0.25f * Mathf.Sin(t * 4f + i));
        }
        float rockT = Tw(1, "rock");
        bool wet = t > rockT;
        foreach (var c in cracks) c.GetComponent<Renderer>().sharedMaterial = wet ? crackWet : crackDry;

        // hole rectangle (cut-out) and the void boxes behind it
        float drillA = Tw(2, "bores"), drillB = Tw(2, "feet", 0, true) + 0.1f;
        float holeBottom = Mathf.Lerp(0f, HOLE_BOTTOM, PE(t, drillA, drillB));
        bool holeOn = holeBottom < -0.03f;
        float x0 = HX - HW, x1 = HX + HW;
        Vector4 cut0 = holeOn ? new Vector4(x0, x1, holeBottom, 0.02f) : Vector4.zero;

        // trench + riser (S6)
        float tr0 = Tw(6, "pushes") - 0.5f;
        float trenchX1 = Mathf.Lerp(HX + HW, RISER_X + 0.15f, PE(t, tr0, tr0 + 0.55f));
        bool trenchOn = t > tr0;
        float riseK = PE(t, tr0 + 0.5f, tr0 + 0.8f);
        Vector4 cut1 = trenchOn ? new Vector4(HX + HW - 0.01f, trenchX1, TRENCH_Y - 0.17f, TRENCH_Y + 0.17f) : Vector4.zero;
        Vector4 cut2 = trenchOn ? new Vector4(RISER_X - 0.14f, RISER_X + 0.14f, TRENCH_Y - 0.17f, Mathf.Lerp(TRENCH_Y, 0.02f, riseK)) : Vector4.zero;
        Shader.SetGlobalVector("_Cut0", cut0);
        Shader.SetGlobalVector("_Cut1", cut1);
        Shader.SetGlobalVector("_Cut2", cut2);
        Shader.SetGlobalFloat("_CutZ", NOTCH);

        Show(voidBack, holeOn); Show(voidL, holeOn); Show(voidR, holeOn); Show(voidFloor, holeOn);
        if (holeOn)
        {
            float d = -holeBottom;
            voidBack.transform.position = new Vector3(HX, holeBottom / 2f, NOTCH + 0.02f);
            voidBack.transform.localScale = new Vector3(2 * HW, d, 0.04f);
            voidL.transform.position = new Vector3(x0 + 0.01f, holeBottom / 2f, NOTCH / 2f);
            voidL.transform.localScale = new Vector3(0.02f, d, NOTCH);
            voidR.transform.position = new Vector3(x1 - 0.01f, holeBottom / 2f, NOTCH / 2f);
            voidR.transform.localScale = new Vector3(0.02f, d, NOTCH);
            voidFloor.transform.position = new Vector3(HX, holeBottom - 0.01f, NOTCH / 2f);
            voidFloor.transform.localScale = new Vector3(2 * HW, 0.02f, NOTCH);
        }
        Show(trenchBack, trenchOn); Show(trenchFloor, trenchOn); Show(riserBack, trenchOn);
        if (trenchOn)
        {
            float tw = trenchX1 - (HX + HW);
            trenchBack.transform.position = new Vector3((HX + HW + trenchX1) / 2f, TRENCH_Y, NOTCH + 0.02f);
            trenchBack.transform.localScale = new Vector3(tw, 0.34f, 0.04f);
            trenchFloor.transform.position = new Vector3((HX + HW + trenchX1) / 2f, TRENCH_Y - 0.17f, NOTCH / 2f);
            trenchFloor.transform.localScale = new Vector3(tw, 0.02f, NOTCH);
            float top = Mathf.Lerp(TRENCH_Y, 0.02f, riseK);
            riserBack.transform.position = new Vector3(RISER_X, (TRENCH_Y - 0.17f + top) / 2f, NOTCH + 0.02f);
            riserBack.transform.localScale = new Vector3(0.28f, top - (TRENCH_Y - 0.17f), 0.04f);
        }

        // S1 arrow: "reach" -> pointing down at the future hole
        float rA = Tw(1, "reach") - 0.1f, rB = Tw(2, "drill");
        Arrow(0, t > rA && t < rB + 0.2f, new Vector3(HX, 1.0f, HZ), Vector3.down, 1.2f * PE(t, rA, rA + 0.25f), t);

        // 300 ft measure line
        float mk = PE(t, Tw(2, "hundreds") - 0.2f, Tw(2, "hundreds") + 0.5f) * (1f - PE(t, Tw(3, "But"), Tw(3, "But") + 0.4f));
        Show(measure, mk > 0.01f); Show(measureTop, mk > 0.01f); Show(measureBot, mk > 0.01f);
        if (mk > 0.01f)
        {
            float mx = HX - 0.85f, len = 5.6f * mk;
            measure.transform.position = new Vector3(mx, -len / 2f, -0.03f);
            measure.transform.localScale = new Vector3(0.05f, len, 0.02f);
            measureTop.transform.position = new Vector3(mx, 0f, -0.03f);
            measureTop.transform.localScale = new Vector3(0.28f, 0.05f, 0.02f);
            measureBot.transform.position = new Vector3(mx, -len, -0.03f);
            measureBot.transform.localScale = new Vector3(0.28f, 0.05f, 0.02f);
        }
    }

    void EvalDrill(float t)
    {
        float inA = Tw(2, "drill") - 0.3f, drillA = Tw(2, "bores"), drillB = Tw(2, "feet", 0, true) + 0.1f;
        float leave = Tw(4, "Now") + 0.1f;
        float rigK = Back(P(t, inA, inA + 0.55f)) * (1f - PE(t, leave, leave + 0.8f));
        Show(rig, rigK > 0.01f);
        rig.transform.localScale = Vector3.one * Mathf.Max(rigK, 0.001f);
        rig.transform.position = new Vector3(HX - 3.5f * PE(t, leave, leave + 0.8f), 0f, HZ);

        float retractA = Tw(3, "But") - 0.1f;
        float holeBottom = Mathf.Lerp(0f, HOLE_BOTTOM, PE(t, drillA, drillB));
        float bitY = holeBottom + 0.3f * (1f - PE(t, drillA, drillA + 0.5f)) + 5.0f * PE(t, retractA, retractA + 0.9f);
        bool rodOn = rigK > 0.05f && t < retractA + 1.2f;
        float top = 2.4f * rigK;
        Show(rod, rodOn); Show(bit, rodOn);
        foreach (var f in flights) Show(f, rodOn);
        if (rodOn)
        {
            float len = top - bitY;
            rod.transform.position = new Vector3(HX, (top + bitY) / 2f, HZ);
            rod.transform.localScale = new Vector3(0.12f, len / 2f, 0.12f);
            bit.transform.position = new Vector3(HX, bitY - 0.07f, HZ);
            bit.transform.localScale = new Vector3(0.24f, 0.07f, 0.24f);
            for (int i = 0; i < flights.Count; i++)
            {
                float y = bitY + 0.25f + i * 0.3f;
                bool vis = y < top - 0.1f;
                Show(flights[i], vis && rodOn);
                flights[i].transform.position = new Vector3(HX, y, HZ);
                flights[i].transform.rotation = Quaternion.Euler(0, t * 360f * 1.6f + i * 40f, 0);
            }
        }

        // rock chips
        bool drilling = t > drillA && t < drillB;
        for (int i = 0; i < chips.Length; i++)
        {
            var g = chips[i];
            Show(g, drilling);
            if (!drilling) continue;
            float life = 0.9f, s = Frac(t * 1.5f + i / (float)chips.Length) * life;
            float vx = (Hash(i, 5) - 0.5f) * 2.2f, vy = 2.0f + Hash(i, 6) * 1.6f;
            g.transform.position = new Vector3(HX + vx * s, 0.05f + vy * s - 4.9f * s * s, -0.06f);
            float sc = 0.05f + 0.05f * Hash(i, 7);
            g.transform.localScale = Vector3.one * sc * (s < life - 0.1f ? 1f : (life - s) * 10f);
            g.transform.rotation = Quaternion.Euler(s * 300f, s * 200f, s * 400f);
        }

        // cave-in: blocks fall off the hole walls
        float cave0 = Tw(3, "cave") - 0.1f, caveGone = Tw(3, "So") + 0.35f;
        for (int i = 0; i < caveBlocks.Length; i++)
        {
            var g = caveBlocks[i];
            bool on = t > cave0 && t < caveGone;
            Show(g, on);
            if (!on) continue;
            float startT = cave0 + 0.06f * i;
            float side = (i % 2 == 0) ? -1f : 1f;
            float x = HX + side * (0.18f + 0.06f * Hash(i, 3));
            float y0 = -0.5f - 0.38f * i;
            float floorY = HOLE_BOTTOM + 0.1f + (i % 3) * 0.17f;
            float dt = Mathf.Max(0f, t - startT);
            float y = Mathf.Max(floorY, y0 - 0.5f * 9.8f * dt * dt);
            g.transform.position = new Vector3(x, y, HZ);
            float sc = 0.2f * (1f - PE(t, caveGone - 0.3f, caveGone));
            g.transform.localScale = Vector3.one * sc;
            g.transform.rotation = Quaternion.Euler(0, 0, dt * 120f * side);
        }
    }

    void EvalCasing(float t)
    {
        float cA = Tw(3, "steel"), cB = Tw(3, "lowered"), cC = Tw(3, "walls", 0, true) + 0.4f;
        float total = CASING_LEN + SCREEN_LEN;
        float topStart = 9.5f, topHang = CASING_TOP_FINAL + total + 0.3f;
        float top = Mathf.Lerp(topStart, topHang, PE(t, cA - 0.2f, cA + 0.6f));
        top = Mathf.Lerp(top, CASING_TOP_FINAL, PE(t, cB - 0.2f, cC));
        bool on = t > cA - 0.25f;
        Show(casing, on); Show(screen, on);
        casing.transform.position = new Vector3(HX, top, HZ);
        casing.transform.localScale = new Vector3(1f, CASING_LEN, 1f);
        screen.transform.position = new Vector3(HX, top - CASING_LEN, HZ);
        screen.transform.localScale = new Vector3(1f, SCREEN_LEN, 1f);
    }

    void EvalSeal(float t)
    {
        float gap0 = Tw(4, "gap");
        // arrows pointing at the two gaps
        bool ga = t > gap0 - 0.1f && t < Tw(4, "filled") + 0.6f;
        Arrow(1, ga, new Vector3(HX - SEAL_CX, 0.55f, HZ), Vector3.down, 0.9f, t);
        Arrow(2, ga, new Vector3(HX + SEAL_CX, 0.55f, HZ), Vector3.down, 0.9f, t);

        float clayA = Tw(4, "filled") + 0.05f, clayB = Tw(4, "and") + 0.05f, cemB = Tw(4, "so") + 0.3f;
        float clayTop = Mathf.Lerp(SEAL_BOTTOM, -2.9f, PE(t, clayA, clayB));
        float cemTop = Mathf.Lerp(-2.9f, 0.0f, PE(t, clayB, cemB));
        float stripW = SEAL_W;
        for (int s = 0; s < 2; s++)
        {
            float sx = s == 0 ? HX - SEAL_CX : HX + SEAL_CX;
            var clay = s == 0 ? sealClayL : sealClayR;
            var cem = s == 0 ? sealCemL : sealCemR;
            float ch = clayTop - SEAL_BOTTOM;
            Show(clay, ch > 0.02f);
            clay.transform.position = new Vector3(sx, SEAL_BOTTOM + ch / 2f, HZ);
            clay.transform.localScale = new Vector3(stripW, Mathf.Max(ch, 0.001f), NOTCH - 0.02f);
            float eh = cemTop - (-2.9f);
            Show(cem, t > clayB && eh > 0.02f);
            cem.transform.position = new Vector3(sx, -2.9f + eh / 2f, HZ);
            cem.transform.localScale = new Vector3(stripW, Mathf.Max(eh, 0.001f), NOTCH - 0.02f);
        }
        // falling cement bits above the fill level
        bool pour = t > clayB && t < cemB + 0.1f;
        for (int i = 0; i < cemDrops.Length; i++)
        {
            var g = cemDrops[i];
            Show(g, pour);
            if (!pour) continue;
            float sx = (i % 2 == 0) ? HX - SEAL_CX : HX + SEAL_CX;
            float ph = Frac(t * 2.2f + i * 0.31f);
            float y = Mathf.Lerp(0.5f, cemTop, ph);
            g.transform.position = new Vector3(sx, y, -0.02f);
            g.transform.localScale = Vector3.one * 0.07f;
        }

        // dirty rain: falls, hits the sealed top and stops
        float r0 = Tw(4, "dirty") - 0.1f, r1 = Tw(5, "At");
        bool rain = t > r0 && t < r1;
        for (int i = 0; i < rainDrops.Length; i++)
        {
            var g = rainDrops[i];
            Show(g, rain);
            if (!rain) continue;
            int lane = i % 4;
            float x = lane == 0 ? HX - SEAL_CX : lane == 1 ? HX + SEAL_CX : lane == 2 ? HX - 0.9f : HX + 0.9f;
            x += (Hash(i, 4) - 0.5f) * 0.12f;
            float ph = Frac(t * 0.9f + i * 0.173f);
            float y = Mathf.Lerp(3.2f, 0.06f, ph * ph * 0.4f + ph * 0.6f);
            bool hit = ph > 0.97f;
            g.transform.position = new Vector3(x, y, -0.05f);
            float s = 0.09f * (hit ? 0.2f : 1f);
            g.transform.localScale = new Vector3(s * 0.8f, s * 1.3f, s * 0.8f);
        }
    }

    void EvalScreenAndWater(float t)
    {
        float scr = Tw(5, "screen"), slots = Tw(5, "slots"), wat = Tw(5, "Water"), sand = Tw(5, "Sand");
        // slot flicker
        float pulse = (t > slots && t < Tw(5, "Water")) ? 0.5f + 0.5f * Mathf.Sin((t - slots) * 14f) : 0f;
        screenMat.color = Color.Lerp(C("#FFB02E"), C("#FFFFFF"), pulse * 0.8f);

        // water inside the casing rises as it squeezes in
        float lvl = Mathf.Lerp(-5.3f, -2.4f, PE(t, wat, wat + 2.4f));
        bool wOn = t > wat - 0.1f;
        Show(casingWater, wOn);
        if (wOn)
        {
            float h = lvl - (-5.3f);
            casingWater.transform.position = new Vector3(HX, -5.3f + h / 2f, HZ + 0.07f);   // behind the pump (camera is at -z)
            casingWater.transform.localScale = new Vector3(0.32f, Mathf.Max(h, 0.001f), 0.1f);
        }

        // water drops from the wet sand into the screen gaps
        bool inOn = t > wat - 0.2f;
        for (int i = 0; i < inDrops.Length; i++)
        {
            var g = inDrops[i];
            Show(g, inOn);
            if (!inOn) continue;
            float ph = Frac(t * 0.8f + i * 0.0713f * 3f);
            float side = (i % 2 == 0) ? -1f : 1f;
            float y = Mathf.Lerp(-4.15f, -5.2f, Hash(i, 8));
            float xStart = HX + side * 1.5f, xEnd = HX + side * 0.28f;
            g.transform.position = new Vector3(Mathf.Lerp(xStart, xEnd, ph), y, -0.04f);
            g.transform.localScale = Vector3.one * 0.1f * (ph > 0.9f ? (1f - ph) * 10f : 1f);
        }
        // sand grains bounce back from the slots
        bool sOn = t > sand - 0.1f && t < Tw(6, "A");
        for (int i = 0; i < sandGrains.Length; i++)
        {
            var g = sandGrains[i];
            Show(g, sOn);
            if (!sOn) continue;
            float ph = Frac((t - sand) * 0.9f + i * 0.111f);
            float side = (i % 2 == 0) ? -1f : 1f;
            float y = Mathf.Lerp(-4.2f, -5.2f, Hash(i, 11));
            float xHit = HX + side * 0.34f;
            float xFar = HX + side * 1.3f;
            float x = ph < 0.5f ? Mathf.Lerp(xFar, xHit, ph * 2f) : Mathf.Lerp(xHit, xFar + side * 0.2f, (ph - 0.5f) * 2f);
            float hop = ph > 0.5f ? 0.25f * Mathf.Sin((ph - 0.5f) * 2f * Mathf.PI) : 0f;
            g.transform.position = new Vector3(x, y + hop, -0.045f);
            g.transform.localScale = Vector3.one * 0.12f;
        }
    }

    void EvalPump(float t)
    {
        float dropA = Tw(6, "drops") - 0.2f, dropB = Tw(6, "pipe") + 0.55f;
        float topY = Mathf.Lerp(1.8f, -3.15f, PE(t, dropA, dropB));
        bool on = t > dropA - 0.2f;
        Show(pump, on); Show(pumpPipe, on);
        pump.transform.position = new Vector3(HX, topY, HZ);
        float pipeTop = Mathf.Lerp(2.2f, TRENCH_Y, PE(t, dropB, dropB + 0.5f));
        pumpPipe.transform.position = new Vector3(HX, (pipeTop + topY) / 2f, HZ);
        pumpPipe.transform.localScale = new Vector3(0.07f, (pipeTop - topY) / 2f, 0.07f);
        Show(pumpPipe, on && topY < pipeTop);

        // trench pipe + riser + spout once the trench is open
        float tr0 = Tw(6, "pushes") - 0.5f;
        float trK = PE(t, tr0, tr0 + 0.55f);
        bool trOn = t > tr0;
        Show(trenchPipe, trOn); Show(riserPipe, trOn); Show(tapSpout, trOn);
        float x0 = HX, x1 = Mathf.Lerp(HX, RISER_X, trK);
        trenchPipe.transform.position = new Vector3((x0 + x1) / 2f, TRENCH_Y, HZ);
        trenchPipe.transform.rotation = Quaternion.Euler(0, 0, 90);
        trenchPipe.transform.localScale = new Vector3(0.11f, Mathf.Max((x1 - x0) / 2f, 0.001f), 0.11f);
        float rK = PE(t, tr0 + 0.5f, tr0 + 0.85f);
        float rTop = Mathf.Lerp(TRENCH_Y, 0.55f, rK);
        riserPipe.transform.position = new Vector3(RISER_X, (TRENCH_Y + rTop) / 2f, HZ);
        riserPipe.transform.localScale = new Vector3(0.11f, Mathf.Max((rTop - TRENCH_Y) / 2f, 0.001f), 0.11f);
        float spK = PE(t, tr0 + 0.8f, tr0 + 1.0f);
        tapSpout.transform.position = new Vector3(RISER_X - 0.12f * spK, 0.55f, HZ);
        tapSpout.transform.rotation = Quaternion.Euler(0, 0, 90);
        tapSpout.transform.localScale = new Vector3(0.09f, Mathf.Max(0.12f * spK, 0.001f), 0.09f);

        // flowing water: pump -> up the casing -> trench -> riser -> spout
        float fA = Tw(6, "pushes"), fB = Tw(8, "glass") + 2.5f;
        bool fOn = t > fA && t < fB;
        Vector3[] path = { new Vector3(HX, -3.0f, HZ - 0.04f), new Vector3(HX, TRENCH_Y, HZ - 0.04f), new Vector3(RISER_X, TRENCH_Y, HZ - 0.04f), new Vector3(RISER_X, 0.55f, HZ - 0.04f), new Vector3(RISER_X - 0.12f, 0.55f, HZ - 0.04f) };
        float totalLen = 0; var seg = new float[path.Length - 1];
        for (int i = 0; i < seg.Length; i++) { seg[i] = Vector3.Distance(path[i], path[i + 1]); totalLen += seg[i]; }
        for (int i = 0; i < flowDrops.Length; i++)
        {
            var g = flowDrops[i];
            float ph = Frac((t - fA) * 0.45f + i / (float)flowDrops.Length);
            float d = ph * totalLen;
            Vector3 p = path[0];
            for (int s = 0; s < seg.Length; s++) { if (d <= seg[s]) { p = Vector3.Lerp(path[s], path[s + 1], d / seg[s]); break; } d -= seg[s]; p = path[s + 1]; }
            // the reached part only
            bool reached = (ph * totalLen) < (seg[0] + PE(t, tr0, tr0 + 0.55f) * seg[1] + (seg[2] + seg[3]) * PE(t, tr0 + 0.5f, tr0 + 0.9f)) + 0.01f;
            Show(g, fOn && reached && topY < -3.0f);
            g.transform.position = p;
            g.transform.localScale = Vector3.one * 0.075f;
        }

        // house lights up at "home"
        bool lit = t > Tw(6, "home") && t < Tw(7, "On") + 0.5f || t > Tw(8, "glass");
        foreach (var r in houseWindows) r.sharedMaterial = lit ? windowLit : Std("#8FD9FF", 0.15f);
        var home = Tw(6, "home");
        Arrow(3, t > home - 0.1f && t < home + 1.2f, new Vector3(HOUSE_X - 0.15f, 2.7f, 1.5f), Vector3.down, 0.9f * PE(t, home - 0.1f, home + 0.2f), t);
    }

    void EvalSurface(float t)
    {
        // cap
        float capA = Tw(7, "cap") - 0.5f, capB = Tw(7, "cap") + 0.1f;
        float lift = PE(t, Tw(8, "flushed") - 0.1f, Tw(8, "flushed") + 0.3f) * (1f - PE(t, Tw(8, "chlorine", 0, true) + 1.3f, Tw(8, "chlorine", 0, true) + 1.8f));
        float capY = Mathf.Lerp(CASING_TOP_FINAL + 1.6f, CASING_TOP_FINAL, Back(P(t, capA, capB))) + 1.1f * lift;
        Show(cap, t > capA - 0.1f);
        cap.transform.position = new Vector3(HX + 0.35f * lift, capY, HZ);

        // slope wedges
        float sl = PE(t, Tw(7, "slopes") - 0.1f, Tw(7, "slopes") + 0.7f);
        Show(mound1, sl > 0.01f); Show(mound2, sl > 0.01f);
        float h = 0.24f * sl;
        mound1.transform.position = new Vector3(HX - HW - 1.5f, 0.0f, 0f);
        mound1.transform.localScale = new Vector3(1.5f, Mathf.Max(h, 0.001f), 1f);
        mound2.transform.position = new Vector3(HX + HW, 0.0f, 0f);
        mound2.transform.localScale = new Vector3(1.5f, Mathf.Max(h, 0.001f), 1f);

        // puddle forms near the well and slides away down the slope
        float pA = Tw(7, "puddles"), pB = Tw(7, "it", 0, true) + 0.2f;
        bool pOn = t > pA - 0.1f && t < pB;
        Show(puddle, pOn);
        if (pOn)
        {
            float k = PE(t, pA, Tw(7, "never"));
            float x = Mathf.Lerp(HX - 0.35f - 0.35f, HX - 0.35f - 1.55f, k);
            float y = Mathf.Lerp(0.2f, 0.02f, k);
            float s = Mathf.Lerp(0.2f, 0.5f, PE(t, pA - 0.1f, pA + 0.3f)) * (1f - PE(t, pB - 0.5f, pB));
            puddle.transform.position = new Vector3(x, y + 0.012f, 0.5f);
            puddle.transform.localScale = new Vector3(s * 1.2f, 0.012f, s * 0.8f);
        }
        float away = Tw(7, "away");
        Arrow(4, t > away - 0.1f && t < Tw(7, "so") + 0.6f, new Vector3(HX - 1.9f, 0.95f, 0.3f), new Vector3(-0.7f, -0.7f, 0f).normalized, 0.9f * PE(t, away - 0.1f, away + 0.2f), t);
    }

    void EvalClean(float t)
    {
        float chA = Tw(8, "chlorine"), chB = chA + 1.3f;
        // green chlorine front travels down the casing, then fades back to clear water
        float front = Mathf.Lerp(0.3f, -5.3f, PE(t, chA, chB));
        float fade = 1f - PE(t, chB + 0.1f, chB + 0.7f);
        bool on = t > chA && fade > 0.01f;
        Show(chlorine, on);
        if (on)
        {
            float h = 0.3f - front;
            chlorine.transform.position = new Vector3(HX, (0.3f + front) / 2f, HZ + 0.07f);
            chlorine.transform.localScale = new Vector3(0.33f * fade, h, 0.1f);
        }
        // germs pop when the front reaches them
        for (int i = 0; i < germs.Length; i++)
        {
            var g = germs[i];
            float gy = -2.9f - 0.38f * i;
            float x = HX + (i % 2 == 0 ? -0.07f : 0.07f) + 0.04f * Mathf.Sin(t * 3f + i);
            float popT = chA + (0.3f - gy) / (0.3f + 5.3f) * (chB - chA);
            float vis = (t > Tw(5, "Water") + 1.5f ? 1f : 0f) * (t < popT ? 1f : 1f - PE(t, popT, popT + 0.18f));
            Show(g, vis > 0.01f);
            g.transform.position = new Vector3(x, gy + 0.03f * Mathf.Sin(t * 4f + i), HZ - 0.1f);
            g.transform.localScale = Vector3.one * 0.13f * vis;
        }
        // sparkles at pop moments and at the end
        for (int i = 0; i < sparkles.Length; i++)
        {
            var g = sparkles[i];
            float st, sx, sy;
            if (i < 6)
            {
                float gy = -2.9f - 0.38f * i;
                st = chA + (0.3f - gy) / 5.6f * (chB - chA);
                sx = HX; sy = gy;
            }
            else
            {
                st = Tw(8, "germ-free");
                sx = 1.9f + (i - 8) * 0.3f; sy = 0.55f + 0.1f * (i % 2);
            }
            float k = P(t, st, st + 0.5f);
            bool vis = k > 0f && k < 1f;
            Show(g, vis);
            if (!vis) continue;
            float ang = i * 1.3f;
            float r = (i < 6 ? 0.25f : 0.3f) * k;
            g.transform.position = new Vector3(sx + Mathf.Cos(ang) * r, sy + Mathf.Sin(ang) * r, -0.15f);
            g.transform.rotation = Quaternion.Euler(0, 0, 45f + k * 90f);
            g.transform.localScale = Vector3.one * 0.07f * (1f - k);
        }

        // glass fills at the spout
        float gA = Tw(8, "glass") - 0.2f, gB = Tw(8, "germ-free", 0, true) + 0.2f;
        bool gOn = t > gA - 0.3f;
        float gx = RISER_X - 0.12f, gh = 0.38f;
        Show(glass, gOn); Show(glassWater, gOn && t > gA); Show(tapDrops[0], false); Show(tapDrops[1], false); Show(tapDrops[2], false);
        float gk = PE(t, gA, gB);
        float gs = Back(P(t, gA - 0.3f, gA));
        glass.transform.position = new Vector3(gx, gh / 2f, HZ);
        glass.transform.localScale = new Vector3(0.34f * gs, gh / 2f * gs, 0.34f * gs);
        float wh = Mathf.Max(0.001f, 0.3f * gk);
        glassWater.transform.position = new Vector3(gx, 0.02f + wh / 2f, HZ);
        glassWater.transform.localScale = new Vector3(0.27f, wh / 2f, 0.27f);
        if (t > gA - 0.1f && t < gB + 0.4f)
        {
            for (int i = 0; i < 3; i++)
            {
                Show(tapDrops[i], true);
                float ph = Frac(t * 2.4f + i / 3f);
                tapDrops[i].transform.position = new Vector3(gx, Mathf.Lerp(0.5f, 0.02f + wh, ph), HZ - 0.02f);
                tapDrops[i].transform.localScale = Vector3.one * 0.07f;
            }
        }
    }

    void EvalLabels(float t)
    {
        foreach (var L in labels)
        {
            float k = Back(P(t, L.t0, L.t0 + 0.28f)) * (1f - PE(t, L.t1 - 0.25f, L.t1));
            bool on = k > 0.01f;
            Show(L.root, on);
            if (!on) continue;
            L.root.transform.position = L.pos;
            L.root.transform.rotation = cam.transform.rotation;
            L.root.transform.localScale = Vector3.one * (k / camZoom) * 1.0f;
        }
    }
}
