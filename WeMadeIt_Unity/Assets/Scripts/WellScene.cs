// "How a Well Gets Dug (and Stays Clean)", realistic pass (HDRP). The whole video is a pure function
// of time: Evaluate(t) puts every object in its state at second t. Beat times come from the voice:
// docs/scenes/well_words.json, looked up with Tw(sentence, "word").
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// What the batch renderer needs from a video's scene.
public interface IShortScene
{
    void Build(string wordsJson);
    void Evaluate(float t);
    Camera Cam { get; }
    float Duration { get; }
    List<WellScene.Label> Labels { get; }
}

public class WellScene : MonoBehaviour, IShortScene
{
    // ------------------------------------------------------------------ voice timing
    [Serializable] public class WordRec { public string w; public float start; public float end; }
    [Serializable] public class SentRec { public int i; public string text; public float start; public float end; public WordRec[] words; }
    [Serializable] public class Doc { public float duration; public float speech_end; public SentRec[] sentences; }
    public Doc doc;
    public const float TAIL = 1.2f;                 // the last shot breathes after the last word
    public float Duration => doc.duration + TAIL;

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

    // ------------------------------------------------------------------ helpers
    static float P(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(1e-4f, b - a));
    static float E(float x) => x * x * (3f - 2f * x);
    static float PE(float t, float a, float b) => E(P(t, a, b));
    static float OutCubic(float x) { x = Mathf.Clamp01(x); return 1f - Mathf.Pow(1f - x, 3f); }
    static float Back(float x) { const float c = 1.7f; x = Mathf.Clamp01(x); return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }
    static float Frac(float x) => x - Mathf.Floor(x);
    static float Hash(int i, int k) => Frac(Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f);
    static Color Hex(string h) => WellLook.Hex(h);

    Transform stage;
    static void Show(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

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

    // our Blender model: front is +Z after import, turn it to the camera; materials by name
    GameObject Model(string file, Vector3 pos, float yaw = 0f)
    {
        var src = Resources.Load<GameObject>("Models/" + file);
        if (src == null) { Debug.LogError("model missing: " + file); return new GameObject(file); }
        var g = Instantiate(src);
        g.name = file;
        g.transform.SetParent(stage, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = WellLook.ModelMaterial(ms[i] != null ? ms[i].name.Replace(" (Instance)", "") : "");
            r.sharedMaterials = ms;
        }
        return g;
    }

    // Poly Haven model or one of its variants (child index), re-centred on its footprint
    readonly Dictionary<string, GameObject> phSrc = new Dictionary<string, GameObject>();
    GameObject PH(string id, int variant, Vector3 pos, float yaw, float scale)
    {
        GameObject src;
        if (!phSrc.TryGetValue(id, out src)) { src = Resources.Load<GameObject>($"PH/model/{id}/{id}_1k"); phSrc[id] = src; }
        if (src == null) { Debug.LogError("PH model missing: " + id); return new GameObject(id); }
        var holder = new GameObject(id + "_" + variant).transform;
        holder.SetParent(stage, false);
        GameObject g;
        var rends = src.GetComponentsInChildren<Renderer>();
        if (variant >= 0 && rends.Length > 1) g = Instantiate(rends[variant % rends.Length].gameObject);
        else g = Instantiate(src);
        g.transform.SetParent(holder, false);
        // re-centre: footprint centre onto the holder
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

    // ------------------------------------------------------------------ layout
    const float HX = -1.0f, HW = 0.45f, HZ = 0.225f, NOTCH = 0.45f;
    const float CASE_R = 0.22f;
    const float SEAL_CX = (CASE_R + HW) / 2f, SEAL_W = HW - CASE_R - 0.015f;
    static readonly float[] LayerY = { 0f, -0.7f, -1.8f, -3.0f, -3.9f, -5.2f, -60f };
    const float HOLE_BOTTOM = -5.6f, CASING_LEN = 4.35f, SCREEN_LEN = 1.4f, CASING_TOP = 0.45f, SEAL_BOTTOM = -3.9f;
    const float TRENCH_Y = -0.95f, RISER_X = 3.4f, HOUSE_X = 6.9f, HOUSE_Z = 4.55f;

    // ------------------------------------------------------------------ objects
    Camera cam;
    CutGround ground;
    GameObject rig, head, rod, bit, casing, screen, pump, pumpPipe, cap, hydrant, paver, glass, glassWater, pour;
    GameObject sealClayL, sealClayR, sealCemL, sealCemR, casingWater, chlorine, jug, jugStream;
    GameObject spoil, slopeL, slopeR, puddle, trenchPipe, riserPipe, measure, measureT, measureB;
    readonly List<GameObject> joints = new List<GameObject>();
    GameObject[] aqDrops, chips, dust, caveBlocks, rain, cemBits, inDrops, grains, flow, germs, sparks, tapDrops;
    GameObject[] arrows;
    GameObject[] aquifer, aquiferLine, gapGlow, tremie, bubbles, chipsBent;
    GameObject seep;
    Material crackDry, crackWet, screenMat, screenGlow, houseWindowMat, windowLit;
    readonly List<GameObject> cracks = new List<GameObject>();
    readonly List<Renderer> houseWindows = new List<Renderer>();

    public class Label { public string text, style; public Vector3 pos; public float t0, t1; public float k; }
    public readonly List<Label> labels = new List<Label>();

    public Camera Cam => cam;
    public List<Label> Labels => labels;

    // ------------------------------------------------------------------ build
    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<Doc>(File.ReadAllText(wordsJson));
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);

        WellLook.Sun(stage);
        WellLook.Environment(stage);
        cam = WellLook.Camera(stage, 34f);

        BuildGround();
        BuildWorld();
        BuildWell();
        BuildFx();
        BuildLabels();
    }

    void BuildGround()
    {
        var mats = new[] {
            WellLook.Textured("excavated_soil_wall", Hex("#B49A84"), 1.7f, 0.22f),        // topsoil
            WellLook.Textured("excavated_soil_wall", Hex("#EAD6C0"), 1.9f, 0.22f),        // subsoil
            WellLook.Textured("cracked_red_ground", new Color(1.35f, 0.95f, 0.7f), 1.6f, 0.28f),   // clay: pushed orange (tint > 1 on purpose)
            WellLook.Textured("sand_01", Hex("#F7E7C9"), 1.1f, 0.3f),                     // dry sand
            WellLook.Textured("ganges_river_pebbles", Hex("#9FB2BF"), 0.9f, 0.8f),       // wet gravel (the aquifer)
            WellLook.Textured("rock_wall_02", Hex("#C9CDD3"), 3.0f, 0.4f),               // bedrock
        };
        var lawn = WellLook.Textured("leafy_grass", new Color(0.62f, 0.95f, 0.45f), 2.0f, 0.25f);
        ground = new CutGround(stage, LayerY, mats, lawn);

        // cracks in the rock: dark, turn wet-blue at "rock"
        crackDry = WellLook.Lit("crackDry", Hex("#2E3236"), 0.3f);
        crackWet = WellLook.Lit("crackWet", Hex("#4FB6E8"), 0.95f);
        float[,] seeds = { { -3.0f, -5.5f }, { -2.2f, -6.7f }, { -0.1f, -5.6f }, { 0.7f, -6.5f }, { 1.6f, -5.7f }, { 2.5f, -6.8f }, { 3.2f, -5.6f }, { -1.0f, -7.1f }, { -3.8f, -6.4f }, { 4.2f, -6.2f } };
        for (int i = 0; i < seeds.GetLength(0); i++)
        {
            float x = seeds[i, 0], y = seeds[i, 1];
            for (int s = 0; s < 5; s++)
            {
                float ang = (Hash(i, s) - 0.5f) * 1.4f + (s % 2 == 0 ? -0.8f : 0.8f);
                float len = 0.22f + 0.14f * Hash(i, s + 9);
                var seg = Prim(PrimitiveType.Cube, "crack", new Vector3(x + Mathf.Sin(ang) * len / 2, y - Mathf.Cos(ang) * len / 2, -0.004f), new Vector3(0.022f + 0.012f * Hash(i, s + 3), len, 0.006f), crackDry);
                seg.transform.rotation = Quaternion.Euler(0, 0, ang * Mathf.Rad2Deg);
                x += Mathf.Sin(ang) * len; y -= Mathf.Cos(ang) * len;
                cracks.Add(seg);
            }
        }
    }

    void BuildWorld()
    {
        // house + garden
        house = Model("house", new Vector3(HOUSE_X, 0f, HOUSE_Z));
        foreach (var r in house.GetComponentsInChildren<Renderer>()) if (r.gameObject.name.Contains("glass")) houseWindows.Add(r);
        houseWindowMat = WellLook.ModelMaterial("house_glass");
        windowLit = WellLook.Lit("windowLit", Hex("#FFD98A"), 0.9f);
        windowLit.SetColor("_EmissiveColor", Hex("#FFC870") * 1.5f);
#if UNITY_EDITOR
        UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(windowLit);
#endif
        PH("garden_hose_wall_mounted_01", -1, new Vector3(HOUSE_X - 3.2f, 0.75f, HOUSE_Z - 3.33f), 180f, 1f);
        for (int i = 0; i < 6; i++) PH("shrub_02", i % 4, new Vector3(HOUSE_X - 3.6f + i * 1.4f + 0.3f * Hash(i, 1), 0f, HOUSE_Z - 3.9f), 360f * Hash(i, 2), 0.55f + 0.15f * Hash(i, 3));

        // trees: near ones real size, far ones in the haze
        PH("tree_small_02", -1, new Vector3(-6.2f, 0f, 6.0f), 30f, 1.15f);
        PH("tree_small_02", -1, new Vector3(13f, 0f, 9.5f), 140f, 1.3f);
        PH("tree_small_02", -1, new Vector3(-12f, 0f, 15f), 250f, 1.2f);
        PH("tree_small_02", -1, new Vector3(2.0f, 0f, 16f), 80f, 1.0f);
        for (int i = 0; i < 14; i++)          // a hazy tree line far behind (broadleaf: keeps its canopy at distance)
            PH("tree_small_02", -1, new Vector3(-48f + i * 7.5f + 4f * Hash(i, 5), 0f, 38f + 16f * Hash(i, 6)), 360f * Hash(i, 7), 1.6f + 0.9f * Hash(i, 8));
        for (int i = 0; i < 10; i++)
            PH("shrub_02", i % 4, new Vector3(-30f + i * 6.5f + 3f * Hash(i, 15), 0f, 26f + 6f * Hash(i, 16)), 360f * Hash(i, 17), 1.2f + 0.6f * Hash(i, 18));

        // props by the rig
        PH("portable_generator", -1, new Vector3(-4.6f, 0f, 1.9f), 200f, 1f);
        var spade = PH("rusted_spade_01", -1, new Vector3(1.55f, 0.48f, 1.25f), 20f, 1f);
        spade.transform.localRotation = Quaternion.Euler(0f, 20f, 14f);
        PH("boulder_01", -1, new Vector3(-7.5f, 0f, 3.0f), 40f, 0.45f);
        PH("boulder_01", -1, new Vector3(9.5f, 0f, 2.0f), 160f, 0.3f);

        // lawn: grass clumps (variants), dense at the cut edge, thinning with distance
        var rng = new System.Random(11);
        Func<float, float, bool> free = (x, z) =>
            !(x > HX - HW - 0.15f && x < HX + HW + 0.15f && z < NOTCH + 0.2f) &&                         // hole mouth
            !(x > RISER_X - 0.45f && x < RISER_X + 0.45f && z < 0.9f) &&                                // hydrant + paver
            !(x > HOUSE_X - 4.3f && x < HOUSE_X + 4.3f && z > HOUSE_Z - 3.6f && z < HOUSE_Z + 3.6f) &&  // house
            !(x > -0.3f && x < 1.6f && z > 0.55f && z < 1.7f);                                         // spoil pile
        int placed = 0;
        for (int tries = 0; placed < 6500 && tries < 40000; tries++)
        {
            float z = Mathf.Pow((float)rng.NextDouble(), 2.2f) * 24f + 0.03f;
            float x = (float)(rng.NextDouble() * 2 - 1) * (9f + z * 0.9f);
            if (!free(x, z)) continue;
            string id = rng.NextDouble() < 0.5 ? "grass_medium_01" : (rng.NextDouble() < 0.5 ? "grass_medium_02" : "grass_bermuda_01");
            int var = rng.Next(0, 21);
            PH(id, var, new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, 1.0f + (float)rng.NextDouble() * 0.7f);
            placed++;
        }
        for (int i = 0; i < 40; i++)
        {
            float z = 0.2f + (float)rng.NextDouble() * 9f, x = (float)(rng.NextDouble() * 2 - 1) * 10f;
            if (free(x, z)) PH("dandelion_01", rng.Next(0, 20), new Vector3(x, 0f, z), (float)rng.NextDouble() * 360f, 1f);
        }
    }

    GameObject house;

    void BuildWell()
    {
        rig = Model("rig", new Vector3(HX, 0f, HZ));
        head = Model("rig_head", new Vector3(HX, 3f, HZ));
        var steel = WellLook.ModelMaterial("steel");
        var dark = WellLook.ModelMaterial("steel_dark");
        rod = Prim(PrimitiveType.Cylinder, "rod", Vector3.zero, Vector3.one, steel);
        bit = Prim(PrimitiveType.Cylinder, "bit", Vector3.zero, Vector3.one, dark);
        for (int i = 0; i < 16; i++) joints.Add(Prim(PrimitiveType.Cylinder, "joint", Vector3.zero, new Vector3(0.15f, 0.05f, 0.15f), dark));

        casing = Model("casing", new Vector3(HX, CASING_TOP, HZ));
        screen = Model("screen", new Vector3(HX, CASING_TOP - CASING_LEN, HZ));
        screenMat = WellLook.ModelMaterial("screen");
        screenGlow = WellLook.Metal("screenGlow", Hex("#FFF6D0"), 0.9f, 1f);
        pump = Model("pump", new Vector3(HX, 0f, HZ));
        cap = Model("cap", new Vector3(HX, CASING_TOP, HZ));
        hydrant = Model("hydrant", new Vector3(RISER_X, 0.62f, HZ));
        paver = Model("paver", new Vector3(RISER_X - 0.2f, 0f, 0.55f));

        pumpPipe = Prim(PrimitiveType.Cylinder, "pumpPipe", Vector3.zero, Vector3.one, WellLook.Lit("hdpe", Hex("#1E2A35"), 0.55f));
        var galv = WellLook.Metal("galv", Hex("#9AA2AA"), 0.55f, 0.85f);
        trenchPipe = Prim(PrimitiveType.Cylinder, "trenchPipe", Vector3.zero, Vector3.one, WellLook.Lit("pvc", Hex("#2C3E57"), 0.6f));
        riserPipe = Prim(PrimitiveType.Cylinder, "riserPipe", Vector3.zero, Vector3.one, galv);

        var clay = WellLook.Textured("sand_01", Hex("#BBA88E"), 0.35f, 0.25f);          // bentonite
        var cem = WellLook.Textured("concrete_floor_02", Hex("#C9CBCC"), 0.6f, 0.3f);  // grout
        sealClayL = Prim(PrimitiveType.Cube, "clayL", Vector3.zero, Vector3.one, clay);
        sealClayR = Prim(PrimitiveType.Cube, "clayR", Vector3.zero, Vector3.one, clay);
        sealCemL = Prim(PrimitiveType.Cube, "cemL", Vector3.zero, Vector3.one, cem);
        sealCemR = Prim(PrimitiveType.Cube, "cemR", Vector3.zero, Vector3.one, cem);

        casingWater = Prim(PrimitiveType.Cube, "casingWater", Vector3.zero, Vector3.one, WellLook.Water("casingWater", Hex("#2B86C4"), 0.42f));
        chlorine = Prim(PrimitiveType.Cube, "chlorine", Vector3.zero, Vector3.one, WellLook.Water("chlorine", Hex("#B8F24B"), 0.6f));
        var jugMat = WellLook.Lit("jug", Hex("#F4F6F2"), 0.6f);
        jug = new GameObject("jug"); jug.transform.SetParent(stage, false);
        Prim(PrimitiveType.Cylinder, "jugBody", new Vector3(0, 0.16f, 0), new Vector3(0.2f, 0.16f, 0.14f), jugMat, jug.transform);
        Prim(PrimitiveType.Cylinder, "jugNeck", new Vector3(0.07f, 0.36f, 0), new Vector3(0.06f, 0.05f, 0.06f), jugMat, jug.transform);
        Prim(PrimitiveType.Cube, "jugLabel", new Vector3(0, 0.16f, -0.071f), new Vector3(0.14f, 0.12f, 0.004f), WellLook.Lit("jugLabel", Hex("#2C8F4E"), 0.4f), jug.transform);
        jugStream = Prim(PrimitiveType.Cylinder, "jugStream", Vector3.zero, Vector3.one, WellLook.Water("jugStream", Hex("#C9F26B"), 0.7f));

        spoil = Prim(PrimitiveType.Sphere, "spoil", Vector3.zero, Vector3.one, WellLook.Textured("excavated_soil_wall", Hex("#C4A88C"), 1.2f, 0.2f));
        var lawnMat = WellLook.Textured("leafy_grass", new Color(0.62f, 0.95f, 0.45f), 2.0f, 0.25f);
        slopeL = Prim(PrimitiveType.Sphere, "slopeL", Vector3.zero, Vector3.one, lawnMat);
        slopeR = Prim(PrimitiveType.Sphere, "slopeR", Vector3.zero, Vector3.one, lawnMat);
        puddle = Prim(PrimitiveType.Cylinder, "puddle", Vector3.zero, Vector3.one, WellLook.Water("puddle", Hex("#6A7F8C"), 0.75f, 0.98f, false));

        glass = Prim(PrimitiveType.Cylinder, "glass", Vector3.zero, Vector3.one, WellLook.Water("glass", Hex("#EAF6FF"), 0.18f, 0.98f));
        glassWater = Prim(PrimitiveType.Cylinder, "glassWater", Vector3.zero, Vector3.one, WellLook.Water("glassWater", Hex("#8FD3F5"), 0.45f));
        pour = Prim(PrimitiveType.Cylinder, "pour", Vector3.zero, Vector3.one, WellLook.Water("pour", Hex("#BFE6FA"), 0.55f));

        var yel = WellLook.Unlit("measure", Hex("#FFD84D"));
        measure = Prim(PrimitiveType.Cube, "measure", Vector3.zero, Vector3.one, yel);
        measureT = Prim(PrimitiveType.Cube, "measureT", Vector3.zero, Vector3.one, yel);
        measureB = Prim(PrimitiveType.Cube, "measureB", Vector3.zero, Vector3.one, yel);
    }

    GameObject[] Many(int n, PrimitiveType t, string name, Material m)
    {
        var a = new GameObject[n];
        for (int i = 0; i < n; i++) a[i] = Prim(t, name + i, Vector3.zero, Vector3.one, m);
        return a;
    }

    void BuildFx()
    {
        var waterDrop = WellLook.Emissive(WellLook.Water("drop", Hex("#5CC6F5"), 0.85f, 0.98f, false), Hex("#5CC6F5"), 2200f);
        aqDrops = Many(34, PrimitiveType.Sphere, "aq", waterDrop);
        var rockMat = WellLook.Textured("rock_face", Hex("#B9AFA6"), 0.4f, 0.3f, true);
        var soilMat = WellLook.Textured("excavated_soil_wall", Hex("#D8C2AA"), 0.4f, 0.2f, true);
        chips = Many(22, PrimitiveType.Sphere, "chip", soilMat);
        for (int i = 0; i < chips.Length; i += 3) chips[i].GetComponent<Renderer>().sharedMaterial = rockMat;
        caveBlocks = Many(10, PrimitiveType.Sphere, "cave", soilMat);
        var dustMat = WellLook.Unlit("dust", new Color(0.78f, 0.68f, 0.56f, 0.35f), WellLook.SoftDot(), true);
        dust = Many(18, PrimitiveType.Quad, "dust", dustMat);
        rain = Many(30, PrimitiveType.Capsule, "rain", WellLook.Water("rain", Hex("#8C7A62"), 0.45f, 0.9f, false));
        cemBits = Many(10, PrimitiveType.Sphere, "cembit", WellLook.Lit("cembit", Hex("#B9BCBE"), 0.3f));
        inDrops = Many(22, PrimitiveType.Sphere, "in", waterDrop);
        grains = Many(14, PrimitiveType.Sphere, "grain", WellLook.Lit("grain", Hex("#D9B46A"), 0.2f));
        flow = Many(14, PrimitiveType.Sphere, "flow", WellLook.Water("flow", Hex("#5CC3F2"), 0.85f, 0.97f, false));
        tapDrops = Many(4, PrimitiveType.Sphere, "tap", waterDrop);
        germs = Many(6, PrimitiveType.Capsule, "germ", WellLook.Lit("germ", Hex("#5E6B2E"), 0.5f));
        var sparkMat = WellLook.Unlit("spark", Color.white * 4f, WellLook.SoftDot(), true);
        sparks = Many(12, PrimitiveType.Quad, "spark", sparkMat);

        // arrows: rounded shaft + cone head, tip at the local origin, pointing DOWN
        arrows = new GameObject[5];
        var am = WellLook.Emissive(WellLook.Lit("arrow", Hex("#FF8A2A"), 0.6f), Hex("#FF8A2A"), 2500f);
        var cone = ConeMesh(0.16f, 0.34f, 32);
        for (int i = 0; i < arrows.Length; i++)
        {
            var root = new GameObject("arrow" + i);
            root.transform.SetParent(stage, false);
            Prim(PrimitiveType.Capsule, "shaft", new Vector3(0, 0.56f, 0), new Vector3(0.11f, 0.27f, 0.11f), am, root.transform);
            var head = new GameObject("head");
            head.transform.SetParent(root.transform, false);
            head.AddComponent<MeshFilter>().sharedMesh = cone;
            head.AddComponent<MeshRenderer>().sharedMaterial = am;
            arrows[i] = root;
        }

        // the aquifer: water fills the gravel's pores (a tinted film over the cut face, left and right of the hole)
        var aqMat = WellLook.Water("aquifer", Hex("#2C7FB8"), 0.26f, 0.98f, false);
        var lineMat = WellLook.Emissive(WellLook.Water("waterline", Hex("#8FDCFF"), 0.85f, 0.98f, false), Hex("#8FDCFF"), 5000f);
        aquifer = new[] { Prim(PrimitiveType.Cube, "aqL", Vector3.zero, Vector3.one, aqMat), Prim(PrimitiveType.Cube, "aqR", Vector3.zero, Vector3.one, aqMat) };
        aquiferLine = new[] { Prim(PrimitiveType.Cube, "aqLineL", Vector3.zero, Vector3.one, lineMat), Prim(PrimitiveType.Cube, "aqLineR", Vector3.zero, Vector3.one, lineMat) };
        // seal work: glowing empty gaps, tremie hoses, bentonite chips, rising bubbles in the casing
        var glowMat = WellLook.Unlit("gapGlow", new Color(1f, 0.62f, 0.2f, 0.2f), null, true);
        gapGlow = new[] { Prim(PrimitiveType.Cube, "glowL", Vector3.zero, Vector3.one, glowMat), Prim(PrimitiveType.Cube, "glowR", Vector3.zero, Vector3.one, glowMat) };
        var hose = WellLook.Lit("hose", Hex("#202224"), 0.35f);
        tremie = new[] { Prim(PrimitiveType.Cylinder, "tremieL", Vector3.zero, Vector3.one, hose), Prim(PrimitiveType.Cylinder, "tremieR", Vector3.zero, Vector3.one, hose) };
        chipsBent = Many(24, PrimitiveType.Cube, "bentonite", WellLook.Lit("bentonite", Hex("#B9A27E"), 0.25f));
        bubbles = Many(14, PrimitiveType.Sphere, "bubble", WellLook.Water("bubble", Hex("#FFFFFF"), 0.55f, 0.98f, false));
        seep = Prim(PrimitiveType.Cylinder, "seep", Vector3.zero, Vector3.one, WellLook.Water("seep", Hex("#6B5134"), 0.7f, 0.95f, false));
    }

    static Mesh ConeMesh(float r, float h, int seg)
    {
        // apex at the origin, base ring at y = h (points DOWN); flat-shaded, wound outward
        var v = new List<Vector3>(); var tri = new List<int>();
        void T(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            int o = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) > 0) { tri.Add(o); tri.Add(o + 1); tri.Add(o + 2); }
            else { tri.Add(o); tri.Add(o + 2); tri.Add(o + 1); }
        }
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
            Vector3 p0 = new Vector3(Mathf.Cos(a0) * r, h, Mathf.Sin(a0) * r), p1 = new Vector3(Mathf.Cos(a1) * r, h, Mathf.Sin(a1) * r);
            var mid = (p0 + p1) * 0.5f;
            T(Vector3.zero, p0, p1, new Vector3(mid.x, -r * r / h, mid.z));
            T(new Vector3(0, h, 0), p0, p1, Vector3.up);
        }
        var m = new Mesh { vertices = v.ToArray(), triangles = tri.ToArray() };
        m.RecalculateNormals();
        return m;
    }

    void AddLabel(string text, string style, Vector3 pos, float t0, float t1)
    {
        labels.Add(new Label { text = text, style = style, pos = pos, t0 = t0, t1 = t1 });
    }

    void BuildLabels()
    {
        AddLabel("WATER", "b", new Vector3(-1.4f, -4.15f, -0.05f), Tw(2, "water") + 0.2f, Tw(2, "A") + 0.6f);
        AddLabel("SAND", "w", new Vector3(1.7f, -3.45f, -0.05f), Tw(2, "sand"), Tw(2, "A") + 0.6f);
        AddLabel("GRAVEL", "w", new Vector3(1.7f, -4.55f, -0.05f), Tw(2, "gravel"), Tw(2, "A") + 0.6f);
        AddLabel("ROCK", "w", new Vector3(1.7f, -6.3f, -0.05f), Tw(2, "rock"), Tw(2, "A") + 0.6f);
        AddLabel("300 FT", "y", new Vector3(HX - 1.25f, -2.8f, -0.05f), Tw(3, "hundreds"), Tw(4, "But") + 0.3f);
        AddLabel("CASING", "w", new Vector3(HX + 1.05f, -0.9f, -0.05f), Tw(4, "casing"), Tw(5, "Now") + 0.2f);
        AddLabel("GAP", "o", new Vector3(HX + 0.95f, -0.75f, -0.05f), Tw(5, "gap"), Tw(5, "filled") + 0.3f);
        AddLabel("BENTONITE CLAY", "w", new Vector3(HX + 1.25f, -3.35f, -0.05f), Tw(5, "clay"), Tw(5, "so"));
        AddLabel("CEMENT", "w", new Vector3(HX + 1.0f, -1.6f, -0.05f), Tw(5, "cement"), Tw(5, "outside"));
        AddLabel("SEALED", "g", new Vector3(HX + 1.0f, -1.6f, -0.05f), Tw(5, "outside"), Tw(6, "At") + 0.2f);
        AddLabel("SCREEN", "w", new Vector3(HX + 0.9f, -3.75f, -0.05f), Tw(6, "screen"), Tw(6, "Water"));
        AddLabel("WATER IN", "b", new Vector3(HX - 0.95f, -4.0f, -0.05f), Tw(6, "Water"), Tw(7, "A"));
        AddLabel("SAND OUT", "o", new Vector3(HX + 0.95f, -4.0f, -0.05f), Tw(6, "Sand"), Tw(7, "A"));
        AddLabel("PUMP", "w", new Vector3(HX + 0.95f, -2.3f, -0.05f), Tw(7, "pump"), Tw(7, "pushes") + 0.4f);
        AddLabel("CAP", "w", new Vector3(HX + 0.9f, 0.85f, 0.2f), Tw(8, "cap"), Tw(9, "Last"));
        AddLabel("CHLORINE", "g", new Vector3(HX + 1.1f, 1.2f, 0.2f), Tw(9, "chlorine"), Tw(9, "chlorine") + 1.9f);
        AddLabel("GERM-FREE", "g", new Vector3(RISER_X - 0.1f, 1.35f, 0.2f), Tw(9, "germ-free"), doc.duration + TAIL + 5f);
    }

    // ------------------------------------------------------------------ camera: smooth spline through keys
    struct Key { public float t, x, y, d, yaw, pitch; public Key(float t, float x, float y, float d, float yaw, float pitch) { this.t = t; this.x = x; this.y = y; this.d = d; this.yaw = yaw; this.pitch = pitch; } }
    Key[] keys;

    void BuildCamKeys()
    {
        keys = new[] {
            new Key(0f,                              2.2f,  0.8f, 26f, -12f, 15f),   // hook: the whole yard, house, lawn
            new Key(Tw(1, "Let's"),                  1.2f, -0.6f, 24f, -8f, 13f),
            new Key(Tw(1, "open", 0, true),          0.3f, -2.4f, 21f, -4f, 10f),    // "cut the ground open": down onto the strata
            new Key(Tw(2, "water") - 0.2f,           0.2f, -3.3f, 19f, -2f, 9f),
            new Key(Tw(2, "reach", 0, true),        -0.6f, -1.6f, 18f,  0f, 10f),
            new Key(Tw(3, "drill"),                 -1.6f, -0.2f, 15f,  5f, 11f),
            new Key(Tw(3, "bores") + 0.6f,          -1.2f, -1.6f, 15.5f, 3f, 10f),
            new Key(Tw(3, "feet", 0, true),         -1.0f, -2.6f, 15f,  2f, 9f),
            new Key(Tw(4, "But"),                   -1.0f, -2.0f, 13f, -2f, 9f),
            new Key(Tw(4, "So"),                    -1.0f, -0.6f, 13.5f, -4f, 10f),
            new Key(Tw(4, "walls", 0, true),        -1.0f, -1.8f, 13f, -3f, 9f),
            new Key(Tw(5, "Now") + 0.6f,            -1.0f, -0.7f, 8.8f, -6f, 10f),
            new Key(Tw(5, "filled") - 0.2f,         -1.0f, -2.2f, 10.5f, -5f, 9f),
            new Key(Tw(5, "cement"),                -1.0f, -1.7f, 10f, -4f, 9f),
            new Key(Tw(5, "so"),                    -1.0f, -0.6f, 9.0f, -3f, 11f),
            new Key(Tw(5, "outside", 0, true),      -1.0f, -2.1f, 10.5f, 0f, 9f),
            new Key(Tw(6, "At"),                    -1.0f, -4.3f, 8.0f,  3f, 7f),
            new Key(Tw(6, "slots"),                 -1.0f, -4.55f, 5.6f,  5f, 6f),
            new Key(Tw(6, "out", 0, true),          -1.0f, -4.5f, 5.2f,  7f, 6f),
            new Key(Tw(7, "A") + 0.3f,              -0.3f, -2.0f, 15f,  2f, 9f),
            new Key(Tw(7, "home", 0, true),          1.6f, -0.6f, 15f, -2f, 10f),
            new Key(Tw(8, "On"),                    -0.7f,  0.2f, 9.5f, -6f, 13f),
            new Key(Tw(8, "it", 0, true),           -1.1f,  0.15f, 9.5f, -2f, 13f),
            new Key(Tw(9, "Last") + 0.2f,           -0.8f, -0.9f, 10.5f, 2f, 10f),
            new Key(Tw(9, "chlorine", 0, true) + 0.5f, -0.8f, -0.5f, 9.5f, 0f, 10f),   // hold on the well while the chlorine runs down
            new Key(Tw(9, "glass") + 0.15f,          2.9f,  0.35f, 5.2f, -4f, 9f),
            new Key(doc.duration + TAIL,             3.0f,  0.4f, 4.0f, -7f, 8f),
        };
    }

    // Catmull-Rom (non-uniform keys): C1 through every key, no stop-and-go
    static float Spline(float t, float[] kt, float[] kv)
    {
        int n = kt.Length;
        if (t <= kt[0]) return kv[0];
        if (t >= kt[n - 1]) return kv[n - 1];
        int i = 0; while (i < n - 2 && t > kt[i + 1]) i++;
        float t0 = kt[i], t1 = kt[i + 1], h = t1 - t0, u = (t - t0) / h;
        float m0 = i > 0 ? (kv[i + 1] - kv[i - 1]) / (kt[i + 1] - kt[i - 1]) * h : 0f;
        float m1 = i < n - 2 ? (kv[i + 2] - kv[i]) / (kt[i + 2] - kt[i]) * h : 0f;
        float u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * kv[i] + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * kv[i + 1] + (u3 - u2) * m1;
    }

    float[] kT, kX, kY, kD, kYaw, kPitch;
    float camDist = 15f;

    void EvalCamera(float t)
    {
        if (keys == null)
        {
            BuildCamKeys();
            int n = keys.Length;
            kT = new float[n]; kX = new float[n]; kY = new float[n]; kD = new float[n]; kYaw = new float[n]; kPitch = new float[n];
            for (int i = 0; i < n; i++) { kT[i] = keys[i].t; kX[i] = keys[i].x; kY[i] = keys[i].y; kD[i] = keys[i].d; kYaw[i] = keys[i].yaw; kPitch[i] = keys[i].pitch; }
        }
        float fx = Spline(t, kT, kX), fy = Spline(t, kT, kY), d = Spline(t, kT, kD);
        float yaw = Spline(t, kT, kYaw), pitch = Spline(t, kT, kPitch);
        // breathing handheld: two slow sines, a fraction of a degree
        yaw += 0.35f * Mathf.Sin(t * 0.61f) + 0.15f * Mathf.Sin(t * 1.37f + 1f);
        pitch += 0.25f * Mathf.Sin(t * 0.83f + 2f);
        float cave = Tw(4, "cave");
        float sh = t >= cave ? Mathf.Exp(-(t - cave) * 4.5f) : 0f;
        float drill = (t > Tw(3, "bores") && t < Tw(3, "feet", 0, true)) ? 0.02f : 0f;
        fx += (0.05f * sh + drill) * Mathf.Sin(t * 61f); fy += (0.04f * sh + drill) * Mathf.Cos(t * 73f);
        camDist = d;
        var focus = new Vector3(fx, fy, 0.3f);
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.position = focus + rot * new Vector3(0f, 0f, -d);
        cam.transform.rotation = rot;
        cam.focusDistance = d;
    }

    // ------------------------------------------------------------------ evaluate
    public void Evaluate(float t)
    {
        EvalCamera(t);
        EvalGround(t);
        EvalDrill(t);
        EvalCasing(t);
        EvalSeal(t);
        EvalScreen(t);
        EvalPump(t);
        EvalSurface(t);
        EvalClean(t);
        EvalLabels(t);
    }

    float HoleBottom(float t) => Mathf.Lerp(0f, HOLE_BOTTOM, PE(t, Tw(3, "bores"), Tw(3, "feet", 0, true) + 0.1f));
    float TrenchStart => Tw(7, "pushes") - 0.5f;

    void Arrow(int i, bool on, Vector3 tip, Vector3 dir, float s, float t)
    {
        var a = arrows[i];
        Show(a, on && s > 0.01f);
        if (!a.activeSelf) return;
        float bob = 0.1f * Mathf.Sin(t * 6.5f + i);
        a.transform.rotation = Quaternion.FromToRotation(Vector3.down, dir);
        a.transform.localScale = Vector3.one * s;
        a.transform.position = tip - dir * bob;
    }

    void Billboard(GameObject g, Vector3 p, float size)
    {
        g.transform.position = p;
        g.transform.rotation = cam.transform.rotation;
        g.transform.localScale = Vector3.one * size;
    }

    void EvalGround(float t)
    {
        var cuts = new List<Bounds>();
        float hb = HoleBottom(t);
        if (hb < -0.02f) { var b = new Bounds(); b.SetMinMax(new Vector3(HX - HW, hb, -1f), new Vector3(HX + HW, 1f, NOTCH)); cuts.Add(b); }
        float tr0 = TrenchStart;
        if (t > tr0)
        {
            float x1 = Mathf.Lerp(HX + HW, RISER_X + 0.15f, PE(t, tr0, tr0 + 0.55f));
            var b = new Bounds(); b.SetMinMax(new Vector3(HX + HW - 0.01f, TRENCH_Y - 0.17f, -1f), new Vector3(x1, TRENCH_Y + 0.17f, NOTCH)); cuts.Add(b);
            float rk = PE(t, tr0 + 0.5f, tr0 + 0.8f);
            if (rk > 0.01f) { var r = new Bounds(); r.SetMinMax(new Vector3(RISER_X - 0.14f, TRENCH_Y - 0.17f, -1f), new Vector3(RISER_X + 0.14f, Mathf.Lerp(TRENCH_Y, 1f, rk), NOTCH)); cuts.Add(r); }
        }
        ground.Rebuild(cuts);

        // S1: the gravel fills with water from below at "water" and stays wet for the rest of the video
        float aq0 = Tw(2, "water") - 0.1f;
        float fill = PE(t, aq0, aq0 + 1.1f);
        float wy0 = LayerY[5], wy1 = Mathf.Lerp(LayerY[5], LayerY[4] - 0.06f, fill);
        float lap = 0.015f * Mathf.Sin(t * 2.2f);
        for (int s2 = 0; s2 < 2; s2++)
        {
            bool split = HoleBottom(t) < LayerY[4];            // no hole through the water yet: one film, no gap
            float xa = s2 == 0 ? -40f : HX + HW + 0.005f, xb = s2 == 0 ? (split ? HX - HW - 0.005f : 40f) : 40f;
            bool on = fill > 0.01f && (s2 == 0 || split);
            Show(aquifer[s2], on); Show(aquiferLine[s2], on);
            if (!on) continue;
            float hh = wy1 + lap - wy0;
            aquifer[s2].transform.position = new Vector3((xa + xb) / 2f, wy0 + hh / 2f, -0.006f);
            aquifer[s2].transform.localScale = new Vector3(xb - xa, hh, 0.004f);
            aquiferLine[s2].transform.position = new Vector3((xa + xb) / 2f, wy1 + lap, -0.009f);
            aquiferLine[s2].transform.localScale = new Vector3(xb - xa, 0.018f, 0.004f);
        }
        float aqEnd = Tw(4, "But");
        float aqShow = PE(t, aq0 + 0.4f, aq0 + 1.0f) * (1f - PE(t, aqEnd, aqEnd + 0.5f));
        for (int i = 0; i < aqDrops.Length; i++)
        {
            var g = aqDrops[i];
            Show(g, aqShow > 0.01f);
            if (!g.activeSelf) continue;
            float y = Mathf.Lerp(-4.05f, -5.1f, Hash(i, 2));
            float x = Mathf.Repeat(Mathf.Lerp(-4.2f, 4.2f, Hash(i, 1)) + t * (0.18f + 0.1f * Hash(i, 4)) + 4.2f, 8.4f) - 4.2f;   // groundwater creeps sideways
            if (Mathf.Abs(x - HX) < HW + 0.05f) { Show(g, false); continue; }
            g.transform.position = new Vector3(x, y + 0.05f * Mathf.Sin(t * 1.7f + i * 1.7f), -0.03f);
            g.transform.localScale = new Vector3(1.35f, 1f, 1f) * 0.11f * aqShow;
        }
        bool wet = t > Tw(2, "rock");
        foreach (var c in cracks) c.GetComponent<Renderer>().sharedMaterial = wet ? crackWet : crackDry;

        // S1 arrow at "reach"
        float rA = Tw(2, "reach") - 0.1f, rB = Tw(3, "drill");
        Arrow(0, t > rA && t < rB + 0.3f, new Vector3(HX, 0.25f, HZ), Vector3.down, 1.3f * Back(P(t, rA, rA + 0.35f)) * (1f - PE(t, rB, rB + 0.3f)), t);

        // 300 ft dimension line
        float mk = PE(t, Tw(3, "hundreds") - 0.2f, Tw(3, "hundreds") + 0.6f) * (1f - PE(t, Tw(4, "But"), Tw(4, "But") + 0.4f));
        bool mOn = mk > 0.01f;
        Show(measure, mOn); Show(measureT, mOn); Show(measureB, mOn);
        if (mOn)
        {
            float mx = HX - HW - 0.35f, len = -HOLE_BOTTOM * mk;
            measure.transform.position = new Vector3(mx, -len / 2f, -0.02f); measure.transform.localScale = new Vector3(0.035f, len, 0.01f);
            measureT.transform.position = new Vector3(mx, 0f, -0.02f); measureT.transform.localScale = new Vector3(0.22f, 0.035f, 0.01f);
            measureB.transform.position = new Vector3(mx, -len, -0.02f); measureB.transform.localScale = new Vector3(0.22f, 0.035f, 0.01f);
        }
    }

    void EvalDrill(float t)
    {
        float arrive0 = Tw(3, "First") - 0.25f, arrive1 = Tw(3, "rig", 0, true) + 0.25f;
        float leave0 = Tw(5, "Now"), leave1 = leave0 + 1.6f;
        float drillA = Tw(3, "bores"), drillB = Tw(3, "feet", 0, true) + 0.1f;
        float inK = OutCubic(P(t, arrive0, arrive1)), outK = E(P(t, leave0, leave1));
        float rx = HX - 9f * (1f - inK) - 10f * outK;
        bool rigOn = t > arrive0 - 0.05f && outK < 0.999f;
        bool drilling = t > drillA && t < drillB;
        float vib = drilling ? 0.006f : 0f;
        float bob = (inK < 1f || outK > 0f) ? 0.01f * Mathf.Sin(t * 22f) : 0f;   // tracks rolling over the lawn
        Show(rig, rigOn); Show(head, rigOn);
        rig.transform.position = new Vector3(rx + vib * Mathf.Sin(t * 157f), bob + vib * Mathf.Cos(t * 131f), HZ);

        // rotary head travels down the mast while drilling, back up for the casing, lowers the casing
        float hb = HoleBottom(t);
        float drillK = PE(t, drillA, drillB);
        float cA = Tw(4, "strong"), cB = Tw(4, "lowered"), cC = Tw(4, "walls", 0, true) + 0.4f;
        float headY = Mathf.Lerp(4.35f, 0.95f, drillK);
        headY = Mathf.Lerp(headY, 4.35f, PE(t, Tw(4, "But"), Tw(4, "But") + 1.0f));
        headY = Mathf.Lerp(headY, 1.55f, PE(t, cB - 0.2f, cC));
        head.transform.position = new Vector3(rx + vib * Mathf.Sin(t * 157f), headY, HZ);

        // drill string: from the head down to the bit (pulled at "But")
        float retract = PE(t, Tw(4, "But") - 0.1f, Tw(4, "But") + 1.0f);
        float bitY = Mathf.Lerp(0.25f, hb, P(t, drillA - 0.3f, drillA + 0.2f) > 0f ? 1f : 0f);
        bitY = t < drillA ? Mathf.Lerp(0.6f, 0.2f, PE(t, arrive1, drillA)) : hb;
        bitY = Mathf.Lerp(bitY, headY - 0.6f, retract);
        bool rodOn = rigOn && retract < 0.99f && t > arrive1 - 0.3f;
        float top = headY - 0.3f;
        Show(rod, rodOn); Show(bit, rodOn);
        foreach (var j in joints) Show(j, rodOn);
        if (rodOn)
        {
            float len = Mathf.Max(0.01f, top - bitY);
            rod.transform.position = new Vector3(rx, (top + bitY) / 2f, HZ);
            rod.transform.localScale = new Vector3(0.1f, len / 2f, 0.1f);
            bit.transform.position = new Vector3(rx, bitY - 0.06f, HZ);
            bit.transform.localScale = new Vector3(0.26f, 0.08f, 0.26f);
            bit.transform.rotation = Quaternion.Euler(0, t * 720f, 0);
            for (int i = 0; i < joints.Count; i++)
            {
                float y = top - 0.5f - i * 0.75f;
                Show(joints[i], y > bitY + 0.1f);
                joints[i].transform.position = new Vector3(rx, y, HZ);
                joints[i].transform.rotation = Quaternion.Euler(0, t * 720f, 0);
            }
        }

        // cuttings + dust out of the hole mouth while drilling
        for (int i = 0; i < chips.Length; i++)
        {
            var g = chips[i];
            Show(g, drilling);
            if (!drilling) continue;
            float life = 0.85f, s = Frac(t * 1.4f + i / (float)chips.Length) * life;
            float vx = (Hash(i, 5) - 0.5f) * 2.4f, vz = Hash(i, 9) * 1.2f, vy = 2.2f + Hash(i, 6) * 1.7f;
            float y = Mathf.Max(0.02f, 0.05f + vy * s - 4.9f * s * s);
            g.transform.position = new Vector3(HX + vx * s, y, HZ - 0.2f + vz * s);
            float sc = 0.035f + 0.045f * Hash(i, 7);
            g.transform.localScale = new Vector3(1f, 0.7f, 0.85f) * sc * (s < life - 0.12f ? 1f : (life - s) / 0.12f);
            g.transform.rotation = Quaternion.Euler(s * 300f + i * 40f, s * 200f, s * 400f);
        }
        for (int i = 0; i < dust.Length; i++)
        {
            var g = dust[i];
            bool on = t > drillA && t < drillB + 1.2f;
            Show(g, on);
            if (!on) continue;
            float life = 1.6f, s = Frac(t * 0.7f + i / (float)dust.Length) * life;
            float fade = (1f - s / life) * (t < drillB ? 1f : 1f - P(t, drillB, drillB + 1.2f));
            Billboard(g, new Vector3(HX + (Hash(i, 2) - 0.5f) * 0.6f + s * 0.35f, 0.15f + s * 0.75f, HZ - 0.4f), (0.35f + s * 0.9f) * fade);
        }

        // spoil pile grows beside the hole
        float pile = PE(t, drillA + 0.3f, drillB + 0.2f);
        Show(spoil, pile > 0.01f);
        spoil.transform.position = new Vector3(0.65f, -0.12f, 1.15f);
        spoil.transform.localScale = new Vector3(1.5f, 0.7f, 1.0f) * pile;

        // cave-in: soil lumps break off the bare walls
        float cave0 = Tw(4, "cave") - 0.1f, gone = Tw(4, "So") + 0.35f;
        for (int i = 0; i < caveBlocks.Length; i++)
        {
            var g = caveBlocks[i];
            bool on = t > cave0 && t < gone;
            Show(g, on);
            if (!on) continue;
            float side = (i % 2 == 0) ? -1f : 1f;
            float x = HX + side * (HW - 0.08f - 0.1f * Hash(i, 3));
            float y0 = -0.4f - 0.42f * i, floorY = HOLE_BOTTOM + 0.12f + (i % 3) * 0.16f;
            float dt = Mathf.Max(0f, t - (cave0 + 0.05f * i));
            float y = Mathf.Max(floorY, y0 - 4.9f * dt * dt);
            g.transform.position = new Vector3(x - side * Mathf.Min(0.25f, dt * 0.5f), y, HZ);
            g.transform.localScale = new Vector3(0.22f, 0.17f, 0.2f) * (1f - PE(t, gone - 0.3f, gone));
            g.transform.rotation = Quaternion.Euler(dt * 160f, 0, dt * 220f * side);
        }
    }

    void EvalCasing(float t)
    {
        float cA = Tw(4, "strong"), cB = Tw(4, "lowered"), cC = Tw(4, "walls", 0, true) + 0.4f;
        // hangs under the head (which is at the top of the mast), then rides down with it
        float total = CASING_LEN + SCREEN_LEN;
        float hang = 4.35f - 0.35f;                          // top of the casing under the head
        float top = Mathf.Lerp(hang + 5f, hang, OutCubic(P(t, cA - 0.3f, cA + 0.5f)));
        top = Mathf.Lerp(top, CASING_TOP, PE(t, cB - 0.2f, cC));
        bool on = t > cA - 0.3f;
        Show(casing, on); Show(screen, on);
        casing.transform.position = new Vector3(HX, top, HZ);
        casing.transform.localScale = new Vector3(1f, CASING_LEN, 1f);
        screen.transform.position = new Vector3(HX, top - CASING_LEN, HZ);
        screen.transform.localScale = new Vector3(1f, SCREEN_LEN, 1f);
        _ = total;
    }

    void EvalSeal(float t)
    {
        float gap0 = Tw(5, "gap"), filled = Tw(5, "filled");
        Arrow(1, false, Vector3.zero, Vector3.down, 0f, t); Arrow(2, false, Vector3.zero, Vector3.down, 0f, t);
        float glow = PE(t, gap0 - 0.15f, gap0 + 0.25f) * (1f - PE(t, filled + 0.2f, filled + 0.6f));
        for (int s2 = 0; s2 < 2; s2++)
        {
            var g = gapGlow[s2];
            Show(g, glow > 0.01f);
            if (!g.activeSelf) continue;
            float sx = s2 == 0 ? HX - SEAL_CX : HX + SEAL_CX;
            float pulse = 0.75f + 0.25f * Mathf.Sin((t - gap0) * 9f);
            g.transform.position = new Vector3(sx, (SEAL_BOTTOM + 0f) / 2f, 0.02f);
            g.transform.localScale = new Vector3(SEAL_W * pulse * glow, -SEAL_BOTTOM, 0.02f);
        }

        float clayA = Tw(5, "filled") + 0.05f, clayB = Tw(5, "and") + 0.05f, cemB = Tw(5, "so") + 0.3f;
        float clayTop = Mathf.Lerp(SEAL_BOTTOM, -2.9f, PE(t, clayA, clayB));
        float cemTop = Mathf.Lerp(-2.9f, -0.02f, PE(t, clayB, cemB));
        for (int s = 0; s < 2; s++)
        {
            float sx = s == 0 ? HX - SEAL_CX : HX + SEAL_CX;
            var clay = s == 0 ? sealClayL : sealClayR;
            var cem = s == 0 ? sealCemL : sealCemR;
            float ch = clayTop - SEAL_BOTTOM;
            Show(clay, ch > 0.02f);
            clay.transform.position = new Vector3(sx, SEAL_BOTTOM + ch / 2f, NOTCH / 2f + 0.005f);
            clay.transform.localScale = new Vector3(SEAL_W, Mathf.Max(ch, 0.001f), NOTCH - 0.01f);
            float eh = cemTop + 2.9f;
            Show(cem, t > clayB && eh > 0.02f);
            cem.transform.position = new Vector3(sx, -2.9f + eh / 2f, NOTCH / 2f + 0.005f);
            cem.transform.localScale = new Vector3(SEAL_W, Mathf.Max(eh, 0.001f), NOTCH - 0.01f);
        }
        // bentonite chips rain down the gap (the clay phase)
        bool chipsOn = t > clayA - 0.35f && t < clayB + 0.1f;
        foreach (var g in cemBits) Show(g, false);
        for (int i = 0; i < chipsBent.Length; i++)
        {
            var g = chipsBent[i];
            Show(g, chipsOn);
            if (!chipsOn) continue;
            float sx = ((i % 2 == 0) ? HX - SEAL_CX : HX + SEAL_CX) + (Hash(i, 3) - 0.5f) * (SEAL_W - 0.04f);
            float ph = Frac(t * 1.9f + i * 0.137f);
            g.transform.position = new Vector3(sx, Mathf.Lerp(0.6f, clayTop + 0.03f, ph * ph), 0.08f + 0.25f * Hash(i, 6));
            g.transform.localScale = new Vector3(0.035f, 0.03f, 0.035f);
            g.transform.rotation = Quaternion.Euler(ph * 400f + i * 30f, ph * 300f, i * 50f);
        }
        // tremie hoses: down to the bottom, then they ride up just under the rising grout
        float hoseIn = PE(t, filled - 0.6f, clayA + 0.1f), hoseOut = PE(t, cemB, cemB + 0.5f);
        float tip = Mathf.Lerp(3.5f, SEAL_BOTTOM + 0.1f, hoseIn);
        if (t > clayB) tip = cemTop - 0.18f;
        tip = Mathf.Lerp(tip, 3.5f, hoseOut);
        for (int s2 = 0; s2 < 2; s2++)
        {
            var g = tremie[s2];
            bool on = hoseIn > 0.01f && hoseOut < 0.99f;
            Show(g, on);
            if (!on) continue;
            float sx = s2 == 0 ? HX - SEAL_CX - 0.02f : HX + SEAL_CX + 0.02f;
            float top = 4.5f;
            g.transform.position = new Vector3(sx, (top + tip) / 2f, 0.12f);
            g.transform.localScale = new Vector3(0.05f, Mathf.Max(0.01f, (top - tip) / 2f), 0.05f);
        }

        // muddy runoff spreads on the ground around the casing and stops at the sealed collar
        float sp0 = Tw(5, "dirty"), sp1 = Tw(5, "sneak") + 0.4f;
        float spk = PE(t, sp0, sp1) * (1f - PE(t, Tw(6, "At") - 0.3f, Tw(6, "At")));
        Show(seep, spk > 0.01f);
        if (spk > 0.01f)
        {
            seep.transform.position = new Vector3(HX - 0.95f + 0.25f * spk, 0.012f, 0.75f);
            seep.transform.localScale = new Vector3(1.1f * spk + 0.1f, 0.006f, 0.7f * spk + 0.1f);
        }
        // dirty rain: streaks fall, the sealed top stops them
        float r0 = Tw(5, "dirty") - 0.15f, r1 = Tw(6, "At");
        bool rainOn = t > r0 && t < r1;
        for (int i = 0; i < rain.Length; i++)
        {
            var g = rain[i];
            Show(g, rainOn);
            if (!rainOn) continue;
            float x = HX + (Hash(i, 4) - 0.5f) * 3.6f;
            if (i % 3 == 0) x = HX + (i % 2 == 0 ? -SEAL_CX : SEAL_CX);
            float z = 0.05f + Hash(i, 8) * 1.2f;
            float ph = Frac(t * 1.1f + i * 0.137f);
            float y = Mathf.Lerp(3.4f, 0.02f, ph);
            g.transform.position = new Vector3(x, y, z);
            g.transform.localScale = new Vector3(0.008f, 0.16f, 0.008f) * (ph > 0.96f ? 0.3f : 1f);
        }
    }

    void EvalScreen(float t)
    {
        float slots = Tw(6, "slots"), wat = Tw(6, "Water"), sand = Tw(6, "Sand");
        bool glow = t > slots && t < wat;
        float pulse = glow ? 0.5f + 0.5f * Mathf.Sin((t - slots) * 14f) : 0f;
        foreach (var r in screen.GetComponentsInChildren<Renderer>())
            if (r.gameObject.name.StartsWith("band")) r.sharedMaterial = pulse > 0.5f ? screenGlow : screenMat;

        float lvl = Mathf.Lerp(-5.3f, -2.4f, PE(t, wat, wat + 2.4f));
        bool wOn = t > wat - 0.1f;
        Show(casingWater, wOn);
        if (wOn)
        {
            float h = lvl + 5.3f;
            casingWater.transform.position = new Vector3(HX, -5.3f + h / 2f, HZ + 0.08f);
            casingWater.transform.localScale = new Vector3(0.38f, Mathf.Max(h, 0.001f), 0.12f);
        }
        bool inOn = t > wat - 0.2f;
        for (int i = 0; i < inDrops.Length; i++)
        {
            var g = inDrops[i];
            Show(g, inOn);
            if (!inOn) continue;
            float ph = Frac(t * 0.6f + i * 0.173f);
            float side = (i % 2 == 0) ? -1f : 1f;
            float y = Mathf.Lerp(-4.0f, -5.2f, Hash(i, 8)) + 0.06f * Mathf.Sin(ph * 6.283f + i);
            float x = Mathf.Lerp(HX + side * 1.9f, HX + side * (CASE_R - 0.02f), E(ph));
            g.transform.position = new Vector3(x, y, -0.03f);
            g.transform.localScale = new Vector3(1.4f, 1f, 1f) * 0.1f * (ph > 0.9f ? (1f - ph) / 0.1f : Mathf.Min(1f, ph * 8f));
        }
        // bubbles rise in the casing while water squeezes in
        bool bOn = t > wat && t < Tw(7, "pump");
        for (int i = 0; i < bubbles.Length; i++)
        {
            var g = bubbles[i];
            Show(g, bOn);
            if (!bOn) continue;
            float ph = Frac(t * 0.55f + i / (float)bubbles.Length);
            float y = Mathf.Lerp(-5.25f, lvl - 0.05f, ph);
            g.transform.position = new Vector3(HX + (Hash(i, 2) - 0.5f) * 0.26f + 0.02f * Mathf.Sin(t * 5f + i), y, HZ - 0.02f);
            g.transform.localScale = Vector3.one * (0.025f + 0.025f * Hash(i, 3)) * (y < lvl - 0.1f ? 1f : 0f);
        }
        bool sOn = t > sand - 0.1f && t < Tw(7, "A");
        for (int i = 0; i < grains.Length; i++)
        {
            var g = grains[i];
            Show(g, sOn);
            if (!sOn) continue;
            // a grain rides in with the water, hits the slots, bounces back and drops into the gravel pack
            float ph = Frac((t - sand) * 0.7f + i * 0.0713f * 1.3f);
            float side = (i % 2 == 0) ? -1f : 1f;
            float y0 = Mathf.Lerp(-4.1f, -5.0f, Hash(i, 11));
            float xHit = HX + side * (CASE_R + 0.035f), xFar = HX + side * 1.4f;
            Vector3 p;
            if (ph < 0.55f) p = new Vector3(Mathf.Lerp(xFar, xHit, E(ph / 0.55f)), y0, -0.035f);
            else
            {
                float u = (ph - 0.55f) / 0.45f;
                p = new Vector3(xHit + side * 0.28f * u, Mathf.Max(-5.25f, y0 + 0.25f * u - 1.4f * u * u), -0.035f);
            }
            g.transform.position = p;
            g.transform.localScale = new Vector3(1f, 0.85f, 1f) * 0.085f;
        }
    }

    void EvalPump(float t)
    {
        float dropA = Tw(7, "drops") - 0.2f, dropB = Tw(7, "pipe") + 0.55f;
        float topY = Mathf.Lerp(2.0f, -3.05f, PE(t, dropA, dropB));
        bool on = t > dropA - 0.25f;
        Show(pump, on);
        pump.transform.position = new Vector3(HX, topY, HZ);
        float pipeTop = Mathf.Lerp(3.0f, TRENCH_Y, PE(t, dropB, dropB + 0.5f));
        Show(pumpPipe, on && topY < pipeTop);
        pumpPipe.transform.position = new Vector3(HX + 0.04f, (pipeTop + topY) / 2f, HZ);
        pumpPipe.transform.localScale = new Vector3(0.065f, Mathf.Max(0.001f, (pipeTop - topY) / 2f), 0.065f);

        float tr0 = TrenchStart, trK = PE(t, tr0, tr0 + 0.55f);
        bool trOn = t > tr0;
        Show(trenchPipe, trOn); Show(riserPipe, trOn);
        float x0 = HX, x1 = Mathf.Lerp(HX, RISER_X, trK);
        trenchPipe.transform.position = new Vector3((x0 + x1) / 2f, TRENCH_Y, HZ);
        trenchPipe.transform.rotation = Quaternion.Euler(0, 0, 90);
        trenchPipe.transform.localScale = new Vector3(0.09f, Mathf.Max((x1 - x0) / 2f, 0.001f), 0.09f);
        float rK = PE(t, tr0 + 0.5f, tr0 + 0.85f);
        float rTop = Mathf.Lerp(TRENCH_Y, 0.62f, rK);
        riserPipe.transform.position = new Vector3(RISER_X, (TRENCH_Y + rTop) / 2f, HZ);
        riserPipe.transform.localScale = new Vector3(0.075f, Mathf.Max((rTop - TRENCH_Y) / 2f, 0.001f), 0.075f);
        float hK = Back(P(t, tr0 + 0.8f, tr0 + 1.1f));
        Show(hydrant, t > tr0 + 0.8f);
        hydrant.transform.localScale = Vector3.one * Mathf.Max(hK, 0.001f);
        Show(paver, true);

        float fA = Tw(7, "pushes"), fB = Tw(9, "glass") + 2.6f;
        bool fOn = t > fA && t < fB && topY < -3.0f;
        Vector3[] path = { new Vector3(HX + 0.04f, -3.0f, HZ - 0.05f), new Vector3(HX + 0.04f, TRENCH_Y, HZ - 0.05f), new Vector3(RISER_X, TRENCH_Y, HZ - 0.06f), new Vector3(RISER_X, 0.55f, HZ - 0.06f) };
        float total = 0; var seg = new float[path.Length - 1];
        for (int i = 0; i < seg.Length; i++) { seg[i] = Vector3.Distance(path[i], path[i + 1]); total += seg[i]; }
        float reach = seg[0] + trK * seg[1] + rK * seg[2];
        for (int i = 0; i < flow.Length; i++)
        {
            var g = flow[i];
            float d = Frac((t - fA) * 0.5f + i / (float)flow.Length) * total;
            bool vis = fOn && d < reach;
            Show(g, vis);
            if (!vis) continue;
            Vector3 p = path[path.Length - 1];
            float dd = d;
            for (int s = 0; s < seg.Length; s++) { if (dd <= seg[s]) { p = Vector3.Lerp(path[s], path[s + 1], dd / seg[s]); break; } dd -= seg[s]; }
            g.transform.position = p;
            g.transform.localScale = Vector3.one * 0.06f;
        }

        bool lit = (t > Tw(7, "home") && t < Tw(8, "On") + 0.6f) || t > Tw(9, "glass");
        foreach (var r in houseWindows) r.sharedMaterial = lit ? windowLit : houseWindowMat;
        var home = Tw(7, "home");
        Arrow(3, t > home - 0.15f && t < home + 1.3f, new Vector3(HOUSE_X - 2.2f, 5.9f, HOUSE_Z - 3.3f), Vector3.down, 1.6f * Back(P(t, home - 0.15f, home + 0.2f)), t);
    }

    void EvalSurface(float t)
    {
        float capA = Tw(8, "cap") - 0.45f, capB = Tw(8, "cap") + 0.15f;
        float chEnd = Tw(9, "chlorine", 0, true);
        float lift = PE(t, Tw(9, "flushed") - 0.15f, Tw(9, "flushed") + 0.25f) * (1f - PE(t, chEnd + 1.3f, chEnd + 1.8f));
        float capY = Mathf.Lerp(CASING_TOP + 1.4f, CASING_TOP, Back(P(t, capA, capB))) + 0.9f * lift;
        Show(cap, t > capA - 0.1f);
        cap.transform.position = new Vector3(HX + 0.45f * lift, capY, HZ);
        cap.transform.rotation = Quaternion.Euler(0, 180f, -25f * lift);

        float sl = PE(t, Tw(8, "slopes") - 0.1f, Tw(8, "slopes") + 0.8f);
        Show(slopeL, sl > 0.01f); Show(slopeR, sl > 0.01f);
        slopeL.transform.position = new Vector3(HX - HW - 0.95f, -0.02f, 1.0f);
        slopeL.transform.localScale = new Vector3(2.0f, 0.32f * sl, 1.9f);
        slopeR.transform.position = new Vector3(HX + HW + 0.95f, -0.02f, 1.0f);
        slopeR.transform.localScale = new Vector3(2.0f, 0.32f * sl, 1.9f);

        float pA = Tw(8, "puddles"), pB = Tw(8, "it", 0, true) + 0.3f;
        bool pOn = t > pA - 0.15f && t < pB;
        Show(puddle, pOn);
        if (pOn)
        {
            float k = PE(t, pA, Tw(8, "never") + 0.3f);
            float x = Mathf.Lerp(HX - HW - 0.55f, HX - HW - 2.3f, k);
            float y = Mathf.Lerp(0.12f, 0.01f, k);
            float s = Mathf.Lerp(0.15f, 0.65f, PE(t, pA - 0.15f, pA + 0.4f)) * (1f - PE(t, pB - 0.5f, pB));
            puddle.transform.position = new Vector3(x, y + 0.006f, 0.9f);
            puddle.transform.localScale = new Vector3(s * 1.3f, 0.006f, s * 0.9f);
        }
        float away = Tw(8, "away");
        Arrow(4, t > away - 0.1f && t < Tw(8, "so") + 0.7f, new Vector3(HX - 2.4f, 0.55f, 0.6f), new Vector3(-0.8f, -0.6f, 0f).normalized, 0.9f * Back(P(t, away - 0.1f, away + 0.25f)), t);
    }

    void EvalClean(float t)
    {
        float chA = Tw(9, "chlorine"), chB = chA + 1.3f;
        // jug tips over the casing mouth and pours
        float jk = PE(t, Tw(9, "flushed") - 0.1f, Tw(9, "flushed") + 0.35f) * (1f - PE(t, chB + 0.4f, chB + 0.9f));
        Show(jug, jk > 0.01f);
        jug.transform.position = new Vector3(HX + 0.32f, CASING_TOP + 0.35f + 0.6f * (1f - jk), HZ - 0.05f);
        jug.transform.rotation = Quaternion.Euler(0, 0, 105f * PE(t, chA - 0.5f, chA));
        bool pouring = t > chA - 0.1f && t < chB + 0.3f;
        Show(jugStream, pouring);
        if (pouring)
        {
            jugStream.transform.position = new Vector3(HX + 0.05f, CASING_TOP + 0.2f, HZ - 0.05f);
            jugStream.transform.localScale = new Vector3(0.035f, 0.22f, 0.035f);
        }
        float front = Mathf.Lerp(0.3f, -5.3f, PE(t, chA, chB));
        float fade = 1f - PE(t, chB + 0.1f, chB + 0.8f);
        bool on = t > chA && fade > 0.01f;
        Show(chlorine, on);
        if (on)
        {
            float h = 0.3f - front;
            chlorine.transform.position = new Vector3(HX, (0.3f + front) / 2f, HZ + 0.08f);
            chlorine.transform.localScale = new Vector3(0.37f * fade, h, 0.12f);
        }
        for (int i = 0; i < germs.Length; i++)
        {
            var g = germs[i];
            float gy = -2.8f - 0.36f * i;
            float popT = chA + (0.3f - gy) / 5.6f * (chB - chA);
            float vis = (t > Tw(6, "Water") + 1.4f ? 1f : 0f) * (t < popT ? 1f : 1f - PE(t, popT, popT + 0.2f));
            Show(g, vis > 0.01f);
            if (vis <= 0.01f) continue;
            g.transform.position = new Vector3(HX + (i % 2 == 0 ? -0.08f : 0.08f) + 0.03f * Mathf.Sin(t * 3f + i), gy + 0.03f * Mathf.Sin(t * 4f + i), HZ - 0.06f);
            g.transform.rotation = Quaternion.Euler(0, 0, t * 40f + i * 50f);
            g.transform.localScale = new Vector3(0.05f, 0.06f, 0.05f) * vis;
        }
        for (int i = 0; i < sparks.Length; i++)
        {
            var g = sparks[i];
            float st; Vector3 c;
            if (i < germs.Length) { float gy = -2.8f - 0.36f * i; st = chA + (0.3f - gy) / 5.6f * (chB - chA); c = new Vector3(HX, gy, HZ - 0.1f); }
            else { st = Tw(9, "germ-free") + 0.1f * (i - germs.Length); c = new Vector3(RISER_X - 0.25f + 0.12f * (i - germs.Length), 0.48f, HZ - 0.25f); }
            float k = P(t, st, st + 0.45f);
            bool vis = k > 0f && k < 1f;
            Show(g, vis);
            if (!vis) continue;
            Billboard(g, c + new Vector3(Mathf.Cos(i * 1.3f), Mathf.Sin(i * 1.3f), 0f) * 0.2f * k, 0.16f * (1f - k));
        }

        // glass under the hydrant spout
        float gA = Tw(9, "glass") - 0.25f, gB = Tw(9, "germ-free", 0, true) + 0.2f;
        Vector3 gp = new Vector3(RISER_X - 0.29f, 0.06f, HZ);
        float gs = Back(P(t, gA - 0.35f, gA));
        Show(glass, gs > 0.01f); Show(glassWater, t > gA && gs > 0.01f);
        glass.transform.position = gp + new Vector3(0f, 0.11f * gs, 0f);
        glass.transform.localScale = new Vector3(0.13f, 0.11f, 0.13f) * gs;
        float wk = PE(t, gA, gB), wh = Mathf.Max(0.001f, 0.18f * wk);
        glassWater.transform.position = gp + new Vector3(0f, 0.012f + wh / 2f, 0f);
        glassWater.transform.localScale = new Vector3(0.115f, wh / 2f, 0.115f);
        bool pOn = t > gA && t < gB + 0.25f;
        Show(pour, pOn);
        if (pOn)
        {
            float topY = 0.62f + 0.06f, botY = gp.y + 0.012f + wh;
            pour.transform.position = new Vector3(gp.x, (topY + botY) / 2f, HZ);
            pour.transform.localScale = new Vector3(0.022f, (topY - botY) / 2f, 0.022f);
        }
        for (int i = 0; i < tapDrops.Length; i++) Show(tapDrops[i], false);
    }

    void EvalLabels(float t)
    {
        foreach (var L in labels) L.k = Back(P(t, L.t0, L.t0 + 0.3f)) * (1f - PE(t, L.t1 - 0.25f, L.t1));
    }
}
