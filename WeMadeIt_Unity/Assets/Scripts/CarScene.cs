// Short 03, "getting out of a car that goes into a river" (HDRP). The whole video is a pure function of
// time: Evaluate(t) puts every object in its state at second t; beat times come from the voice
// (docs/scenes/car_words.json). Framing follows the tunnel video: wide, slow, one idea per picture.
// The people are crash-test dummies built here until the Mixamo characters arrive.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class CarScene : MonoBehaviour, IShortScene
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
    static float Back(float x) { const float c = 1.7f; x = Mathf.Clamp01(x); return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }
    static float Frac(float x) => x - Mathf.Floor(x);
    static float Hash(int i, int k) => Frac(Mathf.Sin(i * 12.9898f + k * 78.233f) * 43758.5453f);
    static Color Hex(string h) => WellLook.Hex(h);
    static void Show(GameObject g, bool on) { if (g != null && g.activeSelf != on) g.SetActive(on); }
    static readonly Quaternion FACE = Quaternion.Euler(0f, 180f, 0f);
    // Blender (x, y, z) inside a model -> Unity local, once the model is turned by FACE (measured on the tunnel models)
    static Vector3 B(float x, float y, float z) => new Vector3(x, z, y);

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

    GameObject Model(string file, Vector3 pos, Transform parent = null)
    {
        var src = Resources.Load<GameObject>("Models/" + file);
        if (src == null) { Debug.LogError("model missing: " + file); return new GameObject(file); }
        var g = Instantiate(src);
        g.name = file;
        g.transform.SetParent(parent ?? stage, false);
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

    GameObject Tree(string id, Vector3 pos, float yaw, float height)
    {
        var g = PH(id, -1, pos, yaw, 1f);
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        g.transform.localScale = Vector3.one * (height / Mathf.Max(0.1f, b.size.y));
        return g;
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

    MaterialPropertyBlock fadeBlock;
    void Fade(GameObject g, Color c, float alpha)
    {
        fadeBlock ??= new MaterialPropertyBlock();
        c.a *= Mathf.Clamp01(alpha);
        fadeBlock.SetColor("_UnlitColor", c);
        g.GetComponent<Renderer>().SetPropertyBlock(fadeBlock);
    }

    // a quad facing the camera, its long side along the velocity: a streak of water, not a flake
    void Streak(GameObject g, Vector3 p, Vector3 vel, float width, float stretch)
    {
        g.transform.position = p;
        var fwd = cam.transform.forward;
        var up = Vector3.ProjectOnPlane(vel, fwd);
        if (up.sqrMagnitude < 1e-6f) up = cam.transform.up;
        g.transform.rotation = Quaternion.LookRotation(fwd, up.normalized);
        g.transform.localScale = new Vector3(width, width + stretch * vel.magnitude, 1f);
    }

    void Billboard(GameObject g, Vector3 p, float size)
    {
        g.transform.position = p;
        g.transform.rotation = cam.transform.rotation;
        g.transform.localScale = Vector3.one * size;
    }

    // ------------------------------------------------------------------ layout
    const float BED = -4.5f, ROAD = 4.2f, BRIDGE_Z = 6f, CAR_Z = 1.7f, RIVER_Z1 = 260f;
    static readonly float[] LayerY = { BED, -5.3f, -16f, -60f };
    static readonly float[] BankX = { 3.6f, 5.2f, 5.8f, 6.0f, 6.8f, 8.2f, 260f };
    static readonly float[] BankY = { BED, -0.8f, -0.05f, 0.1f, 0.75f, 1.2f, 1.2f };

    // ------------------------------------------------------------------ objects
    Camera cam;
    CutGround ground;
    Mesh surfMesh, faceMesh;
    Vector3[] surfV, faceV;
    Transform car;
    GameObject glassFront, windshield, door, railBreak, phone, breaker, cabinWater, belt, crack;
    GameObject[] shards, splash, spray, bubbles, motes, arrows, inflow, crackLines;
    Light fill;
    Human adult, kid;
    GameObject handleBase, handleLever; Quaternion leverRest;
    bool insideNow;                 // the camera is in the car: dry cabin, closed near doors
    GameObject doorInner;
    GameObject beltObj, latch, buckleBox;
    Volume underwater;
    Light phoneLight;

    // ------------------------------------------------------------------ build
    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<WellScene.Doc>(File.ReadAllText(wordsJson));
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);
        WellLook.FoliageTint = new Color(0.62f, 0.82f, 0.5f);
        WellLook.GradeContrast = 6f; WellLook.GradeSaturation = 0f; WellLook.GradeWarm = 10f;
        WellLook.Sun(stage);
        WellLook.Environment(stage);
        cam = WellLook.Camera(stage, 34f);

        var mats = new[] {
            WellLook.Textured("excavated_soil_wall", new Color(1.05f, 0.92f, 0.76f), 1.5f, 0.25f),
            WellLook.Textured("red_laterite_soil_stones", new Color(1.45f, 1.22f, 1.0f), 1.25f, 0.25f),
            WellLook.Textured("rock_wall_02", Hex("#B7B3AD"), 3.0f, 0.35f),
        };
        ground = new CutGround(stage, LayerY, mats, WellLook.Textured("brown_mud_03", Hex("#8C7E66"), 2.0f, 0.5f));
        ground.Rebuild(new List<Bounds>());

        BuildRiver();
        BuildBanks();
        BuildForest();
        Model("bridge", new Vector3(0f, ROAD, BRIDGE_Z));
        railBreak = Model("bridge_rail_break", new Vector3(0f, ROAD, BRIDGE_Z - 4.35f));
        BuildCar();
        BuildFx();

        var lg = new GameObject("fill"); lg.transform.SetParent(stage, false);
        fill = lg.AddComponent<Light>(); fill.type = LightType.Point;
        var hd = lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
        fill.lightUnit = UnityEngine.Rendering.LightUnit.Lumen; fill.intensity = 250000f; fill.range = 9f; fill.color = Hex("#CFE6E0");
        hd.volumetricDimmer = 0f;

        var uw = new GameObject("Underwater"); uw.transform.SetParent(stage, false);
        underwater = uw.AddComponent<Volume>();
        underwater.isGlobal = true; underwater.priority = 200f; underwater.weight = 0f;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var fog = prof.Add<Fog>(true);
        fog.enabled.value = true;
        fog.meanFreePath.value = 3.2f;
        fog.baseHeight.value = -0.15f;
        fog.maximumHeight.value = 0.35f;
        fog.colorMode.value = FogColorMode.ConstantColor;
        fog.color.value = new Color(0.07f, 0.16f, 0.14f);
        fog.enableVolumetricFog.value = true;
        fog.albedo.value = new Color(0.35f, 0.6f, 0.52f);
        underwater.sharedProfile = prof;

        var pl = new GameObject("phoneLight"); pl.transform.SetParent(stage, false);
        phoneLight = pl.AddComponent<Light>(); phoneLight.type = LightType.Point;
        var phd = pl.AddComponent<HDAdditionalLightData>(); phd.volumetricDimmer = 0f;
        phoneLight.lightUnit = LightUnit.Lumen; phoneLight.intensity = 260f; phoneLight.range = 0.9f; phoneLight.color = Hex("#B8D6FF");
    }

    void BuildRiver()
    {
        const int NX = 220, NZ = 150;
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
        surfMesh.MarkDynamic(); surfMesh.vertices = surfV; surfMesh.triangles = tris;
        var sg = new GameObject("RiverSurface"); sg.transform.SetParent(stage, false);
        sg.AddComponent<MeshFilter>().sharedMesh = surfMesh;
        sg.AddComponent<MeshRenderer>().sharedMaterial = WellLook.Water("riverTop", Hex("#4A6E66"), 0.9f, 0.93f, false);

        faceV = new Vector3[(NX + 1) * 2];
        var ft = new int[NX * 6]; q = 0;
        for (int i = 0; i < NX; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            ft[q++] = a; ft[q++] = c; ft[q++] = b; ft[q++] = b; ft[q++] = c; ft[q++] = d;
        }
        faceMesh = new Mesh { name = "RiverFace" };
        faceMesh.MarkDynamic(); faceMesh.vertices = faceV; faceMesh.triangles = ft;
        var fg = new GameObject("RiverFace"); fg.transform.SetParent(stage, false);
        fg.AddComponent<MeshFilter>().sharedMesh = faceMesh;
        fg.AddComponent<MeshRenderer>().sharedMaterial = WellLook.Water("riverFace", Hex("#2F5550"), 0.5f, 0.92f, false);

        var grad = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) grad.SetPixel(0, y, new Color(1, 1, 1, Mathf.Lerp(0.35f, 0f, y / 63f)));
        grad.Apply();
        Prim(PrimitiveType.Quad, "riverDepth", new Vector3(0, (BED - 0.12f) / 2f, -0.012f), new Vector3(120f, -BED - 0.12f, 1f), WellLook.Unlit("riverDepth", Hex("#0E221E"), grad, true));
        var shaftTex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float across = 1f - Mathf.Abs(x / 31f - 0.5f) * 2f;
                shaftTex.SetPixel(x, y, new Color(1, 1, 1, E(across) * (y / 31f) * 0.24f));
            }
        shaftTex.Apply();
        var shaftMat = WellLook.Unlit("shaft", new Color(0.75f, 0.88f, 0.78f, 0.6f), shaftTex, true);
        for (int i = 0; i < 7; i++)
        {
            var s = Prim(PrimitiveType.Quad, "shaft" + i, new Vector3(-4.5f + i * 1.5f + Hash(i, 2), BED / 2f - 0.05f, -0.016f), new Vector3(0.5f + 0.6f * Hash(i, 3), -BED + 0.3f, 1f), shaftMat);
            s.transform.rotation = Quaternion.Euler(0, 0, -14f);
        }
        motes = Many(40, PrimitiveType.Sphere, "mote", WellLook.Lit("mote", Hex("#D8E8E6"), 0.3f));
        for (int i = 0; i < 10; i++)
            PH("boulder_01", -1, new Vector3(-3.5f + i * 0.75f + Hash(i, 3), BED - 0.05f, 0.4f + 6f * Hash(i, 4)), 360f * Hash(i, 5), 0.15f + 0.22f * Hash(i, 6));
        var mud = WellLook.Textured("brown_mud_03", Hex("#B0A08A"), 2.6f, 0.3f);
        Prim(PrimitiveType.Cube, "farBank", new Vector3(0, -1f, RIVER_Z1 + 80f), new Vector3(500f, 4.4f, 160f), mud);
        Prim(PrimitiveType.Cube, "farLawn", new Vector3(0, 1.22f, RIVER_Z1 + 80f), new Vector3(500f, 0.05f, 160f), WellLook.Textured("leafy_grass", new Color(0.7f, 0.72f, 0.42f), 2.0f, 0.25f));
    }

    void BuildBanks()
    {
        var earth = WellLook.Textured("red_laterite_soil_stones", new Color(1.45f, 1.22f, 1.0f), 1.25f, 0.25f);
        var topsoil = WellLook.Textured("excavated_soil_wall", new Color(1.05f, 0.92f, 0.76f), 1.5f, 0.25f);
        var mudM = WellLook.Textured("brown_mud_03", Hex("#CDBC9C"), 2.4f, 0.35f);
        var lawn = WellLook.Textured("leafy_grass", new Color(0.7f, 0.72f, 0.42f), 2.0f, 0.25f);
        const float Z0 = -0.03f, Z1 = 400f, BOT = -5.31f, SPLIT = 0.1f;
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
        float Wander(float side, float z)
        {
            float k = E(Mathf.Clamp01((z - 11f) / 9f));     // straight under the bridge, wandering beyond it
            return -k * (0.85f + 0.55f * Mathf.Sin(z * 0.047f + side * 1.3f) + 0.3f * Mathf.Sin(z * 0.13f + side * 2.1f));
        }
        const int ROWS = 90;
        foreach (float side in new[] { 1f, -1f })
            for (int i = 0; i + 1 < BankX.Length; i++)
            {
                float x0 = side * BankX[i], x1 = side * BankX[i + 1], y0 = BankY[i], y1 = BankY[i + 1];
                Quad(new Vector3(x0, BOT, Z0), new Vector3(x1, BOT, Z0), new Vector3(x1, Mathf.Min(y1, SPLIT), Z0), new Vector3(x0, Mathf.Min(y0, SPLIT), Z0), Vector3.back, 0);
                if (y0 >= SPLIT) Quad(new Vector3(x0, SPLIT, Z0), new Vector3(x1, SPLIT, Z0), new Vector3(x1, y1, Z0), new Vector3(x0, y0, Z0), Vector3.back, 1);
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
            var tt = Vector3.Cross(n[q], Mathf.Abs(n[q].y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            tan[q] = new Vector4(tt.x, tt.y, tt.z, 1f);
        }
        m.tangents = tan;
        m.RecalculateBounds();
        var g = new GameObject("Banks"); g.transform.SetParent(stage, false);
        g.AddComponent<MeshFilter>().sharedMesh = m;
        g.AddComponent<MeshRenderer>().sharedMaterials = new[] { earth, topsoil, mudM, lawn };
    }

    void BuildForest()
    {
        string[] kinds = { "island_tree_01", "island_tree_02" };
        int k = 0;
        foreach (float side in new[] { 1f, -1f })
        {
            for (int i = 0; i < 14; i++)
            {
                float z = 13f + i * 9f + 5f * Hash(i, 41);
                Tree(kinds[k++ % 2], new Vector3(side * (8.6f + 3.5f * Hash(i, 42)), 1.2f, z), 360f * Hash(i, 43), 8f + 5f * Hash(i, 44));
            }
            for (int i = 0; i < 6; i++)
                Tree(kinds[k++ % 2], new Vector3(side * (13f + 14f * Hash(i, 45)), 1.2f, 1.5f + 24f * Hash(i, 46)), 360f * Hash(i, 47), 9f + 5f * Hash(i, 48));
        }
        for (int i = 0; i < 8; i++)
            Tree(kinds[i % 2], new Vector3(-60f + i * 17f + 6f * Hash(i, 51), 1.2f, RIVER_Z1 + 3f + 9f * Hash(i, 52)), 360f * Hash(i, 53), 12f + 6f * Hash(i, 54));
        var rng = new System.Random(23);
        for (int i = 0; i < 1500; i++)
        {
            float z = Mathf.Pow((float)rng.NextDouble(), 1.6f) * 26f + 0.05f;
            float x = (rng.NextDouble() < 0.5 ? -1f : 1f) * (7.1f + (float)rng.NextDouble() * 10f);
            string id = rng.NextDouble() < 0.5 ? "grass_medium_01" : (rng.NextDouble() < 0.5 ? "grass_medium_02" : "grass_bermuda_01");
            PH(id, rng.Next(0, 21), new Vector3(x, 1.2f, z), (float)rng.NextDouble() * 360f, 1.0f + (float)rng.NextDouble() * 0.7f);
        }
        for (int i = 0; i < 22; i++)
            PH("shrub_02", i % 4, new Vector3((i % 2 == 0 ? -1f : 1f) * (7.4f + 9f * Hash(i, 61)), 1.2f, 0.6f + 16f * Hash(i, 62)), 360f * Hash(i, 63), 0.45f + 0.3f * Hash(i, 64));
    }

    void BuildCar()
    {
        car = new GameObject("Car").transform;
        car.SetParent(stage, false);
        Model("car_body", Vector3.zero, car);
        glassFront = Model("car_glass_front", B(-0.22f, -0.87f, 0.74f), car);
        Model("car_glass_rear", B(0.71f, -0.87f, 0.74f), car);
        windshield = Model("car_windshield", B(-1.05f, 0f, 0.66f), car);
        windshield.transform.localRotation = Quaternion.Euler(0f, 0f, -31.6f) * FACE;
        windshield.transform.localScale = new Vector3(1f, 0.91f, 1f);
        door = Model("car_door_near", B(-0.75f, -0.87f, 0.02f), car);
        // the inner door handle: a recess plate and a chrome lever under the door's top rail, where the left hand reaches
        door.transform.localRotation = FACE;
        handleBase = Prim(PrimitiveType.Cube, "handle_base", HANDLE + new Vector3(0f, 0f, -0.012f), new Vector3(0.16f, 0.06f, 0.02f), WellLook.Lit("handle_base", Hex("#16171A"), 0.4f), car);
        handleLever = Prim(PrimitiveType.Cube, "handle_lever", HANDLE + new Vector3(0.02f, 0f, 0.008f), new Vector3(0.12f, 0.022f, 0.022f), WellLook.Lit("chrome_lever", Hex("#C9CDD2"), 0.85f, 1f), car);
        handleBase.transform.SetParent(door.transform, true);
        handleLever.transform.SetParent(door.transform, true);
        leverRest = handleLever.transform.localRotation;
        phone = Model("phone", Vector3.zero);
        breaker = Model("window_breaker", Vector3.zero);
        doorInner = new GameObject("doorInner"); doorInner.transform.SetParent(car, false);
        var trim = WellLook.ModelMaterial("car_interior");
        Prim(PrimitiveType.Cube, "panel_f", new Vector3(-0.27f, 0.42f, -0.85f), new Vector3(0.92f, 0.62f, 0.04f), trim, doorInner.transform);
        Prim(PrimitiveType.Cube, "panel_r", new Vector3(0.72f, 0.42f, -0.85f), new Vector3(0.84f, 0.62f, 0.04f), trim, doorInner.transform);
        Prim(PrimitiveType.Cube, "armrest", new Vector3(-0.3f, 0.6f, -0.8f), new Vector3(0.5f, 0.05f, 0.09f), trim, doorInner.transform);
        Prim(PrimitiveType.Cube, "console", new Vector3(-0.28f, 0.27f, 0f), new Vector3(0.75f, 0.52f, 0.22f), trim, car);
        Prim(PrimitiveType.Cube, "switch", SWITCH - new Vector3(0f, 0.022f, 0f), new Vector3(0.08f, 0.02f, 0.06f), WellLook.Lit("switch", Hex("#0E0F10"), 0.5f), car);
        Prim(PrimitiveType.Cube, "switch_btn", SWITCH - new Vector3(0f, 0.008f, 0f), new Vector3(0.035f, 0.012f, 0.024f), WellLook.Lit("switch_btn", Hex("#4A4E52"), 0.6f), car);
        Prim(PrimitiveType.Cube, "glass_edge", new Vector3(0f, 0.6f, 0f), new Vector3(0.95f, 0.014f, 0.032f), WellLook.Lit("rubber", Hex("#111213"), 0.3f), glassFront.transform);
        belt = new GameObject("belt_unused");
        var beltMat = WellLook.Lit("belt", Hex("#1E2022"), 0.28f);
        beltMat.SetFloat("_DoubleSidedEnable", 1f); beltMat.SetFloat("_CullMode", 0f);
#if UNITY_EDITOR
        UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(beltMat);
#endif
        beltObj = new GameObject("seatbelt"); beltObj.transform.SetParent(stage, false);
        beltMesh = new Mesh { name = "seatbelt" }; beltMesh.MarkDynamic();
        beltObj.AddComponent<MeshFilter>().sharedMesh = beltMesh;
        beltObj.AddComponent<MeshRenderer>().sharedMaterial = beltMat;
        latch = Prim(PrimitiveType.Cube, "latch", Vector3.zero, new Vector3(0.05f, 0.06f, 0.008f), WellLook.Lit("latch", Hex("#9AA0A6"), 0.7f, 1f), stage);
        buckleBox = Prim(PrimitiveType.Cube, "buckle", Vector3.zero, new Vector3(0.05f, 0.1f, 0.035f), WellLook.Lit("buckle", Hex("#141516"), 0.35f), stage);
        cabinWater = Prim(PrimitiveType.Cube, "cabinWater", Vector3.zero, Vector3.one, WellLook.Water("cabinWater", Hex("#2F5A52"), 0.55f, 0.92f, false), car);
        var crackMat = WellLook.Unlit("crack", new Color(0.95f, 0.97f, 1f, 0.9f), null, true);
        crackLines = new GameObject[9];
        crack = new GameObject("crack"); crack.transform.SetParent(windshield.transform, false);
        for (int i = 0; i < crackLines.Length; i++)
        {
            float a = i * 40f + 13f * Hash(i, 2);
            crackLines[i] = Prim(PrimitiveType.Cube, "crack" + i, Vector3.zero, new Vector3(0.006f, 0.18f + 0.12f * Hash(i, 3), 0.006f), crackMat, crack.transform);
            crackLines[i].transform.localRotation = Quaternion.Euler(a, 0f, 0f);
        }
        adult = new Human(this, "Mixamo/remy", 1.78f);
        kid = new Human(this, Resources.Load<GameObject>("Mixamo/sophie") != null ? "Mixamo/sophie" : "Mixamo/remy", 1.42f);
    }

    void BuildFx()
    {
        var glassBit = WellLook.Lit("shard", Hex("#CFE6EA"), 0.95f, 0.1f);
        shards = Many(46, PrimitiveType.Cube, "shard", glassBit);
        var puff = PuffTexture();
        splash = Many(160, PrimitiveType.Quad, "splash", WellLook.Unlit("splash", new Color(0.86f, 0.9f, 0.88f, 0.75f), puff, true));
        spray = Many(90, PrimitiveType.Quad, "spray", WellLook.Unlit("spray", new Color(0.95f, 0.97f, 0.96f, 0.7f), puff, true));
        inflow = Many(120, PrimitiveType.Quad, "inflow", WellLook.Unlit("inflow", new Color(0.55f, 0.68f, 0.64f, 0.6f), puff, true));
        bubbles = Many(60, PrimitiveType.Sphere, "bubble", WellLook.Water("bubble", Hex("#DFF2F0"), 0.55f, 0.98f, false));
        var am = WellLook.Emissive(WellLook.Lit("arrow", Hex("#E9EEF0"), 0.5f), Hex("#E9EEF0"), 3000f);
        var cone = ConeMesh(0.16f, 0.3f, 32);
        arrows = new GameObject[5];
        for (int i = 0; i < arrows.Length; i++)
        {
            var root = new GameObject("arrow"); root.transform.SetParent(stage, false);
            Prim(PrimitiveType.Capsule, "shaft", new Vector3(0, 0.52f, 0), new Vector3(0.11f, 0.25f, 0.11f), am, root.transform);
            var head = new GameObject("head"); head.transform.SetParent(root.transform, false);
            head.AddComponent<MeshFilter>().sharedMesh = cone;
            head.AddComponent<MeshRenderer>().sharedMaterial = am;
            root.SetActive(false);
            arrows[i] = root;
        }
    }

    // ------------------------------------------------------------------ the car's path
    // floor centre of the car and its rotation at second t
    void CarPose(float t, out Vector3 pos, out Quaternion rot)
    {
        float hit = Tw(1, "bridge") - 0.1f, land = Tw(1, "river") + 0.05f;
        float sink0 = Tw(1, "sinks"), pours = Tw(8, "pours"), bed = Tw(10, "fills", 0, true);
        float x, y, z, roll = 0f, pitch = 0f, yaw = 0f;
        if (t < hit)
        {
            float u = t / hit;
            x = Mathf.Lerp(12f, 2.2f, u);
            z = Mathf.Lerp(3.2f, 2.5f, E(Mathf.Clamp01((u - 0.4f) / 0.6f)));
            y = ROAD;
            yaw = -18f * E(Mathf.Clamp01((u - 0.4f) / 0.6f));
        }
        else if (t < land)
        {
            float dt = t - hit, T = land - hit;
            x = 2.2f - 3.4f * dt / T;
            z = Mathf.Lerp(2.5f, CAR_Z, dt / T);
            y = Mathf.Max(-0.45f, ROAD - 4.65f * (dt * dt) / (T * T));
            yaw = -18f + 14f * dt / T;
            pitch = 22f * dt / T;
        }
        else
        {
            float bob = Mathf.Exp(-(t - land) * 1.2f);
            x = -1.2f - 0.15f * PE(t, land, sink0 + 3f);
            z = CAR_Z;
            float floatY = Mathf.Lerp(-0.45f, -0.82f, PE(t, sink0, pours));
            y = floatY + 0.25f * bob * Mathf.Sin((t - land) * 7f) - 0.35f * bob;
            yaw = -4f * (1f - PE(t, land, land + 2f));
            pitch = 22f * bob * 0.6f + 2f * Mathf.Sin(t * 1.3f);
            roll = 1.5f * Mathf.Sin(t * 1.1f);
            float dive = PE(t, pours + 0.6f, bed);
            float nose = 30f * PE(t, Tw(8, "tips") - 0.2f, Tw(8, "down", 0, true) + 0.8f) * (1f - 0.75f * PE(t, bed - 1.2f, bed + 0.5f));
            y = Mathf.Lerp(y, -3.05f, dive);
            pitch += nose;
        }
        pos = new Vector3(x, y, z);
        // pitch = nose down (the nose is -x): a positive rotation about z
        rot = Quaternion.Euler(roll, yaw, pitch);
    }

    // ------------------------------------------------------------------ shots
    // shots from inside the car, in the car's own frame (x to the tail, y up, -z the driver's side)
    struct Inside { public float t0, t1, fov; public Vector3 p0, p1, l0, l1; }
    List<Inside> insides;
    void BuildInsides()
    {
        insides = new List<Inside> {
            // from the passenger seat: his hand goes to his pocket (the camera on his lap), the phone comes up (on his face)
            new Inside { t0 = Tw(2, "Your") - 0.1f, t1 = Tw(2, "call") + 0.15f, fov = 62f,
                p0 = new Vector3(-0.5f, 1.08f, 0.36f), p1 = new Vector3(-0.48f, 1.1f, 0.34f), l0 = new Vector3(-0.18f, 0.66f, -0.36f), l1 = new Vector3(-0.24f, 1.0f, -0.45f) },
            new Inside { t0 = Tw(2, "call") + 0.15f, t1 = Tw(2, "leave") + 0.5f, fov = 58f,
                p0 = new Vector3(-0.48f, 1.1f, 0.34f), p1 = new Vector3(-0.45f, 1.08f, 0.28f), l0 = new Vector3(-0.24f, 1.0f, -0.45f), l1 = new Vector3(-0.26f, 0.98f, -0.47f) },
            // the seatbelt: his hand on the buckle, the latch carried across to the door
            new Inside { t0 = Tw(3, "Seatbelt") - 0.25f, t1 = Tw(4, "Then") - 0.05f, fov = 60f,
                p0 = new Vector3(-0.52f, 1.1f, 0.32f), p1 = new Vector3(-0.5f, 1.12f, 0.28f), l0 = new Vector3(-0.14f, 0.72f, -0.36f), l1 = new Vector3(-0.14f, 0.88f, -0.56f) },
            // the window: his hand on the switch in the armrest, the glass goes down
            new Inside { t0 = Tw(5, "So") - 0.05f, t1 = Tw(5, "works", 0, true) + 0.3f, fov = 62f,
                p0 = new Vector3(-0.52f, 1.08f, 0.4f), p1 = new Vector3(-0.49f, 1.07f, 0.35f), l0 = new Vector3(-0.26f, 0.74f, -0.36f), l1 = new Vector3(-0.24f, 0.86f, -0.52f) },
        };
    }

    struct Key { public float t, x, y, d, yaw, pitch; public Key(float t, float x, float y, float d, float yaw, float pitch) { this.t = t; this.x = x; this.y = y; this.d = d; this.yaw = yaw; this.pitch = pitch; } }
    List<Key[]> shots;

    void BuildShots()
    {
        float end = doc.duration + TAIL + 1f;
        float c = -1.35f;                                  // the car's x once it floats
        float s2 = Tw(2, "Your"), s3 = Tw(3, "Seatbelt"), s4 = Tw(4, "Then"), s5 = Tw(5, "So"), s6 = Tw(6, "But"), s7 = Tw(7, "Never"),
              s8 = Tw(8, "Now"), s9 = Tw(9, "Kids"), s10 = Tw(10, "And"), s11 = Tw(11, "Take"), s12 = Tw(12, "Congratulations");
        shots = new List<Key[]> {
            new[] { new Key(0f, 1.0f, 1.2f, 30f, -6f, 10f), new Key(Tw(1, "river"), 0.0f, 0.4f, 26f, -4f, 10f), new Key(Tw(1, "floats"), -0.6f, -0.3f, 18f, -3f, 9f), new Key(s2, -1.2f, -0.6f, 12f, -2f, 8f) },
            new[] { new Key(s2, c - 0.35f, -0.05f, 4.4f, 4f, 4f), new Key(s3, c - 0.4f, -0.08f, 4.0f, 6f, 4f) },
            new[] { new Key(s3, c - 0.2f, -0.2f, 3.2f, 10f, 3f), new Key(s4, c - 0.25f, -0.25f, 3.0f, 12f, 3f) },
            new[] { new Key(s4, c + 0.1f, -0.2f, 6.2f, -46f, 4f), new Key(s5, c, -0.25f, 5.8f, -42f, 4f) },
            new[] { new Key(s5, c - 0.3f, 0.1f, 3.6f, 4f, 4f), new Key(s6, c - 0.3f, 0.1f, 3.3f, 6f, 4f) },
            new[] { new Key(s6, c - 0.3f, 0.12f, 3.0f, 8f, 4f), new Key(Tw(6, "breaker", 0, true) + 0.2f, c - 0.3f, 0.1f, 2.8f, 10f, 4f), new Key(s7, c - 0.3f, 0.1f, 2.9f, 10f, 4f) },
            new[] { new Key(s7, c - 1.1f, 0.15f, 4.0f, 58f, 6f), new Key(s8, c - 1.1f, 0.1f, 3.7f, 52f, 6f) },
            new[] { new Key(s8, c - 0.3f, 0.1f, 9.5f, -4f, 6f), new Key(s9, c - 0.4f, 0.2f, 9.0f, -2f, 5f) },
            new[] { new Key(s9, c + 0.1f, 0.4f, 5.8f, 6f, 3f), new Key(s10, c, 0.9f, 6.4f, 4f, 3f) },
            new[] { new Key(s10, c, 0.2f, 7.5f, -8f, 4f), new Key(Tw(10, "door") - 0.4f, c - 0.45f, 0.05f, 4.4f, -12f, 4f), new Key(s11, c - 0.45f, 0.05f, 4.6f, -14f, 4f) },
            new[] { new Key(s11, c + 0.4f, -2.3f, 7f, 2f, 3f), new Key(s12, c + 0.4f, -0.5f, 7.5f, 2f, 6f) },
            new[] { new Key(s12, c + 0.2f, 0.4f, 14f, -3f, 9f), new Key(end, 0.6f, 1.0f, 26f, -5f, 10f) },
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
        float yaw = Spline(t, shot, k => k.yaw) + 0.25f * Mathf.Sin(t * 0.43f) + 0.1f * Mathf.Sin(t * 0.97f + 1f);
        float pitch = Spline(t, shot, k => k.pitch) + 0.12f * Mathf.Sin(t * 0.61f + 2f);
        // close shots ride with the car as it settles and dives
        Vector3 cp; Quaternion cr;
        CarPose(t, out cp, out cr);
        bool rides = t >= Tw(2, "Your") && t < Tw(11, "Take");
        if (rides) fy += cp.y + 0.45f;
        float land = Tw(1, "river") + 0.05f;
        float sh = t >= land ? 0.12f * Mathf.Exp(-(t - land) * 5f) : 0f;
        float brk = Tw(6, "breaker") - 0.05f;
        sh += t >= brk ? 0.05f * Mathf.Exp(-(t - brk) * 8f) : 0f;
        fx += sh * Mathf.Sin(t * 61f); fy += sh * Mathf.Cos(t * 73f);
        var focus = new Vector3(fx, fy, t < Tw(2, "Your") || t >= Tw(12, "Congratulations") ? 0.3f : CAR_Z - 0.6f);
        var rot = Quaternion.Euler(pitch, yaw, 0f);
        cam.transform.position = focus + rot * new Vector3(0f, 0f, -d);
        cam.transform.rotation = rot;
        cam.focusDistance = d;
        fill.transform.position = focus + new Vector3(0.6f, 0.8f, -1.6f);
        fill.intensity = rides ? 260000f : 0f;

        // inside the car
        if (insides == null) BuildInsides();
        bool inside = false;
        insideNow = false;
        foreach (var s in insides)
        {
            if (t < s.t0 || t >= s.t1) continue;
            float k = PE(t, s.t0, s.t1);
            var p = cr * Vector3.Lerp(s.p0, s.p1, k) + cp;
            var l = cr * Vector3.Lerp(s.l0, s.l1, k) + cp;
            p += new Vector3(0.006f * Mathf.Sin(t * 1.3f), 0.005f * Mathf.Sin(t * 1.7f + 1f), 0f);   // hand-held
            cam.transform.position = p;
            cam.transform.rotation = Quaternion.LookRotation(l - p, cr * Vector3.up);
            cam.fieldOfView = s.fov;
            cam.focusDistance = Vector3.Distance(p, l) * 0.6f;
            fill.transform.position = p + cr * new Vector3(0.2f, 0.3f, 0.1f);
            fill.intensity = 30000f;
            inside = true;
            insideNow = true;
        }
        if (!inside) cam.fieldOfView = 34f;
        underwater.weight = inside && t >= Tw(8, "Now") ? 1f : 0f;
    }

    // ------------------------------------------------------------------ evaluate
    public void Evaluate(float t)
    {
        EvalCamera(t);
        Vector3 cp; Quaternion cr;
        CarPose(t, out cp, out cr);
        car.position = cp; car.rotation = cr;
        EvalRiver(t, cp);
        EvalPeople(t);
        EvalCarParts(t);
        EvalFx(t, cp);
    }

    float WaveH(float x, float z, float t, Vector3 cp)
    {
        float h = 0.05f * Mathf.Sin(1.1f * x + 0.7f * z + 1.3f * t) + 0.035f * Mathf.Sin(-0.6f * x + 1.7f * z + 1.9f * t)
                + 0.02f * Mathf.Sin(3f * x + 2.2f * z + 3.1f * t) + 0.012f * Mathf.Sin(6f * x - 4f * z + 4.3f * t);
        // rings: the landing, then where the swimmer comes up
        float land = Tw(1, "river") + 0.05f, up = Tw(12, "Congratulations") + 0.3f;
        foreach (var (t0, cx) in new[] { (land, -1.3f), (up, -1.0f) })
            if (t > t0)
            {
                float dt = t - t0, dx = x - cx, dz = z - CAR_Z * 0.6f, r = Mathf.Sqrt(dx * dx + dz * dz);
                h += 0.12f * Mathf.Exp(-dt * 0.9f) * Mathf.Sin(6f * r - 9f * dt) * Mathf.Exp(-Mathf.Abs(r - 2.2f * dt) * 0.8f);
            }
        return h;
    }

    void EvalRiver(float t, Vector3 cp)
    {
        const int NX = 220, NZ = 150;
        for (int j = 0; j <= NZ; j++)
        {
            float z = RIVER_Z1 * Mathf.Pow(j / (float)NZ, 2.4f);
            for (int i = 0; i <= NX; i++)
            {
                float u = (i / (float)NX) * 2f - 1f, x = u * Mathf.Abs(u) * 60f;
                float y = WaveH(x, z, t, cp);
                if (insideNow)
                {
                    var lp = car.InverseTransformPoint(new Vector3(x, y, z));
                    if (Mathf.Abs(lp.x) < 2.3f && Mathf.Abs(lp.z) < 0.95f && lp.y > -0.3f) y = car.TransformPoint(lp.x, -0.35f, lp.z).y;
                }
                surfV[j * (NX + 1) + i] = new Vector3(x, y, z);
            }
        }
        surfMesh.vertices = surfV; surfMesh.RecalculateNormals(); surfMesh.RecalculateBounds();
        for (int i = 0; i <= NX; i++)
        {
            float u = (i / (float)NX) * 2f - 1f, x = u * Mathf.Abs(u) * 60f;
            faceV[i * 2] = new Vector3(x, WaveH(x, 0f, t, cp), 0f);
            faceV[i * 2 + 1] = new Vector3(x, BED, 0f);
        }
        faceMesh.vertices = faceV; faceMesh.RecalculateNormals(); faceMesh.RecalculateBounds();
        for (int i = 0; i < motes.Length; i++)
        {
            var g = motes[i];
            Show(g, true);
            float x = Mathf.Repeat(Hash(i, 7) * 10f + t * 0.12f, 10f) - 5f;
            g.transform.position = new Vector3(x, Mathf.Lerp(-0.25f, BED + 0.2f, Hash(i, 8)) + 0.04f * Mathf.Sin(t + i), -0.02f);
            g.transform.localScale = Vector3.one * (0.012f + 0.02f * Hash(i, 9));
        }
    }

    void EvalCarParts(float t)
    {
        // the rail section is torn off by the car and falls into the river
        float hit = Tw(1, "bridge") - 0.1f;
        float dtR = Mathf.Max(0f, t - hit);
        var rp = new Vector3(0.4f - 2.5f * Mathf.Min(dtR, 1f), ROAD - Mathf.Min(4.9f * dtR * dtR, ROAD + 1.2f), BRIDGE_Z - 4.35f - 1.6f * Mathf.Min(dtR, 0.9f));
        railBreak.transform.position = rp;
        railBreak.transform.rotation = Quaternion.Euler(-70f * Mathf.Min(dtR, 1f), 20f * Mathf.Min(dtR, 1f), 0f) * FACE;
        Show(railBreak, t < hit + 1.6f);

        Show(doorInner, insideNow);

        // the window: down while the power works, stuck part-way when the button dies, then shattered (back for "if nothing breaks")
        float open = 0.2f * PE(t, Tw(5, "open") + 0.1f, Tw(5, "works", 0, true)) + 0.02f * Mathf.Max(0f, Mathf.Sin((t - Tw(6, "button")) * 30f)) * P(t, Tw(6, "button"), Tw(6, "dies", 0, true));
        float shatter = Tw(6, "breaker") + 0.05f, back = Tw(10, "And");
        Show(glassFront, t < shatter || t >= back);
        glassFront.transform.localPosition = B(-0.22f, -0.87f, 0.74f - (t < back ? open : 0f));

        // the windshield takes a hit and only cracks
        float wHit = Tw(7, "laminated") - 0.1f;
        Show(crack, t > wHit);
        crack.transform.localPosition = new Vector3(0f, 0.55f, 0.35f);
        crack.transform.localScale = Vector3.one * Mathf.Min(1f, Back(P(t, wHit, wHit + 0.25f)));

        // the door: shut against the water, opens once the car is full
        float push = Tw(4, "push"), shut = Tw(4, "force", 0, true);
        float give = 3f * Mathf.Max(0f, Mathf.Sin((t - push) * 9f)) * P(t, push, push + 0.3f) * (1f - P(t, shut - 0.6f, shut));
        float openD = 75f * PE(t, Tw(10, "door") - 0.1f, Tw(10, "opens", 0, true) + 0.4f);
        door.transform.localRotation = Quaternion.Euler(0f, give + openD, 0f) * FACE;
        float pull = Mathf.Max(Win(t, Tw(4, "push") - 0.1f, Tw(5, "So") - 0.4f, 0.15f), Win(t, Tw(10, "door") - 0.35f, Tw(11, "push"), 0.15f));
        handleLever.transform.localRotation = leverRest * Quaternion.AngleAxis(28f * pull, Vector3.up);
        door.transform.localPosition = B(-0.75f, -0.87f, 0.02f);

        // water inside: pours in through the broken window, and fills the car before the door opens
        float fillK = PE(t, Tw(8, "pours"), Tw(10, "fills", 0, true));
        Show(cabinWater, fillK > 0.01f);
        if (fillK > 0.01f)
        {
            float h = 1.32f * fillK;
            cabinWater.transform.localPosition = new Vector3(0.1f, 0.06f + h / 2f, 0f);
            cabinWater.transform.localScale = new Vector3(2.3f, h, 1.66f);
        }

        // the seatbelt: a 3-point strap, pillar -> shoulder -> chest -> buckle -> lap -> floor; on "off" the hand takes
        // the latch across to the door, lets go, and the strap retracts up the pillar
        EvalBelt(t);

        // the phone: in the console, picked up on "call", put down on the passenger seat on "leave"
        float pick = PickT, drop = Tw(2, "leave") + 0.05f;
        Show(phone, t < Tw(5, "So"));
        var hand = adult.HandR;
        Show(phone, t >= pick && t < Tw(5, "So"));                     // in the pocket until the hand takes it
        if (t < pick) { phone.transform.position = adult.Pocket; phone.transform.rotation = car.rotation; }
        else if (t < drop)
        {
            // in the palm, screen turned to his eyes (the phone's screen faces its local +y, its long side local +z)
            var pp = hand.position + (adult.Head.position - hand.position).normalized * 0.04f;
            var toEyes = (adult.Head.position + car.rotation * new Vector3(-0.08f, 0.05f, 0f) - pp).normalized;
            phone.transform.position = pp;
            phone.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(car.rotation * Vector3.up, toEyes), toEyes);
        }
        else { phone.transform.position = car.TransformPoint(PHONE_SEAT); phone.transform.rotation = car.rotation * Quaternion.Euler(0f, 30f, 0f); }

        // the window breaker, in the hand from "hit" to the windshield's "won't break"
        Show(breaker, t > Tw(6, "hit") - 0.3f && t < Tw(7, "break", 0, true));
        breaker.transform.position = adult.HandL.position;
        breaker.transform.rotation = adult.HandL.rotation;
    }

    // a weight that rises before a, holds, and falls after b
    static float Win(float t, float a, float b, float ramp) => PE(t, a - ramp, a) * (1f - PE(t, b, b + ramp));
    // the phone: in the console, up in front of the face, down on the passenger seat (car frame)
    float PickT => Tw(2, "Your") + 0.8f;                        // the hand has been in the pocket: the phone comes out
    // steering wheel (Blender centre (-0.56,-0.45,0.87), radius 0.18, face tilted 65 degrees toward the driver):
    // grip point 20 degrees above the rim's middle, the wrist a little behind and outside it (car frame; z = -0.45 -+ ...)
    static readonly Vector2 WHEEL_GRIP = new Vector2(-0.586f, 0.926f), WHEEL_WRIST = new Vector2(-0.541f, 0.947f);
    static readonly Vector3 BUCKLE = new Vector3(-0.1f, 0.5f, -0.25f);
    static readonly Vector3 HANDLE = new Vector3(-0.38f, 0.69f, -0.82f);      // inner door handle, car frame, door shut
    static readonly Vector3 SWITCH = new Vector3(-0.38f, 0.56f, -0.06f);       // window switch on the centre console
    static readonly Vector3 PHONE_REST = new Vector3(-0.42f, 0.6f, -0.18f), PHONE_FACE = new Vector3(-0.44f, 0.95f, -0.38f), PHONE_SEAT = new Vector3(-0.3f, 0.42f, 0.3f);

    Mesh beltMesh;
    void EvalBelt(float t)
    {
        float tOff = Tw(3, "off"), tFirst = Tw(3, "first", 0, true);
        var A = CarPt(0.02f, 1.13f, -0.8f);           // top anchor on the B pillar
        var F = CarPt(-0.02f, 0.38f, -0.76f);         // floor anchor, door side
        var BK = CarPt(BUCKLE.x, BUCKLE.y, BUCKLE.z);
        var Lend = CarPt(0.02f, 0.7f, -0.8f);         // retracted: the latch hangs on the pillar
        bool inSeat = t < Tw(9, "then");
        Show(beltObj, inSeat);
        if (!inSeat) return;
        // the latch: buckled, then in the hand, then let go and pulled up the pillar
        Vector3 L;
        float relT = tFirst + 0.1f;
        if (t < tOff + 0.05f) L = BK;
        else if (t < relT) L = Vector3.Lerp(BK, adult.HandR.position + adult.Fwd * 0.03f, PE(t, tOff + 0.05f, tOff + 0.2f));
        else L = Vector3.Lerp(adult.HandR.position, Lend, PE(t, relT, relT + 0.45f));
        float hug = 1f - PE(t, tOff + 0.05f, tOff + 0.45f);          // the strap leaves the body once unlatched
        Vector3 Body(Vector3 bodyPt, Vector3 a, Vector3 b, float u) => Vector3.Lerp(Vector3.Lerp(a, b, u), bodyPt, hug);
        var fwd = adult.Fwd; var up = adult.Up; var right = adult.Right;
        // on the shirt, not inside it: measured offsets from the spine bones to the front of Remy's torso
        var shoulder = Vector3.Lerp(adult.Bone("LeftArm"), adult.Bone("Neck"), 0.45f) + fwd * 0.07f + up * 0.08f;
        var chest = adult.Bone("Spine2") + fwd * 0.19f + right * 0.02f;
        var belly = adult.Bone("Spine") + fwd * 0.2f + right * 0.11f;
        var lap = adult.Bone("Hips") + fwd * 0.2f + up * 0.06f;
        var pts = new List<Vector3> { A, Body(shoulder, A, L, 0.25f), Body(chest, A, L, 0.5f), Body(belly, A, L, 0.8f), L, Body(lap, L, F, 0.5f), F };
        // smooth it (Catmull-Rom) and sweep a 4.8 cm ribbon lying flat on the body
        var path = new List<Vector3>();
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
            for (int k = 0; k < 8; k++)
            {
                float u = k / 8f, u2 = u * u, u3 = u2 * u;
                path.Add(0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3));
            }
        }
        path.Add(pts[pts.Count - 1]);
        var vs = new Vector3[path.Count * 2]; var tris = new List<int>();
        for (int i = 0; i < path.Count; i++)
        {
            var tg = (path[Mathf.Min(path.Count - 1, i + 1)] - path[Mathf.Max(0, i - 1)]).normalized;
            var side = Vector3.Cross(tg, fwd).normalized * 0.024f;
            vs[2 * i] = beltObj.transform.InverseTransformPoint(path[i] - side);
            vs[2 * i + 1] = beltObj.transform.InverseTransformPoint(path[i] + side);
            if (i > 0) { int q = 2 * i - 2; tris.AddRange(new[] { q, q + 2, q + 1, q + 1, q + 2, q + 3 }); }
        }
        beltMesh.Clear(); beltMesh.vertices = vs; beltMesh.triangles = tris.ToArray(); beltMesh.RecalculateNormals(); beltMesh.RecalculateBounds();
        latch.transform.position = L; latch.transform.rotation = Quaternion.LookRotation(fwd, (A - L).normalized);
        buckleBox.transform.position = BK - car.up * 0.03f; buckleBox.transform.rotation = car.rotation * Quaternion.Euler(0f, 0f, -20f);
    }

    // seat points in the car's frame (hips joint)
    static readonly Vector3 SEAT = new Vector3(-0.18f, 0.5f, -0.45f), KID_SEAT = new Vector3(0.84f, 0.5f, -0.4f);
    Vector3 CarPt(float x, float y, float z) => car.TransformPoint(new Vector3(x, y, z));
    Quaternion FacingNose => car.rotation * Quaternion.Euler(0f, -90f, 0f);
    // swimming head first out of the near window, rising (the swim clip lies prone, head along +z)
    Quaternion OutOfWindow => Quaternion.LookRotation(car.rotation * new Vector3(0f, 0.6f, -1f), car.rotation * Vector3.up);

    void EvalPeople(float t)
    {
        float meOut0 = Tw(9, "then"), meOut1 = Tw(10, "And") - 0.1f;
        float swim0 = Tw(11, "push"), surf = Tw(12, "Congratulations") + 0.3f;
        float tAnd = Tw(10, "And"), tDoor = Tw(10, "door");
        bool inCar = t < meOut0 || (t >= tAnd && t < swim0);

        // ---------------- the driver: every beat is an action on its words (hands by IK, head looks at the job)
        if (inCar)
        {
            float tCall = Tw(2, "call"), tTime = Tw(2, "time"), tLeave = Tw(2, "leave");
            float tBelt = Tw(3, "Seatbelt"), tOff = Tw(3, "off"), tFirst = Tw(3, "first", 0, true);
            float tThen = Tw(4, "Then"), tPush = Tw(4, "push"), tSo = Tw(5, "So"), tOpen = Tw(5, "open");
            float tDies = Tw(6, "dies"), tHit = Tw(6, "hit"), tBrk = Tw(6, "breaker");
            float tNever = Tw(7, "Never"), tLam = Tw(7, "laminated"), tNow = Tw(8, "Now"), tKids = Tw(9, "Kids");
            float panic = PE(t, tNow - 0.3f, tNow + 0.4f);
            adult.Layer("driving", t, "sitting_scared", t * 1.2f, 0.25f + 0.75f * panic);
            adult.Place(CarPt(SEAT.x, SEAT.y, SEAT.z), FacingNose);

            // right hand: wheel, phone up to the face, phone down on the seat, the buckle, back on the wheel
            float tYour = Tw(2, "Your");
            float wR = (1f - Win(t, tSo + 0.1f, tHit - 0.35f, 0.3f)) * (1f - Win(t, tYour + 0.35f, tLeave + 0.1f, 0.35f)) * (1f - Win(t, tBelt - 0.15f, tFirst + 0.1f, 0.3f)) * (1f - panic);
            adult.Grip(false, CarPt(WHEEL_WRIST.x, WHEEL_WRIST.y, -0.45f + 0.204f), CarPt(WHEEL_GRIP.x, WHEEL_GRIP.y, -0.45f + 0.169f), wR);
            float wP = Win(t, tYour + 0.35f, tLeave + 0.15f, 0.35f);
            if (wP > 0.001f)
            {
                // the hand goes to the pocket, the phone comes out, up in front of the face; on "leave" down on the seat
                var pocket = adult.Pocket;
                Vector3 face = car.TransformPoint(PHONE_FACE), seat = car.TransformPoint(PHONE_SEAT);
                // into the pocket (fingers down), a moment there, out with the phone, up in front of the face
                var inPocket = pocket - adult.Up * 0.03f;
                if (t < PickT) adult.Grip(false, inPocket, inPocket - adult.Up * 0.12f + adult.Fwd * 0.03f, wP);
                else
                {
                    Vector3 tgt = t < tTime + 0.1f ? Vector3.Lerp(pocket + adult.Up * 0.06f, face, PE(t, PickT, tCall + 0.3f))
                                                   : Vector3.Lerp(face, seat, PE(t, tTime + 0.1f, tLeave + 0.05f));
                    tgt += Vector3.up * (0.006f * Mathf.Sin(t * 11f)) * PE(t, tCall + 0.3f, tCall + 0.5f);   // thumb on the screen
                    adult.Reach(false, tgt, wP);
                }
            }
            float wB = Win(t, tBelt - 0.15f, tFirst + 0.1f, 0.3f);
            if (wB > 0.001f)
            {
                float dip = 0.02f * Mathf.Sin(Mathf.PI * P(t, tOff - 0.15f, tOff + 0.05f));      // the thumb presses the button
                adult.Reach(false, Vector3.Lerp(CarPt(BUCKLE.x, BUCKLE.y - dip, BUCKLE.z), CarPt(-0.06f, 0.86f, -0.66f), PE(t, tOff + 0.05f, tFirst)), wB);
            }
            // after "Now": the right hand pushes the kid toward the window
            float wK = Win(t, tKids - 0.2f, Tw(9, "then") + 0.2f, 0.3f);
            if (wK > 0.001f) adult.Reach(false, kid.HipsPos + Vector3.up * 0.15f, wK * 0.9f);

            // left hand: wheel, the door, the window button, the breaker on the side glass, the windshield
            float wL = (1f - PE(t, tThen - 0.3f, tThen)) * (1f - panic);
            adult.Grip(true, CarPt(WHEEL_WRIST.x, WHEEL_WRIST.y, -0.45f - 0.204f), CarPt(WHEEL_GRIP.x, WHEEL_GRIP.y, -0.45f - 0.169f), wL);
            float wD = Win(t, tThen - 0.05f, tSo - 0.15f, 0.3f);
            if (wD > 0.001f)
            {
                float shove = t > tPush ? Mathf.Max(0f, Mathf.Sin((t - tPush) * 7f)) : 0f;
                adult.Grip(true, handleLever.transform.position + car.rotation * new Vector3(-0.05f, 0.02f, 0.06f - 0.03f * shove), handleLever.transform.position, wD);
            }
            float wBt = Win(t, tSo + 0.1f, tHit - 0.35f, 0.3f);
            if (wBt > 0.001f)
            {
                float press = t > tDies - 0.2f ? Mathf.Max(0f, Mathf.Sin((t - tDies) * 14f)) : (t > tOpen ? 0.6f : 0f);
                adult.Grip(false, CarPt(SWITCH.x - 0.02f, SWITCH.y + 0.06f - 0.012f * press, SWITCH.z), CarPt(SWITCH.x + 0.04f, SWITCH.y, SWITCH.z), wBt);
            }
            adult.Grip(true, CarPt(WHEEL_WRIST.x, WHEEL_WRIST.y, -0.45f - 0.204f), CarPt(WHEEL_GRIP.x, WHEEL_GRIP.y, -0.45f - 0.169f), Win(t, tSo + 0.1f, tHit - 0.35f, 0.3f));
            float wH = Win(t, tHit - 0.3f, tNever - 0.1f, 0.25f);
            if (wH > 0.001f)
            {
                var back = CarPt(-0.15f, 0.98f, -0.5f); var corner = CarPt(-0.62f, 0.86f, -0.86f);
                float k = PE(t, tBrk - 0.15f, tBrk) * (1f - 0.4f * PE(t, tBrk + 0.05f, tBrk + 0.5f));
                adult.Reach(true, Vector3.Lerp(back, corner, k), wH);
            }
            float wW = Win(t, tLam - 0.55f, tNow - 0.1f, 0.25f);
            if (wW > 0.001f)
            {
                float k = PE(t, tLam - 0.25f, tLam - 0.1f) * (1f - 0.5f * PE(t, tLam, tLam + 0.4f));
                adult.Reach(true, Vector3.Lerp(CarPt(-0.3f, 1.04f, -0.45f), CarPt(-0.86f, 1.0f, -0.36f), k), wW);
            }

            // the head looks at what the hands do
            adult.Look(adult.Pocket, Win(t, tYour + 0.3f, PickT, 0.25f));
            adult.Look(adult.HandR.position, Win(t, PickT + 0.1f, tTime + 0.15f, 0.25f));
            adult.Look(CarPt(-0.35f, 1.0f, -1.3f), Win(t, tTime + 0.15f, tLeave + 0.3f, 0.25f));
            adult.Look(adult.HandR.position, Win(t, tBelt - 0.2f, tFirst, 0.25f));
            adult.Look(CarPt(-0.45f, 0.8f, -0.95f), Win(t, tThen - 0.1f, tSo - 0.1f, 0.25f));
            adult.Look(CarPt(SWITCH.x, SWITCH.y, SWITCH.z), Win(t, tSo, tOpen + 0.2f, 0.25f));
            adult.Look(CarPt(-0.35f, 0.95f, -0.95f), Win(t, tOpen + 0.45f, tNever - 0.1f, 0.25f));
            adult.Look(CarPt(-0.95f, 1.0f, -0.4f), Win(t, tNever - 0.1f, tNow, 0.25f));
            adult.Look(CarPt(-0.3f, 0.85f, -0.95f), Win(t, tNow, tKids - 0.1f, 0.25f));
            adult.Look(kid.HipsPos + Vector3.up * 0.4f, Win(t, tKids - 0.1f, meOut0, 0.25f));
            // the door, at the end: hand on the handle, pull, push it open, out
            float wDo = Win(t, tDoor - 0.6f, swim0 - 0.1f, 0.3f);
            if (wDo > 0.001f) adult.Grip(true, handleLever.transform.position + car.rotation * new Vector3(-0.05f, 0.02f, 0.06f), handleLever.transform.position, wDo);
            adult.Look(handleLever.transform.position, Win(t, tDoor - 0.6f, swim0, 0.25f));
            adult.Show(true);
        }
        else if (t < meOut1)
        {
            // out through the window: climbing pose, from the seat to just outside the car, facing the window
            float k = PE(t, meOut0, meOut1);
            adult.Sample("swimming", 1.3f * (t - meOut0));
            var a0 = CarPt(SEAT.x, SEAT.y + 0.3f, SEAT.z + 0.35f); var a1 = CarPt(-0.25f, 1.25f, -1.75f);
            adult.Place(Vector3.Lerp(a0, a1, k), Quaternion.Slerp(FacingNose, OutOfWindow, PE(t, meOut0, meOut0 + 0.5f)));
            adult.Show(true);
        }
        else if (t < swim0) adult.Show(false);          // "if nothing breaks": the empty car fills and its door opens
        else
        {
            var start = CarPt(-0.3f, 0.65f, -1.15f);              // out of the open door
            var top = new Vector3(start.x - 0.2f, -0.95f, 0.55f);
            if (t < surf)
            {
                adult.Sample("swimming", t - swim0);
                adult.Place(Vector3.Lerp(start, top, PE(t, swim0, surf)), Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-78f, 0f, 0f));
            }
            else
            {
                adult.Sample("treading_water", t - surf);
                adult.Place(new Vector3(top.x, -0.72f + 0.04f * Mathf.Sin((t - surf) * 3f), 0.55f), Quaternion.Euler(0f, 180f, 0f));
            }
            adult.Show(true);
        }

        // ---------------- the kid in the back seat, out of the window first, then up toward the surface
        bool kShow = t >= Tw(2, "Your") && t < Tw(10, "And") + 0.3f;
        kid.Show(kShow);
        if (kShow)
        {
            float k0 = Tw(9, "Kids") - 0.1f, k1 = Tw(9, "window", 0, true);
            if (t < k0)
            {
                kid.Layer("sitting_idle", t + 1.7f, "sitting_scared", t + 3f, t > Tw(8, "Now") ? 1f : 0.4f);
                kid.Place(CarPt(KID_SEAT.x, KID_SEAT.y, KID_SEAT.z), FacingNose);
            }
            else if (t < k1)
            {
                float k = PE(t, k0, k1);
                kid.Sample("swimming", 1.3f * (t - k0));
                kid.Place(Vector3.Lerp(CarPt(KID_SEAT.x, KID_SEAT.y + 0.3f, KID_SEAT.z + 0.3f), CarPt(0.6f, 1.2f, -1.6f), k), Quaternion.Slerp(FacingNose, OutOfWindow, PE(t, k0, k0 + 0.5f)));
            }
            else
            {
                kid.Sample("swimming", t - k1);
                var from = CarPt(0.6f, 1.2f, -1.6f);
                kid.Place(Vector3.Lerp(from, new Vector3(from.x, -0.9f, 0.5f), PE(t, k1, Tw(10, "And") + 1.2f)), Quaternion.Euler(0f, 180f, 0f) * Quaternion.Euler(-78f, 0f, 0f));
            }
        }

        adult.Bake(); kid.Bake();

        // the phone screen lights the face
        phoneLight.enabled = t < Tw(2, "leave") + 0.3f && t > Tw(2, "call") - 0.3f;
        phoneLight.transform.position = phone.transform.position + phone.transform.up * 0.06f;
    }

    void EvalFx(float t, Vector3 cp)
    {
        float land = Tw(1, "river") + 0.05f;
        // the splash: a white crown low around the car, then streaks of spray arcing out and falling back
        var crownC = new Color(0.88f, 0.92f, 0.9f, 0.85f);
        for (int i = 0; i < splash.Length; i++)
        {
            var g = splash[i];
            float dt = t - land - 0.25f * Hash(i, 6), life = 0.9f + 0.5f * Hash(i, 7);
            bool on = dt > 0f && dt < life;
            Show(g, on);
            if (!on) continue;
            float a = Hash(i, 1) * 6.283f, sp = 0.8f + 1.6f * Hash(i, 2), up = 1.6f + 2.4f * Hash(i, 3);
            float k = dt / life;
            var p = new Vector3(cp.x + Mathf.Cos(a) * (2.0f + sp * dt), Mathf.Max(0.12f, up * dt - 4.9f * dt * dt) + 0.1f, CAR_Z + Mathf.Sin(a) * (1.0f + sp * 0.5f * dt));
            Billboard(g, p, (0.5f + 0.7f * Hash(i, 5)) * (0.6f + 0.8f * k));
            Fade(g, crownC, (1f - k) * (1f - k) * Mathf.Clamp01(dt * 12f));
        }
        var sprayC = new Color(0.95f, 0.97f, 0.96f, 0.8f);
        for (int i = 0; i < spray.Length; i++)
        {
            var g = spray[i];
            float dt = t - land - 0.04f - 0.15f * Hash(i, 15), life = 0.75f + 0.45f * Hash(i, 16);
            bool on = dt > 0f && dt < life;
            Show(g, on);
            if (!on) continue;
            float a = Hash(i, 11) * 6.283f, sp = 1.5f + 3f * Hash(i, 12), up = 3f + 3.5f * Hash(i, 13);
            var p = new Vector3(cp.x + Mathf.Cos(a) * (1.8f + sp * dt), up * dt - 4.9f * dt * dt, CAR_Z + Mathf.Sin(a) * (0.9f + sp * 0.4f * dt));
            var v = new Vector3(Mathf.Cos(a) * sp, up - 9.8f * dt, Mathf.Sin(a) * sp * 0.4f);
            Show(g, p.y > 0.08f);
            Streak(g, p, v, 0.035f + 0.04f * Hash(i, 14), 0.07f);
            Fade(g, sprayC, 1f - dt / life);
        }

        // glass bursts inward at the window
        float shatter = Tw(6, "breaker") + 0.05f;
        var win = car.TransformPoint(new Vector3(-0.22f, 1.05f, -0.87f));
        for (int i = 0; i < shards.Length; i++)
        {
            var g = shards[i];
            float dt = t - shatter;
            bool on = dt > 0f && dt < 1.4f;
            Show(g, on);
            if (!on) continue;
            var dir = new Vector3((Hash(i, 1) - 0.5f) * 1.6f, (Hash(i, 2) - 0.6f) * 1.2f, 0.6f + 1.2f * Hash(i, 3));
            g.transform.position = win + dir * dt * 0.9f + Vector3.down * (0.6f * dt * dt);
            g.transform.rotation = Quaternion.Euler(dt * 400f + i * 30f, dt * 300f, i * 70f);
            g.transform.localScale = new Vector3(0.03f + 0.04f * Hash(i, 4), 0.02f + 0.03f * Hash(i, 5), 0.004f);
        }

        // the river pours in through the window
        float pA = Tw(8, "pours") - 0.1f, pB = Tw(9, "Kids") + 0.3f;
        for (int i = 0; i < inflow.Length; i++)
        {
            var g = inflow[i];
            bool on = t > pA && t < pB;
            Show(g, on);
            if (!on) continue;
            float ph = Frac(t * 1.4f + i * 0.0713f);
            var p = win + car.rotation * new Vector3((Hash(i, 1) - 0.5f) * 0.8f, -0.9f * ph * ph, 0.2f + 1.2f * ph);
            Billboard(g, p, (0.18f + 0.22f * Hash(i, 2)) * (0.6f + ph));
            Fade(g, new Color(0.55f, 0.68f, 0.64f, 0.6f), Mathf.Sin(Mathf.PI * ph));
        }

        // air leaves the car as it sinks, the swimmer breathes out on the way up
        for (int i = 0; i < bubbles.Length; i++)
        {
            var g = bubbles[i];
            bool fromCar = t > Tw(8, "pours") && t < Tw(10, "door") + 1f;
            bool fromMe = t > Tw(11, "push") && t < Tw(12, "Congratulations") + 0.3f && i % 3 == 0;
            Show(g, fromCar || fromMe);
            if (!g.activeSelf) continue;
            float ph = Frac(t * 0.5f + i / (float)bubbles.Length);
            Vector3 o = fromMe ? adult.Head.position : car.TransformPoint(new Vector3((Hash(i, 1) - 0.5f) * 1.8f, 1.35f, (Hash(i, 2) - 0.5f) * 1.2f));
            var p = o + new Vector3(0.1f * Mathf.Sin(t * 3f + i), ph * 3.2f, 0f);
            Show(g, p.y < -0.05f);
            g.transform.position = p;
            g.transform.localScale = Vector3.one * (0.03f + 0.05f * Hash(i, 3));
        }

        // the river's push on the door
        float aA = Tw(4, "river") - 0.15f, aB = Tw(5, "So");
        for (int i = 0; i < arrows.Length; i++)
        {
            var g = arrows[i];
            float k = Back(P(t, aA + i * 0.06f, aA + i * 0.06f + 0.3f)) * (1f - PE(t, aB - 0.25f, aB));
            Show(g, t > aA && t < aB && k > 0.01f);
            if (!g.activeSelf) continue;
            var tip = car.TransformPoint(new Vector3(-0.7f + i * 0.22f, 0.25f + 0.14f * (i % 2), -0.93f));
            float bob = 0.06f * Mathf.Sin(t * 6.5f + i);
            g.transform.rotation = Quaternion.FromToRotation(Vector3.down, Vector3.forward);
            g.transform.localScale = Vector3.one * 0.9f * k;
            g.transform.position = tip - Vector3.forward * (0.05f + bob);
        }
    }

    // ------------------------------------------------------------------ the people: Mixamo characters + Mixamo clips
    // Clips are Generic and sampled by time in edit mode; a second clip can drive the upper body; two-bone IK
    // puts a hand on a point (door, window corner, buckle). Place() anchors the hips joint to a world point.
    class Human
    {
        readonly GameObject holder, inst;
        readonly Transform hips;
        readonly Transform[] upper;                  // spine and arms: what Layer() takes from the second clip
        readonly Transform lArm, lFore, lHand, rArm, rFore, rHand, head;
        public Transform HandL => lHand;
        public Transform HandR => rHand;
        public Transform Head => head;
        public Vector3 HipsPos => hips.position;
        public Vector3 Fwd => holder.transform.forward;
        public Vector3 Up => holder.transform.up;
        public Vector3 Right => holder.transform.right;
        readonly Dictionary<string, Transform> boneCache = new Dictionary<string, Transform>();
        public Vector3 Bone(string n)
        {
            Transform b;
            if (!boneCache.TryGetValue(n, out b)) { b = FindDeep(inst.transform, "mixamorig:" + n); boneCache[n] = b; }
            return b != null ? b.position : hips.position;
        }
        // the right front trouser pocket: on top of the right thigh, a third of the way to the knee
        public Vector3 Pocket => Vector3.Lerp(Bone("RightUpLeg"), Bone("RightLeg"), 0.3f) + Up * 0.07f + Right * 0.04f;

        // a hand around a rim: the wrist at `wrist`, the fingers pointing at `grip`
        public void Grip(bool left, Vector3 wrist, Vector3 grip, float w)
        {
            if (w <= 0.001f) return;
            Reach(left, wrist, w);
            var h = left ? lHand : rHand;
            var aim = Quaternion.FromToRotation(h.up, grip - h.position) * h.rotation;
            h.rotation = Quaternion.Slerp(h.rotation, aim, w);
        }
        Transform neck; Vector3 faceLocal = Vector3.forward;
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        readonly Quaternion[] keep;
        static float chainToHeight = 1.12f;
        Transform[] bones; Vector3[] bind;
        Transform[] eyes; Vector3[] eyePos; Quaternion[] eyeRot;   // eye pose in the HEAD's frame (on this rig they are not its children)        // clips come from another skeleton: keep our own bone lengths

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        public Human(CarScene s, string path, float height)
        {
            holder = new GameObject(path.Replace('/', '_'));
            holder.transform.SetParent(s.stage, false);
            var src = Resources.Load<GameObject>(path);
            if (src == null) { Debug.LogError("character missing: " + path); inst = new GameObject("missing"); inst.transform.SetParent(holder.transform, false); return; }
            inst = Instantiate(src);
            inst.transform.SetParent(holder.transform, false);
            string B(string n) => "mixamorig:" + n;
            hips = FindDeep(inst.transform, B("Hips"));
            head = FindDeep(inst.transform, B("Head"));
            lArm = FindDeep(inst.transform, B("LeftArm")); lFore = FindDeep(inst.transform, B("LeftForeArm")); lHand = FindDeep(inst.transform, B("LeftHand"));
            rArm = FindDeep(inst.transform, B("RightArm")); rFore = FindDeep(inst.transform, B("RightForeArm")); rHand = FindDeep(inst.transform, B("RightHand"));
            var up = new List<Transform>();
            void Collect(Transform t) { up.Add(t); foreach (Transform c in t) Collect(c); }
            Collect(FindDeep(inst.transform, B("Spine")));
            upper = up.ToArray();
            keep = new Quaternion[upper.Length];
            bones = hips.GetComponentsInChildren<Transform>();
            bind = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].localPosition;
            eyes = System.Array.FindAll(bones, x => x.name.EndsWith("Eye"));
            eyePos = System.Array.ConvertAll(eyes, x => head.InverseTransformPoint(x.position));
            eyeRot = System.Array.ConvertAll(eyes, x => Quaternion.Inverse(head.rotation) * x.rotation);
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh == null) continue;
                int headIdx = System.Array.IndexOf(smr.bones, head);
                if (headIdx < 0) continue;
                var bp = smr.sharedMesh.bindposes;
                bool any = false;
                for (int e = 0; e < eyes.Length; e++)
                {
                    int ei = System.Array.IndexOf(smr.bones, eyes[e]);
                    if (ei < 0) continue;
                    var rel = bp[headIdx] * bp[ei].inverse;                 // eye in head space, at bind
                    eyePos[e] = rel.MultiplyPoint3x4(Vector3.zero);
                    eyeRot[e] = rel.rotation;
                    any = true;
                }
                if (any) { UnityEngine.Debug.Log($"EYES from bindposes of {smr.name}: {eyePos[0]:F3} rot {eyeRot[0].eulerAngles:F0}"); break; }
            }
            foreach (var n in new[] { "driving", "texting", "sitting_idle", "sitting_scared", "climbing_to_top", "swimming", "treading_water", "punching", "crawl_forward" })
            {
                var all = Resources.LoadAll<AnimationClip>("Mixamo/" + n);
                foreach (var c in all) if (!c.name.StartsWith("__preview")) { clips[n] = c; break; }
                if (!clips.ContainsKey(n)) Debug.LogError("clip missing: " + n);
            }
            // hair and lashes: cut-out, both sides
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                {
                    if (ms[i] == null) continue;
                    var m = new Material(ms[i]);
                    if (r.name.Contains("Hair") || r.name.Contains("Eyelash"))
                    {
                        m.SetFloat("_AlphaCutoffEnable", 1f); m.SetFloat("_AlphaCutoff", 0.3f); m.SetFloat("_DoubleSidedEnable", 1f);
                        // Remy keeps his opacity in a separate map: our merged copy carries it in the alpha
                        var merged = Resources.Load<Texture2D>(r.name.Contains("Hair") ? "Mixamo/Textures/remy/Remy_Hair_DiffuseA" : "Mixamo/Textures/remy/Remy_Body_DiffuseA");
                        if (path.EndsWith("remy") && merged != null) m.SetTexture("_BaseColorMap", merged);
                    }
                    if (path.EndsWith("remy") && r.name == "Body")
                    {
                        var scalp = Resources.Load<Texture2D>("Mixamo/Textures/remy/Remy_Body_DiffuseH");     // tools/remy_scalp.py
                        if (scalp != null) m.SetTexture("_BaseColorMap", scalp);
                    }
                    m.SetFloat("_Smoothness", r.name.Contains("Body") ? 0.38f : 0.25f);
#if UNITY_EDITOR
                    UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(m);
#endif
                    ms[i] = m;
                }
                r.sharedMaterials = ms;
                if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = true; }
                if (path.EndsWith("remy") && (r.name.Contains("Eyelash") || r.name == "Eyes")) r.enabled = false;   // Body has its own eyeballs; "Eyes" is a loose second pair
            }
            // scale from the real skinned vertices in the bind pose (bone markers differ between characters)
            float lo = float.MaxValue, hi = float.MinValue;
            var baked = new Mesh();
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(baked, true);
                var vs = baked.vertices;
                var mtx = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                for (int i = 0; i < vs.Length; i += 7) { float y = mtx.MultiplyPoint3x4(vs[i]).y; lo = Mathf.Min(lo, y); hi = Mathf.Max(hi, y); }
            }
            DestroyImmediate(baked);
            // a skin may be saved in a non-standing pose (Sophie's is the first swim frame): then the height comes
            // from the bone chain, calibrated on a character that stands upright in its bind pose
            float chain = 0f;
            foreach (var pair in new[] { ("LeftFoot", "LeftLeg"), ("LeftLeg", "LeftUpLeg"), ("Spine", "Hips"), ("Spine1", "Spine"), ("Spine2", "Spine1"), ("Neck", "Spine2"), ("Head", "Neck"), ("HeadTop_End", "Head") })
            {
                var x0 = FindDeep(inst.transform, B(pair.Item1)); var x1 = FindDeep(inst.transform, B(pair.Item2));
                if (x0 != null && x1 != null) chain += Vector3.Distance(x0.position, x1.position);
            }
            bool upright = Quaternion.Angle(hips.localRotation, Quaternion.identity) < 10f;
            if (upright && chain > 0f) chainToHeight = (hi - lo) / chain;
            float h = upright ? Mathf.Max(0.01f, hi - lo) : chain * chainToHeight;
            holder.transform.localScale = Vector3.one * (height / h);
            neck = FindDeep(inst.transform, B("Neck"));
            Sample("sitting_idle", 0f);                      // where the face points when the body faces +z
            faceLocal = head.InverseTransformDirection(holder.transform.forward);
            // the eyeballs are partly weighted to the neck, so they slide out of the sockets whenever the head turns:
            // weight them to the head alone
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.name != "Eyes") continue;
                int hIdx = System.Array.IndexOf(smr.bones, head);
                if (hIdx < 0) continue;
                var mesh = Instantiate(smr.sharedMesh);
                var bw = mesh.boneWeights;
                for (int i = 0; i < bw.Length; i++) bw[i] = new BoneWeight { boneIndex0 = hIdx, weight0 = 1f };
                mesh.boneWeights = bw;
                smr.sharedMesh = mesh;
            }
            // hair cards sit almost on the painted scalp and flicker against it: lift them 4 mm
            float lift = 0.004f / holder.transform.localScale.x;
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.name.Contains("Hair")) continue;
                var mesh = Instantiate(smr.sharedMesh);
                var v = mesh.vertices; var nrm = mesh.normals;
                for (int i = 0; i < v.Length; i++) v[i] += nrm[i] * lift;
                mesh.vertices = v; mesh.RecalculateBounds();
                smr.sharedMesh = mesh;
            }
            if (STYLIZED) Stylize(s, height);
            SetupBake();
            Debug.Log($"HUMAN {path} bind height {h:F3} chain {chain:F3} upright {upright} -> scale {height / h:F3}");
        }

        public void DumpPose(Transform car, string who, float t)
        {
            string P(string n) { var b = FindDeep(inst.transform, "mixamorig:" + n); return b == null ? "-" : car.InverseTransformPoint(b.position).ToString("F2"); }
            UnityEngine.Debug.Log($"POSE {who} t {t:F1} hips {P("Hips")} head {P("HeadTop_End")} knee {P("LeftLeg")} foot {P("LeftFoot")} toe {P("LeftToe_End")} holderScale {holder.transform.lossyScale.x:F3}");
        }

        public void DumpArms(Transform car, Vector3 camPos)
        {
            string L(Transform x) => x == null ? "-" : car.InverseTransformPoint(x.position).ToString("F2");
            UnityEngine.Debug.Log($"ARMS cam {car.InverseTransformPoint(camPos):F2} head {L(head)} | L sh {L(lArm)} el {L(lFore)} wr {L(lHand)} | R sh {L(rArm)} el {L(rFore)} wr {L(rHand)} | eyeL {L(FindDeep(inst.transform, "mixamorig:LeftEye"))}");
        }

        public void DumpEyes(string tag)
        {
            foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (r.name == "Eyes")
                    UnityEngine.Debug.Log($"EYES {tag} renderer root {r.rootBone?.name} bones [{string.Join(",", System.Array.ConvertAll(r.bones, b => b.name))}]");
            for (int i = 0; i < eyes.Length; i++)
                UnityEngine.Debug.Log($"EYES {tag} {eyes[i].name} parent {eyes[i].parent.name} inHead {head.InverseTransformPoint(eyes[i].position):F3} stored {eyePos[i]:F3} headScale {head.lossyScale.x:F3}");
        }

        // the eyes go where the head goes
        void FixEyes()
        {
            for (int i = 0; i < eyes.Length; i++) { eyes[i].position = head.TransformPoint(eyePos[i]); eyes[i].rotation = head.rotation * eyeRot[i]; }
        }

        // turn neck and head toward a point (w blends from the clip's pose)
        public void Look(Vector3 target, float w)
        {
            if (w <= 0.001f || head == null) return;
            foreach (var (bone, share) in new[] { (neck, 0.7f) })
            {
                if (bone == null) continue;
                var cur = head.TransformDirection(faceLocal); var want = target - head.position;
                var delta = Quaternion.FromToRotation(cur, want);
                float ang; Vector3 axis; delta.ToAngleAxis(out ang, out axis);
                if (ang > 180f) ang -= 360f;
                ang = Mathf.Clamp(ang, -40f, 40f) * share * w;
                bone.rotation = Quaternion.AngleAxis(ang, axis) * bone.rotation;
            }
            FixEyes();
        }

        // Frames rendered back to back in edit mode do not re-skin the SkinnedMeshRenderers in time: body, hair and eyes
        // lag the bones by different amounts (the hand missed the pocket, the hair slid off the head). So every frame the
        // pose is baked into plain meshes that are drawn instead.
        readonly List<(SkinnedMeshRenderer smr, Mesh mesh)> baked = new List<(SkinnedMeshRenderer, Mesh)>();
        void SetupBake()
        {
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!smr.enabled || smr.forceRenderingOff) continue;
                var go = new GameObject(smr.name + "_baked");
                go.transform.SetParent(smr.transform, false);
                go.transform.localScale = Vector3.one / smr.transform.lossyScale.x;     // BakeMesh(.., false) gives world-size vertices
                var mesh = new Mesh { name = smr.name + "_baked" }; mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = smr.sharedMaterials;
                mr.shadowCastingMode = smr.shadowCastingMode;
                mr.receiveShadows = smr.receiveShadows;
                mr.allowOcclusionWhenDynamic = false;
                smr.forceRenderingOff = true;          // still skins (BakeMesh needs it enabled), never drawn
                baked.Add((smr, mesh));
            }
        }
        public void Bake()
        {
            if (!holder.activeInHierarchy) return;
            foreach (var (smr, mesh) in baked) { smr.BakeMesh(mesh, false); mesh.RecalculateBounds(); }
        }

        // Lazar 2026-11-07: not a realistic person: one plain figure, a blue suit and a white helmet. Same skeleton and
        // the same acting; hair, eyes and lashes off, every other material the suit (shoes darker), a helmet on the head.
        public const bool STYLIZED = true;
        void Stylize(CarScene s, float height)
        {
            var suit = WellLook.Lit("suit", Hex("#2A55BE"), 0.36f);
            var shoe = WellLook.Lit("suit_shoe", Hex("#1E2A44"), 0.35f);
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                string n = r.name;
                if (n.Contains("Hair") || n.Contains("Eyelash") || n == "Eyes") { r.enabled = false; continue; }
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = (n.Contains("Shoe") || n.Contains("Sneaker")) ? shoe : suit;
                r.sharedMaterials = ms;
            }
            // the helmet: a white shell over the top and back of the head, the face open
            var top = FindDeep(inst.transform, "mixamorig:HeadTop_End");
            float hr = Vector3.Distance(head.position, top.position) * 0.5f;
            var up = (top.position - head.position).normalized;
            var fwd = Vector3.ProjectOnPlane(head.TransformDirection(faceLocal), up).normalized;
            var hm = new GameObject("helmet");
            hm.AddComponent<MeshFilter>().sharedMesh = HemiMesh();
            var shell = WellLook.Lit("helmet", Hex("#F1F2F4"), 0.78f);
            shell.SetFloat("_DoubleSidedEnable", 1f); shell.SetFloat("_CullMode", 0f);
#if UNITY_EDITOR
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(shell);
#endif
            hm.AddComponent<MeshRenderer>().sharedMaterial = shell;
            hm.transform.position = Vector3.Lerp(head.position, top.position, 0.66f) - fwd * hr * 0.18f;
            hm.transform.rotation = Quaternion.LookRotation(fwd, up) * Quaternion.Euler(-22f, 0f, 0f);
            hm.transform.localScale = new Vector3(hr * 1.08f, hr * 1.02f, hr * 1.16f);
            hm.transform.SetParent(head, true);
        }

        static Mesh HemiMesh()
        {
            const int SEG = 40, RING = 18;
            var v = new List<Vector3>(); var tr = new List<int>();
            for (int j = 0; j <= RING; j++)
            {
                float th = Mathf.Deg2Rad * 104f * j / RING;          // from the crown to a little past the rim
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
            var m = new Mesh { name = "helmet" };
            m.SetVertices(v); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        public void Show(bool on) { if (holder.activeSelf != on) holder.SetActive(on); }

        public void Sample(string clip, float time)
        {
            AnimationClip c;
            if (!clips.TryGetValue(clip, out c)) return;
            c.SampleAnimation(inst, Mathf.Repeat(time, c.length));
            for (int i = 0; i < bones.Length; i++) bones[i].localPosition = bind[i];
            FixEyes();
        }

        // body from `baseClip`, upper body blended toward `upperClip` by w
        public void Layer(string baseClip, float tb, string upperClip, float tu, float w)
        {
            Sample(upperClip, tu);
            for (int i = 0; i < upper.Length; i++) keep[i] = upper[i].localRotation;
            Sample(baseClip, tb);
            for (int i = 0; i < upper.Length; i++) upper[i].localRotation = Quaternion.Slerp(upper[i].localRotation, keep[i], w);
            FixEyes();
        }

        // the hips joint at `hipsAt`, the body facing `rot` (Mixamo characters face +z)
        public void Place(Vector3 hipsAt, Quaternion rot)
        {
            holder.transform.rotation = rot;
            holder.transform.position = hipsAt;
            holder.transform.position += hipsAt - hips.position;
        }

        // two-bone IK: the hand on `target`, elbow bending outward and down; w blends from the clip's pose
        public void Reach(bool left, Vector3 target, float w)
        {
            Transform a = left ? lArm : rArm, b = left ? lFore : rFore, c = left ? lHand : rHand;
            var ra = a.rotation; var rb = b.rotation;
            float la = Vector3.Distance(a.position, b.position), lb = Vector3.Distance(b.position, c.position);
            var toT = target - a.position;
            float d = Mathf.Clamp(toT.magnitude, 0.05f, (la + lb) * 0.999f);
            // aim the upper arm at the target, then open the elbow angle to fit the distance
            var side = holder.transform.right * (left ? -1f : 1f);
            var pole = (side * 0.6f + Vector3.down).normalized;
            var n = Vector3.Cross(toT, pole).normalized;
            float cosA = (la * la + d * d - lb * lb) / (2f * la * d);
            float angA = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
            var dirUpper = Quaternion.AngleAxis(angA, n) * toT.normalized;
            a.rotation = Quaternion.FromToRotation(b.position - a.position, dirUpper) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
            a.rotation = Quaternion.Slerp(ra, a.rotation, w);
            b.rotation = Quaternion.Slerp(rb, b.rotation, w);
        }
    }
}
