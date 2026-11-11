// The realistic look (HDRP): PBR materials from Poly Haven textures, HDRI sky + sun, volumetric fog,
// post-processing, physical camera. Everything the scene code needs to look like a photo, in one place.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public static class WellLook
{
    // ---------------------------------------------------------------- tuned values (see docs/PIPELINE.md)
    public const float SUN_LUX = 85000f;           // clear-sky direct sun
    public const float SKY_MULT = 12000f;           // HDRI raw cos-weighted irradiance 4.79 (measured) -> ~25 klux
    public static float ExposureEV = 13.4f;        // fixed EV100 (sunny day); -wellEV overrides
    public const float SUN_ELEV = 48f, SUN_YAW = 30f;   // the HDRI's sun is 47.9 deg up (measured)

    public static Color FoliageTint = Color.white;
    public static float GradeContrast = 10f, GradeSaturation = 16f, GradeWarm = 8f;
    static Shader lit, unlit;
    public static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

    static void Keys(Material m)
    {
#if UNITY_EDITOR
        UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(m);
#endif
    }

    public static Material Lit(string name, Color col, float smooth, float metal = 0f)
    {
        lit ??= Shader.Find("HDRP/Lit");
        var m = new Material(lit) { name = name, enableInstancing = true };
        m.SetColor("_BaseColor", col);
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Smoothness", smooth);
        Keys(m);
        return m;
    }

    static void Triplanar(Material m, float metersPerTile, bool objectSpace)
    {
        m.SetFloat("_UVBase", 5f);                         // Triplanar
        m.SetFloat("_TexWorldScale", 1f / metersPerTile);
        m.SetFloat("_ObjectSpaceUVMapping", objectSpace ? 1f : 0f);
    }

    // Poly Haven texture set (Diffuse / nor_gl / composed Mask) mapped triplanar.
    public static Material Textured(string texId, Color tint, float metersPerTile, float smoothMax,
                                    bool objectSpace = false, float normalScale = 1f)
    {
        var m = Lit(texId, tint, 0.5f);
        string b = $"PH/tex/{texId}/{texId}_";
        var d = Resources.Load<Texture2D>(b + "Diffuse");
        var n = Resources.Load<Texture2D>(b + "nor_gl");
        var k = Resources.Load<Texture2D>(b + "Mask");
        if (d == null) Debug.LogError("texture missing: " + b + "Diffuse");
        m.SetTexture("_BaseColorMap", d);
        if (n != null) { m.SetTexture("_NormalMap", n); m.SetFloat("_NormalScale", normalScale); }
        if (k != null)
        {
            m.SetTexture("_MaskMap", k);
            m.SetFloat("_SmoothnessRemapMin", 0f);
            m.SetFloat("_SmoothnessRemapMax", smoothMax);
            m.SetFloat("_AORemapMin", 0f);
            m.SetFloat("_AORemapMax", 1f);
        }
        Triplanar(m, metersPerTile, objectSpace);
        Keys(m);
        return m;
    }

    // Metal / paint with a texture's NORMAL only (no mask: the metallic value stays ours).
    public static Material Metal(string name, Color col, float smooth, float metal, string normalTex = null,
                                 float metersPerTile = 1f, float normalScale = 0.6f)
    {
        var m = Lit(name, col, smooth, metal);
        if (normalTex != null)
        {
            var n = Resources.Load<Texture2D>($"PH/tex/{normalTex}/{normalTex}_nor_gl");
            m.SetTexture("_NormalMap", n);
            m.SetFloat("_NormalScale", normalScale);
            Triplanar(m, metersPerTile, true);
        }
        Keys(m);
        return m;
    }

    // Water / glass: transparent with thin refraction, highlights kept.
    public static Material Water(string name, Color col, float alpha, float smooth = 0.96f, bool refract = true)
    {
        var m = Lit(name, col, smooth);
        var c = col; c.a = alpha;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_SurfaceType", 1f);
        m.SetFloat("_BlendMode", 0f);
        m.SetFloat("_EnableBlendModePreserveSpecularLighting", 1f);
        m.SetFloat("_TransparentZWrite", 0f);
        m.SetFloat("_DoubleSidedEnable", 1f);
        if (refract)
        {
            m.SetFloat("_RefractionModel", 3f);              // Thin
            m.SetFloat("_Ior", 1.33f);
            m.SetColor("_TransmittanceColor", Color.Lerp(Color.white, col, 0.5f));
            m.SetFloat("_ATDistance", 1f);
        }
        Keys(m);
        return m;
    }

    // emission in nits: at EV100 ~13.4 a value of ~13000 is full white on screen
    public static Material Emissive(Material m, Color c, float nits)
    {
        m.SetColor("_EmissiveColor", c * nits);
        Keys(m);
        return m;
    }

    public static Material Unlit(string name, Color col, Texture2D tex = null, bool transparent = false)
    {
        unlit ??= Shader.Find("HDRP/Unlit");
        var m = new Material(unlit) { name = name };
        m.SetColor("_UnlitColor", col);
        if (tex != null) m.SetTexture("_UnlitColorMap", tex);
        if (transparent)
        {
            m.SetFloat("_SurfaceType", 1f);
            m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_TransparentZWrite", 0f);
        }
        Keys(m);
        return m;
    }

    // ---------------------------------------------------------------- model materials by name
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    public static Material ModelMaterial(string key)
    {
        Material m;
        if (cache.TryGetValue(key, out m)) return m;
        switch (key)
        {
            case "house_wall": m = Textured("white_stucco", Hex("#F3EEE4"), 2.2f, 0.35f, true); break;
            case "house_roof": m = Textured("grey_roof_tiles", Hex("#9FB7B3"), 1.6f, 0.45f, true); break;
            case "house_trim": m = Lit(key, Hex("#F2F1EC"), 0.45f); break;
            case "house_glass": m = Lit(key, Hex("#24313B"), 0.97f, 0.1f); break;
            case "house_door": m = Textured("brown_planks_03", Hex("#C08050"), 1.3f, 0.4f, true); break;
            case "house_base": m = Textured("concrete_floor_02", Hex("#CFCAC1"), 1.5f, 0.3f, true); break;
            case "gutter": m = Metal(key, Hex("#D9DCDF"), 0.55f, 0.6f); break;
            case "rig_paint": m = Metal(key, Hex("#E9A521"), 0.55f, 0.15f, "metal_plate", 0.8f, 0.35f); break;
            case "steel_dark": m = Metal(key, Hex("#3A4047"), 0.5f, 0.75f, "metal_plate", 0.6f, 0.4f); break;
            case "steel": m = Metal(key, Hex("#C9CED4"), 0.72f, 1f); break;
            case "rubber": m = Lit(key, Hex("#1C1D1F"), 0.22f); break;
            case "casing": m = Lit(key, Hex("#E9EDF0"), 0.55f); break;                    // white PVC well casing
            case "screen": m = Metal(key, Hex("#D3D7DB"), 0.78f, 1f); break;
            case "cap": m = Metal(key, Hex("#8B96A1"), 0.5f, 0.7f, "metal_plate", 0.4f, 0.3f); break;
            case "hydrant": m = Metal(key, Hex("#2E6B3B"), 0.5f, 0.2f, "metal_plate", 0.4f, 0.3f); break;
            case "car_paint": m = Metal(key, Hex("#2B4C6F"), 0.78f, 0.45f); break;
            case "car_interior": m = Lit(key, Hex("#2A2B2D"), 0.3f); break;
            case "car_seat": m = Textured("brown_planks_03", Hex("#4A4844"), 3f, 0.15f, true); break;
            case "car_trim": m = Lit(key, Hex("#161718"), 0.4f); break;
            case "car_light": m = Emissive(Lit(key, Hex("#F4F1E8"), 0.9f), Hex("#FFF2D8"), 3000f); break;
            case "car_tail": m = Emissive(Lit(key, Hex("#A8231C"), 0.7f), Hex("#C02A20"), 800f); break;
            case "car_glass": m = Water(key, Hex("#7E9EA6"), 0.12f, 0.98f, false); break;
            case "tire": m = Lit(key, Hex("#141414"), 0.25f); break;
            case "chrome": m = Metal(key, Hex("#D6DADF"), 0.85f, 1f); break;
            case "bridge_concrete": m = Textured("concrete_floor_02", Hex("#C9C6BE"), 2f, 0.3f, true); break;
            case "asphalt": m = Lit(key, Hex("#3A3B3D"), 0.25f); break;
            case "phone_screen": m = Emissive(Lit(key, Hex("#9FC9F0"), 0.9f), Hex("#B8DAFF"), 6000f); break;
            case "train_body": m = Metal(key, Hex("#CFCDC6"), 0.5f, 0.3f); break;
            case "train_roof": m = Metal(key, Hex("#6E7073"), 0.4f, 0.5f); break;
            case "train_stripe": m = Lit(key, Hex("#B8452A"), 0.45f); break;
            case "train_door": m = Lit(key, Hex("#3C4148"), 0.45f); break;
            case "train_window": m = Emissive(Lit(key, Hex("#FFE3B0"), 0.8f), Hex("#FFD79A"), 4500f); break;
            case "drum": m = Metal(key, Hex("#8A8476"), 0.4f, 0.2f, "metal_plate", 0.8f, 0.3f); break;
            case "disc_steel": m = Metal(key, Hex("#6D7277"), 0.38f, 0.8f, "rusty_metal_02", 0.5f, 0.5f); break;
            case "tbm_paint": m = Metal(key, Hex("#555A5B"), 0.42f, 0.6f, "metal_plate", 1.1f, 0.3f); break;
            case "tbm_yellow": m = Metal(key, Hex("#A98A4E"), 0.42f, 0.35f, "rusty_metal_02", 0.8f, 0.5f); break;
            case "tbm_yellow_dark": m = Metal(key, Hex("#6F6048"), 0.4f, 0.4f, "rusty_metal_02", 0.6f, 0.6f); break;
            case "tbm_cabinet": m = Metal(key, Hex("#444A4A"), 0.42f, 0.3f, "metal_plate", 0.6f, 0.25f); break;
            case "concrete_seg": m = Textured("brushed_concrete_03", new Color(0.74f, 0.82f, 0.92f), 1.3f, 0.32f, true); break;
            case "grout": m = Textured("concrete_floor_02", Hex("#9EA2A6"), 0.9f, 0.3f, true); break;
            case "gasket": m = Lit(key, Hex("#08090A"), 0.08f); break;
            case "barge_hull": m = Metal(key, Hex("#26343D"), 0.45f, 0.3f, "metal_plate", 1.5f, 0.3f); break;
            case "barge_deck": m = Lit(key, Hex("#6B3A2A"), 0.35f); break;
            case "wall_white": m = Lit(key, Hex("#EEEDE8"), 0.45f); break;
            case "container_r": m = Metal(key, Hex("#6E3A2C"), 0.35f, 0.2f, "rusty_metal_02", 0.6f, 0.6f); break;
            case "container_b": m = Metal(key, Hex("#3A4F5E"), 0.35f, 0.2f, "rusty_metal_02", 0.6f, 0.6f); break;
            case "container_y": m = Metal(key, Hex("#8F7440"), 0.35f, 0.2f, "rusty_metal_02", 0.6f, 0.6f); break;
            case "container_g": m = Metal(key, Hex("#44574A"), 0.35f, 0.2f, "rusty_metal_02", 0.6f, 0.6f); break;
            default: m = Lit(key, Hex("#B0B0B0"), 0.4f); break;
        }
        cache[key] = m;
        return m;
    }

    // Poly Haven model: textures in PH/model/<id>/textures, named <prefix>_diff(a)_1k / _nor_gl_1k.
    static readonly Dictionary<string, Texture2D[]> texLists = new Dictionary<string, Texture2D[]>();

    public static Material PHMaterial(string modelId, string matName)
    {
        string key = modelId + "/" + matName;
        Material m;
        if (cache.TryGetValue(key, out m)) return m;
        Texture2D[] all;
        if (!texLists.TryGetValue(modelId, out all)) { all = Resources.LoadAll<Texture2D>($"PH/model/{modelId}/textures"); texLists[modelId] = all; }
        Texture2D Find(string prefix, string kind)
        {
            foreach (var t in all) if (t.name == $"{prefix}_{kind}_1k") return t;
            return null;
        }
        string[] prefixes = { matName, matName.TrimEnd('s'), matName.Replace("branches", "branch"), modelId, modelId + "_bark", modelId.ToLowerInvariant() };
        Texture2D diff = null, nrm = null; bool alpha = false;
        foreach (var p in prefixes)
        {
            var da = Find(p, "diffa");
            var d = da ?? Find(p, "diff");
            if (d != null) { diff = d; alpha = da != null; nrm = Find(p, "nor_gl"); break; }
        }
        if (diff == null)          // last resort: the asset's main (non-foliage) colour map
            foreach (var t in all)
                if (t.name.EndsWith("_diff_1k") && !t.name.Contains("leaves") && !t.name.Contains("twig"))
                { diff = t; nrm = Find(t.name.Substring(0, t.name.Length - "_diff_1k".Length), "nor_gl"); break; }
        bool foliage = alpha;
        m = Lit(key, Color.white, foliage ? 0.28f : 0.32f);
        if (diff != null) m.SetTexture("_BaseColorMap", diff);
        if (nrm != null) m.SetTexture("_NormalMap", nrm);
        if (foliage)
        {
            m.SetColor("_BaseColor", FoliageTint);
            m.SetFloat("_AlphaCutoffEnable", 1f);
            m.SetFloat("_AlphaCutoff", 0.45f);
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_DoubleSidedNormalMode", 1f);    // mirror: leaves lit on both sides
        }
        if (diff == null) Debug.LogWarning("PH material without texture: " + key);
        Keys(m);
        cache[key] = m;
        return m;
    }

    // ---------------------------------------------------------------- lighting + post
    public static Light Sun(Transform parent)
    {
        var g = new GameObject("Sun");
        g.transform.SetParent(parent, false);
        g.transform.rotation = Quaternion.Euler(SUN_ELEV, SUN_YAW, 0f);
        var l = g.AddComponent<Light>();
        l.type = LightType.Directional;
        var hd = g.AddComponent<HDAdditionalLightData>();
        l.lightUnit = LightUnit.Lux;
        l.intensity = SUN_LUX;
        l.useColorTemperature = true;
        l.colorTemperature = 5500f;
        l.shadows = LightShadows.Soft;
        hd.angularDiameter = 0.8f;
        return l;
    }

    public static Volume Environment(Transform parent)
    {
        var go = new GameObject("WellVolume");
        go.transform.SetParent(parent, false);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100f;
        var p = ScriptableObject.CreateInstance<VolumeProfile>();

        var ve = p.Add<VisualEnvironment>(true);
        ve.skyType.value = (int)SkyType.HDRI;
        ve.skyAmbientMode.value = SkyAmbientMode.Dynamic;
        var hs = p.Add<HDRISky>(true);
        hs.hdriSky.value = Resources.Load<Cubemap>("PH/hdri/sky");
        if (hs.hdriSky.value == null) Debug.LogError("HDRI cubemap missing");
        hs.skyIntensityMode.value = SkyIntensityMode.Multiplier;
        hs.multiplier.value = SKY_MULT;
        hs.rotation.value = 150f;

        var ex = p.Add<Exposure>(true);
        ex.mode.value = ExposureMode.Fixed;
        ex.fixedExposure.value = ExposureEV;
        p.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
        var bl = p.Add<Bloom>(true); bl.intensity.value = 0.12f; bl.scatter.value = 0.6f;
        var ca = p.Add<ColorAdjustments>(true); ca.contrast.value = GradeContrast; ca.saturation.value = GradeSaturation; ca.postExposure.value = 0f;
        var wb = p.Add<WhiteBalance>(true); wb.temperature.value = GradeWarm;
        var vg = p.Add<Vignette>(true); vg.intensity.value = 0.18f; vg.smoothness.value = 0.5f;
        p.Add<MotionBlur>(true).intensity.value = 0f;
        var ssr = p.Add<ScreenSpaceReflection>(true); ssr.enabled.value = true;
        var ao = p.Add<ScreenSpaceAmbientOcclusion>(true); ao.intensity.value = 1.1f; ao.radius.value = 1.2f;
        var cs = p.Add<ContactShadows>(true); cs.enable.value = true; cs.length.value = 0.25f;
        var sh = p.Add<HDShadowSettings>(true); sh.maxShadowDistance.value = 90f;
        var fog = p.Add<Fog>(true);
        fog.enabled.value = true;
        fog.meanFreePath.value = 180f;
        fog.baseHeight.value = 0f;
        fog.maximumHeight.value = 40f;
        fog.enableVolumetricFog.value = true;
        fog.albedo.value = new Color(0.92f, 0.95f, 1f);
        fog.maxFogDistance.value = 3000f;
        p.Add<DepthOfField>(true).focusMode.value = DepthOfFieldMode.Off;   // off: soft frames read as low quality on a phone
        vol.sharedProfile = p;
        return vol;
    }

    public static Camera Camera(Transform parent, float fov)
    {
        var go = new GameObject("Cam");
        go.transform.SetParent(parent, false);
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 4000f;
        var hd = go.AddComponent<HDAdditionalCameraData>();
        hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        hd.SMAAQuality = HDAdditionalCameraData.SMAAQualityLevel.High;
        hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
        hd.dithering = true;
        cam.usePhysicalProperties = true;
        cam.sensorSize = new Vector2(36f * 9f / 16f, 36f);
        cam.gateFit = UnityEngine.Camera.GateFitMode.Vertical;
        cam.focalLength = 18f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        cam.aperture = 8f;
        cam.focusDistance = 10f;
        hd.customRenderingSettings = true;
        foreach (var f in new[] { FrameSettingsField.SSR, FrameSettingsField.SSAO, FrameSettingsField.ContactShadows,
                                  FrameSettingsField.Volumetrics, FrameSettingsField.DepthOfField, FrameSettingsField.Refraction,
                                  FrameSettingsField.AtmosphericScattering })
        {
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)f] = true;
            hd.renderingPathCustomFrameSettings.SetEnabled(f, true);
        }
        return cam;
    }

    // soft round sprite for dust
    public static Texture2D SoftDot(int n = 64)
    {
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        t.Apply();
        return t;
    }
}
