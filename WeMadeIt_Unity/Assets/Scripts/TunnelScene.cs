// Short 02, "a tunnel under a river" (HDRP). The whole video is a pure function of time: Evaluate(t) puts
// every object in its state at second t; beat times come from the voice (docs/scenes/tunnel_words.json).
// In motion at frame 0: stay-to-watch is decided in the first seconds (docs/scenes/tunnel.md).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class TunnelScene : MonoBehaviour, IShortScene
{
    // ------------------------------------------------------------------ voice timing
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

    // ------------------------------------------------------------------ helpers
    static float P(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(1e-4f, b - a));
    static float E(float x) => x * x * (3f - 2f * x);
    static float PE(float t, float a, float b) => E(P(t, a, b));
    static float OutCubic(float x) { x = Mathf.Clamp01(x); return 1f - Mathf.Pow(1f - x, 3f); }
    static float InCubic(float x) { x = Mathf.Clamp01(x); return x * x * x; }
    static float Back(float x) { const float c = 1.7f; x = Mathf.Clamp01(x); return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }
    static float Frac(float x) => x - Mathf.Floor(x);
    static float Hash(int i, int k) => Frac(Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f);
    static Color Hex(string h) => WellLook.Hex(h);
    static void Show(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }

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

    GameObject[] Many(int n, PrimitiveType t, string name, Material m)
    {
        var a = new GameObject[n];
        for (int i = 0; i < n; i++) { a[i] = Prim(t, name + i, Vector3.zero, Vector3.one, m); a[i].SetActive(false); }
        return a;
    }

    static readonly Quaternion FACE = Quaternion.Euler(0f, 180f, 0f);   // FBX fronts land on +Z: turn to the camera

    GameObject Model(string file, Vector3 pos)
    {
        var src = Resources.Load<GameObject>("Models/" + file);
        if (src == null) { Debug.LogError("model missing: " + file); return new GameObject(file); }
        var g = Instantiate(src);
        g.name = file;
        g.transform.SetParent(stage, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = FACE;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++) ms[i] = WellLook.ModelMaterial(ms[i] != null ? ms[i].name.Replace(" (Instance)", "") : "");
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

    static Mesh ConeMesh(float r, float h, int seg)
    {
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

    // ------------------------------------------------------------------ layout
    const float BED = -3f, AXIS = -6.5f, R = 2.1f, TOP = AXIS + R, FLOOR = AXIS - R, Z_CUT = R + 0.12f;
    static readonly float[] LayerY = { BED, -3.75f, -16f, -60f };
    const float RING_W = 1.4f;
    const float FLOOD_X = 0.1f;              // where the river breaks in
    const float RIVER_Z1 = 260f;

    // ------------------------------------------------------------------ objects
    Camera cam;
    CutGround ground;
    Mesh surfMesh, faceMesh;
    Vector3[] surfV, faceV;
    GameObject barge;
    GameObject cutter, shield, flight, tube, erector, chamberFill, chamberGlow;
    GameObject[] gantries, rams, rods, railPieces, fish, motes;
    GameObject[][] rings;               // [ring][seg]
    const int RING_BACK = 26;           // rings behind ring 0 (index 0..RING_BACK-1 = already built)
    const int RING_NEW = 5;             // rings built on screen
    GameObject floodTop;
    Mesh floodSheet; Vector3[] floodV;
    GameObject floodSheetGo;
    GameObject[] floodBodyPuffs, floodDrops, floodSpray, floodFoamPuffs, floodChunks, props, bulbs;
    GameObject[] chips, sparks, dust, dirtScrew, dirtBelt, arrowsBlue, arrowsOrange;
    Light lampA, lampB;
    GameObject groutOld, sunBlock, spade, mudTop;
    Mesh mudMesh; Vector3[] mudV; GameObject mudGo;
    GameObject[] stones;
    GameObject[] crownBulbs;
    Light lampC;
    readonly List<GameObject> shieldHoses = new List<GameObject>();
    GameObject[] macroSeal, macroDrops, trainCars;
    GameObject deck;

    // ------------------------------------------------------------------ build
    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<WellScene.Doc>(File.ReadAllText(wordsJson));
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);
        WellLook.FoliageTint = new Color(0.62f, 0.82f, 0.5f);      // the leaves read pale under this sun
        WellLook.GradeContrast = 6f; WellLook.GradeSaturation = 0f; WellLook.GradeWarm = 10f;
        WellLook.Sun(stage);
        WellLook.Environment(stage);
        cam = WellLook.Camera(stage, 34f);

        var mats = new[] {
            WellLook.Textured("excavated_soil_wall", new Color(1.05f, 0.92f, 0.76f), 1.5f, 0.25f),              // silt band with vertical streaks
            WellLook.Textured("red_laterite_soil_stones", new Color(1.45f, 1.22f, 1.0f), 1.25f, 0.25f),        // one calm, fine-grained earth
            WellLook.Textured("rock_wall_02", Hex("#B7B3AD"), 3.0f, 0.35f),                    // bedrock
        };
        var bedTop = WellLook.Textured("brown_mud_03", Hex("#8C7E66"), 2.0f, 0.5f);
        ground = new CutGround(stage, LayerY, mats, bedTop);

        BuildRiver();
        BuildMachine();
        BuildFlood();
        BuildFx();
        BuildLabels();
    }

    void BuildRiver()
    {
        // the surface: a moving grid, dense near the camera
        const int NX = 220, NZ = 90;
        surfV = new Vector3[(NX + 1) * (NZ + 1)];
        var tris = new int[NX * NZ * 6];
        int q = 0;
        for (int j = 0; j < NZ; j++)
            for (int i = 0; i < NX; i++)
            {
                int a = j * (NX + 1) + i, b = a + 1, c = a + NX + 1, d = c + 1;
                tris[q++] = a; tris[q++] = c; tris[q++] = b; tris[q++] = b; tris[q++] = c; tris[q++] = d;
            }
        surfMesh = new Mesh { name = "RiverSurface", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        surfMesh.MarkDynamic();
        surfMesh.vertices = surfV;
        surfMesh.triangles = tris;
        var sg = new GameObject("RiverSurface"); sg.transform.SetParent(stage, false);
        sg.AddComponent<MeshFilter>().sharedMesh = surfMesh;
        sg.AddComponent<MeshRenderer>().sharedMaterial = WellLook.Water("riverTop", Hex("#4A6E66"), 0.9f, 0.93f, false);

        // the cut face of the water (a sheet at z = 0 whose top edge follows the waves)
        faceV = new Vector3[(NX + 1) * 2];
        var ft = new int[NX * 6]; q = 0;
        for (int i = 0; i < NX; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            ft[q++] = a; ft[q++] = c; ft[q++] = b; ft[q++] = b; ft[q++] = c; ft[q++] = d;
        }
        faceMesh = new Mesh { name = "RiverFace" };
        faceMesh.MarkDynamic();
        faceMesh.vertices = faceV;
        faceMesh.triangles = ft;
        var fg = new GameObject("RiverFace"); fg.transform.SetParent(stage, false);
        fg.AddComponent<MeshFilter>().sharedMesh = faceMesh;
        fg.AddComponent<MeshRenderer>().sharedMaterial = WellLook.Water("riverFace", Hex("#2F5550"), 0.62f, 0.92f, false);

        // depth: water darkens toward the bed
        var grad = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) grad.SetPixel(0, y, new Color(1, 1, 1, Mathf.Lerp(0.3f, 0f, y / 63f)));
        grad.Apply();
        Prim(PrimitiveType.Quad, "riverDepth", new Vector3(0, (BED - 0.12f) / 2f, -0.012f), new Vector3(120f, -BED - 0.12f, 1f),
             WellLook.Unlit("riverDepth", Hex("#0E221E"), grad, true));

        // sun shafts through the water
        var shaftTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float across = 1f - Mathf.Abs(x / 31f - 0.5f) * 2f;
                shaftTex.SetPixel(x, y, new Color(1, 1, 1, E(across) * (y / 31f) * 0.22f));
            }
        shaftTex.Apply();
        var shaftMat = WellLook.Unlit("shaft", new Color(0.75f, 0.85f, 0.75f, 0.55f), shaftTex, true);
        for (int i = 0; i < 9; i++)
        {
            var s = Prim(PrimitiveType.Quad, "shaft" + i, new Vector3(-9f + i * 2.3f + Hash(i, 2), BED / 2f - 0.05f, -0.016f), new Vector3(0.55f + 0.7f * Hash(i, 3), -BED + 0.3f, 1f), shaftMat);
            s.transform.rotation = Quaternion.Euler(0, 0, -14f);
        }
        fish = Many(14, PrimitiveType.Capsule, "fish", WellLook.Lit("fish", Hex("#40515A"), 0.7f, 0.3f));
        motes = Many(40, PrimitiveType.Sphere, "mote", WellLook.Lit("mote", Hex("#D8E8E6"), 0.3f));

        // bed, banks along the river, forest on both sides
        for (int i = 0; i < 12; i++)
            PH("boulder_01", -1, new Vector3(-4f + i * 0.75f + Hash(i, 3), BED - 0.05f, 0.6f + 9f * Hash(i, 4)), 360f * Hash(i, 5), 0.18f + 0.25f * Hash(i, 6));
        BuildBanks();
        var mud = WellLook.Textured("brown_mud_03", Hex("#B0A08A"), 2.6f, 0.3f);
        Prim(PrimitiveType.Cube, "farBank", new Vector3(0, -1f, RIVER_Z1 + 80f), new Vector3(500f, 4.4f, 160f), mud);
        var lawn = WellLook.Textured("leafy_grass", new Color(0.7f, 0.72f, 0.42f), 2.0f, 0.25f);
        Prim(PrimitiveType.Cube, "farLawn", new Vector3(0, 1.22f, RIVER_Z1 + 80f), new Vector3(500f, 0.05f, 160f), lawn);
        barge = Model("barge", Vector3.zero);
        BuildForest();
    }

    // Profile of the right bank from the river bed out; the left bank mirrors it. Extruded along the river.
    static readonly float[] BankX = { 3.6f, 5.0f, 5.6f, 5.8f, 6.6f, 8f, 260f };
    static readonly float[] BankY = { BED, -0.6f, -0.05f, 0.1f, 0.75f, 1.2f, 1.2f };

    void BuildBanks()
    {
        var earth = WellLook.Textured("red_laterite_soil_stones", new Color(1.45f, 1.22f, 1.0f), 1.25f, 0.25f);
        var topsoil = WellLook.Textured("excavated_soil_wall", new Color(1.05f, 0.92f, 0.76f), 1.5f, 0.25f);
        var mud = WellLook.Textured("brown_mud_03", Hex("#CDBC9C"), 2.4f, 0.35f);
        var lawn = WellLook.Textured("leafy_grass", new Color(0.7f, 0.72f, 0.42f), 2.0f, 0.25f);
        const float Z0 = -0.03f, Z1 = 400f, BOT = -3.76f, SPLIT = 0.1f;
        var v = new List<Vector3>(); var n = new List<Vector3>();
        var tr = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 nrm, int sub)
        {
            int o = v.Count;
            v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
            n.Add(nrm); n.Add(nrm); n.Add(nrm); n.Add(nrm);
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), nrm) > 0f) { tr[sub].Add(o); tr[sub].Add(o + 1); tr[sub].Add(o + 2); tr[sub].Add(o); tr[sub].Add(o + 2); tr[sub].Add(o + 3); }
            else { tr[sub].Add(o); tr[sub].Add(o + 2); tr[sub].Add(o + 1); tr[sub].Add(o); tr[sub].Add(o + 3); tr[sub].Add(o + 2); }
        }
        // how far a bank comes in toward the river at distance z (0 at the cut face, so the section stays clean)
        float Wander(float side, float z)
        {
            float k = E(Mathf.Clamp01(z / 9f));
            return -k * (0.85f + 0.55f * Mathf.Sin(z * 0.047f + side * 1.3f) + 0.3f * Mathf.Sin(z * 0.13f + side * 2.1f));
        }
        const int ROWS = 90;
        foreach (float side in new[] { 1f, -1f })
            for (int i = 0; i + 1 < BankX.Length; i++)
            {
                float x0 = side * BankX[i], x1 = side * BankX[i + 1], y0 = BankY[i], y1 = BankY[i + 1];
                // cut face: earth below SPLIT, topsoil above
                Quad(new Vector3(x0, BOT, Z0), new Vector3(x1, BOT, Z0), new Vector3(x1, Mathf.Min(y1, SPLIT), Z0), new Vector3(x0, Mathf.Min(y0, SPLIT), Z0), Vector3.back, 0);
                if (y0 >= SPLIT) Quad(new Vector3(x0, SPLIT, Z0), new Vector3(x1, SPLIT, Z0), new Vector3(x1, y1, Z0), new Vector3(x0, y0, Z0), Vector3.back, 1);
                // the surface running away along the river
                var up = new Vector3(-(y1 - y0) * side, BankX[i + 1] - BankX[i], 0f).normalized;
                for (int r = 0; r < ROWS; r++)
                {
                    float za = r == 0 ? Z0 : Z1 * Mathf.Pow(r / (float)ROWS, 1.6f), zb = Z1 * Mathf.Pow((r + 1) / (float)ROWS, 1.6f);
                    float wa = side * Wander(side, za), wb = side * Wander(side, zb);
                    float x1a = i + 2 < BankX.Length ? x1 + wa : x1, x1b = i + 2 < BankX.Length ? x1 + wb : x1;
                    Quad(new Vector3(x0 + wa, y0, za), new Vector3(x1a, y1, za), new Vector3(x1b, y1, zb), new Vector3(x0 + wb, y0, zb), up, y0 >= 0.75f ? 3 : 2);
                }
            }
        var m = new Mesh { name = "Banks", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(v); m.SetNormals(n);
        m.subMeshCount = 4;
        for (int i = 0; i < 4; i++) m.SetTriangles(tr[i], i, false);
        var tan = new Vector4[v.Count];
        for (int q = 0; q < v.Count; q++)
        {
            var t = Vector3.Cross(n[q], Mathf.Abs(n[q].y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            tan[q] = new Vector4(t.x, t.y, t.z, 1f);
        }
        m.tangents = tan;
        m.RecalculateBounds();
        var g = new GameObject("Banks"); g.transform.SetParent(stage, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        g.AddComponent<MeshRenderer>().sharedMaterials = new[] { earth, topsoil, mud, lawn };
    }

    // a Poly Haven model scaled to a target height
    GameObject Tree(string id, Vector3 pos, float yaw, float height)
    {
        var g = PH(id, -1, pos, yaw, 1f);
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.localScale = Vector3.one * (height / Mathf.Max(0.1f, b.size.y));
        return g;
    }

    void BuildForest()
    {
        string[] kinds = { "island_tree_01", "island_tree_02", "tree_small_02" };
        int k = 0;
        foreach (float side in new[] { 1f, -1f })
        {
            for (int i = 0; i < 16; i++)      // lining the river, into the distance
            {
                float z = 5f + i * 8.5f + 5f * Hash(i, 41);
                Tree(kinds[k++ % 2], new Vector3(side * (8.2f + 3.5f * Hash(i, 42)), 1.2f, z), 360f * Hash(i, 43), 8f + 5f * Hash(i, 44));
            }
            for (int i = 0; i < 6; i++)       // behind them
                Tree(kinds[k++ % 2], new Vector3(side * (14f + 14f * Hash(i, 45)), 1.2f, 4f + 26f * Hash(i, 46)), 360f * Hash(i, 47), 9f + 5f * Hash(i, 48));
            Tree("tree_small_02", new Vector3(side * 7.6f, 1.2f, 2.2f), 90f * side, 6f);
        }
        for (int i = 0; i < 8; i++)
            Tree(kinds[i % 2], new Vector3(-60f + i * 17f + 6f * Hash(i, 51), 1.2f, RIVER_Z1 + 3f + 9f * Hash(i, 52)), 360f * Hash(i, 53), 12f + 6f * Hash(i, 54));

        // low plants on the bank tops
        var rng = new System.Random(23);
        for (int i = 0; i < 1800; i++)
        {
            float z = Mathf.Pow((float)rng.NextDouble(), 1.6f) * 26f + 0.05f;
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * (6.9f + (float)rng.NextDouble() * 10f);
            string id = rng.NextDouble() < 0.5 ? "grass_medium_01" : (rng.NextDouble() < 0.5 ? "grass_medium_02" : "grass_bermuda_01");
            PH(id, rng.Next(0, 21), new Vector3(x, 1.2f, z), (float)rng.NextDouble() * 360f, 1.0f + (float)rng.NextDouble() * 0.7f);
        }
        for (int i = 0; i < 26; i++)
            PH("shrub_02", i % 4, new Vector3((i % 2 == 0 ? -1f : 1f) * (7.2f + 9f * Hash(i, 61)), 1.2f, 0.6f + 16f * Hash(i, 62)), 360f * Hash(i, 63), 0.45f + 0.3f * Hash(i, 64));
    }

    void BuildMachine()
    {
        cutter = Model("cutter", Vector3.zero);
        shield = Model("tbm_shield", Vector3.zero);
        foreach (Transform c in shield.transform) if (c.name.StartsWith("hose")) shieldHoses.Add(c.gameObject);
        flight = Model("screw_flight", Vector3.zero);
        tube = Model("screw_tube", Vector3.zero);
        erector = Model("erector", Vector3.zero);

        chamberFill = Prim(PrimitiveType.Cube, "chamberFill", Vector3.zero, new Vector3(0.34f, 2f * R - 0.2f, R - 0.05f), WellLook.Textured("excavated_soil_wall", Hex("#B9A08A"), 0.8f, 0.5f));
        chamberGlow = Prim(PrimitiveType.Cube, "chamberGlow", Vector3.zero, new Vector3(0.36f, 2f * R - 0.3f, R - 0.15f), WellLook.Unlit("chamberGlow", new Color(1f, 0.42f, 0.08f, 0.16f), null, true));

        rams = new GameObject[3]; rods = new GameObject[3];
        var barrelMat = WellLook.ModelMaterial("tbm_yellow");
        var rodMat = WellLook.Metal("chrome", Hex("#E4E8EC"), 0.92f, 1f);
        for (int i = 0; i < 3; i++)
        {
            rams[i] = Prim(PrimitiveType.Cylinder, "ram" + i, Vector3.zero, Vector3.one, barrelMat);
            rods[i] = Prim(PrimitiveType.Cylinder, "rod" + i, Vector3.zero, Vector3.one, rodMat);
        }

        rings = new GameObject[RING_BACK + RING_NEW][];
        for (int k = 0; k < rings.Length; k++)
        {
            rings[k] = new GameObject[4];
            for (int s = 0; s < 4; s++) { rings[k][s] = Model("ring_seg", Vector3.zero); rings[k][s].SetActive(false); }
        }
        groutOld = Model("grout_shell", Vector3.zero);
        mudV = new Vector3[41 * 2];
        var mt = new int[40 * 6]; int mq = 0;
        for (int i = 0; i < 40; i++)
        {
            int v0 = i * 2;
            mt[mq++] = v0; mt[mq++] = v0 + 2; mt[mq++] = v0 + 1; mt[mq++] = v0 + 1; mt[mq++] = v0 + 2; mt[mq++] = v0 + 3;
        }
        mudMesh = new Mesh { name = "Mud" };
        mudMesh.MarkDynamic(); mudMesh.vertices = mudV; mudMesh.triangles = mt;
        mudGo = new GameObject("Mud"); mudGo.transform.SetParent(stage, false);
        mudGo.AddComponent<MeshFilter>().sharedMesh = mudMesh;
        mudGo.AddComponent<MeshRenderer>().sharedMaterial = WellLook.Emissive(WellLook.Water("mud", Hex("#7A5A3C"), 0.97f, 0.72f, false), Hex("#7A5A3C"), 2600f);
        mudTop = Prim(PrimitiveType.Cube, "mudTop", Vector3.zero, Vector3.one, WellLook.Emissive(WellLook.Water("mudTop", Hex("#7A5A3C"), 0.97f, 0.75f, false), Hex("#7A5A3C"), 2600f));
        // scattered stones on the cut face: the soil reads as earth, not wallpaper
        var stoneA = WellLook.Textured("rock_wall_02", new Color(1.1f, 0.98f, 0.86f), 0.25f, 0.3f, true);
        var stoneB = WellLook.Textured("excavated_soil_wall", new Color(0.8f, 0.68f, 0.55f), 0.3f, 0.2f, true);
        stones = new GameObject[150];
        for (int i = 0; i < stones.Length; i++)
        {
            float sx = -14f + 30f * Hash(i, 31), sy = Mathf.Lerp(LayerY[1] - 0.1f, -14.5f, Hash(i, 32));
            if (sy > FLOOR - 0.18f && sy < TOP + 0.18f) sy = FLOOR - 0.4f - 2.5f * Hash(i, 33);
            float r = 0.03f + 0.09f * Hash(i, 34) * Hash(i, 35);
            stones[i] = Prim(PrimitiveType.Sphere, "stone", new Vector3(sx, sy, -0.008f), new Vector3(r * 1.5f, r, 0.016f), i % 3 == 0 ? stoneB : stoneA);
        }
        macroSeal = new GameObject[3];
        for (int i = 0; i < 3; i++) { macroSeal[i] = Model("seal_arc", Vector3.zero); macroSeal[i].SetActive(false); }
        trainCars = new GameObject[3];
        for (int i = 0; i < 3; i++) { trainCars[i] = Model("train_car", Vector3.zero); trainCars[i].SetActive(false); }
        deck = Prim(PrimitiveType.Cube, "deck", new Vector3(0f, AXIS - 1.66f, 0.73f), new Vector3(140f, 0.12f, 2.1f), WellLook.ModelMaterial("concrete_seg"));
        macroDrops = Many(36, PrimitiveType.Sphere, "sealDrop", WellLook.Emissive(WellLook.Water("sealDrop", Hex("#5CC6F5"), 0.9f, 0.98f, false), Hex("#5CC6F5"), 2500f));
        gantries = new GameObject[4];
        for (int i = 0; i < gantries.Length; i++) gantries[i] = Model("gantry", Vector3.zero);
        railPieces = new GameObject[16];
        for (int i = 0; i < railPieces.Length; i++) railPieces[i] = Model("rail_piece", Vector3.zero);

        sunBlock = Prim(PrimitiveType.Cube, "sunBlock", Vector3.zero, Vector3.one, WellLook.Lit("sunBlock", Color.black, 0f));
        sunBlock.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        var flame = WellLook.Emissive(WellLook.Lit("crownBulb", Hex("#FFE2B0"), 0.8f), Hex("#FFC880"), 60000f);
        crownBulbs = new GameObject[8];
        for (int i = 0; i < crownBulbs.Length; i++) crownBulbs[i] = Prim(PrimitiveType.Sphere, "crownBulb", Vector3.zero, Vector3.one * 0.085f, flame);
        var lc = new GameObject("lampC"); lc.transform.SetParent(stage, false);
        lampC = lc.AddComponent<Light>(); lampC.type = LightType.Point; lc.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        lampC.lightUnit = UnityEngine.Rendering.LightUnit.Lumen; lampC.intensity = 300000f; lampC.range = 11f; lampC.color = Hex("#FFC58A");
        var lg = new GameObject("lampA"); lg.transform.SetParent(stage, false);
        lampA = lg.AddComponent<Light>(); lampA.type = LightType.Point; lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        lampA.lightUnit = UnityEngine.Rendering.LightUnit.Lumen; lampA.intensity = 320000f; lampA.range = 11f; lampA.color = Hex("#FFC58A");
        lg = new GameObject("lampB"); lg.transform.SetParent(stage, false);
        lampB = lg.AddComponent<Light>(); lampB.type = LightType.Point; lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        lampB.lightUnit = UnityEngine.Rendering.LightUnit.Lumen; lampB.intensity = 500000f; lampB.range = 10f; lampB.color = Hex("#FFD9A8");
        foreach (var l in new[] { lampA, lampB, lampC }) l.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>().volumetricDimmer = 0f;
    }

    void BuildFlood()
    {
        // water in the tunnel: a dark, silty sheet on the cut plane with a moving top edge
        const int NF = 90;
        floodV = new Vector3[(NF + 1) * 2];
        var ft = new int[NF * 6]; int q = 0;
        for (int i = 0; i < NF; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            ft[q++] = a; ft[q++] = c; ft[q++] = b; ft[q++] = b; ft[q++] = c; ft[q++] = d;
        }
        floodSheet = new Mesh { name = "FloodSheet" };
        floodSheet.MarkDynamic();
        floodSheet.vertices = floodV;
        floodSheet.triangles = ft;
        floodSheetGo = new GameObject("FloodSheet"); floodSheetGo.transform.SetParent(stage, false);
        floodSheetGo.AddComponent<MeshFilter>().sharedMesh = floodSheet;
        floodSheetGo.AddComponent<MeshRenderer>().sharedMaterial = WellLook.Water("floodSheet", Hex("#3C4A36"), 0.86f, 0.88f, false);
        floodTop = Prim(PrimitiveType.Cube, "floodTop", Vector3.zero, Vector3.one, WellLook.Water("floodTop", Hex("#4B5A42"), 0.92f, 0.9f, false));
        // whitewater: soft noisy puffs, a dark body layer behind a bright one
        var puff = PuffTexture();
        floodBodyPuffs = Many(150, PrimitiveType.Quad, "fbody", WellLook.Unlit("fbody", new Color(0.45f, 0.48f, 0.44f, 0.8f), puff, true));
        floodDrops = Many(300, PrimitiveType.Quad, "fdrop", WellLook.Unlit("fdrop", new Color(0.74f, 0.8f, 0.72f, 0.72f), puff, true));
        floodSpray = Many(130, PrimitiveType.Quad, "fspray", WellLook.Unlit("fspray", new Color(0.85f, 0.88f, 0.82f, 0.7f), puff, true));
        floodFoamPuffs = Many(90, PrimitiveType.Quad, "ffoam", WellLook.Unlit("ffoam", new Color(0.72f, 0.76f, 0.68f, 0.65f), puff, true));
        floodChunks = Many(44, PrimitiveType.Sphere, "fchunk", WellLook.Textured("red_laterite_soil_stones", new Color(1.5f, 1.25f, 1.05f), 0.5f, 0.2f, true));
        // the WRONG way: a bare hole, a few timber props, work lamps on a cable
        var wood = WellLook.Textured("brown_planks_03", Hex("#C8A070"), 1.2f, 0.3f, true);
        props = new GameObject[12];
        for (int i = 0; i < 6; i++)
        {
            float x = -8f + i * 2.4f;
            props[i * 2] = Prim(PrimitiveType.Cube, "post", new Vector3(x, AXIS, 0.9f), new Vector3(0.16f, 2f * R, 0.16f), wood);
            props[i * 2 + 1] = Prim(PrimitiveType.Cube, "beam", new Vector3(x, TOP - 0.1f, 0.9f), new Vector3(0.2f, 0.2f, 2.2f), wood);
        }
        var bulbMat = WellLook.Emissive(WellLook.Lit("bulb", Hex("#FFE9B8"), 0.8f), Hex("#FFD890"), 26000f);
        bulbs = new GameObject[12];
        for (int i = 0; i < 12; i++) bulbs[i] = Prim(PrimitiveType.Sphere, "bulb", new Vector3(2f - i * 2.4f, TOP - 0.55f, 0.5f), Vector3.one * 0.11f, bulbMat);
        spade = PH("rusted_spade_01", -1, Vector3.zero, 90f, 2.4f);
    }

    // soft cloud sprite: fractal value noise under a round mask
    static Texture2D PuffTexture(int n = 128)
    {
        var rnd = new System.Random(5);
        var grid = new float[32, 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) grid[x, y] = (float)rnd.NextDouble();
        float Noise(float u, float v)
        {
            int x0 = Mathf.FloorToInt(u), y0 = Mathf.FloorToInt(v);
            float fx = E(u - x0), fy = E(v - y0);
            float a = grid[x0 & 31, y0 & 31], b = grid[(x0 + 1) & 31, y0 & 31], c = grid[x0 & 31, (y0 + 1) & 31], d = grid[(x0 + 1) & 31, (y0 + 1) & 31];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(u * u + v * v) * 2f;
                float f = 0.5f * Noise(u * 6f + 9f, v * 6f + 3f) + 0.3f * Noise(u * 13f + 2f, v * 13f + 7f) + 0.2f * Noise(u * 27f + 5f, v * 27f + 1f);
                float a = Mathf.Clamp01(1.25f * (1f - r) - (1f - f) * 0.75f);
                t.SetPixel(x, y, new Color(1, 1, 1, E(Mathf.Clamp01(a * 1.6f))));
            }
        t.Apply(true);
        return t;
    }

    void BuildFx()
    {
        var soil = WellLook.Textured("excavated_soil_wall", Hex("#D8C2AA"), 0.4f, 0.2f, true);
        var rock = WellLook.Textured("rock_wall_02", Hex("#B9B5AE"), 0.4f, 0.3f, true);
        chips = Many(46, PrimitiveType.Sphere, "chip", soil);
        for (int i = 0; i < chips.Length; i += 2) chips[i].GetComponent<Renderer>().sharedMaterial = rock;
        var soft = WellLook.SoftDot();
        sparks = Many(26, PrimitiveType.Quad, "spark", WellLook.Unlit("spark", new Color(1f, 0.72f, 0.3f, 1f) * 3f, soft, true));
        dust = Many(22, PrimitiveType.Quad, "dust", WellLook.Unlit("dust", new Color(0.78f, 0.68f, 0.56f, 0.32f), soft, true));
        dirtScrew = Many(22, PrimitiveType.Sphere, "dirtS", soil);
        dirtBelt = Many(40, PrimitiveType.Sphere, "dirtB", soil);

        var blue = WellLook.Emissive(WellLook.Lit("arrowBlue", Hex("#E9EEF0"), 0.5f), Hex("#E9EEF0"), 3000f);
        var orange = WellLook.Emissive(WellLook.Lit("arrowOrange", Hex("#F6DCC4"), 0.5f), Hex("#F6DCC4"), 3000f);
        var cone = ConeMesh(0.28f, 0.5f, 32);
        arrowsBlue = MakeArrows(5, blue, cone);
        arrowsOrange = MakeArrows(5, orange, cone);
    }

    GameObject[] MakeArrows(int n, Material m, Mesh cone)
    {
        var a = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            var root = new GameObject("arrow"); root.transform.SetParent(stage, false);
            Prim(PrimitiveType.Capsule, "shaft", new Vector3(0, 0.88f, 0), new Vector3(0.2f, 0.42f, 0.2f), m, root.transform);
            var head = new GameObject("head"); head.transform.SetParent(root.transform, false);
            head.AddComponent<MeshFilter>().sharedMesh = cone;
            head.AddComponent<MeshRenderer>().sharedMaterial = m;
            root.SetActive(false);
            a[i] = root;
        }
        return a;
    }

    void AddLabel(string text, string style, Vector3 pos, float t0, float t1)
    {
        labels.Add(new WellScene.Label { text = text, style = style, pos = pos, t0 = t0, t1 = t1 });
    }

    void BuildLabels()
    {
    }

    // ------------------------------------------------------------------ the machine's position
    float XStart => 1.4f;                 // bulkhead x once it has arrived
    bool Finished(float t) => t < Tw(2, "If") - 0.02f || t >= Tw(13, "Congratulations");
    bool MachineOn(float t) => t > Tw(3, "steel") - 0.05f && !Finished(t);

    // bulkhead x at second t: rushes in at "You send in this", creeps while it cuts, then lunges ring by ring
    float MachineX(float t)
    {
        float a = Tw(3, "steel") - 0.05f, b = Tw(3, "up") + 0.1f;
        return Mathf.Lerp(-12f, XStart, OutCubic(P(t, a, b))) + RING_W * Pushes(t);
    }

    float Lunge0 => Tw(12, "shove") - 0.05f;
    float Again0 => Tw(12, "built", 0, true) + 0.02f;
    const float CYCLE = 0.42f;

    float Pushes(float t)
    {
        float n = E(P(t, Lunge0, Lunge0 + 0.45f));
        for (int i = 0; i < 3; i++) { float s = Again0 + i * CYCLE; n += E(P(t, s + 0.18f, s + CYCLE)); }
        return n;
    }

    // ring index k counts from the oldest; ring RING_BACK is the first one built on screen
    float RingX(int k) => XStart - 6.3f + (k - RING_BACK) * RING_W;

    float JointX(int i) => RingX(RING_BACK - 3 - i) - RING_W / 2f;     // joints between finished rings, clear of the machine

    // ------------------------------------------------------------------ shots: hard cuts, each with its own move
    struct Key { public float t, x, y, d, yaw, pitch; public Key(float t, float x, float y, float d, float yaw, float pitch) { this.t = t; this.x = x; this.y = y; this.d = d; this.yaw = yaw; this.pitch = pitch; } }
    List<Key[]> shots;

    // Wide and slow, one idea per picture (research/format.md): most shots hold the whole machine or the whole action.
    void BuildShots()
    {
        float m = XStart;
        float end = doc.duration + TAIL + 1f;
        float s2 = Tw(2, "If"), s3 = Tw(3, "So") + 0.6f, s4 = Tw(4, "But"), bolt = Tw(4, "bolt") - 0.1f, s5 = Tw(5, "This"), s6 = Tw(6, "But"), pack = Tw(6, "so"),
              s7 = Tw(7, "Now"), s8 = Tw(8, "But"), s9 = Tw(9, "But"), robot = Tw(9, "robot") - 0.1f, rings = Tw(9, "rings") - 0.1f, s10 = Tw(10, "But"),
              s11 = Tw(11, "And"), s12 = Tw(12, "You"), s13 = Tw(13, "Congratulations");
        float j = JointX(1);
        shots = new List<Key[]> {
            new[] { new Key(0f, 0f, -3.2f, 38f, -4f, 14f), new Key(s2, 0.2f, -3.4f, 33f, -2f, 14f) },
            new[] { new Key(s2, 0.1f, -4.6f, 17f, -3f, 13f), new Key(Tw(3, "So"), 0.1f, -4.8f, 15.5f, -2f, 13f), new Key(s3, 0.1f, -4.8f, 15.5f, -2f, 13f) },
            new[] { new Key(s3, m - 2.6f, -5f, 24f, -4f, 13f), new Key(s4, m - 2.2f, -5.2f, 21f, -2f, 13f) },
            new[] { new Key(s4, 2.9f, AXIS - 1.1f, 6f, -8f, 5f), new Key(bolt, 2.95f, AXIS - 1.15f, 5.2f, -10f, 5f) },
            new[] { new Key(bolt, m - 1.2f, AXIS + 0.2f, 15f, -14f, 6f), new Key(s5, m - 0.6f, AXIS + 0.2f, 13f, -18f, 6f) },
            new[] { new Key(s5, m + 1.15f, AXIS, 8.4f, -70f, 2f), new Key(s6, m + 1.15f, AXIS, 7.6f, -62f, 2f) },
            new[] { new Key(s6, m - 2.4f, AXIS, 13f, 8f, 4f), new Key(pack, m - 2f, AXIS, 12f, 6f, 4f) },
            new[] { new Key(pack, m + 0.3f, AXIS, 9.5f, 3f, 3f), new Key(s7, m + 0.5f, AXIS, 8.5f, -2f, 3f) },
            new[] { new Key(s7, m - 1f, -4.4f, 24f, -6f, 13f), new Key(s8, m - 1f, -4.2f, 26f, -4f, 13f) },
            new[] { new Key(s8, m - 1.6f, AXIS - 0.4f, 11f, 8f, 4f), new Key(s9, m - 3.2f, AXIS - 0.2f, 12f, 12f, 4f) },
            new[] { new Key(s9, m - 11f, AXIS + 0.2f, 17f, 6f, 5f), new Key(robot, m - 11.5f, AXIS + 0.2f, 16f, 8f, 5f) },
            new[] { new Key(robot, m - 5.4f, AXIS + 0.1f, 11f, 14f, 4f), new Key(rings, m - 5.2f, AXIS + 0.2f, 9.5f, 18f, 4f) },
            new[] { new Key(rings, m - 6.5f, AXIS + 0.2f, 17f, 12f, 5f), new Key(s10, m - 13f, AXIS + 0.2f, 22f, 10f, 5f) },
            new[] { new Key(s10, j, AXIS + 0.2f, 7.5f, 6f, 3f), new Key(s11, j, AXIS + 0.1f, 6.2f, 4f, 3f) },
            new[] { new Key(s11, m - 4.6f, AXIS + 0.1f, 18f, 4f, 4f), new Key(s12, m - 4.6f, AXIS + 0.1f, 16.5f, 3f, 4f), new Key(s13, m - 4.4f, AXIS + 0.1f, 18f, 2f, 4f) },
            new[] { new Key(s13, 0.2f, -3.4f, 33f, -2f, 14f), new Key(end, 0f, -3.2f, 38f, -4f, 14f) },
        };
    }

    static float Spline(float t, Key[] k, Func<Key, float> f)
    {
        int n = k.Length;
        if (t <= k[0].t) return f(k[0]);
        if (t >= k[n - 1].t) return f(k[n - 1]);
        int i = 0; while (i < n - 2 && t > k[i + 1].t) i++;
        float h = k[i + 1].t - k[i].t, u = (t - k[i].t) / h;
        float m0 = i > 0 ? (f(k[i + 1]) - f(k[i - 1])) / (k[i + 1].t - k[i - 1].t) * h : 0f;
        float m1 = i < n - 2 ? (f(k[i + 2]) - f(k[i])) / (k[i + 2].t - k[i].t) * h : 0f;
        float u2 = u * u, u3 = u2 * u;
        return (2 * u3 - 3 * u2 + 1) * f(k[i]) + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * f(k[i + 1]) + (u3 - u2) * m1;
    }

    void EvalCamera(float t)
    {
        if (shots == null) BuildShots();
        Key[] shot = shots[0];
        foreach (var s in shots) if (t >= s[0].t) shot = s;
        float fx = Spline(t, shot, k => k.x), fy = Spline(t, shot, k => k.y), d = Spline(t, shot, k => k.d);
        float yaw = Spline(t, shot, k => k.yaw), pitch = Spline(t, shot, k => k.pitch);
        // the camera rides along with the machine after its lunges
        if (!Finished(t)) fx += RING_W * Pushes(t);
        yaw += 0.25f * Mathf.Sin(t * 0.43f) + 0.1f * Mathf.Sin(t * 0.97f + 1f);   // a slow drift, never a jitter
        pitch += 0.12f * Mathf.Sin(t * 0.61f + 2f);
        // shake: the flood, every lunge
        float sh = 0.11f * FloodK(t);
        sh += Bump(t, Lunge0 + 0.32f) * 0.12f;
        for (int i = 0; i < 3; i++) sh += Bump(t, Again0 + i * CYCLE + 0.36f) * 0.05f;
        fx += sh * Mathf.Sin(t * 61f); fy += sh * Mathf.Cos(t * 73f);
        var focus = new Vector3(fx, fy, 0.3f);
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.position = focus + rot * new Vector3(0f, 0f, -d);
        cam.transform.rotation = rot;
        cam.focusDistance = d;
    }

    static float Bump(float t, float at) => t >= at ? Mathf.Exp(-(t - at) * 7f) : 0f;

    // ------------------------------------------------------------------ evaluate
    public void Evaluate(float t)
    {
        EvalCamera(t);
        float flood = FloodK(t);
        EvalGround(t, flood);
        EvalRiver(t, flood);
        EvalFlood(t, flood);
        EvalMachine(t);
        EvalRings(t);
        EvalFx(t);
        EvalLabels(t);
    }

    // 0.3 at frame 0 (already breaking), 1 at "happens", snaps back to 0 on "So"
    float FloodK(float t)
    {
        float up = PE(t, Tw(2, "gives") - 0.05f, Tw(2, "in", 0, true) + 0.1f);
        float rA = Tw(3, "So") - 0.05f;
        return up * (1f - E(P(t, rA, rA + 0.55f)));
    }

    // how fast the wheel turns (degrees per second) and how far it has turned
    float WheelRate(float t)
    {
        float r = 170f * PE(t, Tw(4, "spinning"), Tw(4, "wheel", 0, true) + 0.4f);
        r *= 1f - PE(t, Tw(8, "nowhere"), Tw(8, "go", 0, true));
        r += 170f * PE(t, Tw(8, "screw"), Tw(8, "wall"));
        r *= 1f - PE(t, Tw(9, "robot") - 0.3f, Tw(9, "robot") + 0.3f);
        r += 120f * PE(t, Lunge0 - 0.1f, Lunge0 + 0.2f);
        return r;
    }

    float WheelAngle(float t)
    {
        float a = 0f;
        for (float u = Tw(4, "spinning"); u < t; u += 1f / 60f) a += WheelRate(u) / 60f;
        return a;
    }

    void EvalGround(float t, float flood)
    {
        var cuts = new List<Bounds>();
        var b = new Bounds();
        if (Finished(t))
        {
            b.SetMinMax(new Vector3(-200f, FLOOR, -1f), new Vector3(200f, TOP, Z_CUT));
            cuts.Add(b);
            ground.Rebuild(cuts);
            return;
        }
        float mx = Mathf.Max(XStart, MachineX(t));
        bool hero = t >= Tw(5, "This") && t < Tw(6, "But");       // nothing between the camera and the wheel
        b.SetMinMax(new Vector3(-200f, hero ? FLOOR - 1.2f : FLOOR, -1f), new Vector3(hero ? mx + 60f : mx + 1.75f, hero ? TOP + 1.2f : TOP, Z_CUT));
        cuts.Add(b);
        if (flood > 0.01f)
        {
            float w = 0.22f + 0.75f * flood;
            var f = new Bounds();
            f.SetMinMax(new Vector3(FLOOD_X - w, TOP - 0.5f, -1f), new Vector3(FLOOD_X + w, 1f, 1.1f));
            cuts.Add(f);
        }
        ground.Rebuild(cuts);
    }

    float WaveH(float x, float z, float t, float flood)
    {
        float h = 0.05f * Mathf.Sin(1.1f * x + 0.7f * z + 1.3f * t)
                + 0.035f * Mathf.Sin(-0.6f * x + 1.7f * z + 1.9f * t)
                + 0.02f * Mathf.Sin(3f * x + 2.2f * z + 3.1f * t)
                + 0.012f * Mathf.Sin(6f * x - 4f * z + 4.3f * t);
        if (flood > 0.001f)
        {
            float dx = x - FLOOD_X, dz = z - 0.7f, r2 = dx * dx + dz * dz;
            h -= 1.05f * flood * Mathf.Exp(-r2 / 3.2f);
            h += 0.05f * flood * Mathf.Sin(9f * Mathf.Sqrt(r2) - 10f * t) * Mathf.Exp(-r2 / 8f);
        }
        return h;
    }

    void EvalRiver(float t, float flood)
    {
        const int NX = 220, NZ = 90;
        for (int j = 0; j <= NZ; j++)
        {
            float z = RIVER_Z1 * Mathf.Pow(j / (float)NZ, 1.7f);
            for (int i = 0; i <= NX; i++)
            {
                float u = (i / (float)NX) * 2f - 1f;
                float x = u * Mathf.Abs(u) * 60f;
                surfV[j * (NX + 1) + i] = new Vector3(x, WaveH(x, z, t, flood), z);
            }
        }
        surfMesh.vertices = surfV;
        surfMesh.RecalculateNormals();
        surfMesh.RecalculateBounds();
        for (int i = 0; i <= NX; i++)
        {
            float u = (i / (float)NX) * 2f - 1f;
            float x = u * Mathf.Abs(u) * 60f;
            faceV[i * 2] = new Vector3(x, WaveH(x, 0f, t, flood), 0f);
            faceV[i * 2 + 1] = new Vector3(x, BED, 0f);
        }
        faceMesh.vertices = faceV;
        faceMesh.RecalculateNormals();
        faceMesh.RecalculateBounds();

        // life in the water
        for (int i = 0; i < fish.Length; i++)
        {
            var g = fish[i];
            Show(g, false);                 // they read as dark holes in the water
            if (!g.activeSelf) continue;
            float dir = i % 2 == 0 ? 1f : -1f;
            float x = Mathf.Repeat(Hash(i, 1) * 10f + dir * t * (0.5f + 0.5f * Hash(i, 2)), 10f) - 5f;
            g.transform.position = new Vector3(x, Mathf.Lerp(-0.6f, -2.5f, Hash(i, 3)) + 0.05f * Mathf.Sin(t * 2f + i), 0.5f + 6f * Hash(i, 4));
            g.transform.rotation = Quaternion.Euler(0, 0, 90f);
            g.transform.localScale = new Vector3(0.1f, 0.2f, 0.05f) * (0.8f + 0.7f * Hash(i, 5));
        }
        for (int i = 0; i < motes.Length; i++)
        {
            var g = motes[i];
            Show(g, true);
            float x = Mathf.Repeat(Hash(i, 7) * 10f + t * 0.12f, 10f) - 5f;
            g.transform.position = new Vector3(x, Mathf.Lerp(-0.25f, -2.9f, Hash(i, 8)) + 0.04f * Mathf.Sin(t + i), -0.02f);
            g.transform.localScale = Vector3.one * (0.012f + 0.02f * Hash(i, 9));
        }
        Show(barge, cam.transform.position.y > 0.15f);
        barge.transform.position = new Vector3(-1.2f, 0.06f * Mathf.Sin(t * 1.1f) - 0.5f * flood, 72f - t * 0.8f);
        barge.transform.rotation = Quaternion.Euler(0.5f * Mathf.Sin(t * 0.9f), 90f, 0.4f * Mathf.Sin(t * 0.7f));
    }

    void EvalFlood(float t, float flood)
    {
        bool bare = !MachineOn(t) && !Finished(t);
        foreach (var g in props) Show(g, bare);
        float tail = MachineX(t) - 7.4f;
        for (int i = 0; i < bulbs.Length; i++)
        {
            bool flick = flood > 0.6f && Hash(i, Mathf.FloorToInt(t * 16f)) > 0.75f;
            bool lined = t > Tw(9, "rings") + 0.1f;
            Show(bulbs[i], !Finished(t) && !flick && !lined && (bare || bulbs[i].transform.position.x < tail));
        }
        // time runs backwards during the snap: particles retrace their way
        float rA = Tw(3, "So") - 0.05f;
        float tk = t < rA ? t + 0.4f : rA + 0.4f - (t - rA) * 2.4f;
        float level = Mathf.Lerp(FLOOR + 0.05f, TOP - 0.25f, E(flood) * 0.95f);
        bool on = flood > 0.01f;
        Show(floodSheetGo, on); Show(floodTop, on);
        if (on)
        {
            const int NF = 90;
            for (int i = 0; i <= NF; i++)
            {
                float x = Mathf.Lerp(-30f, 2.58f, i / (float)NF);
                float dist = Mathf.Abs(x - FLOOD_X);
                float wave = 0.05f * Mathf.Sin(2.2f * x - 7f * tk) + 0.035f * Mathf.Sin(5.1f * x + 11f * tk)
                           + 0.22f * flood * Mathf.Exp(-dist * dist / 3f) * Mathf.Sin(5f * dist - 13f * tk);
                floodV[i * 2] = new Vector3(x, Mathf.Min(TOP - 0.02f, level + wave), -0.008f);
                floodV[i * 2 + 1] = new Vector3(x, FLOOR, -0.008f);
            }
            floodSheet.vertices = floodV;
            floodSheet.RecalculateNormals();
            floodSheet.RecalculateBounds();
            floodTop.transform.position = new Vector3(-13.7f, level - 0.01f, Z_CUT / 2f);
            floodTop.transform.localScale = new Vector3(32.56f, 0.02f, Z_CUT - 0.02f);
        }
        float width = 0.3f + 0.75f * flood;
        for (int i = 0; i < floodBodyPuffs.Length; i++)
        {
            var g = floodBodyPuffs[i];
            Show(g, on);
            if (!on) continue;
            float ph = Frac(tk * 1.3f + i * 0.0713f);
            var p = new Vector3(FLOOD_X + width * 0.8f * (Hash(i, 1) - 0.5f) * 2f, Mathf.Lerp(BED + 0.4f, level, ph * ph), -0.05f);
            Billboard(g, p, 0.5f + 0.5f * Hash(i, 2));
            g.transform.Rotate(0f, 0f, 360f * Hash(i, 3));
            g.transform.localScale = new Vector3(0.45f + 0.4f * Hash(i, 2), 0.8f + 0.9f * ph, 1f);
        }
        for (int i = 0; i < floodDrops.Length; i++)
        {
            var g = floodDrops[i];
            Show(g, on);
            if (!on) continue;
            float ph = Frac(tk * 1.6f + i * 0.0913f);
            float spread = width * ((Hash(i, 1) - 0.5f) * 2f);
            var p = new Vector3(FLOOD_X + spread * (0.8f + 0.45f * ph), Mathf.Lerp(BED + 0.3f, level, ph * ph), -0.15f - 0.2f * Hash(i, 2));
            Billboard(g, p, 1f);
            float s = (0.1f + 0.17f * Hash(i, 3)) * (0.7f + 0.7f * flood);
            g.transform.localScale = new Vector3(s, s * (1.5f + 3.5f * ph), 1f);    // stretched by the fall
        }
        for (int i = 0; i < floodSpray.Length; i++)
        {
            var g = floodSpray[i];
            Show(g, on);
            if (!on) continue;
            float life = 0.8f, s = Frac(tk * 1.25f + i * 0.0617f) * life;
            float vx = (Hash(i, 4) - 0.5f) * 7f * (0.4f + flood), vy = 1.6f + 3f * Hash(i, 5);
            float y = level + vy * s - 4.9f * s * s;
            bool up = y > level - 0.05f;
            Show(g, up);
            if (!up) continue;
            Billboard(g, new Vector3(FLOOD_X + vx * s, y, -0.25f), (0.12f + 0.34f * Hash(i, 7)) * (0.5f + 1.2f * s));
        }
        for (int i = 0; i < floodFoamPuffs.Length; i++)
        {
            var g = floodFoamPuffs[i];
            Show(g, on);
            if (!on) continue;
            float x = FLOOD_X + (Hash(i, 1) - 0.5f) * 2f * (1.4f + 4.5f * flood) + 0.3f * Mathf.Sin(tk * 1.4f + i);
            Billboard(g, new Vector3(x, level + 0.03f + 0.04f * Mathf.Sin(tk * 5f + i), -0.2f), 0.2f + 0.34f * Hash(i, 2));
        }
        for (int i = 0; i < floodChunks.Length; i++)
        {
            var g = floodChunks[i];
            Show(g, on);
            if (!on) continue;
            float ph = Frac(tk * 0.75f + i * 0.137f);
            float side = (Hash(i, 1) - 0.5f) * 2f * (0.3f + 0.8f * flood);
            float y = Mathf.Lerp(TOP + 0.3f, level - 0.2f, ph * ph);
            g.transform.position = new Vector3(FLOOD_X + side, y, -0.2f + 0.8f * Hash(i, 2));
            float s = 0.09f + 0.17f * Hash(i, 3);
            g.transform.localScale = new Vector3(s, s * 0.8f, s * 0.9f);
            g.transform.rotation = Quaternion.Euler(ph * 500f + i * 40f, ph * 300f, i * 70f);
        }
    }

    void EvalMachine(float t)
    {
        float x = MachineX(t);
        bool on = MachineOn(t);
        float screwK = PE(t, Tw(8, "run") - 0.1f, Tw(8, "wall", 0, true));
        bool wheelOn = on && t > Tw(4, "bolt") - 0.2f;
        bool erectOn = on && t > Tw(9, "robot") - 0.35f;
        bool ramsOn = on && t > Tw(11, "And") - 0.1f;
        bool backup = on && t > Tw(11, "And") - 0.1f;
        bool fin = Finished(t);
        Show(cutter, wheelOn); Show(shield, on); Show(flight, on && screwK > 0.01f); Show(tube, on && screwK > 0.01f); Show(erector, erectOn);
        foreach (var g in gantries) Show(g, backup);
        foreach (var g in railPieces) Show(g, backup || fin);
        Show(deck, fin);
        foreach (var g in rams) Show(g, ramsOn);
        foreach (var g in rods) Show(g, ramsOn);

        // the lamps: fixed in the bare tunnel, riding with the machine afterwards
        float lx = on ? x : (fin ? 70f : XStart);
        lampA.enabled = true; lampB.enabled = true; lampC.enabled = true;
        lampA.transform.position = on ? new Vector3(x - 2.5f, AXIS + 0.3f, 0.7f) : new Vector3(-0.6f, AXIS + 0.9f, 0.8f);
        lampB.transform.position = new Vector3(lx - 8f, AXIS + 0.3f, 0.7f);
        lampC.transform.position = new Vector3(lx - 12.5f, AXIS + 0.7f, 0.8f);
        Show(sunBlock, true);
        var toSun = -(Quaternion.Euler(WellLook.SUN_ELEV, WellLook.SUN_YAW, 0f) * Vector3.forward);
        float reachZ = Mathf.Abs(7f / toSun.z);
        sunBlock.transform.position = new Vector3((lx + 1.75f - 80f) / 2f, AXIS, 1.1f) + toSun * reachZ;
        sunBlock.transform.localScale = new Vector3(lx + 1.75f + 80f, 2f * R + 1.0f, 3.2f);
        bool faceShot = t >= Tw(4, "But") && t < Tw(7, "Now");
        bool sealShot = t >= Tw(10, "But") && t < Tw(11, "And");
        if (faceShot) lampB.transform.position = new Vector3(x + 3.5f, AXIS + 1.4f, -3.6f);
        lampB.intensity = faceShot ? 1300000f : 320000f;
        lampB.range = faceShot ? 22f : 10f;
        lampB.color = faceShot ? Hex("#FFE9CC") : Hex("#FFC58A");
        if (sealShot)
        {
            lampB.transform.position = new Vector3(JointX(1), AXIS + 0.3f, -0.6f);
            lampB.intensity = 260000f;
            lampB.range = 8f;
            lampB.color = Hex("#FFE9CC");
        }
        if (t >= Tw(9, "But") && t < Tw(9, "robot") - 0.1f)
        {
            lampB.transform.position = new Vector3(x - 11f, AXIS + 0.6f, -1.8f);
            lampB.intensity = 320000f;
            lampB.range = 9f;
        }
        // the finished tunnel: lamps along it, a train going through
        if (fin)
        {
            lampA.transform.position = new Vector3(-12f, AXIS + 0.9f, 0.7f);
            lampB.transform.position = new Vector3(0f, AXIS + 0.9f, 0.7f);
            lampC.transform.position = new Vector3(12f, AXIS + 0.9f, 0.7f);
            lampB.intensity = 600000f; lampB.range = 16f; lampB.color = Hex("#FFC58A");
            for (int i = 0; i < crownBulbs.Length; i++)
                crownBulbs[i].transform.position = new Vector3(-17.5f + i * 5f, AXIS + 1.45f, 0.72f);
            for (int i = 0; i < railPieces.Length; i++)
                railPieces[i].transform.position = new Vector3(-37.5f + i * 5f, AXIS - 1.56f, 0.45f);
            float t0 = t < Tw(2, "If") ? 0f : Tw(13, "Congratulations");
            float front = -7f + (t - t0) * 7f;
            for (int i = 0; i < trainCars.Length; i++)
            {
                Show(trainCars[i], true);
                trainCars[i].transform.position = new Vector3(front - i * 6.6f, AXIS - 1.52f, 0.73f);
            }
        }
        else foreach (var g in trainCars) Show(g, false);
        lampA.intensity = fin ? 600000f : 320000f; lampA.range = fin ? 16f : 11f;
        lampC.intensity = fin ? 600000f : 300000f; lampC.range = fin ? 16f : 11f;
        foreach (var g in crownBulbs) Show(g, (on && !sealShot) || fin);
        if (!on) { Show(chamberGlow, false); Show(chamberFill, false); Show(mudGo, false); Show(mudTop, false); Show(spade, false); return; }
        for (int i = 0; i < crownBulbs.Length; i++)
            crownBulbs[i].transform.position = new Vector3(x - 1.9f - i * 3.5f, AXIS + 1.48f, 0.72f);

        float rate = WheelRate(t);
        float vib = rate > 30f ? 0.006f : 0f;
        var pos = new Vector3(x + vib * Mathf.Sin(t * 151f), AXIS + vib * Mathf.Cos(t * 127f), 0f);
        shield.transform.position = pos;

        // the wheel swings in from the open side and bolts on
        float wz = Mathf.Lerp(-8f, 0f, Back(P(t, Tw(4, "bolt") - 0.2f, Tw(4, "wheel"))));
        cutter.transform.position = pos + new Vector3(1.0f, 0f, wz);
        cutter.transform.rotation = Quaternion.AngleAxis(WheelAngle(t), Vector3.right) * FACE;

        // mud floods the shield, then is pushed back out
        float mudK = PE(t, Tw(6, "pushes"), Tw(6, "floods", 0, true) + 0.25f) * (1f - E(P(t, Tw(6, "pack"), Tw(6, "dirt", 0, true) + 0.15f)));
        bool mudOn = mudK > 0.01f;
        Show(mudGo, mudOn); Show(mudTop, mudOn);
        if (mudOn)
        {
            float level = Mathf.Lerp(FLOOR + 0.03f, AXIS + 0.75f, mudK);
            for (int i = 0; i <= 40; i++)
            {
                float mx = Mathf.Lerp(x - 7.2f, x + 0.95f, i / 40f);
                mudV[i * 2] = new Vector3(mx, level + 0.035f * Mathf.Sin(2.6f * mx - 4f * t) + 0.02f * Mathf.Sin(6f * mx + 5f * t), -0.012f);
                mudV[i * 2 + 1] = new Vector3(mx, FLOOR + 0.02f, -0.012f);
            }
            mudMesh.vertices = mudV; mudMesh.RecalculateNormals(); mudMesh.RecalculateBounds();
            mudTop.transform.position = new Vector3(x - 3.12f, level - 0.015f, 1f);
            mudTop.transform.localScale = new Vector3(8.15f, 0.02f, 2f);
        }

        // packed dirt rises behind the wheel, then pushes back
        float fill = PE(t, Tw(6, "pack"), Tw(6, "dirt", 0, true) + 0.1f);
        Show(chamberFill, fill > 0.01f);
        if (fill > 0.01f)
        {
            float h = (2f * R - 0.2f) * fill;
            chamberFill.transform.position = new Vector3(pos.x + 0.39f, FLOOR + 0.1f + h / 2f, (R - 0.05f) / 2f);
            chamberFill.transform.localScale = new Vector3(0.66f, h, R - 0.05f);
        }
        float gA = Tw(6, "squeeze") - 0.1f, gB = Tw(8, "But");
        float glow = PE(t, gA, gA + 0.3f) * (1f - PE(t, gB - 0.3f, gB));
        Show(chamberGlow, glow > 0.01f);
        if (glow > 0.01f)
        {
            chamberGlow.transform.position = pos + new Vector3(0.39f, 0f, (R - 0.15f) / 2f - 0.03f);
            chamberGlow.transform.localScale = new Vector3(0.68f * glow * (0.92f + 0.08f * Mathf.Sin(t * 8f)), 2f * R - 0.3f, R - 0.15f);
        }

        // digging by hand: a spade at the face
        bool dig = t >= Tw(4, "But") - 0.05f && t < Tw(4, "bolt");
        Show(spade, dig);
        if (dig)
        {
            float st = Mathf.Abs(Mathf.Sin(t * 3.4f));
            spade.transform.position = new Vector3(2.55f + 0.32f * st, FLOOR + 0.18f + 0.1f * st, 0.32f);
            spade.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, -52f - 12f * st);
        }

        // the screw slides in along its own axis
        var tilt = Quaternion.Euler(0f, 0f, -15f);
        var intake = pos + new Vector3(0.05f, -1.45f, 0.62f) + tilt * new Vector3(-6.5f * (1f - screwK), 0f, 0f);
        tube.transform.position = intake;
        tube.transform.rotation = tilt * FACE;
        flight.transform.position = intake;
        flight.transform.rotation = tilt * Quaternion.AngleAxis(-t * 260f, Vector3.right) * FACE;

        // back-up cars and rails
        for (int i = 0; i < gantries.Length; i++)
            gantries[i].transform.position = new Vector3(x - 10.1f - i * 5.05f, AXIS + 0.22f, 0f);
        for (int i = 0; i < railPieces.Length; i++)
            railPieces[i].transform.position = new Vector3(Mathf.Floor(x / 5f) * 5f - 5f - i * 5f + 2.5f, AXIS - 1.56f, 0f);

        // thrust rams: barrel fixed in the machine, the rod reaches back to the last finished ring
        Vector3[] at = { new Vector3(0f, 1.62f, 0.45f), new Vector3(0f, 0.15f, 1.72f), new Vector3(0f, -1.62f, 0.45f) };
        float ringFace = RingX(LastRingDone(t)) + RING_W / 2f;
        for (int i = 0; i < 3; i++)
        {
            float bx0 = x - 4.15f, bx1 = x - 5.35f;
            var cc = new Vector3((bx0 + bx1) / 2f, AXIS + at[i].y, at[i].z);
            rams[i].transform.position = cc;
            rams[i].transform.rotation = Quaternion.Euler(0, 0, 90);
            rams[i].transform.localScale = new Vector3(0.24f, (bx0 - bx1) / 2f, 0.24f);
            float reach = PE(t, Tw(11, "forward"), Tw(12, "You") + 0.05f);    // reaches for the ring on the question
            float rodEnd = Mathf.Lerp(bx1 - 0.15f, Mathf.Min(bx1 - 0.02f, ringFace), reach);
            rods[i].transform.position = new Vector3((bx1 + rodEnd) / 2f, cc.y, cc.z);
            rods[i].transform.rotation = Quaternion.Euler(0, 0, 90);
            rods[i].transform.localScale = new Vector3(0.13f, Mathf.Max(0.01f, (bx1 - rodEnd) / 2f), 0.13f);
        }
    }

    int LastRingDone(float t)
    {
        int k = RING_BACK - 1;
        if (t > SegTime(3) + 0.6f) k = RING_BACK;
        for (int i = 0; i < 3; i++) if (t > Again0 + i * CYCLE + 0.17f) k = RING_BACK + 1 + i;
        return k;
    }

    // when segment s of the on-screen ring is carried off the feeder
    float SegTime(int s)
    {
        float a = Tw(9, "robot") - 0.05f, b = Tw(9, "rings") + 0.1f;
        return Mathf.Lerp(a, b, s / 4f);
    }

    void EvalRings(float t)
    {
        float x = MachineX(t);
        bool mach = MachineOn(t);
        if (Finished(t))
        {
            for (int k = 0; k < rings.Length; k++)
                for (int ss = 0; ss < 4; ss++)
                {
                    var g = rings[k][ss];
                    Show(g, true);
                    g.transform.position = new Vector3(-21.7f + k * RING_W, AXIS, 0f);
                    g.transform.rotation = Quaternion.AngleAxis(ss * 45f, Vector3.right) * FACE;
                }
            Show(groutOld, true);
            groutOld.transform.position = new Vector3(45f, AXIS, 0f);
            groutOld.transform.localScale = new Vector3(90f, 1f, 1f);
            foreach (var g in macroSeal) Show(g, false);
            foreach (var g in macroDrops) Show(g, false);
            return;
        }
        // old rings: they run back down the bare tunnel from "rings" on
        float wave = Tw(9, "rings") + 0.1f;
        for (int k = 0; k < RING_BACK; k++)
        {
            float rx = RingX(k);
            float t0 = wave + (RING_BACK - 1 - k) * 0.055f;
            float pop = P(t, t0, t0 + 0.14f);
            bool vis = mach && pop > 0f;
            for (int ss = 0; ss < 4; ss++)
            {
                var g = rings[k][ss];
                Show(g, vis);
                if (!vis) continue;
                var offR = Quaternion.AngleAxis(ss * 45f + 22.5f, Vector3.right) * new Vector3(0f, -0.3f * (1f - E(pop)), 0f);
                g.transform.position = new Vector3(rx, AXIS, 0f) + offR;
                g.transform.rotation = Quaternion.AngleAxis(ss * 45f, Vector3.right) * FACE;
            }
        }

        // the erector: picks at the bottom (180), carries round, places, returns
        float ex = x - 6.25f;
        erector.transform.position = new Vector3(ex, AXIS, 0.2f);
        float phi = 180f; float reach = 0f;
        float station = RingX(RING_BACK);

        for (int s = 0; s < 4; s++)
        {
            var g = rings[RING_BACK][s];
            float s0 = SegTime(s), dur = SegTime(1) - SegTime(0);
            float target = s * 45f + 22.5f;
            float k = P(t, s0, s0 + dur * 0.62f);          // carried
            float seat = P(t, s0 + dur * 0.62f, s0 + dur * 0.78f);
            bool show = mach && t > s0 - 0.05f;
            Show(g, show);
            if (!show) continue;
            float a = Mathf.Lerp(180f, target, E(k));
            float rad = -0.32f * (1f - E(seat));
            var off = Quaternion.AngleAxis(a, Vector3.right) * new Vector3(0f, rad, 0f);
            g.transform.position = new Vector3(station, AXIS, 0f) + off;
            g.transform.rotation = Quaternion.AngleAxis(a - 22.5f, Vector3.right) * FACE;
            if (t >= s0 && t < s0 + dur) { phi = a; reach = 1f - 0.32f * (1f - E(seat)); if (t > s0 + dur * 0.78f) { phi = Mathf.Lerp(target, 180f, E(P(t, s0 + dur * 0.78f, s0 + dur))); } }
        }
        erector.transform.rotation = Quaternion.AngleAxis(phi, Vector3.right) * FACE;
        _ = reach;

        // grout outside the older rings
        float gl = (t - wave - 0.1f) / 0.055f * RING_W;
        Show(groutOld, mach && gl > 0.01f);
        if (groutOld.activeSelf)
        {
            groutOld.transform.position = new Vector3(RingX(RING_BACK - 2) - RING_W / 2f, AXIS, 0f);
            groutOld.transform.localScale = new Vector3(Mathf.Min(gl, 60f), 1f, 1f);
        }

        // inside the lined tunnel: water runs down three joints; rubber closes them one after the other
        bool sealShot = mach && t >= Tw(10, "But") && t < Tw(11, "And");
        float sA = Tw(10, "rubber") - 0.1f;
        float[] closeT = new float[3];
        for (int i = 0; i < 3; i++)
        {
            closeT[i] = sA + i * 0.22f;
            var g = macroSeal[i];
            float k = Back(P(t, closeT[i], closeT[i] + 0.3f));
            Show(g, mach && k > 0.01f);
            if (!g.activeSelf) continue;
            g.transform.position = new Vector3(JointX(i), AXIS, 0f);
            g.transform.localScale = new Vector3(1f, 1f, 1f) * 1f;
            g.transform.localScale = new Vector3(Mathf.Max(k, 0.01f), 1f, 1f);
        }
        float dA = Tw(10, "water") - 0.15f;
        for (int i = 0; i < macroDrops.Length; i++)
        {
            var g = macroDrops[i];
            int jo = i % 3;
            float ph = Frac((t - dA) * 0.55f + i * 0.0893f);
            bool vis = sealShot && t > dA && t < closeT[jo] + 0.15f;
            Show(g, vis);
            if (!vis) continue;
            float ang = ph * Mathf.PI;             // from the crown, round the back wall, to the floor
            g.transform.position = new Vector3(JointX(jo), AXIS + 1.6f * Mathf.Cos(ang), 1.6f * Mathf.Sin(ang));
            g.transform.localScale = new Vector3(0.75f, 1.5f, 1f) * 0.075f;
        }

        // the time-lapse rings pop in, one per cycle
        for (int r = 1; r < RING_NEW; r++)
        {
            float s0 = Again0 + (r - 1) * CYCLE;
            float rx = RingX(RING_BACK + r);
            for (int s = 0; s < 4; s++)
            {
                var g = rings[RING_BACK + r][s];
                float pop = P(t, s0 + s * 0.035f, s0 + s * 0.035f + 0.1f);
                Show(g, mach && pop > 0f && r <= 3);
                if (!g.activeSelf) continue;
                var off = Quaternion.AngleAxis(s * 45f + 22.5f, Vector3.right) * new Vector3(0f, -0.3f * (1f - pop), 0f);
                g.transform.position = new Vector3(rx, AXIS, 0f) + off;
                g.transform.rotation = Quaternion.AngleAxis(s * 45f, Vector3.right) * FACE;
            }
        }
    }

    void Arrow(GameObject a, bool on, Vector3 tip, Vector3 dir, float s, float t, int i)
    {
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

    void EvalFx(float t)
    {
        float x = MachineX(t);
        float face = x + 1.75f;
        bool cutting = MachineOn(t) && WheelRate(t) > 30f;

        for (int i = 0; i < chips.Length; i++)
        {
            var g = chips[i];
            Show(g, cutting);
            if (!cutting) continue;
            float life = 0.7f, s = Frac(t * 1.8f + i / (float)chips.Length) * life;
            float a = Hash(i, 3) * 6.283f, r = 0.5f + 1.5f * Hash(i, 4);
            float y0 = AXIS + r * Mathf.Sin(a), z0 = -Mathf.Abs(r * Mathf.Cos(a)) * 0.9f - 0.15f;
            float vx = -0.9f - 1.6f * Hash(i, 5), vy = 0.6f + 1.8f * Hash(i, 6), vz = -0.7f * Hash(i, 7);
            g.transform.position = new Vector3(face + 0.05f + vx * s, Mathf.Max(FLOOR + 0.05f, y0 + vy * s - 4.9f * s * s), z0 + vz * s);
            float sc = 0.035f + 0.075f * Hash(i, 8);
            g.transform.localScale = new Vector3(1f, 0.75f, 0.85f) * sc;
            g.transform.rotation = Quaternion.Euler(s * 600f + i * 30f, s * 400f, i * 77f);
        }
        for (int i = 0; i < sparks.Length; i++)
        {
            var g = sparks[i];
            Show(g, cutting);
            if (!cutting) continue;
            float life = 0.28f, s = Frac(t * 3.6f + i * 0.137f) * life;
            float a = (Hash(i, 3) + Mathf.Floor(t * 3.6f + i * 0.137f) * 0.37f) * 6.283f, r = 0.7f + 1.3f * Hash(i, 4);
            var p = new Vector3(face + 0.04f + 0.5f * s, AXIS + r * Mathf.Sin(a) + 1.6f * s, -Mathf.Abs(r * Mathf.Cos(a)) - 0.05f);
            Billboard(g, p, 0.09f * (1f - s / life));
        }
        for (int i = 0; i < dust.Length; i++)
        {
            var g = dust[i];
            Show(g, cutting);
            if (!cutting) continue;
            float life = 1.5f, s = Frac(t * 0.8f + i / (float)dust.Length) * life;
            float a = Hash(i, 2) * 6.283f;
            var p = new Vector3(face - 0.1f - s * 0.6f, AXIS + 1.6f * Mathf.Sin(a) + s * 0.3f, -0.5f - 0.9f * Hash(i, 3));
            Billboard(g, p, (0.45f + s * 0.9f) * (1f - s / life));
        }

        // dirt up the screw, out along the belts
        bool running = MachineOn(t) && t > Tw(8, "wall");
        bool belt = running && t > Tw(11, "And");
        var tilt = Quaternion.Euler(0f, 0f, -15f);
        var intake = new Vector3(x + 0.05f, AXIS - 1.45f, 0.62f);
        for (int i = 0; i < dirtScrew.Length; i++)
        {
            var g = dirtScrew[i];
            Show(g, running);
            if (!running) continue;
            float d = Frac(t * 0.42f + i / (float)dirtScrew.Length) * 5.9f;
            float a = t * 5f + i * 1.3f;
            var p = intake + tilt * new Vector3(-d, 0.16f * Mathf.Cos(a), 0.14f * Mathf.Sin(a));
            g.transform.position = p;
            g.transform.localScale = new Vector3(0.13f, 0.11f, 0.12f) * (0.8f + 0.5f * Hash(i, 2));
        }
        for (int i = 0; i < dirtBelt.Length; i++)
        {
            var g = dirtBelt[i];
            Show(g, belt);
            if (!belt) continue;
            float d = Frac(t * 0.18f + i / (float)dirtBelt.Length) * 21f;
            g.transform.position = new Vector3(x - 7.6f - d, AXIS + 0.22f + 0.49f, 0.15f + (Hash(i, 5) - 0.5f) * 0.2f);
            g.transform.localScale = new Vector3(0.34f, 0.12f + 0.08f * Hash(i, 6), 0.26f);
        }

        // the river's weight stops at the machine; packed dirt pushes back at the face
        float bA = Tw(7, "Now") - 0.1f, bB = Tw(8, "But");
        for (int i = 0; i < arrowsBlue.Length; i++)
        {
            float k = Back(P(t, bA + i * 0.05f, bA + i * 0.05f + 0.3f)) * (1f - PE(t, bB - 0.25f, bB));
            Arrow(arrowsBlue[i], t > bA && t < bB, new Vector3(x - 3.2f + i * 1.25f, TOP - 0.25f, -0.25f), Vector3.down, 1.25f * k, t, i);
        }
        float oA = Tw(6, "squeeze") - 0.1f, oB = Tw(7, "Now") + 0.1f;
        for (int i = 0; i < arrowsOrange.Length; i++)
        {
            float k = Back(P(t, oA + i * 0.05f, oA + i * 0.05f + 0.3f)) * (1f - PE(t, oB - 0.25f, oB));
            Arrow(arrowsOrange[i], t > oA && t < oB, new Vector3(face - 0.2f, AXIS + (i - 2f) * 0.8f, -0.45f), Vector3.right, 0.5f * k, t, i);
        }

        // the bare tunnel behind starts to fall
        float cA = Tw(9, "hole") - 0.1f, cB = Tw(9, "rings") + 0.4f;
        if (t > cA && t < cB)
            for (int i = 0; i < floodChunks.Length; i++)
            {
                var g = floodChunks[i];
                Show(g, true);
                // each lump falls once and stays: a heap grows along the floor
                float dt = t - (cA + 1.5f * Hash(i, 6));
                Show(g, dt > 0f);
                if (dt <= 0f) continue;
                float sc = 0.2f + 0.42f * Hash(i, 5);
                float rest = FLOOR + 0.1f + 0.45f * Hash(i, 7);
                float y = Mathf.Max(rest, TOP - 0.05f - 4.9f * dt * dt);
                float spin = Mathf.Min(dt, 0.55f);
                g.transform.position = new Vector3(x - 8f - 7f * Hash(i, 3), y, 0.1f + 1.0f * Hash(i, 4));
                g.transform.localScale = new Vector3(sc * 1.1f, sc * 0.8f, sc * 0.9f);
                g.transform.rotation = Quaternion.Euler(spin * 500f + i * 40f, spin * 300f, i * 70f);
            }
        if (t > cA && t < cB)
            for (int i = 0; i < dust.Length; i++)
            {
                var g = dust[i];
                Show(g, true);
                float life = 1.3f, sd = Frac((t - cA) * 0.75f + i / (float)dust.Length) * life;
                Billboard(g, new Vector3(x - 8f - 7f * Hash(i, 8) + sd * 0.3f, Mathf.Lerp(TOP, FLOOR + 0.3f, Hash(i, 9)) + sd * 0.25f, -0.05f), (0.8f + sd * 1.1f) * (1f - sd / life));
            }
    }

    void EvalLabels(float t)
    {
        float x = MachineX(t);
        foreach (var L in labels)
        {
            L.k = Back(P(t, L.t0, L.t0 + 0.28f)) * (1f - PE(t, L.t1 - 0.22f, L.t1));
            switch (L.text)
            {
                case "SHIELD": L.pos = new Vector3(x - 3.6f, AXIS + 2.75f, 0f); break;
                case "BORING MACHINE": L.pos = new Vector3(x + 1.0f, AXIS + 2.15f, -0.6f); break;
                case "WET MUD": L.pos = new Vector3(x - 3.4f, AXIS - 0.9f, -0.1f); break;
                case "PACKED DIRT": L.pos = new Vector3(x + 0.39f, AXIS + 1.75f, -0.1f); break;
                case "SCREW": L.pos = new Vector3(x - 2.9f, AXIS + 0.7f, 0f); break;
                case "ROBOT ARM": L.pos = new Vector3(x - 5.2f, AXIS + 1.85f, 0f); break;
                case "CONCRETE RINGS": L.pos = new Vector3(x - 12f, AXIS + 2.75f, 0f); break;
            }
        }
    }
}
