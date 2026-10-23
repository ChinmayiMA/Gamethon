// Bengaluru 2077 - procedural cyberpunk city generator for Unity (URP).
// Menu: Tools > Bengaluru 2077 > City Generator
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BengaluruCyberpunkGenerator : EditorWindow
{
    // ---- layout constants (metres) ----
    const float LOT = 24f;          // one building lot
    const float BLOCK = 72f;        // 3x3 lots
    const float SIDEWALK = 3f;
    const float CARRIAGE = 18f;     // drivable road width
    const float PITCH = 96f;        // block centre to block centre (= BLOCK + 2*SIDEWALK + CARRIAGE)
    const float FLOOR_H = 3.5f;
    const float LANE_FAST = 3.5f;   // lane offsets from road centre (India: drive on the LEFT)
    const float LANE_SLOW = 7.0f;
    const string OUT = "Assets/BengaluruCyberpunk";

    // ---- settings ----
    int blocks = 5, seed = 2077, maxLights = 120;
    float trafficSpacing = 90f, signDensity = 0.85f;
    bool rain = true, lampLights = true;

    // ---- state ----
    Shader litShader;
    readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    readonly List<Texture2D> signH = new List<Texture2D>(), signV = new List<Texture2D>();
    Transform tRoads, tSidewalks, tBuildings, tSigns, tProps, tTraffic, tMetro, tLights;
    Texture2D[] facadeAlbedo, facadeEmit;
    Material[] ledMats;
    Material roadMat, asphaltMat, concreteMat, darkMetalMat;
    float half; int lightCount;

    static readonly Color[] NEON = {
        new Color(0f,0.9f,1f), new Color(0f,0.9f,1f), new Color(1f,0.1f,0.65f), new Color(1f,0.1f,0.65f),
        new Color(1f,0.45f,0.05f), new Color(1f,0.45f,0.05f), new Color(1f,0.85f,0.1f), new Color(1f,0.08f,0.12f),
        new Color(0.55f,0.2f,1f), new Color(0.15f,1f,0.45f), new Color(1f,0.4f,0.8f) };
    static readonly Color[] SHOP = {
        new Color(1f,0.8f,0.55f), new Color(0.6f,0.9f,1f), new Color(1f,0.3f,0.7f), new Color(1f,0.5f,0.15f), new Color(0.4f,1f,0.6f) };

    Color Neon() { return NEON[Random.Range(0, NEON.Length)]; }

    [MenuItem("Tools/Bengaluru 2077/City Generator")]
    static void Open() { GetWindow<BengaluruCyberpunkGenerator>("BLR 2077"); }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Bengaluru 2077 - City Generator", EditorStyles.boldLabel);
        blocks = EditorGUILayout.IntSlider("City size (blocks per side)", blocks, 3, 8);
        seed = EditorGUILayout.IntField("Random seed", seed);
        signDensity = EditorGUILayout.Slider("Neon sign density", signDensity, 0f, 1f);
        trafficSpacing = EditorGUILayout.Slider("Traffic spacing (m, lower = busier)", trafficSpacing, 40f, 200f);
        maxLights = EditorGUILayout.IntSlider("Max real-time lights", maxLights, 0, 300);
        lampLights = EditorGUILayout.Toggle("Street lamps emit light", lampLights);
        rain = EditorGUILayout.Toggle("Rain", rain);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Creates a NEW scene (BengaluruCyberpunk.unity). Needs the Universal Render Pipeline with HDR enabled, " +
            "Linear colour space, and the SignTextures folder (Kannada/Hindi/English neon signs).", MessageType.Info);
        if (GUILayout.Button("Generate City", GUILayout.Height(38))) Generate();
    }

    // =====================================================================================
    void Generate()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if ((GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset) == null &&
            !EditorUtility.DisplayDialog("URP not active",
                "No Universal Render Pipeline asset is assigned (Project Settings > Graphics). The city will look wrong without URP.",
                "Continue anyway", "Cancel")) return;

        LoadSignTextures();
        if (signH.Count == 0)
        {
            EditorUtility.DisplayDialog("Sign textures missing",
                "No h_*.png files found in " + OUT + "/SignTextures. Copy that folder from the zip into your project.", "OK");
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("BLR 2077", "Setting up scene...", 0.05f);
            Random.InitState(seed);
            matCache.Clear(); lightCount = 0;
            litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            EnsureFolder(OUT + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            half = blocks * PITCH * 0.5f;

            tRoads = Root("Roads"); tSidewalks = Root("Sidewalks"); tBuildings = Root("Buildings"); tSigns = Root("Signs");
            tProps = Root("Props"); tTraffic = Root("Traffic"); tMetro = Root("NammaMetro"); tLights = Root("Lights");

            EditorUtility.DisplayProgressBar("BLR 2077", "Textures & materials...", 0.15f);
            BuildMaterials();
            EditorUtility.DisplayProgressBar("BLR 2077", "Roads...", 0.3f);
            BuildRoads();
            EditorUtility.DisplayProgressBar("BLR 2077", "Blocks, towers, neon...", 0.45f);
            for (int i = 0; i < blocks; i++) for (int j = 0; j < blocks; j++) BuildBlock(i, j);
            EditorUtility.DisplayProgressBar("BLR 2077", "Namma Metro...", 0.7f);
            BuildMetro();
            EditorUtility.DisplayProgressBar("BLR 2077", "Traffic...", 0.8f);
            BuildTraffic();
            BuildBackdrop();
            EditorUtility.DisplayProgressBar("BLR 2077", "Atmosphere & post-processing...", 0.9f);
            BuildEnvironment();
            BuildPostFx();
            BuildCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, OUT + "/Scenes/BengaluruCyberpunk.unity");
            AssetDatabase.SaveAssets();
            Debug.Log("[BLR 2077] Done. Real-time lights: " + lightCount + ". Press Play and hold RIGHT MOUSE to look around.");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // =====================================================================================
    // helpers
    Transform Root(string n) { return new GameObject(n).transform; }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    float RoadCoord(int k) { return (k - blocks * 0.5f) * PITCH; }
    float BlockCenter(int i) { return (i + 0.5f - blocks * 0.5f) * PITCH; }

    GameObject PrimR(PrimitiveType t, Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material m,
                     bool collider = false, bool stat = true, bool shadows = false)
    {
        var g = GameObject.CreatePrimitive(t);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.position = pos; g.transform.rotation = rot; g.transform.localScale = scale;
        var r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
        if (!collider) { var c = g.GetComponent<Collider>(); if (c != null) DestroyImmediate(c); }
        g.isStatic = stat;
        return g;
    }
    GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material m,
                    bool collider = false, bool stat = true, bool shadows = false)
    { return PrimR(t, parent, name, pos, Quaternion.identity, scale, m, collider, stat, shadows); }

    Material NewMat(Color c, float smooth, float metal)
    {
        var m = new Material(litShader);
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Glossiness", smooth); m.SetFloat("_Metallic", metal);
        return m;
    }
    Material Lit(Color c, float smooth, float metal = 0f)
    {
        string key = "L" + c.r.ToString("F3") + c.g.ToString("F3") + c.b.ToString("F3") + smooth.ToString("F2") + metal.ToString("F2");
        Material m;
        if (matCache.TryGetValue(key, out m)) return m;
        m = NewMat(c, smooth, metal); matCache[key] = m; return m;
    }
    Material Emissive(Color c, float intensity)
    {
        string key = "E" + c.r.ToString("F3") + c.g.ToString("F3") + c.b.ToString("F3") + intensity.ToString("F2");
        Material m;
        if (matCache.TryGetValue(key, out m)) return m;
        m = NewMat(c * 0.1f, 0.4f, 0f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        matCache[key] = m; return m;
    }
    Material SignMat(Texture2D tex, Color neon)
    {
        string key = "S" + tex.name + neon.r.ToString("F2") + neon.g.ToString("F2") + neon.b.ToString("F2");
        Material m;
        if (matCache.TryGetValue(key, out m)) return m;
        m = NewMat(neon * 0.15f, 0.3f, 0f);
        m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
        m.SetTextureScale("_BaseMap", new Vector2(-1f, 1f)); m.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
        m.SetTextureScale("_MainTex", new Vector2(-1f, 1f)); m.SetTextureOffset("_MainTex", new Vector2(1f, 0f));
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", neon * 3.2f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        matCache[key] = m; return m;
    }

    void AddLight(Vector3 p, Color c, float range, float intensity)
    {
        var g = new GameObject("PointLight");
        g.transform.SetParent(tLights, false); g.transform.position = p;
        var l = g.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        lightCount++;
    }

    void LoadSignTextures()
    {
        signH.Clear(); signV.Clear();
        string folder = OUT + "/SignTextures";
        if (!AssetDatabase.IsValidFolder(folder)) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileName(p);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex == null) continue;
            if (n.StartsWith("h_")) signH.Add(tex); else if (n.StartsWith("v_")) signV.Add(tex);
        }
    }

    // =====================================================================================
    // textures & materials
    void BuildMaterials()
    {
        concreteMat = Lit(new Color(0.11f, 0.115f, 0.14f), 0.45f);
        asphaltMat = Lit(new Color(0.05f, 0.055f, 0.07f), 0.92f);
        darkMetalMat = Lit(new Color(0.03f, 0.03f, 0.04f), 0.6f, 0.7f);

        roadMat = NewMat(Color.white, 0.92f, 0f);
        var rt = BuildRoadTexture(PITCH - CARRIAGE, CARRIAGE, 28);
        roadMat.SetTexture("_BaseMap", rt); roadMat.SetTexture("_MainTex", rt);

        BuildFacadeTextures();
        BuildLedMats();
    }

    Texture2D BuildRoadTexture(float len, float width, int ppm)
    {
        int W = Mathf.RoundToInt(len * ppm), H = Mathf.RoundToInt(width * ppm);
        var px = new Color32[W * H];
        for (int y = 0; y < H; y++)
        {
            float c = (y + 0.5f) / ppm, cc = c - width * 0.5f, ac = Mathf.Abs(cc);
            for (int x = 0; x < W; x++)
            {
                float t = (x + 0.5f) / ppm;
                float n = Mathf.PerlinNoise(t * 0.7f, c * 0.7f);
                float g = 0.05f + 0.025f * n + Random.value * 0.006f;
                Color col = new Color(g, g * 1.04f, g * 1.15f);
                float wear = 0.65f + 0.35f * Mathf.PerlinNoise(t * 2.3f + 40f, c * 2.3f);
                if (Mathf.Abs(ac - 0.18f) < 0.05f) col = new Color(0.85f, 0.62f, 0.08f) * wear;                                  // double yellow
                else if (Mathf.Abs(ac - 5.25f) < 0.07f && t > 9f && t < len - 9f && (t % 6f) < 3.2f) col = Color.white * (0.75f * wear); // lane dashes
                else if (Mathf.Abs(ac - 8.6f) < 0.06f) col = Color.white * (0.7f * wear);                                          // edge lines
                else if (((t > 1.2f && t < 4.5f) || (t < len - 1.2f && t > len - 4.5f)) && ac < 8.2f &&
                         (Mathf.FloorToInt((cc + 9f) / 0.7f) % 2 == 0)) col = Color.white * (0.72f * wear);                        // zebra crossings
                col.a = 1f;
                px[y * W + x] = col;
            }
        }
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, false);
        tex.SetPixels32(px);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 16;
        tex.Apply(true);
        return tex;
    }

    void BuildFacadeTextures()
    {
        Color warm = new Color(1f, 0.8f, 0.5f), cool = new Color(0.75f, 0.9f, 1f), cyan = new Color(0f, 0.9f, 1f),
              mag = new Color(1f, 0.15f, 0.7f), orange = new Color(1f, 0.5f, 0.1f), purple = new Color(0.6f, 0.25f, 1f),
              yellow = new Color(1f, 0.9f, 0.3f), pink = new Color(1f, 0.45f, 0.8f), teal = new Color(0.1f, 0.8f, 0.7f);
        Color[][] pal = {
            new[]{ warm, cool, orange, cyan }, new[]{ cyan, cool, cool, teal }, new[]{ mag, purple, cool, pink },
            new[]{ warm, warm, orange, yellow }, new[]{ cyan, teal, cool }, new[]{ mag, pink, orange } };
        float[] litP = { 0.55f, 0.5f, 0.5f, 0.4f, 0.6f, 0.55f };
        Color none = new Color(0, 0, 0, 0);
        Color[] stripe = { none, none, none, none, cyan, mag };

        facadeAlbedo = new Texture2D[6]; facadeEmit = new Texture2D[6];
        const int S = 256, CELL = 64;
        for (int v = 0; v < 6; v++)
        {
            var alb = new Color32[S * S]; var emi = new Color32[S * S];
            var cellLit = new Color[16];
            for (int i = 0; i < 16; i++)
                cellLit[i] = Random.value < litP[v] ? pal[v][Random.Range(0, pal[v].Length)] * Random.Range(0.6f, 1.3f) : Color.black;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    int cx = x / CELL, cy = y / CELL, lx = x % CELL, ly = y % CELL;
                    bool glass = lx >= 12 && lx < 52 && ly >= 16 && ly < 50;
                    float nz = Random.value * 0.02f;
                    Color a = new Color(0.075f + nz, 0.08f + nz, 0.1f + nz); Color e = Color.black;
                    if (ly < 4) a = new Color(0.04f, 0.04f, 0.05f);
                    if (glass)
                    {
                        a = new Color(0.02f, 0.03f, 0.05f);
                        Color l = cellLit[cy * 4 + cx];
                        e = l.maxColorComponent > 0f ? l * (0.85f + 0.3f * ((float)(ly - 16) / 34f)) : new Color(0.01f, 0.015f, 0.025f);
                    }
                    if (stripe[v].a > 0f && x < 3) { a = new Color(0.02f, 0.02f, 0.03f); e = stripe[v]; }
                    a.a = 1f; e.a = 1f;
                    alb[y * S + x] = a; emi[y * S + x] = e;
                }
            facadeAlbedo[v] = MakeTex(S, S, alb, true); facadeEmit[v] = MakeTex(S, S, emi, true);
        }
    }

    Texture2D MakeTex(int w, int h, Color32[] px, bool repeat)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
        t.SetPixels32(px);
        t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Trilinear; t.anisoLevel = 8;
        t.Apply(true);
        return t;
    }

    void BuildLedMats()
    {
        Color[][] pal = {
            new[]{ new Color(1f,0.3f,0.5f), new Color(1f,0.7f,0.3f), new Color(0.3f,0.9f,1f) },
            new[]{ new Color(0.5f,0.2f,1f), new Color(1f,0.2f,0.7f), new Color(1f,0.7f,0.2f) },
            new[]{ new Color(0.1f,1f,0.8f), new Color(0.2f,0.4f,1f), new Color(1f,0.3f,0.8f) },
            new[]{ new Color(1f,0.5f,0.1f), new Color(1f,0.15f,0.3f), new Color(0.6f,0.2f,1f) } };
        ledMats = new Material[pal.Length];
        const int W = 64, H = 512;
        for (int p = 0; p < pal.Length; p++)
        {
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float g = (float)y / H + (Mathf.PerlinNoise(x * 0.06f + p * 9f, y * 0.012f) - 0.5f) * 0.45f;
                    g = Mathf.Repeat(g * 1.5f, 1f) * (pal[p].Length - 1);
                    int i = Mathf.Min((int)g, pal[p].Length - 2);
                    Color c = Color.Lerp(pal[p][i], pal[p][i + 1], g - i);
                    c *= (x % 2 == 0) ? 1f : 0.5f;           // vertical LED-strip look
                    c.a = 1f; px[y * W + x] = c;
                }
            var tex = MakeTex(W, H, px, true);
            var m = NewMat(Color.white * 0.12f, 0.7f, 0f);
            m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
            m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", Color.white * 2.4f);
            ledMats[p] = m;
        }
    }

    Material BuildingMat(int v, float u, float vt)
    {
        string key = "B" + v + "_" + u.ToString("F2") + "_" + vt.ToString("F2");
        Material m;
        if (matCache.TryGetValue(key, out m)) return m;
        m = NewMat(Color.white, 0.55f, 0.2f);
        m.SetTexture("_BaseMap", facadeAlbedo[v]); m.SetTexture("_MainTex", facadeAlbedo[v]);
        m.SetTextureScale("_BaseMap", new Vector2(u, vt)); m.SetTextureScale("_MainTex", new Vector2(u, vt));
        m.EnableKeyword("_EMISSION");
        m.SetTexture("_EmissionMap", facadeEmit[v]);
        m.SetColor("_EmissionColor", Color.white * 1.8f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        matCache[key] = m; return m;
    }

    // =====================================================================================
    // roads
    void BuildRoads()
    {
        Prim(PrimitiveType.Cube, tRoads, "Ground", new Vector3(0, -0.12f, 0), new Vector3(3000, 0.2f, 3000),
             Lit(new Color(0.02f, 0.022f, 0.03f), 0.6f), true);

        for (int k = 0; k <= blocks; k++)
        {
            float c = RoadCoord(k);
            for (int j = -1; j <= blocks; j++)
            {
                float bc = BlockCenter(j);
                RoadSegment(new Vector3(bc, 0.02f, c), 0f);     // road running along X
                RoadSegment(new Vector3(c, 0.02f, bc), 90f);    // road running along Z
            }
        }
        for (int a = 0; a <= blocks; a++)
            for (int b = 0; b <= blocks; b++)
            {
                Vector3 ic = new Vector3(RoadCoord(a), 0f, RoadCoord(b));
                PrimR(PrimitiveType.Quad, tRoads, "Intersection", ic + Vector3.up * 0.015f, Quaternion.Euler(90, 0, 0),
                      new Vector3(CARRIAGE, CARRIAGE, 1), asphaltMat, true);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        TrafficLight(ic + new Vector3(sx * 10f, 0f, sz * 10f));
            }
    }

    void RoadSegment(Vector3 pos, float yaw)
    {
        PrimR(PrimitiveType.Quad, tRoads, "RoadSegment", pos, Quaternion.Euler(90, yaw, 0),
              new Vector3(PITCH - CARRIAGE, CARRIAGE, 1), roadMat, true);
    }

    void TrafficLight(Vector3 p)
    {
        Prim(PrimitiveType.Cylinder, tProps, "SignalPole", p + Vector3.up * 2.95f, new Vector3(0.1f, 2.75f, 0.1f), darkMetalMat);
        Prim(PrimitiveType.Cube, tProps, "SignalHead", p + Vector3.up * 5.75f, new Vector3(0.45f, 1.3f, 0.35f), darkMetalMat);
        bool red = Random.value < 0.5f;
        Prim(PrimitiveType.Sphere, tProps, "Red", p + Vector3.up * 6.15f, Vector3.one * 0.28f, Emissive(new Color(1f, 0.05f, 0.05f), red ? 5f : 0.05f));
        Prim(PrimitiveType.Sphere, tProps, "Green", p + Vector3.up * 5.35f, Vector3.one * 0.28f, Emissive(new Color(0.1f, 1f, 0.3f), red ? 0.05f : 5f));
    }

    // =====================================================================================
    // blocks, buildings, signs
    void BuildBlock(int bi, int bj)
    {
        Vector3 c = new Vector3(BlockCenter(bi), 0f, BlockCenter(bj));
        float edge = BLOCK * 0.5f + SIDEWALK;
        Prim(PrimitiveType.Cube, tSidewalks, "Sidewalk", c + Vector3.up * 0.1f, new Vector3(edge * 2, 0.2f, edge * 2), concreteMat, true);

        Material curb = Emissive(Neon(), 2.5f);
        Vector3[] sides = { new Vector3(0, 0, -1), new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(1, 0, 0) };
        int sideIdx = 0;
        foreach (var n in sides)
        {
            Vector3 t = Vector3.Cross(Vector3.up, n);
            Vector3 sc = Mathf.Abs(n.x) > 0.5f ? new Vector3(0.12f, 0.05f, edge * 2) : new Vector3(edge * 2, 0.05f, 0.12f);
            Prim(PrimitiveType.Cube, tSidewalks, "CurbGlow", c + n * (edge - 0.08f) + Vector3.up * 0.21f, sc, curb);

            int li = 0;
            foreach (float s in new[] { -24f, 24f })
            {
                Color lc = Random.value < 0.5f ? new Color(1f, 0.7f, 0.35f) : Neon();
                Lamp(c + n * (edge - 0.8f) + t * s, n, lc, lampLights && ((sideIdx + li) % 2 == 0));
                li++;
            }
            foreach (float s in new[] { -8f, 8f })
                Tree(c + n * (edge - 1.3f) + t * s);
            sideIdx++;
        }

        float dist = new Vector2(c.x, c.z).magnitude;
        float fall = Mathf.Clamp01(1f - dist / (half * 1.15f));
        for (int ix = 0; ix < 3; ix++)
            for (int iz = 0; iz < 3; iz++)
            {
                Vector3 lot = c + new Vector3((ix - 1) * LOT, 0, (iz - 1) * LOT);
                int maxK = Mathf.RoundToInt(Mathf.Lerp(3f, 16f, Mathf.Pow(fall, 0.9f)));
                int k = Random.Range(2, maxK + 1);
                if (Random.value < 0.15f) k += Random.Range(2, 5);
                int floors = Mathf.Min(k * 4, 80);
                float w = (floors >= 28 || Random.value < 0.6f) ? 24f : 12f;
                float off = LOT * 0.5f - w * 0.5f;
                Vector3 bc = lot + new Vector3(ix == 0 ? -off : (ix == 2 ? off : 0f), 0f, iz == 0 ? -off : (iz == 2 ? off : 0f));
                var exp = new List<Vector3>();
                if (ix == 0) exp.Add(new Vector3(-1, 0, 0));
                if (ix == 2) exp.Add(new Vector3(1, 0, 0));
                if (iz == 0) exp.Add(new Vector3(0, 0, -1));
                if (iz == 2) exp.Add(new Vector3(0, 0, 1));
                BuildBuilding(bc, w, floors, exp);
            }
    }

    void BuildBuilding(Vector3 bc, float w, int floors, List<Vector3> exposures)
    {
        const float baseY = 0.2f;
        int v = Random.Range(0, 6);
        float H = floors * FLOOR_H;
        Prim(PrimitiveType.Cube, tBuildings, "Building", new Vector3(bc.x, baseY + H * 0.5f, bc.z), new Vector3(w, H, w),
             BuildingMat(v, w / 12f, floors / 4f), true, true, true);

        float topY = baseY + H, topW = w; Vector3 roofC = bc;
        if (w >= 24f && floors >= 24)
        {
            int tf = 4 * Random.Range(1, 4); float H2 = tf * FLOOR_H;
            Vector3 o = new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
            Prim(PrimitiveType.Cube, tBuildings, "Tier", new Vector3(bc.x + o.x, topY + H2 * 0.5f, bc.z + o.z), new Vector3(12, H2, 12),
                 BuildingMat(v, 1f, tf / 4f), true, true, true);
            topY += H2; topW = 12f; roofC = bc + o;
        }
        if (Random.value < 0.5f)
            Prim(PrimitiveType.Cube, tBuildings, "RoofTrim", new Vector3(roofC.x, topY + 0.15f, roofC.z), new Vector3(topW + 0.3f, 0.3f, topW + 0.3f), Emissive(Neon(), 3f));
        if (topY > 70f)
        {
            Prim(PrimitiveType.Cylinder, tBuildings, "Antenna", new Vector3(roofC.x, topY + 6f, roofC.z), new Vector3(0.25f, 6f, 0.25f), darkMetalMat);
            var red = Prim(PrimitiveType.Sphere, tBuildings, "Beacon", new Vector3(roofC.x, topY + 12.3f, roofC.z), Vector3.one * 0.7f,
                           Emissive(new Color(1f, 0.05f, 0.05f), 6f), false, false);
            var nf = red.AddComponent<NeonFlicker>(); nf.mode = NeonFlicker.Mode.Blink; nf.emission = new Color(1f, 0.05f, 0.05f) * 6f; nf.speed = Random.Range(0.5f, 1f);
        }

        foreach (var n in exposures)
        {
            Vector3 t = Vector3.Cross(Vector3.up, n);
            Vector3 fc = new Vector3(bc.x, 0f, bc.z) + n * (w * 0.5f);
            Quaternion face = Quaternion.LookRotation(-n, Vector3.up);

            // lit shop-front
            PrimR(PrimitiveType.Cube, tSigns, "Shopfront", fc + n * 0.06f + Vector3.up * (baseY + 2.0f), face,
                  new Vector3(w * 0.9f, 3.4f, 0.12f), Emissive(SHOP[Random.Range(0, SHOP.Length)], 1.3f));

            // fascia sign over the shop
            if (Random.value < signDensity)
                MakeSign(fc + n * 0.14f + Vector3.up * (baseY + 5.1f), n, signH[Random.Range(0, signH.Count)], 2.0f, w * 0.88f, 3.0f, Neon(), true, false);

            // vertical blade sign
            if (floors >= 8 && signV.Count > 0 && Random.value < signDensity * 0.85f)
            {
                float s = Random.value < 0.5f ? -1f : 1f;
                float yc = baseY + Random.Range(8f, Mathf.Max(9f, H * 0.6f));
                Vector3 pos = fc + n * 0.95f + t * (s * (w * 0.5f - 1.5f)) + Vector3.up * yc;
                MakeSign(pos, t, signV[Random.Range(0, signV.Count)], 1.8f, 1.9f, 12f, Neon(), true, Random.value < 0.15f);
            }

            // tall LED billboard
            if (floors >= 20 && Random.value < 0.45f)
            {
                float bw = Mathf.Min(w * 0.45f, 9f), bh = H * Random.Range(0.35f, 0.55f), yc = baseY + H * Random.Range(0.45f, 0.6f);
                float off = Random.Range(-1f, 1f) * (w * 0.5f - bw * 0.5f - 0.5f);
                var led = PrimR(PrimitiveType.Cube, tSigns, "LedBillboard", fc + n * 0.25f + t * off + Vector3.up * yc, face,
                                new Vector3(bw, bh, 0.3f), ledMats[Random.Range(0, ledMats.Length)], false, false);
                led.AddComponent<ScrollUV>().speed = new Vector2(0f, Random.Range(0.02f, 0.08f));
            }

            // vertical neon tube on a building corner
            if (Random.value < 0.5f)
            {
                Color col = Neon(); float th = Mathf.Min(H, 120f) * Random.Range(0.4f, 0.95f);
                bool flick = Random.value < 0.2f;
                var tube = Prim(PrimitiveType.Cube, tSigns, "NeonTube", fc + n * 0.18f + t * ((Random.value < 0.5f ? -1f : 1f) * (w * 0.5f - 0.15f)) + Vector3.up * (baseY + th * 0.5f),
                                new Vector3(0.22f, th, 0.22f), Emissive(col, 4f), false, !flick);
                if (flick) { var nf = tube.AddComponent<NeonFlicker>(); nf.emission = col * 4f; nf.speed = Random.Range(0.6f, 1.4f); }
            }

            if (lightCount < maxLights && Random.value < 0.18f)
                AddLight(fc + n * 4f + Vector3.up * Random.Range(4f, 14f), Neon(), 16f, 5f);
        }
    }

    // A neon sign: textured panel + glowing frame. 'normal' = direction the sign faces (blade signs show on both sides).
    void MakeSign(Vector3 center, Vector3 normal, Texture2D tex, float targetSize, float maxW, float maxH, Color neon, bool border, bool flicker)
    {
        float aspect = tex.width / (float)tex.height;
        float w, h;
        if (aspect >= 1f) { h = targetSize; w = h * aspect; } else { w = targetSize; h = w / aspect; }
        float sc = Mathf.Min(1f, Mathf.Min(maxW / w, maxH / h));
        w *= sc; h *= sc;

        Quaternion rot = Quaternion.LookRotation(-normal, Vector3.up);
        var root = new GameObject("Sign").transform;
        root.SetParent(tSigns, false); root.position = center; root.rotation = rot;
        root.gameObject.isStatic = !flicker;

        var panel = Prim(PrimitiveType.Cube, root, "Panel", center, new Vector3(w, h, 0.15f), SignMat(tex, neon), false, !flicker);
        panel.transform.rotation = rot;
        if (Random.value < 0.35f)    // dim back-lit plate behind the lettering
        {
            var plate = Prim(PrimitiveType.Cube, root, "Plate", center + normal * -0.06f, new Vector3(w + 0.3f, h + 0.3f, 0.05f), Emissive(neon, 0.5f), false, !flicker);
            plate.transform.rotation = rot;
        }
        if (border)
        {
            Material bm = Emissive(neon, 3.8f); float bt = 0.1f;
            Vector3 r = rot * Vector3.right, u = Vector3.up; Vector3 z = rot * Vector3.back * 0.04f;
            Vector3[] pos = { u * (h * 0.5f), -u * (h * 0.5f), r * (w * 0.5f), -r * (w * 0.5f) };
            Vector3[] scl = { new Vector3(w + bt, bt, 0.2f), new Vector3(w + bt, bt, 0.2f), new Vector3(bt, h + bt, 0.2f), new Vector3(bt, h + bt, 0.2f) };
            for (int i = 0; i < 4; i++)
            {
                var b = Prim(PrimitiveType.Cube, root, "Frame", center + pos[i] + z, scl[i], bm, false, !flicker);
                b.transform.rotation = rot;
                if (flicker) { var nf = b.AddComponent<NeonFlicker>(); nf.emission = neon * 3.8f; nf.speed = Random.Range(0.6f, 1.4f); }
            }
        }
    }

    void Lamp(Vector3 basePos, Vector3 n, Color glow, bool light)
    {
        Prim(PrimitiveType.Cylinder, tProps, "LampPole", basePos + Vector3.up * 3.8f, new Vector3(0.12f, 3.6f, 0.12f), darkMetalMat);
        PrimR(PrimitiveType.Cube, tProps, "LampArm", basePos + Vector3.up * 7.6f + n * 0.8f, Quaternion.LookRotation(n), new Vector3(0.1f, 0.1f, 1.8f), darkMetalMat);
        PrimR(PrimitiveType.Cube, tProps, "LampHead", basePos + Vector3.up * 7.5f + n * 1.7f, Quaternion.LookRotation(n), new Vector3(0.5f, 0.15f, 0.9f), Emissive(glow, 3.5f));
        if (light && lightCount < maxLights) AddLight(basePos + Vector3.up * 6.8f + n * 1.7f, glow, 20f, 4f);
    }

    void Tree(Vector3 p)
    {
        // Bengaluru = Garden City: pink Tabebuia / orange Gulmohar / purple Jacaranda canopies
        Color[] bloom = { new Color(1f, 0.35f, 0.7f), new Color(1f, 0.35f, 0.7f), new Color(1f, 0.3f, 0.1f), new Color(0.6f, 0.35f, 1f) };
        Color c = bloom[Random.Range(0, bloom.Length)];
        Prim(PrimitiveType.Cylinder, tProps, "Trunk", p + Vector3.up * 2.0f, new Vector3(0.3f, 1.8f, 0.3f), Lit(new Color(0.06f, 0.04f, 0.04f), 0.2f));
        float s = Random.Range(3.8f, 5f);
        Prim(PrimitiveType.Sphere, tProps, "Canopy", p + Vector3.up * 4.8f, new Vector3(s, s * 0.8f, s), Emissive(c, 0.7f));
    }

    // =====================================================================================
    // Namma Metro viaduct (purple line) with trains
    void BuildMetro()
    {
        float zc = RoadCoord(blocks / 2);
        float len = 2f * (half + PITCH);
        Material deck = Lit(new Color(0.09f, 0.08f, 0.12f), 0.35f);
        Color purple = new Color(0.65f, 0.2f, 1f);

        Prim(PrimitiveType.Cube, tMetro, "Deck", new Vector3(0, 10f, zc), new Vector3(len, 1.4f, 7f), deck, true, true, true);
        Prim(PrimitiveType.Cube, tMetro, "UnderGlow", new Vector3(0, 9.22f, zc), new Vector3(len, 0.1f, 0.6f), Emissive(purple, 3.5f));
        foreach (float s in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cube, tMetro, "Parapet", new Vector3(0, 11.05f, zc + s * 3.4f), new Vector3(len, 0.7f, 0.25f), deck, false, true, true);
            Prim(PrimitiveType.Cube, tMetro, "RailGlow", new Vector3(0, 10.82f, zc + s * 3.4f), new Vector3(len, 0.1f, 0.3f), Emissive(new Color(1f, 0.15f, 0.7f), 3f));
        }
        for (float x = -half - 72f; x <= half + 72f; x += 24f)
        {
            bool skip = false;
            for (int m = 0; m <= blocks; m++) if (Mathf.Abs(x - RoadCoord(m)) < 11f) skip = true;
            if (skip) continue;
            Prim(PrimitiveType.Cube, tMetro, "Pillar", new Vector3(x, 4.65f, zc), new Vector3(1.8f, 9.3f, 1.8f), deck, true, true, true);
        }
        // station-name signs on both faces of the viaduct
        for (int m = 0; m < blocks; m++)
            foreach (float s in new[] { -1f, 1f })
            {
                var tex = signH[Random.Range(0, signH.Count)];
                MakeSign(new Vector3(BlockCenter(m), 12.1f, zc + s * 3.8f), new Vector3(0, 0, s), tex, 1.7f, 9f, 1.7f, purple, true, false);
            }
        // trains
        for (int d = 0; d < 2; d++)
        {
            float sign = d == 0 ? 1f : -1f;
            var train = new GameObject("MetroTrain").transform;
            train.SetParent(tMetro, false);
            train.position = new Vector3(-sign * half * 0.4f, 12.4f, zc + sign * 1.8f);
            train.rotation = Quaternion.LookRotation(new Vector3(sign, 0, 0));
            Material body = Lit(new Color(0.75f, 0.75f, 0.85f), 0.7f, 0.7f);
            for (int car = -1; car <= 1; car++)
            {
                Vector3 cp = train.position + train.forward * (car * 16.4f);
                Prim(PrimitiveType.Cube, train, "Car", cp, new Vector3(2.8f, 3.4f, 16f), body, false, false, true).transform.rotation = train.rotation;
                Prim(PrimitiveType.Cube, train, "Windows", cp + Vector3.up * 0.35f, new Vector3(2.84f, 0.9f, 15f), Emissive(new Color(0.8f, 0.95f, 1f), 2.2f), false, false).transform.rotation = train.rotation;
                Prim(PrimitiveType.Cube, train, "Stripe", cp - Vector3.up * 1.1f, new Vector3(2.86f, 0.22f, 16f), Emissive(purple, 4f), false, false).transform.rotation = train.rotation;
            }
            Prim(PrimitiveType.Cube, train, "Headlight", train.position + train.forward * 24.6f, new Vector3(1.6f, 0.3f, 0.2f), Emissive(Color.white, 6f), false, false).transform.rotation = train.rotation;
            var mv = train.gameObject.AddComponent<LoopingMover>();
            mv.dir = new Vector3(sign, 0, 0); mv.speed = Random.Range(14f, 18f); mv.limit = half + PITCH - 20f;
        }
    }

    // =====================================================================================
    // traffic: cars, auto-rickshaws and buses driving on the LEFT, looping forever
    void BuildTraffic()
    {
        float limit = half + 80f;
        for (int axis = 0; axis < 2; axis++)
            for (int k = 0; k <= blocks; k++)
            {
                float c = RoadCoord(k);
                for (int dsign = -1; dsign <= 1; dsign += 2)
                {
                    Vector3 dir = axis == 0 ? new Vector3(dsign, 0, 0) : new Vector3(0, 0, dsign);
                    Vector3 right = Vector3.Cross(Vector3.up, dir);
                    Vector3 center = axis == 0 ? new Vector3(0, 0, c) : new Vector3(c, 0, 0);
                    foreach (float lane in new[] { LANE_FAST, LANE_SLOW })
                    {
                        Vector3 lanePos = center - right * lane;
                        float speed = lane == LANE_FAST ? Random.Range(11f, 15f) : Random.Range(6f, 9f);
                        int count = Mathf.Max(1, Mathf.CeilToInt(2f * limit / trafficSpacing));
                        float step = 2f * limit / count;
                        for (int i = 0; i < count; i++)
                            SpawnVehicle(lanePos + dir * (-limit + (i + Random.Range(0.1f, 0.9f)) * step), dir, speed, limit);
                    }
                }
            }
    }

    void SpawnVehicle(Vector3 pos, Vector3 dir, float speed, float limit)
    {
        var root = new GameObject("Vehicle").transform;
        root.SetParent(tTraffic, false);
        root.position = pos; root.rotation = Quaternion.LookRotation(dir);
        Color glow = Neon();
        float r = Random.value;
        if (r < 0.52f) BuildCar(root, glow);
        else if (r < 0.88f) BuildAuto(root);
        else BuildBus(root, glow);
        var mv = root.gameObject.AddComponent<LoopingMover>();
        mv.dir = dir; mv.speed = speed; mv.limit = limit;
    }

    GameObject Part(Transform root, PrimitiveType t, Vector3 lp, Vector3 scale, Material m, bool shadows = false)
    {
        var g = Prim(t, root, "p", root.TransformPoint(lp), scale, m, false, false, shadows);
        g.transform.rotation = root.rotation;
        return g;
    }

    void BuildCar(Transform root, Color glow)
    {
        Material paint = Lit(Color.HSVToRGB(Mathf.Floor(Random.value * 6f) / 6f, 0.55f, 0.18f), 0.85f, 0.8f);
        Part(root, PrimitiveType.Cube, new Vector3(0, 0.65f, 0), new Vector3(1.9f, 0.7f, 4.3f), paint, true);
        Part(root, PrimitiveType.Cube, new Vector3(0, 1.2f, -0.2f), new Vector3(1.6f, 0.6f, 2.3f), Lit(new Color(0.02f, 0.03f, 0.05f), 0.95f, 0.5f));
        Material hl = Emissive(Color.white, 4f), tl = Emissive(new Color(1f, 0.05f, 0.05f), 3.5f);
        foreach (float sx in new[] { -0.65f, 0.65f })
        {
            Part(root, PrimitiveType.Cube, new Vector3(sx, 0.7f, 2.16f), new Vector3(0.45f, 0.15f, 0.1f), hl);
            Part(root, PrimitiveType.Cube, new Vector3(sx, 0.75f, -2.16f), new Vector3(0.45f, 0.15f, 0.1f), tl);
        }
        Part(root, PrimitiveType.Cube, new Vector3(0, 0.22f, 0), new Vector3(1.7f, 0.05f, 3.8f), Emissive(glow, 3f));
    }

    void BuildAuto(Transform root)
    {   // Bengaluru auto-rickshaw: yellow top, green body
        Material green = Lit(new Color(0.04f, 0.3f, 0.1f), 0.5f), yellow = Lit(new Color(0.95f, 0.75f, 0.05f), 0.5f);
        Part(root, PrimitiveType.Cube, new Vector3(0, 0.55f, 0), new Vector3(1.4f, 0.55f, 2.4f), green, true);
        Part(root, PrimitiveType.Cube, new Vector3(0, 1.3f, -0.15f), new Vector3(1.35f, 0.95f, 1.9f), Lit(new Color(0.03f, 0.04f, 0.04f), 0.7f));
        Part(root, PrimitiveType.Cube, new Vector3(0, 1.8f, 0f), new Vector3(1.5f, 0.1f, 2.3f), yellow);
        Material wheel = Lit(new Color(0.02f, 0.02f, 0.02f), 0.3f);
        foreach (Vector3 wp in new[] { new Vector3(0, 0.3f, 1.0f), new Vector3(-0.7f, 0.3f, -0.8f), new Vector3(0.7f, 0.3f, -0.8f) })
        {
            var w = Part(root, PrimitiveType.Cylinder, wp, new Vector3(0.6f, 0.1f, 0.6f), wheel);
            w.transform.rotation = root.rotation * Quaternion.Euler(0, 0, 90);
        }
        Part(root, PrimitiveType.Cube, new Vector3(0, 0.8f, 1.2f), new Vector3(0.3f, 0.2f, 0.1f), Emissive(Color.white, 4f));
        foreach (float sx in new[] { -0.5f, 0.5f }) Part(root, PrimitiveType.Cube, new Vector3(sx, 0.8f, -1.2f), new Vector3(0.2f, 0.15f, 0.1f), Emissive(new Color(1f, 0.05f, 0.05f), 3.5f));
        Part(root, PrimitiveType.Cube, new Vector3(0, 0.2f, 0), new Vector3(1.2f, 0.05f, 2f), Emissive(new Color(0.6f, 1f, 0.2f), 3f));
    }

    void BuildBus(Transform root, Color glow)
    {
        Part(root, PrimitiveType.Cube, new Vector3(0, 1.9f, 0), new Vector3(2.5f, 3f, 10f), Lit(new Color(0.08f, 0.14f, 0.3f), 0.6f, 0.4f), true);
        Part(root, PrimitiveType.Cube, new Vector3(0, 2.35f, 0), new Vector3(2.54f, 0.9f, 9f), Emissive(new Color(0.8f, 0.95f, 1f), 1.8f));
        Part(root, PrimitiveType.Cube, new Vector3(0, 0.75f, 0), new Vector3(2.54f, 0.15f, 10.02f), Emissive(glow, 3.5f));
        Part(root, PrimitiveType.Cube, new Vector3(0, 1.0f, 5.02f), new Vector3(1.8f, 0.25f, 0.1f), Emissive(Color.white, 4f));
        Part(root, PrimitiveType.Cube, new Vector3(0, 1.0f, -5.02f), new Vector3(1.8f, 0.2f, 0.1f), Emissive(new Color(1f, 0.05f, 0.05f), 3.5f));
    }

    // distant skyline so the grid does not just stop
    void BuildBackdrop()
    {
        var root = Root("Backdrop");
        for (int ring = 0; ring < 2; ring++)
        {
            float R = half + PITCH * 1.4f + 40f + ring * 70f;
            for (float t = -R; t <= R; t += 36f)
                for (int side = 0; side < 4; side++)
                {
                    Vector3 p = side == 0 ? new Vector3(t, 0, -R) : side == 1 ? new Vector3(t, 0, R) : side == 2 ? new Vector3(-R, 0, t) : new Vector3(R, 0, t);
                    p += new Vector3(Random.Range(-8f, 8f), 0, Random.Range(-8f, 8f));
                    float w = Random.value < 0.5f ? 24f : 36f; int floors = 4 * Random.Range(8, 24);
                    Prim(PrimitiveType.Cube, root, "FarTower", new Vector3(p.x, floors * FLOOR_H * 0.5f, p.z), new Vector3(w, floors * FLOOR_H, w),
                         BuildingMat(Random.Range(0, 6), w / 12f, floors / 4f));
                }
        }
    }

    // =====================================================================================
    // atmosphere
    void BuildEnvironment()
    {
        Color fogCol = new Color(0.07f, 0.04f, 0.16f);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.13f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogCol; RenderSettings.fogDensity = 0.0065f;

        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional; moon.color = new Color(0.4f, 0.5f, 1f); moon.intensity = 0.5f; moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(52, -35, 0);
        RenderSettings.sun = moon;

        var probe = new GameObject("CityReflectionProbe");
        probe.transform.position = new Vector3(0, 25f, 0);
        var rp = probe.AddComponent<ReflectionProbe>();
        rp.mode = ReflectionProbeMode.Realtime; rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        rp.size = new Vector3(half * 2f + 300f, 120f, half * 2f + 300f);
        rp.resolution = 256; rp.boxProjection = true; rp.intensity = 1.2f; rp.hdr = true;
        rp.clearFlags = ReflectionProbeClearFlags.SolidColor; rp.backgroundColor = fogCol; rp.farClipPlane = 600f;
    }

    void BuildPostFx()
    {
        string path = OUT + "/CyberpunkVolumeProfile.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.75f); bloom.intensity.Override(1.4f); bloom.scatter.Override(0.75f); bloom.tint.Override(new Color(1f, 0.85f, 1f));
        var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.2f); ca.contrast.Override(18f); ca.saturation.Override(12f);
        var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.32f); vig.smoothness.Override(0.5f);
        var chroma = profile.Add<ChromaticAberration>(true); chroma.intensity.Override(0.12f);

        foreach (var comp in profile.components) AssetDatabase.AddObjectToAsset(comp, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true; vol.sharedProfile = profile;
    }

    void BuildCamera()
    {
        float zMid = RoadCoord(blocks / 2);
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.07f, 0.04f, 0.16f);
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500f; cam.allowHDR = true;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camGo.transform.position = new Vector3(-half + 60f, 2.2f, zMid);
        camGo.transform.rotation = Quaternion.Euler(-4f, 90f, 0f);
        camGo.AddComponent<FreeFlyCamera>();

        // empty marker for your future player start
        var spawn = new GameObject("PlayerSpawn");
        spawn.transform.position = new Vector3(-half + 60f, 0.25f, zMid + 12f);
        spawn.transform.rotation = Quaternion.Euler(0, 90f, 0);

        if (rain)
        {
            var go = new GameObject("Rain");
            var ps = go.AddComponent<ParticleSystem>();
            var f = go.AddComponent<FollowTarget>(); f.target = camGo.transform; f.offset = new Vector3(0, 18f, 8f);
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.startLifetime = 1.3f; main.startSpeed = 0f; main.startSize = 0.035f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 7000; main.gravityModifier = 3f;
            main.startColor = new Color(0.7f, 0.85f, 1f, 0.35f);
            var em = ps.emission; em.rateOverTime = 3500f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(60f, 1f, 60f);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.04f; pr.lengthScale = 2.5f;
            pr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
