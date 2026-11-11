// Batch renderer (HDRP): builds the well scene in edit mode and renders it frame by frame.
//   Unity.exe -batchmode -quit -projectPath WeMadeIt_Unity -executeMethod WellRender.Run -logFile log.txt
//        -wellScene tunnel         which video (default well)
//        -wellOut <mp4>            video-only mp4 (raw frames piped into ffmpeg) + <dir>/labels.json
//        -wellFrom 0 -wellTo 1500  frame range (default: whole video)      -wellFps 30
//        -wellStills 6.5,12.1      PNG stills of those seconds instead of a video (to <wellOut dir>)
//        -wellScale 0.5            output size scale (default 1 = 1080x1920)
//        -wellSS 1.5               supersampling factor (render bigger, lanczos down)
//        -wellEV 14.6              exposure override
// HDRP needs history (shadows, sky probe, volumetrics): the first frame is rendered WARMUP times.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WellRender
{
    const int WARMUP = 24;

    static string Arg(string name, string def)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return def;
    }
    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    public static void Run()
    {
        try { RunInner(); }
        catch (Exception e) { UnityEngine.Debug.LogError("WellRender failed: " + e); EditorApplication.Exit(1); return; }
        EditorApplication.Exit(0);
    }

    static void RunInner()
    {
        AssetDatabase.Refresh();
        QualitySettings.SetQualityLevel(0, true);          // "High Fidelity"
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        WellLook.ExposureEV = F(Arg("-wellEV", WellLook.ExposureEV.ToString(CultureInfo.InvariantCulture)));

        string slug = Arg("-wellScene", "well");
        var go = new GameObject("Scene_" + slug);
        IShortScene ws = slug == "tunnel" ? (IShortScene)go.AddComponent<TunnelScene>() : slug == "car" ? (IShortScene)go.AddComponent<CarScene>() : slug == "island" ? (IShortScene)go.AddComponent<IslandScene>() : slug == "pisa" ? (IShortScene)go.AddComponent<PisaScene>() : slug == "snowcave" ? (IShortScene)go.AddComponent<SnowScene>() : slug == "escape" ? (IShortScene)go.AddComponent<EscapeScene>() : go.AddComponent<WellScene>();
        string json = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/scenes/" + slug + "_words.json"));
        var sw0 = Stopwatch.StartNew();
        ws.Build(json);
        UnityEngine.Debug.Log($"BUILD {sw0.Elapsed.TotalSeconds:F1}s, device {SystemInfo.graphicsDeviceName} {SystemInfo.graphicsDeviceType}");

        float scale = F(Arg("-wellScale", "1")), ss = F(Arg("-wellSS", "1.5"));
        int W = Mathf.RoundToInt(1080 * scale) / 2 * 2, H = Mathf.RoundToInt(1920 * scale) / 2 * 2;
        int RW = Mathf.RoundToInt(W * ss) / 2 * 2, RH = Mathf.RoundToInt(H * ss) / 2 * 2;
        int fps = int.Parse(Arg("-wellFps", "30"));
        string outPath = Path.GetFullPath(Arg("-wellOut", ""));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));

        var rt = new RenderTexture(RW, RH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        rt.Create();
        var tex = new Texture2D(RW, RH, TextureFormat.RGB24, false, false);
        var cam = ws.Cam;
        cam.targetTexture = rt;
        cam.aspect = (float)RW / RH;

        byte[] Grab()
        {
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, RW, RH), 0, 0, false);
            tex.Apply(false);
            RenderTexture.active = null;
            return tex.GetRawTextureData();
        }

        string stills = Arg("-wellStills", "");
        if (stills != "")
        {
            foreach (var s in stills.Split(','))
            {
                float t = F(s);
                ws.Evaluate(t);
                for (int i = 0; i < WARMUP; i++) cam.Render();
                Grab();
                var small = new Texture2D(RW, RH, TextureFormat.RGB24, false, false);
                small.LoadRawTextureData(tex.GetRawTextureData());
                small.Apply();
                string p = Path.Combine(Path.GetDirectoryName(outPath), "still_" + t.ToString("0.00", CultureInfo.InvariantCulture) + ".png");
                File.WriteAllBytes(p, small.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(small);
                UnityEngine.Debug.Log("STILL " + p);
            }
            return;
        }

        int total = Mathf.CeilToInt(ws.Duration * fps);
        int from = int.Parse(Arg("-wellFrom", "0")), to = int.Parse(Arg("-wellTo", total.ToString()));
        // BT.709 converted AND tagged; light temporal grain so x264 does not band the sky (3D channel's measured fix)
        string args = $"-y -hide_banner -loglevel error -f rawvideo -pix_fmt rgb24 -s {RW}x{RH} -r {fps} -i - " +
                      $"-vf vflip,scale={W}:{H}:flags=lanczos:out_color_matrix=bt709:out_range=tv,format=yuv420p," +
                      "setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv,noise=c0s=2:c0f=t " +
                      "-c:v libx264 -preset medium -crf 15 -tune grain -pix_fmt yuv420p " +
                      "-colorspace bt709 -color_primaries bt709 -color_trc bt709 -color_range tv " +
                      $"-r {fps} -movflags +faststart \"{outPath}\"";
        var psi = new ProcessStartInfo("ffmpeg", args) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true };
        var err = new StringBuilder();
        var ff = Process.Start(psi);
        ff.ErrorDataReceived += (o, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
        ff.BeginErrorReadLine();
        var stdin = ff.StandardInput.BaseStream;

        // labels: screen positions per frame for finish_video.py (ASS pills, crisp at any size)
        var lab = new StringBuilder();
        lab.Append("{\"fps\":").Append(fps).Append(",\"w\":1080,\"h\":1920,\"frames\":[");

        ws.Evaluate(from / (float)fps);
        for (int i = 0; i < WARMUP; i++) cam.Render();
        var sw = Stopwatch.StartNew();
        for (int f = from; f < to; f++)
        {
            float t = f / (float)fps;
            ws.Evaluate(t);
            cam.Render();
            var raw = Grab();
            stdin.Write(raw, 0, raw.Length);

            if (f > from) lab.Append(',');
            lab.Append('[');
            bool first = true;
            foreach (var L in ws.Labels)
            {
                if (L.k <= 0.01f) continue;
                var sp = cam.WorldToScreenPoint(L.pos);
                if (sp.z <= 0) continue;
                float x = sp.x / RW * 1080f, y = (1f - sp.y / RH) * 1920f;
                if (!first) lab.Append(',');
                first = false;
                lab.Append("{\"text\":\"").Append(L.text).Append("\",\"style\":\"").Append(L.style)
                   .Append("\",\"x\":").Append(x.ToString("0.0", CultureInfo.InvariantCulture))
                   .Append(",\"y\":").Append(y.ToString("0.0", CultureInfo.InvariantCulture))
                   .Append(",\"k\":").Append(L.k.ToString("0.000", CultureInfo.InvariantCulture)).Append('}');
            }
            lab.Append(']');
            if ((f - from) % 150 == 0) UnityEngine.Debug.Log($"frame {f}/{to}  {sw.Elapsed.TotalSeconds:0}s");
        }
        lab.Append("]}");
        stdin.Close();
        ff.WaitForExit();
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(outPath), "labels.json"), lab.ToString());
        UnityEngine.Debug.Log($"RENDER DONE {to - from} frames {RW}x{RH} -> {W}x{H} in {sw.Elapsed.TotalSeconds:0.0}s ({1000 * sw.Elapsed.TotalSeconds / Math.Max(1, to - from):0} ms/frame), ffmpeg exit {ff.ExitCode} -> {outPath}");
        if (ff.ExitCode != 0) throw new Exception("ffmpeg failed: " + err);
    }
}
