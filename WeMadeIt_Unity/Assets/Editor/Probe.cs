// Batch probe: prints every model under Resources/PH/model and Resources/Models with its size and
// material names, so the scene code can place and texture them without guessing.
//   Unity.exe -batchmode -quit -projectPath WeMadeIt_Unity -executeMethod Probe.Models -logFile out.txt
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Probe
{
    public static void Models()
    {
        AssetDatabase.Refresh();
        var guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Resources" });
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;
            var inst = Object.Instantiate(go);
            var rs = inst.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds();
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var mats = rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct();
            int tris = inst.GetComponentsInChildren<MeshFilter>().Sum(mf => mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0);
            Debug.Log($"PROBE {Path.GetFileName(path)} size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2} centre {b.center} min.y {b.min.y:F2} tris {tris} renderers {rs.Length} mats [{string.Join(", ", mats)}]");
            Object.DestroyImmediate(inst);
        }
        foreach (var g in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Resources/Mixamo" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip c && !c.name.StartsWith("__preview"))
                    Debug.Log($"CLIP {Path.GetFileName(path)} '{c.name}' {c.length:F2}s legacy {c.legacy} curves {AnimationUtility.GetCurveBindings(c).Length}");
        }
        var remy = Resources.Load<GameObject>("Mixamo/remy");
        if (remy != null)
        {
            var r = Object.Instantiate(remy);
            var rs = r.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
            Debug.Log($"REMY size {b.size} min {b.min} scale {r.transform.localScale} children {r.transform.childCount} first {r.transform.GetChild(0).name}");
            foreach (var x in rs) Debug.Log($"REMY mat {x.name}: " + string.Join(",", System.Array.ConvertAll(x.sharedMaterials, m => m == null ? "null" : m.name + "/" + m.shader.name)));
        }
        EditorApplication.Exit(0);
    }
    // Mixamo FBX files embed their textures; pull them out once so the HDRP materials get them on reimport.
    public static void ExtractMixamo()
    {
        foreach (var n in new[] { "remy", "sophie" })
        {
            string p = "Assets/Resources/Mixamo/" + n + ".fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(p);
            string dir = "Assets/Resources/Mixamo/Textures/" + n;
            Directory.CreateDirectory(dir);
            bool ok = mi.ExtractTextures(dir);
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            Debug.Log("EXTRACT " + n + " " + ok + " -> " + string.Join(", ", Directory.GetFiles(dir)));
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p))
                if (o is Material m)
                    Debug.Log("MAT " + n + " " + m.name + " base " + (m.HasProperty("_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null) + " normal " + (m.HasProperty("_NormalMap") ? m.GetTexture("_NormalMap") : null));
        }
        EditorApplication.Exit(0);
    }
    public static void Rigs()
    {
        foreach (var n in new[] { "remy", "sophie" })
        {
            var go = Object.Instantiate(Resources.Load<GameObject>("Mixamo/" + n));
            void Dump(Transform t, int d) { if (d > 3) return; Debug.Log($"RIG {n} {new string(' ', d * 2)}{t.name} pos {t.localPosition} rot {t.localEulerAngles} scale {t.localScale}"); foreach (Transform c in t) Dump(c, d + 1); }
            Dump(go.transform, 0);
        }
        foreach (var n in new[] { "sitting_idle", "sophie_sitting_idle" })
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/Mixamo/" + n + ".fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview"))
                {
                    var bs = AnimationUtility.GetCurveBindings(c);
                    Debug.Log($"BIND {n} " + string.Join(" | ", bs.Where(b => !b.propertyName.StartsWith("m_LocalRotation")).Select(b => b.path.Split('/').Last() + "." + b.propertyName).Take(40)));
                    var hp = bs.FirstOrDefault(b => b.path.EndsWith("Hips") && b.propertyName == "m_LocalPosition.y");
                    Debug.Log($"HIPSY {n} path {hp.path} v0 {AnimationUtility.GetEditorCurve(c, hp)?.Evaluate(0)}");
                }
        EditorApplication.Exit(0);
    }
}
