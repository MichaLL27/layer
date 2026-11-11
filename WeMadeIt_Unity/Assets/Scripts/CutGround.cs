// The ground as REAL geometry with cut-outs: one big layered block (strata), minus axis-aligned cut
// boxes (the hole, the trench, the riser). Every frame the block is split along the cuts' faces into
// cells; solid cells emit only the faces that can be seen (the cut face, the lawn, and every wall of a
// cut). Textures are world-triplanar, so cells never show a seam. Wide and deep enough that no edge is
// ever in frame.
using System.Collections.Generic;
using UnityEngine;

public class CutGround
{
    public readonly float X0 = -250f, X1 = 250f, Z1 = 400f;
    readonly float[] layerY;        // top -> bottom, layerY[0] = 0 (surface)
    readonly Mesh mesh;
    readonly int nLayers;

    public CutGround(Transform parent, float[] layerTopToBottom, Material[] layerMats, Material lawn)
    {
        layerY = layerTopToBottom;
        nLayers = layerY.Length - 1;
        var go = new GameObject("CutGround");
        go.transform.SetParent(parent, false);
        mesh = new Mesh { name = "CutGround", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var mats = new Material[nLayers + 1];
        for (int i = 0; i < nLayers; i++) mats[i] = layerMats[i];
        mats[nLayers] = lawn;
        mr.sharedMaterials = mats;
    }

    static void AddSorted(List<float> l, float v, float lo, float hi)
    {
        if (v <= lo + 1e-4f || v >= hi - 1e-4f) return;
        foreach (var x in l) if (Mathf.Abs(x - v) < 1e-4f) return;
        l.Add(v);
    }

    // cuts: boxes (min,max); a cut's front is open to the camera (it extends to z = -inf)
    public void Rebuild(List<Bounds> cuts)
    {
        float yBot = layerY[layerY.Length - 1];
        var xs = new List<float> { X0, X1 }; var ys = new List<float>(); var zs = new List<float> { 0f, Z1 };
        foreach (var y in layerY) ys.Add(y);
        foreach (var c in cuts)
        {
            AddSorted(xs, c.min.x, X0, X1); AddSorted(xs, c.max.x, X0, X1);
            AddSorted(ys, c.min.y, yBot, 0f); AddSorted(ys, c.max.y, yBot, 0f);
            AddSorted(zs, c.max.z, 0f, Z1);
        }
        xs.Sort(); ys.Sort(); zs.Sort();
        int nx = xs.Count - 1, ny = ys.Count - 1, nz = zs.Count - 1;
        var solid = new bool[nx, ny, nz];
        var layer = new int[ny];
        for (int j = 0; j < ny; j++)
        {
            float yc = (ys[j] + ys[j + 1]) * 0.5f;
            int L = nLayers - 1;
            for (int k = 0; k < nLayers; k++) if (yc <= layerY[k] && yc >= layerY[k + 1]) { L = k; break; }
            layer[j] = L;
        }
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
                for (int k = 0; k < nz; k++)
                {
                    var p = new Vector3((xs[i] + xs[i + 1]) * 0.5f, (ys[j] + ys[j + 1]) * 0.5f, (zs[k] + zs[k + 1]) * 0.5f);
                    bool s = true;
                    foreach (var c in cuts)
                        if (p.x > c.min.x && p.x < c.max.x && p.y > c.min.y && p.y < c.max.y && p.z < c.max.z) { s = false; break; }
                    solid[i, j, k] = s;
                }

        var v = new List<Vector3>(); var n = new List<Vector3>();
        var tris = new List<int>[nLayers + 1];
        for (int s = 0; s <= nLayers; s++) tris[s] = new List<int>();

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm, int sub)
        {
            int o = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(nrm); n.Add(nrm); n.Add(nrm); n.Add(nrm);
            // Unity front faces: cross(b-a, c-a) points along the normal (toward the viewer)
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nrm) > 0f) { tris[sub].Add(o); tris[sub].Add(o + 1); tris[sub].Add(o + 2); tris[sub].Add(o); tris[sub].Add(o + 2); tris[sub].Add(o + 3); }
            else { tris[sub].Add(o); tris[sub].Add(o + 2); tris[sub].Add(o + 1); tris[sub].Add(o); tris[sub].Add(o + 3); tris[sub].Add(o + 2); }
        }

        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
                for (int k = 0; k < nz; k++)
                {
                    if (!solid[i, j, k]) continue;
                    float x0 = xs[i], x1 = xs[i + 1], y0 = ys[j], y1 = ys[j + 1], z0 = zs[k], z1 = zs[k + 1];
                    int L = layer[j];
                    // -Z (front)
                    if (k == 0 || !solid[i, j, k - 1])
                        Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.back, L);
                    // +Y (top)
                    if (j == ny - 1 || !solid[i, j + 1, k])
                        Quad(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.up, j == ny - 1 ? nLayers : L);
                    // -Y (a cut's ceiling, never seen but closes the solid)
                    if (j > 0 && !solid[i, j - 1, k])
                        Quad(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), Vector3.down, L);
                    // +Z (back wall of a cut, facing the camera is -Z of the NEXT cell: handled when that cell is solid)
                    if (k < nz - 1 && !solid[i, j, k + 1])
                        Quad(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.forward, L);
                    // -X / +X walls of cuts
                    if (i > 0 && !solid[i - 1, j, k])
                        Quad(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), Vector3.left, L);
                    if (i < nx - 1 && !solid[i + 1, j, k])
                        Quad(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), Vector3.right, L);
                }

        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.subMeshCount = nLayers + 1;
        for (int s = 0; s <= nLayers; s++) mesh.SetTriangles(tris[s], s, false);
        // tangents for normal maps (triplanar builds its own frame, but HDRP wants the channel)
        var tan = new Vector4[v.Count];
        for (int q = 0; q < v.Count; q++)
        {
            var t = Vector3.Cross(n[q], Mathf.Abs(n[q].y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            tan[q] = new Vector4(t.x, t.y, t.z, 1f);
        }
        mesh.tangents = tan;
        mesh.bounds = new Bounds(new Vector3(0, yBot / 2f, Z1 / 2f), new Vector3(X1 - X0, -yBot + 1f, Z1 + 1f));
    }
}
