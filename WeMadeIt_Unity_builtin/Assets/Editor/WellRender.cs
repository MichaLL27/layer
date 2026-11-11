// Batch renderer: builds the well scene in edit mode and renders it frame by frame, no play mode.
//   Unity.exe -batchmode -quit -projectPath WeMadeIt_Unity -executeMethod WellRender.Run -logFile log.txt
//        -wellOut <mp4>            video-only mp4 (raw frames piped into ffmpeg)
//        -wellFrom 0 -wellTo 1500  frame range (default: whole video)      -wellFps 30
//        -wellStills 6.5,12.1      PNG stills of those seconds instead of a video (to <wellOut dir>)
//        -wellScale 0.5            resolution scale (default 1 = 1080x1920)
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WellRender
{
    static string Arg(string name, string def)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return def;
    }

    static GameObject Load(string n)
    {
        var g = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + n + ".fbx");
        if (g == null) throw new Exception("model missing: " + n);
        return g;
    }

    public static void Run()
    {
        try { RunInner(); }
        catch (Exception e) { UnityEngine.Debug.LogError("WellRender failed: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static void RunInner()
    {
        AssetDatabase.Refresh();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var go = new GameObject("WellScene");
        var ws = go.AddComponent<WellScene>();
        ws.mHouse = Load("house"); ws.mTree = Load("tree"); ws.mRig = Load("rig"); ws.mPump = Load("pump");
        ws.mCap = Load("cap"); ws.mCasing = Load("casing"); ws.mScreen = Load("screen");
        string json = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/scenes/well_words.json"));
        ws.Build(json);

        float scale = float.Parse(Arg("-wellScale", "1"), CultureInfo.InvariantCulture);
        int W = Mathf.RoundToInt(1080 * scale) / 2 * 2, H = Mathf.RoundToInt(1920 * scale) / 2 * 2;
        int fps = int.Parse(Arg("-wellFps", "30"));
        string outPath = Arg("-wellOut", "");
        if (outPath == "") throw new Exception("-wellOut required");
        outPath = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        rt.Create();
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var cam = ws.Cam;
        cam.aspect = (float)W / H;
        cam.targetTexture = rt;

        string stills = Arg("-wellStills", "");
        if (stills != "")
        {
            foreach (var s in stills.Split(','))
            {
                float t = float.Parse(s, CultureInfo.InvariantCulture);
                ws.Evaluate(t);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply(false);
                string p = Path.Combine(Path.GetDirectoryName(outPath), "still_" + t.ToString("0.00", CultureInfo.InvariantCulture) + ".png");
                File.WriteAllBytes(p, tex.EncodeToPNG());
                UnityEngine.Debug.Log("STILL " + p);
            }
            return;
        }

        int total = Mathf.CeilToInt(ws.Duration * fps);
        int from = int.Parse(Arg("-wellFrom", "0")), to = int.Parse(Arg("-wellTo", total.ToString()));
        var psi = new ProcessStartInfo("ffmpeg",
            "-y -loglevel error -f rawvideo -pix_fmt rgb24 -s " + W + "x" + H + " -r " + fps + " -i - -vf vflip " +
            "-c:v libx264 -preset medium -crf 16 -pix_fmt yuv420p -movflags +faststart \"" + outPath + "\"")
        { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
        var ff = Process.Start(psi);
        var stream = ff.StandardInput.BaseStream;
        var sw = Stopwatch.StartNew();
        for (int f = from; f < to; f++)
        {
            ws.Evaluate(f / (float)fps);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply(false);
            var raw = tex.GetRawTextureData();
            stream.Write(raw, 0, raw.Length);
            if (f % 150 == 0) UnityEngine.Debug.Log("frame " + f + "/" + to + "  " + sw.Elapsed.TotalSeconds.ToString("0") + "s");
        }
        stream.Close();
        ff.WaitForExit();
        UnityEngine.Debug.Log("RENDER DONE " + (to - from) + " frames in " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s -> " + outPath);
    }
}
