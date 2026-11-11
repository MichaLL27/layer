// Short "drinking water on a desert island" (HDRP). Pure function of time: Evaluate(t) puts every object in its
// state at second t; beat times come from the voice (docs/scenes/island_words.json).
// The island is sliced in half like a cake along z = 0; the camera looks along +z at the cut face.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class IslandScene : MonoBehaviour, IShortScene
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
    float S(int i) => doc.sentences[i - 1].start;
    float En(int i) => doc.sentences[i - 1].end;

    // ------------------------------------------------------------------ helpers
    static float P(float t, float a, float b) => Mathf.Clamp01((t - a) / Mathf.Max(1e-4f, b - a));
    static float E(float x) => x * x * (3f - 2f * x);
    static float PE(float t, float a, float b) => E(P(t, a, b));
    static float OutCubic(float x) { x = Mathf.Clamp01(x); return 1f - Mathf.Pow(1f - x, 3f); }
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
            case "palm_bark": m = WellLook.Metal(key, Hex("#8A7458"), 0.22f, 0f, "rock_wall_02", 0.55f, 1.0f); break;
            case "palm_leaf":
                m = WellLook.Lit(key, Hex("#5C9A33"), 0.42f);
                m.SetFloat("_DoubleSidedEnable", 1f);
                m.SetFloat("_DoubleSidedNormalMode", 1f);
                m.SetFloat("_CullMode", 0f);
                Keys(m);
                break;
            case "coconut": m = WellLook.Lit(key, Hex("#5A3E22"), 0.45f); break;
            case "driftwood": m = WellLook.Metal(key, Hex("#8E7C63"), 0.2f, 0f, "brown_planks_03", 0.7f, 1f); break;
            case "pot_metal": m = WellLook.Metal(key, Hex("#A8A49B"), 0.36f, 0.25f, "rusty_metal_02", 0.5f, 0.5f); break;
            case "pot_water": m = WellLook.Water(key, Hex("#BFE4F2"), 0.95f, 0.96f, false); break;
            case "hut_wall": m = WellLook.Metal(key, Hex("#B79A68"), 0.18f, 0f, "brown_planks_03", 0.45f, 1f); m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_CullMode", 0f); Keys(m); break;
            case "hut_roof": m = WellLook.Lit(key, Hex("#BFA36C"), 0.2f); m.SetFloat("_DoubleSidedEnable", 1f); m.SetFloat("_CullMode", 0f); Keys(m); break;
            default: m = WellLook.Lit(key, Hex("#B0B0B0"), 0.4f); break;
        }
        matCache[key] = m;
        return m;
    }

    GameObject Model(string file, Vector3 pos, float yaw, float scale)
    {
        var src = Resources.Load<GameObject>("Models/" + file);
        if (src == null) { Debug.LogError("model missing: " + file); return new GameObject(file); }
        var g = Instantiate(src);
        g.name = file;
        g.transform.SetParent(stage, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = Vector3.one * scale;
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

    // a vertical sheet on the cut plane between two curves, one column per entry
    class Strip
    {
        readonly Mesh mesh; readonly Vector3[] v; readonly Vector2[] uv; readonly int n;
        public readonly GameObject go;
        public Strip(IslandScene s, string name, int columns, Material m)
        {
            n = columns;
            v = new Vector3[n * 2]; uv = new Vector2[n * 2];
            var nrm = new Vector3[n * 2]; var tan = new Vector4[n * 2];
            for (int i = 0; i < n * 2; i++) { nrm[i] = Vector3.back; tan[i] = new Vector4(1, 0, 0, 1); }
            var tr = new int[(n - 1) * 6];
            for (int i = 0, q = 0; i < n - 1; i++)
            {
                int top0 = i * 2, bot0 = i * 2 + 1, top1 = i * 2 + 2, bot1 = i * 2 + 3;
                tr[q++] = bot0; tr[q++] = top0; tr[q++] = bot1;
                tr[q++] = top0; tr[q++] = top1; tr[q++] = bot1;
            }
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = v; mesh.normals = nrm; mesh.tangents = tan; mesh.uv = uv; mesh.triangles = tr;
            go = s.MeshObj(name, mesh, m);
        }
        public void Set(int i, float x, float top, float bot, float z, float u, float v0 = 0f, float v1 = 1f)
        {
            v[i * 2] = new Vector3(x, top, z); v[i * 2 + 1] = new Vector3(x, bot, z);
            uv[i * 2] = new Vector2(u, v0); uv[i * 2 + 1] = new Vector2(u, v1);
        }
        public void Commit()
        {
            mesh.vertices = v; mesh.uv = uv;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 8000f);
        }
    }

    // a ring on the ground or the sea, from the cut (theta = 0) around the back to the cut (theta = pi)
    class Ring
    {
        readonly Mesh mesh; readonly Vector3[] v; readonly Vector2[] uv;
        public readonly int NT, NR;
        public readonly GameObject go;
        public Ring(IslandScene s, string name, int nt, int nr, Material m)
        {
            NT = nt; NR = nr;
            int c = (NT + 1) * (NR + 1);
            v = new Vector3[c]; uv = new Vector2[c];
            var tr = new int[NT * NR * 6];
            for (int j = 0, q = 0; j < NR; j++)
                for (int i = 0; i < NT; i++)
                {
                    int a = j * (NT + 1) + i, cc = a + 1, b = a + NT + 1, d = b + 1;
                    tr[q++] = a; tr[q++] = cc; tr[q++] = b; tr[q++] = cc; tr[q++] = d; tr[q++] = b;
                }
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = tr;
            go = s.MeshObj(name, mesh, m);
        }
        public void Set(int j, int i, Vector3 p, Vector2 t) { int k = j * (NT + 1) + i; v[k] = p; uv[k] = t; }
        public void Commit()
        {
            mesh.vertices = v; mesh.uv = uv;
            mesh.RecalculateNormals();
            var nn = mesh.normals; var tan = new Vector4[nn.Length];
            for (int i = 0; i < nn.Length; i++) tan[i] = new Vector4(1, 0, 0, 1);
            mesh.tangents = tan;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 9000f);
        }
    }

    // ------------------------------------------------------------------ the island's shape
    // radius -> height above sea level along the cut; the shore is at r = 24
    static readonly float[] KR = { 0f, 3f, 6f, 9f, 12f, 15f, 18f, 21f, 24f, 27f, 34f, 60f, 120f, 400f, 4000f };
    static readonly float[] KH = { 1.75f, 1.7f, 1.55f, 1.35f, 2.45f, 3.6f, 2.1f, 0.95f, 0f, -0.9f, -4.5f, -20f, -46f, -60f, -60f };
    const float SHORE = 24f;

    static float Spline(float r)
    {
        int n = KR.Length;
        if (r >= KR[n - 1]) return KH[n - 1];
        int i = 0;
        while (r > KR[i + 1]) i++;
        float h = KR[i + 1] - KR[i], u = (r - KR[i]) / h;
        float m0 = i == 0 ? 0f : (KH[i + 1] - KH[i - 1]) / (KR[i + 1] - KR[i - 1]);
        float m1 = i + 2 >= n ? 0f : (KH[i + 2] - KH[i]) / (KR[i + 2] - KR[i]);
        float u2 = u * u, u3 = u2 * u;
        return (2f * u3 - 3f * u2 + 1f) * KH[i] + (u3 - 2f * u2 + u) * h * m0 + (-2f * u3 + 3f * u2) * KH[i + 1] + (u3 - u2) * h * m1;
    }

    // the coast wanders away from the cut, and is exactly round on it (theta 0 and pi)
    static float Amp(float th) => 1f + Mathf.Sin(th) * (0.08f * Mathf.Sin(2f * th + 0.8f) + 0.06f * Mathf.Sin(3f * th + 2.1f) + 0.04f * Mathf.Sin(7f * th + 0.4f));

    static float BaseH(float x, float z)
    {
        float r = Mathf.Sqrt(x * x + z * z);
        float th = Mathf.Atan2(Mathf.Max(z, 0f), x);
        return Spline(r / Amp(th));
    }

    class Pit { public float x, R, baseY, floorY, depth = 0f, wl = -999f; }
    const float PIT_R = 1.15f;
    readonly Pit beachPit = new Pit { x = 20.8f, R = PIT_R, floorY = -0.05f };
    readonly Pit inlandPit = new Pit { x = 9f, R = PIT_R, floorY = 0.22f };
    readonly Pit edgePit = new Pit { x = 18.7f, R = 0.95f, floorY = -2.9f };

    float H(float x, float z)
    {
        float h = BaseH(x, z);
        foreach (var p in pits)
        {
            if (p.depth <= 0.001f) continue;
            float dx = x - p.x, d = Mathf.Sqrt(dx * dx + z * z);
            if (d < p.R) { float k = 1f - d * d / (p.R * p.R); h -= k * (p.baseY - p.floorY) * p.depth; }
            float rim = (d - p.R - 0.35f) / 0.42f;
            h += 0.2f * p.depth * Mathf.Exp(-rim * rim);
        }
        return h;
    }

    Pit[] pits;

    static float Rock(float x) => -34f + 3f * Mathf.Sin(x * 0.13f) + 2f * Mathf.Sin(x * 0.31f + 1f);

    // the top of the fresh layer above sea level; w0 is the height at the middle of the island
    const float LENS_R = 19.5f;
    static float Wt(float x, float w0) => w0 * Mathf.Max(0f, 1f - x * x / (LENS_R * LENS_R));

    float coneK;       // the salt rising under the well, 0..1
    float Cone(float x) { float d = (x - inlandPit.x) / 2.2f; return coneK * 18.2f * Mathf.Exp(-d * d); }

    // ------------------------------------------------------------------ objects
    Camera cam;
    Mesh groundMesh; float[] gx, gz; Vector3[] gV, gN; Vector4[] gT; string gKey = "";
    Strip faceSand, faceRock, seaFace, saltBand, freshBand, pitWaterA, pitWaterB, pitWaterC;
    GameObject pitTopC; Material pitCMat, pitCTop;
    float[] fx, sx, bx, px;
    Mesh seaMesh; Vector3[] seaV; float[] seaR, seaRs, seaX, seaZ; const int SEA_NT = 720, SEA_NR = 200;
    Material seaMat;
    Ring wet, swash; Ring[] breakers; Material[] breakerMats; Material foamMat;
    GameObject pitTopA, pitTopB; Material pitAMat, pitBMat, pitATop, pitBTop;
    GameObject[] sand, rain, sinks, arrowsSalt, arrowsUp;
    Human human; Human[] crowd;
    GameObject[] prints; float[] printT; int printsN; Vector3[] printPos; GameObject tin, rope;
    GameObject barTop, barBot, seaLine, camp;
    GameObject[] logs, flames, steam, huts, arrowsCone;
    Vector3[] logFoot; Vector3[] logTop;
    Vector3[] hutPos;
    WellScene.Label lab1Ft, lab40Ft, labSeaLvl, labThin, labTooDeep, labStop, labTooMuch, labWeeks, labYears;
    float hookX, hookZ = 5.85f;
    WellScene.Label labHook, labDays, labSalt, labFresh, labFreshL, labSaltL, labYou;

    WellScene.Label Lab(string text, string style) { var l = new WellScene.Label { text = text, style = style }; labels.Add(l); return l; }

    // ------------------------------------------------------------------ build
    public void Build(string wordsJson)
    {
        doc = JsonUtility.FromJson<WellScene.Doc>(File.ReadAllText(wordsJson));
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);
        pits = new[] { beachPit, inlandPit, edgePit };
        foreach (var p in pits) p.baseY = BaseH(p.x, 0f);
        WellLook.FoliageTint = new Color(0.62f, 0.86f, 0.46f);
        WellLook.GradeContrast = 8f; WellLook.GradeSaturation = 16f; WellLook.GradeWarm = 7f;
        WellLook.Sun(stage);
        var vol = WellLook.Environment(stage);
        Fog fog;
        if (vol.sharedProfile.TryGet(out fog)) { fog.meanFreePath.value = 1500f; fog.maximumHeight.value = 70f; }
        HDRISky sky;
        if (vol.sharedProfile.TryGet(out sky)) sky.multiplier.value = 17000f;
        cam = WellLook.Camera(stage, 34f);

        hookX = 30f;
        while (BaseH(hookX, hookZ) < 0.16f && hookX > 10f) hookX -= 0.02f;

        BuildGround();
        BuildSea();
        BuildSurf();
        BuildBands();
        BuildPlants();
        BuildFx();
        BuildLate();
        human = new Human(stage, "Mixamo/remy", 1.78f);
        crowd = new[] { new Human(stage, "Mixamo/remy", 1.78f), new Human(stage, "Mixamo/remy", 1.72f), new Human(stage, "Mixamo/remy", 1.5f) };
        labHook = Lab("SALT WATER", "o");
        labDays = Lab("3 DAYS", "y");
        labSalt = Lab("SALT", "o");
        labFresh = Lab("FRESH", "b");
        labFreshL = Lab("FRESH WATER", "b");
        labSaltL = Lab("SALT WATER", "o");
        labYou = Lab("YOU", "w");
    }

    static float[] Grid(float a, float b, float step, params float[] fineCentres)
    {
        const float FINE = 0.05f, HALF = 1.9f;
        var l = new List<float>();
        float x = a;
        while (x < b)
        {
            l.Add(x);
            bool fine = false;
            foreach (var c in fineCentres) if (x >= c - HALF && x <= c + HALF) fine = true;
            x += fine ? FINE : step;
        }
        l.Add(b);
        return l.ToArray();
    }

    void BuildGround()
    {
        var faceTex = SandFaceTex();
        var sandTop = WellLook.Textured("sand_01", new Color(0.98f, 0.96f, 0.92f), 2.4f, 0.14f);
        sandTop.SetTexture("_BaseColorMap", faceTex);
        var sandFace = WellLook.Lit("sandFace", Color.white, 0.08f);
        sandFace.SetTexture("_BaseColorMap", faceTex);
        Keys(sandFace);
        var rock = WellLook.Lit("rockFace", Hex("#857B6C"), 0.12f);
        rock.SetTexture("_BaseColorMap", faceTex);
        Keys(rock);

        gx = Grid(-37f, 37f, 0.32f, beachPit.x, inlandPit.x, edgePit.x);
        gz = Grid(0f, 37f, 0.32f, 0f);
        int nx = gx.Length, nz = gz.Length;
        gV = new Vector3[nx * nz]; gN = new Vector3[nx * nz]; gT = new Vector4[nx * nz];
        var tr = new int[(nx - 1) * (nz - 1) * 6];
        for (int i = 0, q = 0; i < nx - 1; i++)
            for (int j = 0; j < nz - 1; j++)
            {
                int a = i * nz + j, b = a + 1, c = a + nz, d = c + 1;
                tr[q++] = a; tr[q++] = b; tr[q++] = c; tr[q++] = b; tr[q++] = d; tr[q++] = c;
            }
        groundMesh = new Mesh { name = "Ground", indexFormat = IndexFormat.UInt32 };
        groundMesh.MarkDynamic();
        groundMesh.vertices = gV; groundMesh.triangles = tr;
        MeshObj("Ground", groundMesh, sandTop);

        var l = new List<float>();
        foreach (var v in new[] { -1000f, -600f, -300f, -150f, -100f, -70f, -50f, -38f }) l.Add(v);
        l.AddRange(Grid(-37.5f, 37.5f, 0.1f, beachPit.x, inlandPit.x, edgePit.x));
        foreach (var v in new[] { 38f, 50f, 70f, 100f, 150f, 300f, 600f, 1000f }) l.Add(v);
        fx = l.ToArray();
        faceSand = new Strip(this, "FaceSand", fx.Length, sandFace);
        faceRock = new Strip(this, "FaceRock", fx.Length, rock);
    }

    const float FACE_TILE = 7f;

    // a bare footprint seen from above: heel, arch, ball, five toes (toes toward +v)
    static Texture2D FootTex()
    {
        const int W = 128, H_ = 256;
        var t = new Texture2D(W, H_, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        float El(float u, float v, float cx, float cy, float rx, float ry)
        {
            float dx = (u - cx) / rx, dy = (v - cy) / ry;
            return Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) / 0.16f);
        }
        var toes = new[] { (0.27f, 0.895f, 0.095f, 0.062f), (0.46f, 0.925f, 0.066f, 0.048f), (0.6f, 0.92f, 0.06f, 0.044f), (0.72f, 0.895f, 0.054f, 0.04f), (0.83f, 0.855f, 0.05f, 0.036f) };
        var c = new Color[W * H_];
        for (int y = 0; y < H_; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H_;
                float a = Mathf.Max(El(u, v, 0.45f, 0.17f, 0.25f, 0.15f), El(u, v, 0.5f, 0.62f, 0.37f, 0.16f));
                a = Mathf.Max(a, El(u, v, 0.34f, 0.4f, 0.17f, 0.24f));         // the outer edge of the foot joins heel and ball
                foreach (var q in toes) a = Mathf.Max(a, El(u, v, q.Item1, q.Item2, q.Item3, q.Item4));
                c[y * W + x] = new Color(1f, 1f, 1f, a);
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    // sand seen in section: pale, calm, fine grain, faint wavy beds
    static Texture2D SandFaceTex()
    {
        const int N = 1024;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var c = new Color[N * N];
        Color pale = Hex("#EFDDB6"), deep = Hex("#CFAF7E");
        var rnd = new System.Random(7);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float wv = v + 0.012f * Mathf.Sin(2f * Mathf.PI * (u * 2f + 0.3f)) + 0.006f * Mathf.Sin(2f * Mathf.PI * (u * 5f + v * 2f));
                float beds = 0.5f + 0.5f * (0.5f * Mathf.Sin(2f * Mathf.PI * wv * 5f) + 0.3f * Mathf.Sin(2f * Mathf.PI * (wv * 13f + 0.2f)) + 0.2f * Mathf.Sin(2f * Mathf.PI * (wv * 31f + 0.6f)));
                float grain = (float)rnd.NextDouble();
                var col = Color.Lerp(pale, deep, 0.55f * beds);
                col *= 0.9f + 0.2f * grain;
                col.a = 1f;
                c[y * N + x] = col;
            }
        t.SetPixels(c); t.Apply(true);
        return t;
    }

    void UpdateGround()
    {
        string key = $"{beachPit.depth:F3}_{inlandPit.depth:F3}_{edgePit.depth:F3}";
        if (key == gKey) return;
        gKey = key;
        int nx = gx.Length, nz = gz.Length;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                gV[i * nz + j] = new Vector3(gx[i], H(gx[i], gz[j]), gz[j]);
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(nx - 1, i + 1), j0 = Mathf.Max(0, j - 1), j1 = Mathf.Min(nz - 1, j + 1);
                float dx = (gV[i1 * nz + j].y - gV[i0 * nz + j].y) / (gx[i1] - gx[i0]);
                float dz = (gV[i * nz + j1].y - gV[i * nz + j0].y) / (gz[j1] - gz[j0]);
                var n = new Vector3(-dx, 1f, -dz).normalized;
                gN[i * nz + j] = n;
                gT[i * nz + j] = new Vector4(n.y, -n.x, 0f, 1f);
            }
        groundMesh.vertices = gV; groundMesh.normals = gN; groundMesh.tangents = gT;
        groundMesh.bounds = new Bounds(new Vector3(0f, 0f, 18f), new Vector3(80f, 80f, 80f));

        const float BOT = -130f;
        for (int i = 0; i < fx.Length; i++)
        {
            float x = fx[i], top = H(x, 0f), rk = Rock(x);
            float sb = Mathf.Min(top, rk);
            faceSand.Set(i, x, top, sb, 0f, x / FACE_TILE, top / FACE_TILE, sb / FACE_TILE);
            faceRock.Set(i, x, sb, BOT, 0f, x / 22f, sb / 22f, BOT / 22f);
        }
        faceSand.Commit(); faceRock.Commit();
    }

    static float TileNoise(float u, float v, int seed, int octaves, int minF, int maxF)
    {
        float s = 0f, a = 0f;
        for (int i = 0; i < octaves; i++)
        {
            int fx = minF + Mathf.FloorToInt(Hash(i, seed) * (maxF - minF));
            int fy = minF + Mathf.FloorToInt(Hash(i, seed + 7) * (maxF - minF));
            float amp = 1f / (1f + Mathf.Sqrt(fx * fx + fy * fy) * 0.2f);
            s += amp * Mathf.Sin(2f * Mathf.PI * (fx * u + fy * v + Hash(i, seed + 13)));
            a += amp;
        }
        return s / a;
    }

    Texture2D Ramp(int h, Func<float, Color> f)
    {
        var t = new Texture2D(4, h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < h; y++) { var c = f(y / (h - 1f)); for (int x = 0; x < 4; x++) t.SetPixel(x, y, c); }
        t.Apply();
        return t;
    }

    void BuildSea()
    {
        // colour by distance from the shore: clear turquoise over the sand, then deep blue
        var ramp = Ramp(512, v =>
        {
            float r = v * v * 340f;                 // metres past r = 18
            Color a = Hex("#6BE3D2"), b = Hex("#17A6B8"), c = Hex("#0B6C93"), d = Hex("#08406E");
            Color col = r < 12f ? Color.Lerp(a, b, r / 12f) : r < 50f ? Color.Lerp(b, c, (r - 12f) / 38f) : Color.Lerp(c, d, Mathf.Clamp01((r - 50f) / 200f));
            col.a = Mathf.Lerp(0.42f, 1f, Mathf.Clamp01((r - 4f) / 15f));
            return col;
        });
        seaMat = WellLook.Water("sea", Color.white, 1f, 0.82f, false);
        seaMat.SetTexture("_BaseColorMap", ramp);

        // ripples: a tiling detail map (R grey, G normal y, B smoothness, A normal x)
        const int N = 512;
        var det = new Texture2D(N, N, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        float Hh(float u, float v) => TileNoise(u, v, 3, 34, 3, 42);
        var px_ = new Color[N * N];
        float eps = 1f / N;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x * eps, v = y * eps;
                float dx = (Hh(u + eps, v) - Hh(u - eps, v)) * 22f, dy = (Hh(u, v + eps) - Hh(u, v - eps)) * 22f;
                px_[y * N + x] = new Color(0.5f, Mathf.Clamp01(0.5f + dy), 0.5f, Mathf.Clamp01(0.5f + dx));
            }
        det.SetPixels(px_); det.Apply(true);
        seaMat.SetTexture("_DetailMap", det);
        seaMat.SetFloat("_UVDetail", 1f);
        seaMat.SetVector("_UVDetailsMappingMask", new Vector4(0f, 1f, 0f, 0f));
        seaMat.SetTextureScale("_DetailMap", new Vector2(1f / 9f, 1f / 9f));
        seaMat.SetFloat("_DetailAlbedoScale", 0f);
        seaMat.SetFloat("_DetailNormalScale", 0.75f);
        seaMat.SetFloat("_DetailSmoothnessScale", 0f);
        Keys(seaMat);

        int c = (SEA_NT + 1) * (SEA_NR + 1);
        seaV = new Vector3[c]; seaR = new float[c]; seaRs = new float[c]; seaX = new float[c]; seaZ = new float[c];
        var uv = new Vector2[c]; var uv2 = new Vector2[c];
        var tr = new int[SEA_NT * SEA_NR * 6];
        for (int j = 0, q = 0; j < SEA_NR; j++)
            for (int i = 0; i < SEA_NT; i++)
            {
                int a = j * (SEA_NT + 1) + i, cc = a + 1, b = a + SEA_NT + 1, d = b + 1;
                tr[q++] = a; tr[q++] = cc; tr[q++] = b; tr[q++] = cc; tr[q++] = d; tr[q++] = b;
            }
        for (int j = 0; j <= SEA_NR; j++)
        {
            float k = j / (float)SEA_NR;
            float r = k < 0.6f ? 18f + 42f * Mathf.Pow(k / 0.6f, 1.25f) : 60f + 3860f * Mathf.Pow((k - 0.6f) / 0.4f, 3f);
            for (int i = 0; i <= SEA_NT; i++)
            {
                float th = Mathf.PI * i / SEA_NT;
                float x = r * Mathf.Cos(th), z = r * Mathf.Sin(th);
                int q = j * (SEA_NT + 1) + i;
                seaX[q] = x; seaZ[q] = z; seaR[q] = r; seaRs[q] = SHORE * Amp(th);
                uv[q] = new Vector2(0.5f, Mathf.Sqrt(Mathf.Clamp01(Mathf.Max(0f, r - (seaRs[q] - 6f)) / 340f)));
                uv2[q] = new Vector2(x, z);
                seaV[q] = new Vector3(x, 0f, z);
            }
        }
        seaMesh = new Mesh { name = "Sea", indexFormat = IndexFormat.UInt32 };
        seaMesh.MarkDynamic();
        seaMesh.vertices = seaV; seaMesh.uv = uv; seaMesh.uv2 = uv2; seaMesh.triangles = tr;
        MeshObj("Sea", seaMesh, seaMat);

        // the sea's cut face: bright under the surface, deep navy by the sea bed
        var faceRamp = Ramp(256, v => Color.Lerp(Hex("#27B6C6"), Hex("#0C4E80"), Mathf.Pow(v, 0.6f)));
        var fm = WellLook.Lit("seaFace", Color.white, 0.55f);
        fm.SetTexture("_BaseColorMap", faceRamp);
        Keys(fm);
        var l = new List<float>();
        for (float x = SHORE - 1f; x < 60f; x += 0.25f) l.Add(x);
        foreach (var v in new[] { 60f, 70f, 85f, 100f, 130f, 170f, 240f, 340f, 500f, 700f, 1000f }) l.Add(v);
        sx = l.ToArray();
        seaFace = new Strip(this, "SeaFaceR", sx.Length, fm);
        seaFaceL = new Strip(this, "SeaFaceL", sx.Length, fm);
    }
    Strip seaFaceL;

    static float W(float x, float z, float r, float rs, float t)
    {
        float near = E(Mathf.Clamp01((r - rs - 0.5f) / 7f));
        float far = 1f - E(Mathf.Clamp01((r - 55f) / 95f));      // the rings are too far apart out there: sampled waves turn to noise
        float h = 0.07f * Mathf.Sin(0.78f * r + 1.9f * t)
                + 0.05f * Mathf.Sin(0.31f * x + 0.52f * z - 1.05f * t)
                + 0.035f * Mathf.Sin(0.97f * x - 0.63f * z + 1.8f * t + 1f)
                + 0.025f * Mathf.Sin(-1.4f * x + 1.9f * z + 2.6f * t + 2f)
                + 0.012f * Mathf.Sin(2.9f * x + 1.3f * z - 3.4f * t)
                + 0.009f * Mathf.Sin(-2.2f * x + 3.1f * z + 3.9f * t + 0.7f);
        return h * near * far;
    }

    void UpdateSea(float t)
    {
        for (int i = 0; i < seaV.Length; i++)
            seaV[i].y = W(seaX[i], seaZ[i], seaR[i], seaRs[i], t);
        seaMesh.vertices = seaV;
        seaMesh.RecalculateNormals();
        seaMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 9000f);
        seaMat.SetTextureOffset("_DetailMap", new Vector2(t * 0.012f, t * -0.009f));

        const float BOT = -130f;
        for (int i = 0; i < sx.Length; i++)
        {
            float x = sx[i];
            float top = Mathf.Max(W(x, 0f, x, SHORE, t), H(x, 0f) + 0.001f);
            float bed = Mathf.Min(H(x, 0f), top - 0.001f);
            float depth = Mathf.Clamp01(-bed / 60f);
            seaFace.Set(i, x, top, Mathf.Max(bed, BOT), 0f, 0.5f, 0f, depth);
            top = Mathf.Max(W(-x, 0f, x, SHORE, t), H(-x, 0f) + 0.001f);
            bed = Mathf.Min(H(-x, 0f), top - 0.001f);
            depth = Mathf.Clamp01(-bed / 60f);
            seaFaceL.Set(i, -x, top, Mathf.Max(bed, BOT), 0f, 0.5f, 0f, depth);
        }
        seaFace.Commit(); seaFaceL.Commit();
    }

    void BuildSurf()
    {
        const int N = 512;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
        var cols = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float n = 0.5f + 0.5f * TileNoise(u, v, 11, 30, 2, 26);
                float a = Mathf.Clamp01((n - 0.46f) / 0.16f);
                cols[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(cols); tex.Apply(true);

        // the swash: a thin sheet of water running up the sand, a lace of foam at its leading edge
        var swashTex = new Texture2D(N, N, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float n = 0.5f + 0.5f * TileNoise(u, v, 23, 30, 2, 26);
                float edge = Mathf.Clamp01(1f - v / 0.13f);
                float foam = Mathf.Clamp01((n - 0.66f + edge * 0.14f) / 0.08f) * Mathf.Clamp01(edge * 4f + (v < 0.45f ? 0.12f : 0f));
                var film = new Color(0.72f, 0.92f, 0.9f, 0.4f * E(Mathf.Clamp01(v / 0.3f)) * (1f - v * 0.3f));
                cols[y * N + x] = Color.Lerp(film, new Color(1f, 1f, 1f, 1f), foam);
            }
        swashTex.SetPixels(cols); swashTex.Apply(true);
        var swashMat = WellLook.Water("swash", Color.white, 1f, 0.9f, false);
        swashMat.SetTexture("_BaseColorMap", swashTex);
        Keys(swashMat);
        swash = new Ring(this, "Swash", 720, 16, swashMat);

        var wetTex = Ramp(64, v => new Color(0.32f, 0.26f, 0.18f, 0.62f * E(Mathf.Clamp01(v / 0.5f))));
        var wetMat = WellLook.Water("wet", Color.white, 1f, 0.72f, false);
        wetMat.SetTexture("_BaseColorMap", wetTex);
        Keys(wetMat);
        wet = new Ring(this, "Wet", 720, 10, wetMat);

        foamMat = WellLook.Water("breaker", Color.white, 1f, 0.6f, false);
        foamMat.SetTexture("_BaseColorMap", tex);
        Keys(foamMat);
        breakers = new Ring[3]; breakerMats = new Material[3];
        for (int b = 0; b < 3; b++)
        {
            breakerMats[b] = new Material(foamMat) { name = "breaker" + b };
            breakers[b] = new Ring(this, "Breaker" + b, 720, 4, breakerMats[b]);
        }
    }

    static float Swash(float th, float t) => 0.55f * Mathf.Sin(1.15f * t + 6f * th) + 0.32f * Mathf.Sin(2.1f * t + 13f * th + 1f);

    void UpdateSurf(float t)
    {
        for (int i = 0; i <= swash.NT; i++)
        {
            float th = Mathf.PI * i / swash.NT, ca = Mathf.Cos(th), sa = Mathf.Sin(th);
            float rs = SHORE * Amp(th), r0 = rs - 1.05f + Swash(th, t);
            for (int j = 0; j <= swash.NR; j++)
            {
                float v = j / (float)swash.NR, r = r0 + v * 3.2f;
                float x = r * ca, z = r * sa;
                float y = Mathf.Max(H(x, z), W(x, z, r, rs, t)) + 0.018f;
                swash.Set(j, i, new Vector3(x, y, z), new Vector2(th * rs / 3.2f + t * 0.02f, v));
            }
        }
        swash.Commit();
        for (int i = 0; i <= wet.NT; i++)
        {
            float th = Mathf.PI * i / wet.NT, ca = Mathf.Cos(th), sa = Mathf.Sin(th);
            float rs = SHORE * Amp(th), inner = rs - 1.55f;
            for (int j = 0; j <= wet.NR; j++)
            {
                float v = j / (float)wet.NR, r = inner + v * 2.1f;
                float x = r * ca, z = r * sa;
                wet.Set(j, i, new Vector3(x, H(x, z) + 0.008f, z), new Vector2(0.5f, v));
            }
        }
        wet.Commit();
        for (int b = 0; b < breakers.Length; b++)
        {
            float life = Frac(t / 6f + b / 3f);
            float off = Mathf.Lerp(11f, 2.2f, life);
            float a = E(Mathf.Clamp01(life * 4f)) * E(Mathf.Clamp01((1f - life) * 3f));
            var ring = breakers[b];
            for (int i = 0; i <= ring.NT; i++)
            {
                float th = Mathf.PI * i / ring.NT, ca = Mathf.Cos(th), sa = Mathf.Sin(th);
                float rs = SHORE * Amp(th);
                float w = 0.9f + 0.55f * Mathf.Sin(th * 9f + b);
                for (int j = 0; j <= ring.NR; j++)
                {
                    float v = j / (float)ring.NR, r = rs + off + w * v;
                    float x = r * ca, z = r * sa;
                    float y = W(x, z, r, rs, t) + 0.03f;
                    ring.Set(j, i, new Vector3(x, y, z), new Vector2(th * rs / 9f, v * 0.2f + b * 0.3f));
                }
            }
            ring.Commit();
            breakerMats[b].SetColor("_BaseColor", new Color(1f, 1f, 1f, a * 0.85f));
        }
    }

    // ------------------------------------------------------------------ the two waters on the cut face
    void BuildBands()
    {
        var saltTex = new Texture2D(64, 256, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < 256; y++)
            for (int x = 0; x < 64; x++)
            {
                float u = x / 63f, v = y / 255f;
                var c = Color.Lerp(Hex("#178FA0"), Hex("#0D3F6E"), Mathf.Pow(v, 0.6f));
                c.a = 0.95f * E(u) * E(Mathf.Clamp01(v / 0.06f));
                saltTex.SetPixel(x, y, c);
            }
        saltTex.Apply();
        var saltMat = WellLook.Water("saltBand", Color.white, 1f, 0.3f, false);
        saltMat.SetTexture("_BaseColorMap", saltTex);
        Keys(saltMat);

        var freshTex = Ramp(256, v =>
        {
            var c = Color.Lerp(Hex("#45C6FF"), Hex("#1F86F0"), v);
            c.a = 0.86f * E(Mathf.Clamp01(v / 0.015f)) * (1f - 0.4f * E(Mathf.Clamp01((v - 0.9f) / 0.1f)));
            return c;
        });
        var freshMat = WellLook.Water("freshBand", Color.white, 1f, 0.3f, false);
        freshMat.SetTexture("_BaseColorMap", freshTex);
        WellLook.Emissive(freshMat, new Color(0.05f, 0.3f, 0.75f), 900f);
        Keys(freshMat);

        var l = new List<float>();
        l.AddRange(Grid(-37f, 37f, 0.25f));
        bx = l.ToArray();
        saltBand = new Strip(this, "SaltBand", bx.Length, saltMat);
        saltBand.go.transform.localPosition = new Vector3(0, 0, -0.01f);
        freshBand = new Strip(this, "FreshBand", bx.Length, freshMat);
        freshBand.go.transform.localPosition = new Vector3(0, 0, -0.014f);

        l.Clear();
        for (float x = -1.5f; x <= 1.5001f; x += 0.03f) l.Add(x);
        px = l.ToArray();
        pitAMat = WellLook.Water("pitA", Hex("#5B8F86"), 0.92f, 0.9f, false);
        pitBMat = WellLook.Water("pitB", Hex("#2BA6F2"), 0.9f, 0.9f, false);
        WellLook.Emissive(pitBMat, Hex("#2BA6F2"), 500f);
        pitATop = WellLook.Water("pitAt", Hex("#7BAEA3"), 0.92f, 0.95f, false);
        pitBTop = WellLook.Water("pitBt", Hex("#6FCBFF"), 0.92f, 0.95f, false);
        pitWaterA = new Strip(this, "PitWaterA", px.Length, pitAMat);
        pitWaterB = new Strip(this, "PitWaterB", px.Length, pitBMat);
        pitCMat = WellLook.Water("pitC", Hex("#2BA6F2"), 0.9f, 0.9f, false);
        WellLook.Emissive(pitCMat, Hex("#2BA6F2"), 500f);
        pitCTop = WellLook.Water("pitCt", Hex("#6FCBFF"), 0.92f, 0.95f, false);
        pitWaterC = new Strip(this, "PitWaterC", px.Length, pitCMat);
        pitWaterC.go.transform.localPosition = new Vector3(0, 0, -0.012f);
        pitTopC = Prim(PrimitiveType.Cube, "pitTopC", Vector3.zero, Vector3.one, pitCTop);
        pitWaterA.go.transform.localPosition = new Vector3(0, 0, -0.012f);
        pitWaterB.go.transform.localPosition = new Vector3(0, 0, -0.012f);
        pitTopA = Prim(PrimitiveType.Cube, "pitTopA", Vector3.zero, Vector3.one, pitATop);
        pitTopB = Prim(PrimitiveType.Cube, "pitTopB", Vector3.zero, Vector3.one, pitBTop);
    }

    void UpdateBands(float t)
    {
        float s5 = S(4);
        float rainT = Tw(7, "rain");
        float saltStart = Tw(4, "sea");

        // the salt creeps in from the shore, and later runs under the whole island
        float front = Mathf.Lerp(SHORE + 2f, 18.6f, PE(t, saltStart, saltStart + 1.8f));
        front = Mathf.Lerp(front, -40f, PE(t, S(7), S(7) + 1.8f));
        float w0 = 0.62f * OutCubic(P(t, rainT + 0.3f, Tw(7, "floats", 0, true)));
        float saltOn = PE(t, saltStart, saltStart + 0.3f);

        Show(saltBand.go, saltOn > 0.002f);
        if (saltOn > 0.002f)
        {
            for (int i = 0; i < bx.Length; i++)
            {
                float x = bx[i];
                float ground = H(x, 0f);
                float wedge = x < front ? -1.1f * Mathf.Pow(front - x, 1.5f) : 0f;      // inland the salt dives
                float iface = Mathf.Abs(x) < SHORE ? -40f * Wt(x, w0) + Cone(x) : 0f;
                float top = Mathf.Min(ground, Mathf.Min(wedge, iface + (w0 > 0.01f ? 2.2f : 0f)));
                float bot = Mathf.Min(top, Rock(x));
                float u = saltOn;
                float v0 = 0.06f * Mathf.Clamp01((iface - top) / 0.5f);
                saltBand.Set(i, x, top, bot, 0f, u, v0, 1f);
            }
            saltBand.Commit();
        }
        Show(freshBand.go, w0 > 0.002f);
        if (w0 > 0.002f)
        {
            for (int i = 0; i < bx.Length; i++)
            {
                float x = bx[i];
                float wt = Wt(x, w0), ground = H(x, 0f);
                float top = Mathf.Min(ground, wt), bot = Mathf.Min(top, -40f * wt + Cone(x));
                freshBand.Set(i, x, top, bot, 0f, 0.5f, 0f, 1f);
            }
            freshBand.Commit();
        }
        PitWater(beachPit, pitWaterA, pitTopA);
        PitWater(inlandPit, pitWaterB, pitTopB);
        PitWater(edgePit, pitWaterC, pitTopC);
    }

    void PitWater(Pit p, Strip s, GameObject top)
    {
        bool on = p.wl > -100f;
        Show(s.go, on); Show(top, on);
        if (!on) return;
        for (int i = 0; i < px.Length; i++)
        {
            float x = p.x + px[i];
            float g = H(x, 0f);
            float y = p.wl;
            float b = Mathf.Min(g, y);
            s.Set(i, x, y, b, 0f, 0f);
        }
        s.Commit();
        top.transform.localPosition = new Vector3(p.x, p.wl - 0.01f, 0.97f);
        top.transform.localScale = new Vector3(3f, 0.02f, 1.7f);
    }

    // ------------------------------------------------------------------ plants
    void BuildPlants()
    {
        Vector3 G(float x, float z) => new Vector3(x, BaseH(x, z) - 0.05f, z);
        var palms = new (string f, float x, float z, float yaw, float s)[]
        {
            ("palm_a", -3f, 5.2f, 20f, 1.0f), ("palm_b", 2.4f, 7.5f, 140f, 1.05f), ("palm_c", -8.5f, 7.5f, 250f, 1.0f),
            ("palm_b", 5.5f, 4.6f, 70f, 0.95f), ("palm_a", 8f, 12f, 200f, 1.1f), ("palm_c", 1f, 15f, 300f, 1.05f),
            ("palm_a", -13f, 12f, 40f, 0.95f), ("palm_b", -5f, 19f, 100f, 1.1f), ("palm_c", 12f, 18f, 160f, 0.92f),
            ("palm_a", -16f, 5f, 310f, 0.9f), ("palm_b", 14.5f, 9f, 10f, 0.9f), ("palm_a", 3f, 22f, 75f, 1.0f),
        };
        foreach (var q in palms) Model(q.f, G(q.x, q.z), q.yaw, q.s);
        for (int i = 0; i < 16; i++)
        {
            float x = Mathf.Lerp(-15f, 16f, Hash(i, 3)), z = Mathf.Lerp(2.5f, 26f, Hash(i, 4));
            PH(i % 2 == 0 ? "shrub_02" : "shrub_04", i, G(x, z), 360f * Hash(i, 5), 0.8f + 0.7f * Hash(i, 6));
        }
        for (int i = 0; i < 70; i++)
        {
            float a = Mathf.PI * Hash(i, 7), r = Mathf.Lerp(7f, 19f, Hash(i, 8));
            float x = r * Mathf.Cos(a), z = Mathf.Max(0.9f, r * Mathf.Sin(a));
            if (Mathf.Abs(x - inlandPit.x) < 2.4f && z < 2.4f) continue;
            if (Mathf.Abs(x - beachPit.x) < 2.4f && z < 2.4f) continue;
            PH(i % 2 == 0 ? "grass_medium_01" : "grass_medium_02", i, G(x, z), 360f * Hash(i, 9), 0.9f + 0.6f * Hash(i, 10));
        }
    }

    // ------------------------------------------------------------------ small things
    Material arrowMat;
    Mesh ArrowMesh()
    {
        var m = new Mesh { name = "arrow" };
        m.vertices = new[] { new Vector3(-0.5f, -0.09f), new Vector3(0.12f, -0.09f), new Vector3(0.12f, -0.27f), new Vector3(0.5f, 0f), new Vector3(0.12f, 0.27f), new Vector3(0.12f, 0.09f), new Vector3(-0.5f, 0.09f) };
        m.triangles = new[] { 0, 6, 1, 6, 5, 1, 2, 1, 5, 2, 5, 4, 2, 4, 3 };
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void BuildFx()
    {
        var sm = WellLook.Lit("sandGrain", Hex("#EBD8AE"), 0.1f);
        sand = Many(240, PrimitiveType.Sphere, "sand", sm);
        var rm = WellLook.Water("rain", Hex("#DFF2FF"), 0.5f, 0.6f, false);
        rain = Many(260, PrimitiveType.Cube, "rain", rm);
        var sinkM = WellLook.Emissive(WellLook.Lit("sink", Hex("#55C8FF"), 0.5f), Hex("#55C8FF"), 3200f);
        sinks = Many(70, PrimitiveType.Sphere, "sink", sinkM);
        arrowMat = WellLook.Emissive(WellLook.Lit("arrow", Color.white, 0.2f), Color.white, 9000f);
        arrowMat.SetFloat("_DoubleSidedEnable", 1f); arrowMat.SetFloat("_CullMode", 0f); Keys(arrowMat);
        var am = ArrowMesh();
        arrowsSalt = new GameObject[5]; arrowsUp = new GameObject[4];
        for (int i = 0; i < arrowsSalt.Length; i++) { arrowsSalt[i] = MeshObj("arrowS" + i, am, arrowMat); arrowsSalt[i].SetActive(false); }
        for (int i = 0; i < arrowsUp.Length; i++) { arrowsUp[i] = MeshObj("arrowU" + i, am, arrowMat); arrowsUp[i].SetActive(false); }
    }

    void BuildLate()
    {
        var yellow = WellLook.Emissive(WellLook.Lit("barY", Hex("#FFD24A"), 0.2f), Hex("#FFD24A"), 9000f);
        barTop = Prim(PrimitiveType.Cube, "barTop", Vector3.zero, Vector3.one, yellow);
        barBot = Prim(PrimitiveType.Cube, "barBot", Vector3.zero, Vector3.one, yellow);
        var white = WellLook.Emissive(WellLook.Lit("line", Color.white, 0.2f), Color.white, 9000f);
        seaLine = Prim(PrimitiveType.Cube, "seaLine", Vector3.zero, Vector3.one, white);
        Show(barTop, false); Show(barBot, false); Show(seaLine, false);

        // the lining: logs standing round the back half of the hole, leaning on its wall
        logs = new GameObject[11]; logFoot = new Vector3[11]; logTop = new Vector3[11];
        var kinds = new[] { "driftwood_a", "driftwood_b", "driftwood_c" };
        for (int i = 0; i < logs.Length; i++)
        {
            float ph = Mathf.Lerp(0.3f, Mathf.PI - 0.3f, i / (logs.Length - 1f));
            logFoot[i] = new Vector3(inlandPit.x + 0.5f * Mathf.Cos(ph), 0.42f, 0.5f * Mathf.Sin(ph));
            logTop[i] = new Vector3(inlandPit.x + 1.12f * Mathf.Cos(ph), 1.58f, 1.12f * Mathf.Sin(ph));
            logs[i] = Model(kinds[i % 3], Vector3.zero, 0f, 1f);
            logs[i].SetActive(false);
        }

        // footprints over the dune: one foot-shaped dent per step, left and right mirrored
        var pm = WellLook.Water("print", Hex("#6A5230"), 0.9f, 0.2f, false);
        pm.SetTexture("_BaseColorMap", FootTex());
        Keys(pm);
        printsN = Mathf.FloorToInt((WALK_FROM - WALK_TO) / 0.76f) + 1;
        prints = new GameObject[printsN]; printPos = new Vector3[printsN];
        for (int i = 0; i < printsN; i++)
        {
            float x = WALK_FROM - i * 0.76f, z = 0.98f + (i % 2 == 0 ? 0.17f : -0.17f);
            float g0 = BaseH(x, z), gx0 = BaseH(x + 0.1f, z), gz0 = BaseH(x, z + 0.1f);
            printPos[i] = new Vector3(x, g0, z);
            var n = new Vector3((g0 - gx0) / 0.1f, 1f, (g0 - gz0) / 0.1f).normalized;
            var q = Prim(PrimitiveType.Quad, "print" + i, new Vector3(x, g0 + 0.012f, z), Vector3.one, pm);
            q.transform.rotation = Quaternion.LookRotation(-n, Vector3.left);      // the toes point inland (-x)
            prints[i] = q;
            q.SetActive(false);
        }

        // the scoop: the tin on a rope, let down from out of frame
        tin = Model("cook_pot", Vector3.zero, 15f, 1f);
        tin.SetActive(false);
        rope = Prim(PrimitiveType.Cylinder, "rope", Vector3.zero, Vector3.one, WellLook.Lit("rope", Hex("#B39A6C"), 0.25f));
        rope.SetActive(false);

        // the camp: stones, a little fire, a tin of water
        camp = new GameObject("camp");
        camp.transform.SetParent(stage, false);
        float cx = 6.05f, cz = 1.35f, cg = BaseH(cx, cz);
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f;
            var st = PH("boulder_01", -1, new Vector3(cx + 0.27f * Mathf.Cos(a), cg - 0.02f, cz + 0.27f * Mathf.Sin(a)), 360f * Hash(i, 3), 0.1f + 0.05f * Hash(i, 4));
            st.transform.SetParent(camp.transform, true);
        }
        var pot = Model("cook_pot", new Vector3(cx, cg + 0.2f, cz), 40f, 1f);
        pot.transform.SetParent(camp.transform, true);
        for (int i = 0; i < 3; i++)
        {
            var w = Model(kinds[i], new Vector3(cx - 0.3f * Mathf.Cos(i * 2.1f), cg + 0.05f + i * 0.03f, cz - 0.3f * Mathf.Sin(i * 2.1f)), 0f, 0.42f);
            w.transform.rotation = Quaternion.FromToRotation(Vector3.up, new Vector3(Mathf.Cos(i * 2.1f), 0.12f, Mathf.Sin(i * 2.1f)));
            w.transform.SetParent(camp.transform, true);
        }
        camp.SetActive(false);
        var dot = WellLook.SoftDot();
        var cO = Hex("#FF6A12") * 3f; cO.a = 0.85f;
        var cY = Hex("#FFC24A") * 5f; cY.a = 0.9f;
        var fm = WellLook.Unlit("flame", cO, dot, true);
        var fy = WellLook.Unlit("flameY", cY, dot, true);
        flames = new GameObject[60];
        for (int i = 0; i < flames.Length; i++) { flames[i] = Prim(PrimitiveType.Quad, "flame" + i, Vector3.zero, Vector3.one, i % 3 == 0 ? fy : fm); flames[i].SetActive(false); }
        var sm = WellLook.Water("steam", Color.white, 0.09f, 0.1f, false);
        sm.SetTexture("_BaseColorMap", WellLook.SoftDot());
        Keys(sm);
        steam = Many(22, PrimitiveType.Quad, "steam", sm);

        // the village
        huts = new GameObject[5];
        hutPos = new[] { new Vector3(-5.6f, 0f, 11f), new Vector3(-0.6f, 0f, 11.5f), new Vector3(5.3f, 0f, 9.5f), new Vector3(10.4f, 0f, 7.5f), new Vector3(-12f, 0f, 8.5f) };
        for (int i = 0; i < huts.Length; i++)
        {
            var q = hutPos[i]; q.y = BaseH(q.x, q.z) - 0.03f; hutPos[i] = q;
            huts[i] = Model(i % 2 == 0 ? "hut_a" : "hut_b", q, 200f + 37f * i, 1f);
            huts[i].SetActive(false);
        }
        arrowsCone = new GameObject[3];
        for (int i = 0; i < arrowsCone.Length; i++) { arrowsCone[i] = MeshObj("arrowC" + i, ArrowMesh(), arrowMat); arrowsCone[i].SetActive(false); }

        lab1Ft = Lab("1 FT", "y");
        lab40Ft = Lab("40 FT", "y");
        labSeaLvl = Lab("SEA LEVEL", "w");
        labThin = Lab("THIN", "y");
        labTooDeep = Lab("SALT!", "o");
        labStop = Lab("STOP", "y");
        labTooMuch = Lab("TOO MUCH", "o");
        labWeeks = Lab("WEEKS", "y");
        labYears = Lab("THOUSANDS OF YEARS", "y");
    }

    void UpdateLate(float t)
    {
        // ------ footprints appear one after the other
        float wA = S(5) + 0.05f, wB = Tw(5, "dig") - 0.05f;
        for (int i = 0; i < printsN; i++)
        {
            float st = wA + (wB - wA) * i / printsN;
            bool on = t >= st && t < S(7) + 0.1f;
            float pk = Back(P(t, st, st + 0.12f));
            Show(prints[i], on);
            if (on) prints[i].transform.localScale = new Vector3((i % 2 == 0 ? 1f : -1f) * 0.27f * pk, 0.5f * pk, 1f);
        }

        // ------ the tin goes down on its rope, slowly, and comes up again
        float dipA = S(12), dipB = Tw(12, "take") - 0.02f;
        bool dip = t >= dipA && t < dipB;
        Show(tin, dip); Show(rope, dip);
        if (dip)
        {
            float u = P(t, dipA, dipB);
            float y = Mathf.Lerp(1.95f, 0.5f, E(Mathf.Clamp01(u / 0.52f)));
            y = Mathf.Lerp(y, 1.5f, E(Mathf.Clamp01((u - 0.62f) / 0.38f)));
            var tp = new Vector3(inlandPit.x, y, 0.5f);
            tin.transform.localPosition = tp;
            float topY = y + 0.5f, len = 12f;
            rope.transform.localPosition = new Vector3(tp.x, topY + len * 0.5f, tp.z);
            rope.transform.localScale = new Vector3(0.014f, len * 0.5f, 0.014f);
        }

        // ------ one foot up, forty feet down
        float s9 = S(9), s10 = S(10), s11 = S(11), s12 = S(12), s13 = S(13), people = Tw(13, "People");
        const float UP = 0.62f;
        bool nine = t >= s9 && t < s10;
        Show(barTop, nine); Show(barBot, nine); Show(seaLine, nine);
        if (nine)
        {
            float a = OutCubic(P(t, Tw(9, "foot"), Tw(9, "rises", 0, true)));
            barTop.transform.localPosition = new Vector3(0f, UP * a * 0.5f, -0.12f);
            barTop.transform.localScale = new Vector3(0.42f, UP * a + 0.001f, 0.02f);
            float b = OutCubic(P(t, Tw(9, "pushes"), Tw(9, "down", 0, true)));
            barBot.transform.localPosition = new Vector3(0f, -UP * 40f * b * 0.5f, -0.12f);
            barBot.transform.localScale = new Vector3(0.42f, UP * 40f * b + 0.001f, 0.02f);
            seaLine.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            seaLine.transform.localScale = new Vector3(44f, 0.05f, 0.02f);
        }

        // ------ the lining
        float l0 = Tw(11, "line"), step = 0.15f;
        for (int i = 0; i < logs.Length; i++)
        {
            int k = (i * 7) % logs.Length;             // not in order round the hole
            float start = l0 + k * step;
            bool on = t >= start;
            Show(logs[i], on);
            if (!on) continue;
            float f = P(t, start, start + 0.3f);
            var dir = (logTop[i] - logFoot[i]).normalized;
            var pos = logFoot[i] + Vector3.up * (1f - f * f) * 1.9f;
            logs[i].transform.position = pos;
            logs[i].transform.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        // ------ the salt coming up
        float cA = Tw(12, "too"), cB = s13 - 0.02f;
        for (int i = 0; i < arrowsCone.Length; i++)
        {
            bool on = t >= cA && t < cB;
            Show(arrowsCone[i], on);
            if (!on) continue;
            float rise = Frac(t * 1.2f + i * 0.33f);
            float y = Mathf.Lerp(-23f, -4f + (1f - coneK) * -14f, rise);
            float sz = 5f * E(Mathf.Clamp01(rise * 4f)) * E(Mathf.Clamp01((1f - rise) * 4f));
            arrowsCone[i].transform.position = new Vector3(inlandPit.x + (i - 1) * 1.1f, y, -0.1f);
            arrowsCone[i].transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            arrowsCone[i].transform.localScale = new Vector3(sz, sz * 0.8f, sz);
        }

        // ------ the fire
        bool fire = t >= s13 - 0.02f && t < people;
        if (camp.activeSelf != fire) camp.SetActive(fire);
        float cx = 6.05f, cz = 1.35f, cg = BaseH(cx, cz);
        for (int i = 0; i < flames.Length; i++)
        {
            Show(flames[i], fire);
            if (!fire) continue;
            float life = Frac(t * (1.3f + 0.9f * Hash(i, 1)) + Hash(i, 2));
            float a = Hash(i, 3) * Mathf.PI * 2f, r = (0.03f + 0.17f * Hash(i, 4)) * (1f - life * 0.75f);
            float sway = 0.03f * Mathf.Sin(t * 9f + i);
            flames[i].transform.position = new Vector3(cx + r * Mathf.Cos(a) * 1.25f + sway, cg + 0.03f + life * 0.26f, cz - 0.24f - 0.002f * i);
            float w = (i % 3 == 0 ? 0.075f : 0.13f) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(life * 0.9f + 0.1f));
            flames[i].transform.localScale = new Vector3(w, w * 1.9f, 1f);
        }
        for (int i = 0; i < steam.Length; i++)
        {
            bool on = fire && t >= s13 + 0.35f;
            Show(steam[i], on);
            if (!on) continue;
            float life = Frac(t * 0.65f + Hash(i, 6));
            steam[i].transform.position = new Vector3(cx + (Hash(i, 7) - 0.5f) * 0.18f + 0.07f * Mathf.Sin(t * 2f + i), cg + 0.5f + life * 0.95f, cz - 0.28f - 0.002f * i);
            float sz = 0.14f + life * 0.3f;
            steam[i].transform.localScale = new Vector3(sz, sz * 1.25f, 1f);
        }

        // ------ the village
        for (int i = 0; i < huts.Length; i++)
        {
            float st = people + 0.08f + i * 0.14f;
            bool on = t >= st;
            Show(huts[i], on);
            if (!on) continue;
            float k = Back(P(t, st, st + 0.32f));
            huts[i].transform.localPosition = hutPos[i];
            huts[i].transform.localScale = new Vector3(k, k, k);
        }
        var main = human;
        var paths = new[] { (-13.5f, 3.5f, 1f), (2.6f, 3.0f, -1f), (-3.2f, 4.5f, 1f) };
        bool crowdOn = t >= people;
        for (int i = 0; i < crowd.Length; i++)
        {
            crowd[i].Show(crowdOn);
            if (!crowdOn) continue;
            float m = 1.25f * (t - people) + 0.4f * i;
            human = crowd[i];
            Walk(paths[i].Item1 + paths[i].Item3 * m, paths[i].Item2, paths[i].Item3, m);
        }
        human = main;
    }

    int nSand;
    // every bite of the hole throws a plume of sand up and out over both rims
    void Plume(float t, Pit p, float t0, float t1)
    {
        const int PER_BITE = 40;
        for (int b = 0; b < BITES; b++)
        {
            float bt = t0 + (t1 - t0) * b / BITES;
            if (t < bt || t > bt + 1.1f) continue;
            float side = b % 2 == 0 ? -1f : 1f;
            float y0 = BaseH(p.x, 0f) - (p.baseY - p.floorY) * (b + 0.3f) / BITES;
            for (int i = 0; i < PER_BITE; i++)
            {
                int k = b * 57 + i;
                float start = bt + 0.13f * Hash(k, 1);
                float age = t - start;
                if (age < 0f || age > 0.95f || nSand >= sand.Length) continue;
                var v = Vector3.right * side * (0.7f + 2.6f * Hash(k, 2)) + Vector3.up * (3.0f + 2.5f * Hash(k, 3)) + Vector3.forward * (Hash(k, 4) - 0.35f) * 1.5f;
                var q = new Vector3(p.x + side * 0.15f + (Hash(k, 5) - 0.5f) * 0.7f, y0, 0.25f + 0.5f * Hash(k, 6)) + v * age + Vector3.down * 4.9f * age * age;
                if (age > 0.1f && q.y < H(q.x, Mathf.Max(q.z, 0f))) continue;
                var g = sand[nSand++];
                Show(g, true);
                g.transform.position = q;
                float sz = (0.018f + 0.05f * Hash(k, 7) * Hash(k, 8)) * (1f - 0.35f * age);
                g.transform.localScale = new Vector3(sz, sz * 0.85f, sz);
            }
        }
    }

    void UpdateFx(float t)
    {
        nSand = 0;
        float dig1 = Tw(3, "dig"), fill1 = Tw(3, "fills");
        float dig2 = Tw(5, "dig"), fill2 = S(6);
        if (t >= dig1 && t <= fill1 + 1.2f) Plume(t, beachPit, dig1, fill1 - 0.05f);
        if (t >= dig2 && t <= fill2 + 1.2f) Plume(t, inlandPit, dig2, fill2 + 0.1f);
        if (t >= Tw(10, "dig") && t < S(11)) Plume(t, edgePit, Tw(10, "dig"), Tw(10, "punch", 0, true) + 0.1f);
        for (int i = nSand; i < sand.Length; i++) Show(sand[i], false);

        // rain
        float rainA = Tw(7, "rain") - 0.25f, rainB = Tw(7, "floats", 0, true) + 0.4f;
        float density = PE(t, rainA, rainA + 0.4f) * (1f - PE(t, rainB, rainB + 0.5f));
        for (int i = 0; i < rain.Length; i++)
        {
            bool on = density > Hash(i, 1) * 0.98f + 0.01f;
            float x = Mathf.Lerp(-34f, 34f, Hash(i, 2)), z = Mathf.Lerp(-10f, 24f, Hash(i, 3));
            float y = 56f - Frac(t * 1.55f + Hash(i, 4)) * 56f;
            float floor = H(x, Mathf.Max(z, 0f));
            if (y < floor) on = false;
            Show(rain[i], on);
            if (!on) continue;
            rain[i].transform.position = new Vector3(x, y, z);
            rain[i].transform.localScale = new Vector3(0.12f, 2.3f, 0.12f);
        }
        // the rain soaking down through the sand
        float sinkA = rainA + 0.6f, sinkB = Tw(7, "floats", 0, true) + 0.6f;
        float sinkOn = PE(t, sinkA, sinkA + 0.3f) * (1f - PE(t, sinkB, sinkB + 0.4f));
        float w0 = 0.62f * OutCubic(P(t, Tw(7, "rain") + 0.3f, Tw(7, "floats", 0, true)));
        for (int i = 0; i < sinks.Length; i++)
        {
            bool on = sinkOn > 0.02f && Hash(i, 1) < sinkOn;
            Show(sinks[i], on);
            if (!on) continue;
            float x = Mathf.Lerp(-20f, 20f, Hash(i, 2));
            float life = Frac(t * 0.9f + Hash(i, 3));
            float top = H(x, 0f), bot = Wt(x, w0);
            float y = Mathf.Lerp(top, bot, life);
            float s = 0.5f * Mathf.Min(1f, (1f - life) * 8f);
            sinks[i].transform.position = new Vector3(x, y, -0.06f);
            sinks[i].transform.localScale = new Vector3(s * 0.55f, s * 1.5f, s * 0.2f);
        }

        // arrows: the sea pushing into the beach
        float a0 = Tw(4, "sea"), a1 = S(5) - 0.05f;
        float onA = PE(t, a0, a0 + 0.25f) * (1f - PE(t, a1 - 0.25f, a1));
        for (int i = 0; i < arrowsSalt.Length; i++)
        {
            bool on = onA > 0.01f;
            Show(arrowsSalt[i], on);
            if (!on) continue;
            float y = -0.55f - i * 0.72f;
            float life = Frac(t * 0.7f + i * 0.31f);
            float x = Mathf.Lerp(26.2f, 22.0f, life);
            float fade = E(Mathf.Clamp01(life * 5f)) * E(Mathf.Clamp01((1f - life) * 4f));
            float s = 1.25f * fade * onA;
            arrowsSalt[i].transform.position = new Vector3(x, y, -0.05f);
            arrowsSalt[i].transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            arrowsSalt[i].transform.localScale = new Vector3(s, s, s);
        }
        // arrows: fresh water floating up on the salt
        float u0 = Tw(7, "floats") - 1.5f, u1 = En(7) + 0.4f;
        float onU = PE(t, u0, u0 + 0.3f) * (1f - PE(t, u1 - 0.3f, u1));
        float[] ux = { -13f, -4.5f, 4.5f, 13f };
        for (int i = 0; i < arrowsUp.Length; i++)
        {
            bool on = onU > 0.01f;
            Show(arrowsUp[i], on);
            if (!on) continue;
            float yBase = -40f * Wt(ux[i], 0.62f) - 3f;
            float rise = Frac(t * 0.8f + i * 0.27f);
            float s = 6.5f * onU * E(Mathf.Clamp01(rise * 5f)) * E(Mathf.Clamp01((1f - rise) * 3f));
            arrowsUp[i].transform.position = new Vector3(ux[i], yBase + rise * 5f, -0.08f);
            arrowsUp[i].transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            arrowsUp[i].transform.localScale = new Vector3(s, s * 0.8f, s);
        }
    }

    // ------------------------------------------------------------------ pits over time
    const int BITES = 6;
    static float Bites(float t, float a, float b, int n)
    {
        float q = P(t, a, b) * n;
        int i = Mathf.Min(Mathf.FloorToInt(q), n - 1);
        return (i + E(Mathf.Clamp01((q - i) / 0.5f))) / n;
    }

    void UpdatePits(float t)
    {
        float dig1 = Tw(3, "dig"), fill1 = Tw(3, "fills");
        beachPit.depth = Bites(t, dig1, fill1 - 0.05f, BITES);
        beachPit.wl = t >= fill1 - 0.1f ? Mathf.Lerp(beachPit.floorY + 0.01f, 0.32f, OutCubic(P(t, fill1 - 0.1f, Tw(3, "water", 0, true) + 0.25f))) : -999f;
        float dig2 = Tw(5, "dig"), fill2 = S(6);
        inlandPit.depth = Bites(t, dig2, fill2 + 0.1f, BITES);
        inlandPit.wl = t >= fill2 ? Mathf.Lerp(inlandPit.floorY + 0.01f, 0.72f, OutCubic(P(t, fill2, Tw(6, "water", 0, true) + 0.2f))) : -999f;

        // the deep hole at the edge of the lens: fresh at first, then it breaks into the salt
        float d0 = Tw(10, "dig"), d1 = Tw(10, "punch", 0, true) + 0.1f;
        edgePit.depth = t >= S(11) ? 0f : Bites(t, d0, d1, BITES);
        float floorNow = edgePit.baseY - (edgePit.baseY - edgePit.floorY) * edgePit.depth;
        float flip = PE(t, Tw(10, "punch"), Tw(10, "salt", 0, true));
        edgePit.wl = (t < S(11) && floorNow < 0.05f) ? 0.05f + 0.26f * flip : -999f;
        Tint(pitCMat, pitCTop, flip);

        // scooping too fast pulls the salt up under the well
        coneK = t < S(13) ? E(P(t, Tw(12, "too"), Tw(12, "meet") - 0.05f)) : 0f;
        Tint(pitBMat, pitBTop, PE(t, Tw(12, "meet") - 0.25f, Tw(12, "meet") + 0.2f) * (t < S(13) ? 1f : 0f));
    }

    static void Tint(Material body, Material top, float salt)
    {
        var a = Color.Lerp(Hex("#2BA6F2"), Hex("#4A9088"), salt); a.a = 0.9f;
        body.SetColor("_BaseColor", a);
        body.SetColor("_EmissiveColor", Color.Lerp(Hex("#2BA6F2") * 500f, Hex("#4A9088") * 120f, salt));
        var b = Color.Lerp(Hex("#6FCBFF"), Hex("#8ABAB0"), salt); b.a = 0.92f;
        top.SetColor("_BaseColor", b);
    }

    // ------------------------------------------------------------------ the figure
    void Kneel(float x, float z, float yaw, float lean, Vector3 handL, Vector3 handR, Vector3 look, float lookW, Vector3? fingerL = null, Vector3? fingerR = null)
    {
        var rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
        float g = H(x, z);
        human.ResetPose();
        var hips = new Vector3(x, g + 0.5f, z);
        human.Place(hips, rot);
        human.Lean(lean);
        foreach (float s in new[] { -1f, 1f })
        {
            var foot = hips - f * 0.5f + r * s * 0.13f;
            foot.y = H(foot.x, foot.z) + 0.1f;
            human.Leg(s < 0f, foot, f + Vector3.down * 0.3f, 72f);
        }
        human.Reach(true, handL, 1f);
        human.Reach(false, handR, 1f);
        if (fingerL.HasValue) human.Point(true, fingerL.Value);
        if (fingerR.HasValue) human.Point(false, fingerR.Value);
        human.Look(look, lookW);
        human.Bake();
    }

    void Stand(float x, float z, float yaw, Vector3 look, float lookW)
    {
        var rot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
        float g = H(x, z);
        human.ResetPose();
        var hips = new Vector3(x, g + 0.93f, z);
        human.Place(hips, rot);
        foreach (float s in new[] { -1f, 1f })
        {
            var foot = hips + r * s * 0.12f;
            foot.y = H(foot.x, foot.z) + 0.1f;
            human.Leg(s < 0f, foot, f, 0f);
            human.Reach(s < 0f, hips + r * s * 0.29f + Vector3.up * -0.04f + f * 0.04f, 1f);
        }
        human.Look(look, lookW);
        human.Bake();
    }

    // the Mixamo walk (Generic, other skeleton) played by distance, so the feet don't slide; the clip walks on flat
    // ground, so each ankle is then moved onto the dune
    const float STRIDE = 0.85f;
    void Walk(float xNow, float z, float dir, float metres)
    {
        var rot = Quaternion.Euler(0f, dir > 0f ? 90f : -90f, 0f);
        var f = rot * Vector3.forward;
        human.SampleWalk(metres / STRIDE);
        float g0 = H(xNow, z);
        var hips = new Vector3(xNow, g0 + human.HipHeight + human.WalkBob, z);
        human.Place(hips, rot);
        var aL = human.Ankle(true); var aR = human.Ankle(false);
        float gL = H(aL.x, aL.z), gR = H(aR.x, aR.z), gm = (gL + gR) * 0.5f;
        hips.y += gm - g0;
        human.Place(hips, rot);
        aL = human.Ankle(true); aR = human.Ankle(false);
        aL.y += gL - gm; aR.y += gR - gm;
        aL.x = hips.x + (aL.x - hips.x) * STRIDE; aR.x = hips.x + (aR.x - hips.x) * STRIDE;      // a shorter step on the slope
        float slope = Mathf.Atan2(H(xNow - dir * 0.3f, z) - H(xNow + dir * 0.3f, z), 0.6f) * Mathf.Rad2Deg;
        human.LegKeep(true, aL, f, slope);
        human.LegKeep(false, aR, f, slope);
        human.Bake();
    }

    const float WALK_FROM = 19.0f, WALK_TO = 10.6f;       // the line of footprints, from the beach over the dune

    void UpdateFigure(float t)
    {
        const float Z = 0.8f;
        float x2 = inlandPit.x + 1.42f;
        float take = Tw(12, "take");
        bool hook = t < S(2) + 0.25f;
        bool shore = t >= S(2) + 0.25f && t < S(3);
        bool lensShots = (t >= S(7) && t < S(10)) || (t >= take && t < S(13));
        human.Show(hook || shore || lensShots);
        if (hook)
        {
            // kneeling at the edge of the sea, head down, hands on his thighs, breathing hard
            float br = Mathf.Sin(t * 2.6f);
            var rot = Quaternion.Euler(0f, 78f, 0f);
            Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
            var hip = new Vector3(hookX, H(hookX, hookZ) + 0.5f, hookZ);
            var hand = hip + f * 0.27f + Vector3.up * (0.05f + 0.008f * br);
            Kneel(hookX, hookZ, 78f, 20f + 2.5f * br, hand - r * 0.17f, hand + r * 0.17f, hip + f * 3f + Vector3.up * 0.2f, 0.55f);
        }
        else if (shore)
        {
            Stand(beachPit.x - 1.42f, Z, 90f, new Vector3(30f, 2f, Z), 0.3f);
        }
        else if (lensShots)
        {
            Stand(x2, Z, -90f, new Vector3(x2, 3f, -5f), 0.2f);
        }
    }

    // ------------------------------------------------------------------ the camera
    struct Shot { public float t; public Vector3 p, look; public Shot(float t, Vector3 p, Vector3 look) { this.t = t; this.p = p; this.look = look; } }

    void UpdateCamera(float t)
    {
        float s2 = S(2), s3 = S(3), s4 = S(4), s5 = S(5), s6 = S(6), s7 = S(7), s8 = S(8);
        float seaT = Tw(4, "sea"), digAgain = Tw(5, "dig");
        float hx = hookX;

        var keys = new List<Shot>
        {
            new Shot(0f, new Vector3(hx - 0.7f, 1.0f, 1.0f), new Vector3(hx + 0.35f, 0.65f, 5.85f)),
            new Shot(s2 - 0.05f, new Vector3(hx - 0.6f, 0.9f, 2.3f), new Vector3(hx + 0.15f, 0.7f, 5.85f)),
            new Shot(s2 + 2.1f, new Vector3(7f, 74f, -126f), new Vector3(2f, -5f, 24f)),
            new Shot(s3 - 0.02f, new Vector3(1f, 68f, -118f), new Vector3(0f, -3f, 24f)),
            new Shot(s3, new Vector3(20.8f, 1.35f, -9.9f), new Vector3(20.8f, 0.75f, 0f)),
            new Shot(s4 - 0.02f, new Vector3(20.8f, 1.1f, -8.9f), new Vector3(20.8f, 0.55f, 0f)),
            new Shot(seaT, new Vector3(20.8f, 1.05f, -8.5f), new Vector3(20.8f, 0.5f, 0f)),
            new Shot(seaT + 0.7f, new Vector3(22.0f, 1.0f, -21f), new Vector3(22.0f, -0.3f, 0f)),
            new Shot(s5 - 0.02f, new Vector3(22.0f, 1.0f, -20f), new Vector3(22.0f, -0.3f, 0f)),
        };
        Vector3 pos, look;
        float walkA = s5 + 0.05f, walkB = digAgain - 0.05f;
        if (t >= s5 && t < digAgain)
        {
            float x = Mathf.Lerp(WALK_FROM, WALK_TO, P(t, walkA, walkB));
            float g = 0f; for (int i = -3; i <= 3; i++) g += BaseH(x + i * 0.8f, 0f); g /= 7f;
            pos = new Vector3(x + 0.35f, g + 5.6f, -6.5f);
            look = new Vector3(x + 0.35f, g - 0.05f, 1.0f);
        }
        else if (t >= digAgain)
        {
            var more = new List<Shot>
            {
                new Shot(digAgain, new Vector3(9f, 1.75f, -9.9f), new Vector3(9f, 1.05f, 0f)),
                new Shot(s6 - 0.02f, new Vector3(9f, 1.5f, -9.0f), new Vector3(9f, 0.85f, 0f)),
                new Shot(s6, new Vector3(9f, 1.5f, -8.9f), new Vector3(9f, 0.85f, 0f)),
                new Shot(s7, new Vector3(9f, 1.4f, -8.2f), new Vector3(9f, 0.8f, 0f)),
                new Shot(s7 + 1.9f, new Vector3(3f, 9f, -148f), new Vector3(3f, -3f, 0f)),
                new Shot(s8 - 0.02f, new Vector3(1f, 6f, -152f), new Vector3(1f, -4f, 0f)),
                new Shot(s8 + 2f, new Vector3(0f, 2f, -148f), new Vector3(0f, -8f, 0f)),
                new Shot(S(9) - 0.02f, new Vector3(0f, 2f, -146f), new Vector3(0f, -8f, 0f)),
                new Shot(S(9), new Vector3(0f, 0.9f, -23.5f), new Vector3(0f, 0.7f, 0f)),
                new Shot(Tw(9, "pushes") - 0.05f, new Vector3(0f, 0.9f, -22f), new Vector3(0f, 0.7f, 0f)),
                new Shot(Tw(9, "down", 0, true) + 0.1f, new Vector3(0f, -8f, -108f), new Vector3(0f, -12.5f, 0f)),
                new Shot(S(10) - 0.02f, new Vector3(0f, -8f, -106f), new Vector3(0f, -12.5f, 0f)),
                new Shot(S(10), new Vector3(18.7f, 0.5f, -20f), new Vector3(18.7f, -0.6f, 0f)),
                new Shot(S(11) - 0.02f, new Vector3(18.7f, 0.3f, -17.5f), new Vector3(18.7f, -0.7f, 0f)),
                new Shot(S(11), new Vector3(9f, 1.75f, -10.6f), new Vector3(9f, 1.05f, 0f)),
                new Shot(S(12) - 0.02f, new Vector3(9f, 1.6f, -9.6f), new Vector3(9f, 1.0f, 0f)),
                new Shot(S(12), new Vector3(9f, 1.5f, -8.6f), new Vector3(9f, 1.0f, 0f)),
                new Shot(Tw(12, "take") - 0.02f, new Vector3(9f, 1.4f, -7.7f), new Vector3(9f, 0.95f, 0f)),
                new Shot(Tw(12, "take"), new Vector3(7.5f, -7f, -82f), new Vector3(7.5f, -9.5f, 0f)),
                new Shot(S(13) - 0.02f, new Vector3(7.5f, -7f, -78f), new Vector3(7.5f, -9.5f, 0f)),
                new Shot(S(13), new Vector3(6.05f, 2.0f, -3.9f), new Vector3(6.05f, 1.72f, 1.35f)),
                new Shot(Tw(13, "People") - 0.02f, new Vector3(6.05f, 1.92f, -3.4f), new Vector3(6.05f, 1.7f, 1.35f)),
                new Shot(Tw(13, "People"), new Vector3(0f, 44f, -118f), new Vector3(0f, 0f, 20f)),
                new Shot(1000f, new Vector3(0f, 36f, -96f), new Vector3(0f, 0f, 18f)),
            };
            Eval(more, t, out pos, out look);
        }
        else Eval(keys, t, out pos, out look);
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        seaMat.SetFloat("_DetailNormalScale", Mathf.Lerp(1f, 0f, Mathf.Clamp01((Mathf.Abs(cam.transform.position.z) - 8f) / 45f)));
    }

    static void Eval(List<Shot> keys, float t, out Vector3 pos, out Vector3 look)
    {
        int i = 0;
        while (i < keys.Count - 2 && t >= keys[i + 1].t) i++;
        var a = keys[i]; var b = keys[i + 1];
        float k = E(P(t, a.t, b.t));
        pos = Vector3.Lerp(a.p, b.p, k);
        look = Vector3.Lerp(a.look, b.look, k);
    }

    // ------------------------------------------------------------------ labels
    static float Pop(float t, float a, float b) => Back(P(t, a, a + 0.28f)) * (1f - E(P(t, b - 0.18f, b)));

    void UpdateLabels(float t)
    {
        labHook.pos = new Vector3(hookX + 2.3f, 0.62f, hookZ + 7f);
        labHook.k = Pop(t, Tw(1, "water") - 0.05f, S(2) + 0.05f);
        labDays.pos = new Vector3(0f, 16f, 0f);
        labDays.k = Pop(t, Tw(2, "three"), S(3));
        labSalt.pos = new Vector3(beachPit.x, 2.05f, 0f);
        labSalt.k = Pop(t, Tw(4, "salt"), S(5));
        labFresh.pos = new Vector3(inlandPit.x, 3.15f, 0f);
        labFresh.k = Pop(t, Tw(6, "fresh"), S(7));
        labFreshL.pos = new Vector3(0f, -10f, 0f);
        labFreshL.k = Pop(t, Tw(7, "fresh"), S(9));
        labSaltL.pos = new Vector3(-15f, -19.5f, 0f);
        labSaltL.k = Pop(t, Tw(7, "salt"), S(9));
        labYou.pos = new Vector3(inlandPit.x + 1.5f, 9f, 0f);
        labYou.k = Pop(t, Tw(8, "feet"), S(9));
        lab1Ft.pos = new Vector3(1.95f, 0.3f, 0f);
        lab1Ft.k = Pop(t, Tw(9, "foot"), Tw(9, "pushes"));
        labSeaLvl.pos = new Vector3(-2.3f, -0.02f, 0f);
        labSeaLvl.k = Pop(t, Tw(9, "sea"), Tw(9, "pushes"));
        lab40Ft.pos = new Vector3(5.2f, -14f, 0f);
        lab40Ft.k = Pop(t, Tw(9, "forty"), S(10));
        labThin.pos = new Vector3(16.6f, -0.95f, 0f);
        labThin.k = Pop(t, Tw(10, "thin"), Tw(10, "dig"));
        labTooDeep.pos = new Vector3(20.1f, -1.1f, 0f);
        labTooDeep.k = Pop(t, Tw(10, "salt"), S(11));
        labStop.pos = new Vector3(9.0f, 3.0f, 0f);
        labStop.k = Pop(t, Tw(11, "stop"), Tw(11, "line"));
        labTooMuch.pos = new Vector3(15f, -4.2f, 0f);
        labTooMuch.k = Pop(t, Tw(12, "too"), S(13));
        labWeeks.pos = new Vector3(6.05f, 2.75f, 1.35f);
        labWeeks.k = Pop(t, Tw(13, "weeks"), Tw(13, "People"));
        labYears.pos = new Vector3(0f, 11f, 18f);
        labYears.k = Pop(t, Tw(13, "thousands"), 1000f);
    }

    // ------------------------------------------------------------------ one frame
    public void Evaluate(float t)
    {
        UpdatePits(t);
        UpdateGround();
        UpdateSea(t);
        UpdateSurf(t);
        UpdateBands(t);
        UpdateFigure(t);
        UpdateFx(t);
        UpdateLate(t);
        UpdateCamera(t);
        UpdateLabels(t);
    }

    // ------------------------------------------------------------------ the character: one plain figure, posed by IK
    // Same skeleton and look as the car video's figure (blue suit, white helmet). There is no digging or walking
    // clip on this PC, so every pose is built here from the bind pose.
    class Human
    {
        readonly GameObject holder, inst;
        readonly Transform hips, head, neck, lArm, lFore, lHand, rArm, rFore, rHand, lUp, lLow, lFoot, rUp, rLow, rFoot;
        readonly Transform[] spine;
        readonly Transform[] bones; readonly Vector3[] bindP; readonly Quaternion[] bindR;
        readonly Quaternion lFootBind, rFootBind;
        Vector3 faceLocal;
        readonly float legScale;
        public Transform HandL => lHand;
        public Transform HandR => rHand;
        public Vector3 Right => holder.transform.right;
        public Vector3 Forward => holder.transform.forward;

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
            legScale = 1f;

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
            SetupWalk();
        }

        void Stylize()
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
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            var top = FindDeep(inst.transform, "mixamorig:HeadTop_End");
            float hr = Vector3.Distance(head.position, top.position) * 0.5f;
            var up = (top.position - head.position).normalized;
            var fwd = Vector3.ProjectOnPlane(head.TransformDirection(faceLocal), up).normalized;
            var hm = new GameObject("helmet");
            hm.AddComponent<MeshFilter>().sharedMesh = HemiMesh();
            var shell = WellLook.Lit("helmet", Hex("#F1F2F4"), 0.78f);
            shell.SetFloat("_DoubleSidedEnable", 1f); shell.SetFloat("_CullMode", 0f);
            Keys(shell);
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
                float th = Mathf.Deg2Rad * 104f * j / RING;
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
        public void Bake() { if (!holder.activeInHierarchy) return; foreach (var (smr, mesh) in baked) { smr.BakeMesh(mesh, false); mesh.RecalculateBounds(); } }

        public void ResetPose() { for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = bindP[i]; bones[i].localRotation = bindR[i]; } }

        public void Place(Vector3 hipsAt, Quaternion rot)
        {
            holder.transform.rotation = rot;
            holder.transform.position = hipsAt;
            holder.transform.position += hipsAt - hips.position;
        }

        public void Lean(float deg)
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

        public void Leg(bool left, Vector3 ankle, Vector3 kneeDir, float toeDownDeg)
        {
            Transform a = left ? lUp : rUp, b = left ? lLow : rLow, c = left ? lFoot : rFoot;
            Solve(a, b, c, ankle, kneeDir);
            c.rotation = Quaternion.AngleAxis(toeDownDeg, holder.transform.right) * holder.transform.rotation * (left ? lFootBind : rFootBind);
        }

        public void Reach(bool left, Vector3 target, float w)
        {
            Transform a = left ? lArm : rArm, b = left ? lFore : rFore, c = left ? lHand : rHand;
            var side = holder.transform.right * (left ? -1f : 1f);
            Solve(a, b, c, target, (side * 0.6f + Vector3.down).normalized);
        }

        public void Show(bool on) { if (holder.activeSelf != on) holder.SetActive(on); }

        // the walk file's skeleton has other bone names: a hidden copy plays the clip and every bone's world rotation,
        // relative to its own rest pose, is copied onto ours
        GameObject driver; AnimationClip walk; Transform dHips, dLeft;
        (Transform d, Transform t, Quaternion dRest, Quaternion tRest)[] map;
        float walkSpeed, dHipRest, hipHeight, walkBob;
        public float HipHeight => hipHeight;
        public float WalkBob => walkBob;
        public Vector3 Ankle(bool left) => (left ? lFoot : rFoot).position;

        void SetupWalk()
        {
            var src = Resources.Load<GameObject>("Mixamo/walking");
            foreach (var c in Resources.LoadAll<AnimationClip>("Mixamo/walking"))
                if (!c.name.StartsWith("__preview")) { walk = c; break; }
            if (src == null || walk == null) { Debug.LogError("walking clip missing"); return; }
            driver = Instantiate(src);
            driver.name = "walkDriver";
            driver.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var r in driver.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var names = new[] { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot" };
            var nextOf = new Dictionary<string, string> {
                { "Spine", "Spine1" }, { "Spine1", "Spine2" }, { "Spine2", "Neck" }, { "Neck", "Head" },
                { "LeftShoulder", "LeftArm" }, { "LeftArm", "LeftForeArm" }, { "LeftForeArm", "LeftHand" },
                { "RightShoulder", "RightArm" }, { "RightArm", "RightForeArm" }, { "RightForeArm", "RightHand" },
                { "LeftUpLeg", "LeftLeg" }, { "LeftLeg", "LeftFoot" }, { "RightUpLeg", "RightLeg" }, { "RightLeg", "RightFoot" } };
            Transform DF(string n) => System.Array.Find(driver.GetComponentsInChildren<Transform>(), x => x.name == n || x.name.EndsWith(":" + n));
            var all = driver.GetComponentsInChildren<Transform>();
            var list = new List<(Transform, Transform, Quaternion, Quaternion)>();
            foreach (var n in names)
            {
                var d = System.Array.Find(all, x => x.name == n || x.name.EndsWith(":" + n));
                var t = FindDeep(inst.transform, "mixamorig:" + n);
                if (d == null || t == null) { Debug.LogError("walk bone missing: " + n); continue; }
                var dr = d.rotation;
                string nx;
                if (nextOf.TryGetValue(n, out nx))
                {
                    var dn = DF(nx); var tn = FindDeep(inst.transform, "mixamorig:" + nx);
                    dr = Quaternion.FromToRotation(dn.position - d.position, tn.position - t.position) * dr;
                }
                list.Add((d, t, dr, Quaternion.Inverse(holder.transform.rotation) * t.rotation));
            }
            map = list.ToArray();
            dHips = map[0].d;
            dLeft = System.Array.Find(all, x => x.name == "LeftArm" || x.name.EndsWith(":LeftArm"));
            var dFore = System.Array.Find(all, x => x.name == "LeftForeArm" || x.name.EndsWith(":LeftForeArm"));
            dHipRest = dHips.position.y;
            Debug.Log($"WALK rest: driver left arm dir {(dFore.position - dLeft.position).normalized:F2}, ours {(lFore.position - lArm.position).normalized:F2}; driver hips y {dHipRest:F3}, clip {walk.name} {walk.length:F3}s");

            // how fast a planted foot moves back relative to the hips = the speed the clip walks at
            const int N = 120;
            var zs = new float[N]; var ys = new float[N];
            for (int i = 0; i < N; i++)
            {
                PoseWalk(walk.length * i / N);
                zs[i] = lFoot.position.z - hips.position.z;
            }
            var v = new float[N];
            float dt = walk.length / N;
            for (int i = 0; i < N; i++) v[i] = (zs[(i + 1) % N] - zs[i]) / dt;
            System.Array.Sort(v);
            walkSpeed = -v[N / 5];
            ResetPose();
            Debug.Log($"WALK speed {walkSpeed:F2} m/s, foot range {Mathf.Min(zs):F2}..{Mathf.Max(zs):F2}, hip height {hipHeight:F3}");
        }

        void PoseWalk(float time)
        {
            walk.SampleAnimation(driver, Mathf.Repeat(time, walk.length));
            ResetPose();
            foreach (var m in map)
                m.t.rotation = holder.transform.rotation * (m.d.rotation * Quaternion.Inverse(m.dRest)) * m.tRest;
            walkBob = (dHips.position.y - dHipRest) / Mathf.Max(0.01f, dHipRest) * hipHeight;
        }

        public void SampleWalk(float metres) { PoseWalk(metres / Mathf.Max(0.1f, walkSpeed)); }

        public void LegKeep(bool left, Vector3 ankle, Vector3 kneeDir, float toeDownDeg)
        {
            Transform a = left ? lUp : rUp, b = left ? lLow : rLow, c = left ? lFoot : rFoot;
            var keep = c.rotation;
            Solve(a, b, c, ankle, kneeDir);
            c.rotation = Quaternion.AngleAxis(toeDownDeg, holder.transform.right) * keep;
        }

        public void Point(bool left, Vector3 dir)
        {
            var h = left ? lHand : rHand;
            h.rotation = Quaternion.FromToRotation(h.up, dir) * h.rotation;
        }

        public void Look(Vector3 target, float w)
        {
            if (w <= 0.001f) return;
            var cur = head.TransformDirection(faceLocal); var want = target - head.position;
            var delta = Quaternion.FromToRotation(cur, want);
            float ang; Vector3 axis; delta.ToAngleAxis(out ang, out axis);
            if (ang > 180f) ang -= 360f;
            ang = Mathf.Clamp(ang, -40f, 40f) * 0.7f * w;
            neck.rotation = Quaternion.AngleAxis(ang, axis) * neck.rotation;
        }
    }
}
