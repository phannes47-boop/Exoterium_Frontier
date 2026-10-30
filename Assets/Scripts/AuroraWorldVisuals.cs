using System;
using System.Collections.Generic;
using UnityEngine;

// Exoterium: Frontier – Aurora-Gürtel: rein visuelle 2.5D-Spielwelt.
// Fügt sich zur Laufzeit automatisch an FrontierGame an. Keine Änderungen am Kampfsystem.
[DisallowMultipleComponent]
public sealed class AuroraWorldVisuals : MonoBehaviour
{
    [Header("Weltraum")]
    [Range(0f, 1f)] public float NebulaIntensity = 0.62f;
    [Range(0, 240)] public int AsteroidCount = 145;
    [Range(0f, 1f)] public float AsteroidBrightness = 0.64f;
    public bool EnableAsteroids = false;
    public bool EnableNebula = true;

    const float Width = 2000f, Height = 1350f;
    GameObject visualRoot;
    GameObject observedMap;
    Texture2D backdrop;
    readonly List<Material> ownedMaterials = new List<Material>();
    readonly List<Mesh> ownedMeshes = new List<Mesh>();

    // Wird beim Start automatisch hinzugefügt; die existierende Frontier.unity bleibt unverändert.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToGame()
    {
        var game = FindFirstObjectByType<FrontierGame>();
        if (game != null && game.GetComponent<AuroraWorldVisuals>() == null)
            game.gameObject.AddComponent<AuroraWorldVisuals>();
    }

    void LateUpdate()
    {
        var map = GameObject.Find("Space Map");
        if (map == null || map == observedMap) return;
        observedMap = map;
        Rebuild();
    }

    void Rebuild()
    {
        if (visualRoot != null) Destroy(visualRoot);
        foreach (var m in ownedMaterials) if (m != null) Destroy(m);
        foreach (var m in ownedMeshes) if (m != null) Destroy(m);
        ownedMaterials.Clear(); ownedMeshes.Clear();
        if (backdrop != null) Destroy(backdrop);
        visualRoot = new GameObject("Aurora-Gürtel | Weltraum & Asteroiden (Visuals)");
        visualRoot.transform.SetParent(transform, false);

        // Die Weltkarte liegt auf y=-3 mit Oberkante y=-2.5. Overlay leicht darüber,
        // damit es den bestehenden Boden ersetzt, ohne Schiffe oder Schüsse zu verdecken.
        BuildBackdrop();
        // Asteroiden in diesem Teststand bewusst komplett deaktiviert.
    }

    // Update 15: Vollstaendig schwarzer Hintergrund, keine Texturen oder Sterne.
    // Asteroiden bleiben davon unberuehrt.
    void BuildPureBlackBackground()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        var material = new Material(shader) { name = "Aurora | Rein Schwarz" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.black);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.black);
        material.color = Color.black;
        ownedMaterials.Add(material);
        var mesh = new Mesh { name = "Aurora | Schwarzer Hintergrund" };
        mesh.vertices = new[] {
            new Vector3(0f, -2.38f, 0f), new Vector3(Width, -2.38f, 0f),
            new Vector3(0f, -2.38f, Height), new Vector3(Width, -2.38f, Height)
        };
        mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        mesh.RecalculateNormals();
        ownedMeshes.Add(mesh);
        var obj = new GameObject("Aurora | SCHWARZER Hintergrund (Alt)");
        obj.transform.SetParent(visualRoot.transform, false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    void BuildBackdrop()
    {
        const int texW = 1536, texH = 1024;
        backdrop = new Texture2D(texW, texH, TextureFormat.RGB24, false);
        backdrop.name = "Aurora Nebula & Stars (runtime)";
        backdrop.wrapMode = TextureWrapMode.Clamp;
        backdrop.filterMode = FilterMode.Point;
        var pixels = new Color[texW * texH];
        var rand = new System.Random(72419);
        // Perlin-Wolken mit 3 Schichten. Keine externen Texturen/Downloads nötig.
        for (int y = 0; y < texH; y++)
        for (int x = 0; x < texW; x++)
        {
            float u = (float)x / texW, v = (float)y / texH;
            float cloud = Mathf.PerlinNoise(u * 3.1f + 1.3f, v * 3.1f + 8.1f) * .58f
                        + Mathf.PerlinNoise(u * 8.4f + 11.3f, v * 8.4f + 5.2f) * .28f
                        + Mathf.PerlinNoise(u * 19.2f + 6.1f, v * 19.2f + 3.8f) * .14f;
            float band = Mathf.Exp(-Mathf.Pow((v - (.50f + .16f * Mathf.Sin(u * 7f))) * 3.2f, 2));
            float glow = Mathf.Pow(Mathf.Clamp01((cloud - .40f) * 2.5f), 2.4f) * band * NebulaIntensity;
            Color baseColor = new Color(.003f, .006f, .014f);
            Color blue = new Color(.025f, .12f, .31f);
            Color violet = new Color(.045f, .06f, .17f);
            Color tint = Color.Lerp(blue, violet, Mathf.PerlinNoise(u * 2.1f + 12f, v * 2.3f + 7f));
            pixels[y * texW + x] = baseColor + tint * glow;
        }
        // Mehrere Sterngrößen, festes Seed -> Karte sieht bei jedem Start gleich aus.
        for (int i = 0; i < 9000; i++)
        {
            int x = rand.Next(texW), y = rand.Next(texH);
            bool bright = rand.NextDouble() < .055;
            float value = bright ? .83f : .25f + (float)rand.NextDouble() * .36f;
            Color star = new Color(value * .77f, value * .87f, value);
            pixels[y * texW + x] = Color.Lerp(pixels[y * texW + x], star, bright ? .96f : .70f);
            if (bright && x > 1 && x < texW - 2 && y > 1 && y < texH - 2)
            {
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int p = (y + dy) * texW + x + dx;
                    pixels[p] = Color.Lerp(pixels[p], star, .14f);
                }
            }
        }
        backdrop.SetPixels(pixels); backdrop.Apply(false, true);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var mat = new Material(shader) { name = "Aurora Backdrop Material" };
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", backdrop);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", backdrop);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        mat.color = Color.white;
        ownedMaterials.Add(mat);
        var mesh = new Mesh { name = "Aurora Ground Quad" };
        mesh.vertices = new[] { new Vector3(0, -2.40f, 0), new Vector3(Width, -2.40f, 0), new Vector3(0, -2.40f, Height), new Vector3(Width, -2.40f, Height) };
        mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
        mesh.triangles = new[] { 0,2,1, 1,2,3 }; mesh.RecalculateNormals();
        ownedMeshes.Add(mesh);
        var o = new GameObject("Sternenhimmel + blau-violetter Nebel");
        o.transform.SetParent(visualRoot.transform, false);
        o.AddComponent<MeshFilter>().sharedMesh = mesh;
        o.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // FIX 11: PNG-Kacheln sind von oben nach unten nummeriert (Bildkoordinaten),
    // Unity-Welt-Z nimmt von unten nach oben zu. Daher 3-row beim Laden.
    // 16 unterschiedliche, nahtlose Ausschnitte eines zusammenhaengenden Atlas.
    // Keine Spiegelungen und keine Spielobjekte im Hintergrundbild.
    void BuildAurora10Tiles()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        for (int row = 0; row < 4; row++)
        for (int col = 0; col < 4; col++)
        {
            var texture = Resources.Load<Texture2D>("Aurora/Aurora10_" + col + "_" + (3 - row));
            if (texture == null) continue;
            var material = new Material(shader) { name = "Aurora 10 - Nebel " + col + "/" + row };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            ownedMaterials.Add(material);
            float x = col * Width * .25f, z = row * Height * .25f;
            float w = Width * .25f, h = Height * .25f;
            var mesh = new Mesh { name = "Aurora 10 Kachelmesh" };
            mesh.vertices = new[] { new Vector3(x,-2.38f,z), new Vector3(x+w,-2.38f,z),
                new Vector3(x,-2.38f,z+h), new Vector3(x+w,-2.38f,z+h) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0,2,1, 1,2,3 };
            mesh.RecalculateNormals();
            ownedMeshes.Add(mesh);
            var o = new GameObject("Aurora 10 Hintergrundkachel " + col + "-" + row);
            o.transform.SetParent(visualRoot.transform,false);
            o.AddComponent<MeshFilter>().sharedMesh = mesh;
            o.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    void BuildImportedBackdrop(Texture2D imported)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = "Aurora 4K Background - Unlit" };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", imported);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", imported);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        material.color = Color.white;
        ownedMaterials.Add(material);
        var mesh = new Mesh { name = "Aurora 4K Background Plane" };
        mesh.vertices = new[] { new Vector3(0,-2.38f,0), new Vector3(Width,-2.38f,0), new Vector3(0,-2.38f,Height), new Vector3(Width,-2.38f,Height) };
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        mesh.triangles = new[] { 0,2,1, 1,2,3 };
        mesh.RecalculateNormals();
        ownedMeshes.Add(mesh);
        var obj = new GameObject("Aurora 4K Weltraumtextur (keine Kollision)");
        obj.transform.SetParent(visualRoot.transform,false);
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        obj.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    void BuildAsteroids()
    {
        var random = new System.Random(4218);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var rocks = new Material[4];
        var colors = new[] { new Color(.22f,.25f,.31f), new Color(.28f,.25f,.29f), new Color(.21f,.28f,.30f), new Color(.32f,.29f,.30f) };
        for (int i = 0; i < rocks.Length; i++)
        {
            rocks[i] = new Material(shader) { name = "Aurora Rock " + i };
            rocks[i].color = colors[i] * AsteroidBrightness;
            ownedMaterials.Add(rocks[i]);
        }
        // Ein gemeinsames Low-Poly-Felsenmesh, dadurch wenig zusätzliche Last.
        var rockMesh = MakeRockMesh(); ownedMeshes.Add(rockMesh);
        for (int i = 0; i < AsteroidCount; i++)
        {
            float x = 15f + (float)random.NextDouble() * (Width - 30f);
            float z = 15f + (float)random.NextDouble() * (Height - 30f);
            // Keine Felsen direkt auf Station, Sprungtoren oder Spawn.
            if (Vector2.Distance(new Vector2(x,z), new Vector2(54,63)) < 34f ||
                Vector2.Distance(new Vector2(x,z), new Vector2(795,526.5f)) < 28f ||
                Vector2.Distance(new Vector2(x,z), new Vector2(45,43.5f)) < 28f) continue;
            float size = 1.1f + Mathf.Pow((float)random.NextDouble(), 2f) * 3.3f;
            var o = new GameObject("Asteroid " + (i+1));
            o.transform.SetParent(visualRoot.transform, false);
            o.transform.position = new Vector3(x, -1.4f + size*.16f, z);
            o.transform.rotation = Quaternion.Euler((float)random.NextDouble()*40, (float)random.NextDouble()*360, (float)random.NextDouble()*40);
            o.transform.localScale = new Vector3(size, size*(.65f+(float)random.NextDouble()*.35f), size*(.75f+(float)random.NextDouble()*.5f));
            o.AddComponent<MeshFilter>().sharedMesh = rockMesh;
            o.AddComponent<MeshRenderer>().sharedMaterial = rocks[random.Next(rocks.Length)];
            // Absichtlich kein Collider: Bewegung/Kampf bleiben identisch.
        }
    }

    static Mesh MakeRockMesh()
    {
        // Gedrungener Felsen mit 8 Breiten- und 5 Höhenringen statt Kristallspitzen.
        const int sides = 9, rings = 6;
        var vertices = new Vector3[sides * rings];
        var triangles = new int[(rings - 1) * sides * 6];
        var rand = new System.Random(48291);
        for (int r = 0; r < rings; r++)
        {
            float lat = Mathf.PI * (r + .25f) / (rings - .5f);
            for (int c = 0; c < sides; c++)
            {
                float lon = 2f * Mathf.PI * c / sides;
                float jitter = .79f + (float)rand.NextDouble() * .33f;
                vertices[r*sides+c] = new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon)*jitter,
                    Mathf.Cos(lat)*.72f*jitter, Mathf.Sin(lat)*Mathf.Sin(lon)*jitter);
            }
        }
        int k=0;
        for(int r=0;r<rings-1;r++)for(int c=0;c<sides;c++)
        {
            int a=r*sides+c,b=r*sides+(c+1)%sides,d=(r+1)*sides+c,e=(r+1)*sides+(c+1)%sides;
            triangles[k++]=a;triangles[k++]=d;triangles[k++]=b;
            triangles[k++]=b;triangles[k++]=d;triangles[k++]=e;
        }
        var mesh = new Mesh { name = "Unregelmaessiger Asteroid" };
        mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot);
        if (backdrop != null) Destroy(backdrop);
        foreach (var m in ownedMaterials) if (m != null) Destroy(m);
        foreach (var m in ownedMeshes) if (m != null) Destroy(m);
    }
}
