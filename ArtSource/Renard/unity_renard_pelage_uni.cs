// Pelage du renard en couleur unie + leger degrade vertical (remplace le pelage « meches » de renard_fur_texture.py,
// juge marbre / granuleux par Gabin le 2026-11-19). Tout se fait dans Unity, sans Blender :
//   unity command script eval --file ArtSource/Renard/unity_renard_pelage_uni.cs --project-path <projet>
// 1. relit Renard.png (texture d'origine en aplats : orange 242/113/68, creme, coussinets roses, trait de la bouche) ;
// 2. recolore l'orange en un degrade doux selon la hauteur du sommet sur le renard au repos (plus clair en haut,
//    un peu plus fonce vers les pieds), la creme en creme du pelage, garde les autres couleurs et les bords lisses ;
// 3. ecrit Renard_Pelage_Uni.png (2048) et l'assigne a Materiau_Renard, sans normal map (mat, brillance 0,15).
// Changer les teintes = modifier TOP / BOTTOM / CREAM ci-dessous puis relancer.
using System.IO;
using UnityEditor;
using UnityEngine;

const string texDir = "Assets/Art/Textures/Characters/Renard/";
const string outName = "Renard_Pelage_Uni.png";
const int RES = 2048;
var TOP = new Color(0xD4 / 255f, 0x6A / 255f, 0x2F / 255f);     // haut de la tete / oreilles
var BOTTOM = new Color(0xB8 / 255f, 0x57 / 255f, 0x28 / 255f);  // pieds
var CREAM = new Color(0xF2 / 255f, 0xE3 / 255f, 0xCD / 255f);   // ventre, museau, bout de la queue (= Materiau_Sourcils)
var ORANGE_OLD = new Color(242 / 255f, 113 / 255f, 68 / 255f);
// couleurs-cles de Renard.png : creme (recoloree), coussinets roses et trait de la bouche (gardes)
var keysOld = new[] { new Color(252 / 255f, 229 / 255f, 204 / 255f), new Color(224 / 255f, 159 / 255f, 155 / 255f), new Color(138 / 255f, 126 / 255f, 112 / 255f) };
var keysNew = new[] { CREAM, keysOld[1], keysOld[2] };

// --- ancienne texture, ramenee a RES par moyenne de blocs (garde l'anticrenelage des bords)
var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
src.LoadImage(File.ReadAllBytes(texDir + "Renard.png"));
int step = src.width / RES;
var sp = src.GetPixels32();
var old = new Color[RES * RES];
for (int y = 0; y < RES; y++)
    for (int x = 0; x < RES; x++)
    {
        float r = 0, g = 0, b = 0;
        for (int dy = 0; dy < step; dy++)
            for (int dx = 0; dx < step; dx++)
            {
                var c = sp[(y * step + dy) * src.width + x * step + dx];
                r += c.r; g += c.g; b += c.b;
            }
        float k = 255f * step * step;
        old[y * RES + x] = new Color(r / k, g / k, b / k);
    }
Object.DestroyImmediate(src);

// --- hauteur de chaque texel sur le renard au repos (rasterisation des UV du mesh du prefab)
var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
SkinnedMeshRenderer smr = null;
foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
    if (s.sharedMaterial != null && s.sharedMaterial.name == "Materiau_Renard") smr = s;
var mesh = smr.sharedMesh;
var verts = mesh.vertices;
var uvs = mesh.uv;
var tris = mesh.triangles;
var hv = new float[verts.Length];
float hMin = float.MaxValue, hMax = float.MinValue;
for (int i = 0; i < verts.Length; i++)
{
    hv[i] = root.transform.InverseTransformPoint(smr.transform.TransformPoint(verts[i])).y;
    hMin = Mathf.Min(hMin, hv[i]);
    hMax = Mathf.Max(hMax, hv[i]);
}
var H = new float[RES * RES];
var best = new float[RES * RES];
for (int i = 0; i < best.Length; i++) best[i] = float.NegativeInfinity;
const float PAD = 0.75f;
for (int t = 0; t < tris.Length; t += 3)
{
    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
    float x0 = uvs[a].x * RES, y0 = uvs[a].y * RES, x1 = uvs[b].x * RES, y1 = uvs[b].y * RES, x2 = uvs[c].x * RES, y2 = uvs[c].y * RES;
    float den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2);
    if (Mathf.Abs(den) < 1e-9f) continue;
    float a2 = Mathf.Abs(den);
    float e0 = Mathf.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
    float e1 = Mathf.Sqrt((x0 - x2) * (x0 - x2) + (y0 - y2) * (y0 - y2));
    float e2 = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
    if (e0 < 1e-6f || e1 < 1e-6f || e2 < 1e-6f) continue;
    int xa = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, Mathf.Min(x1, x2)) - PAD));
    int xb = Mathf.Min(RES - 1, Mathf.CeilToInt(Mathf.Max(x0, Mathf.Max(x1, x2)) + PAD));
    int ya = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, Mathf.Min(y1, y2)) - PAD));
    int yb = Mathf.Min(RES - 1, Mathf.CeilToInt(Mathf.Max(y0, Mathf.Max(y1, y2)) + PAD));
    for (int py = ya; py <= yb; py++)
        for (int px = xa; px <= xb; px++)
        {
            float X = px + 0.5f, Y = py + 0.5f;
            float w0 = ((y1 - y2) * (X - x2) + (x2 - x1) * (Y - y2)) / den;
            float w1 = ((y2 - y0) * (X - x2) + (x0 - x2) * (Y - y2)) / den;
            float w2 = 1f - w0 - w1;
            float sc = Mathf.Min(w0 * a2 / e0, Mathf.Min(w1 * a2 / e1, w2 * a2 / e2));
            int id = py * RES + px;
            if (sc <= -PAD || sc <= best[id]) continue;
            best[id] = sc;
            H[id] = w0 * hv[a] + w1 * hv[b] + w2 * hv[c];
        }
}
// dilatation des hauteurs dans les marges entre ilots UV (evite des coutures avec les mipmaps)
var filled = new bool[RES * RES];
int nFilled = 0;
for (int i = 0; i < filled.Length; i++) if (best[i] > float.NegativeInfinity) { filled[i] = true; nFilled++; }
for (int it = 0; it < 16; it++)
{
    var nf = (bool[])filled.Clone();
    for (int y = 0; y < RES; y++)
        for (int x = 0; x < RES; x++)
        {
            int id = y * RES + x;
            if (filled[id]) continue;
            float sum = 0; int n = 0;
            if (x > 0 && filled[id - 1]) { sum += H[id - 1]; n++; }
            if (x < RES - 1 && filled[id + 1]) { sum += H[id + 1]; n++; }
            if (y > 0 && filled[id - RES]) { sum += H[id - RES]; n++; }
            if (y < RES - 1 && filled[id + RES]) { sum += H[id + RES]; n++; }
            if (n > 0) { H[id] = sum / n; nf[id] = true; }
        }
    filled = nf;
}

// --- composition des couleurs
float Smooth(float e0, float e1, float x) { float u = Mathf.Clamp01((x - e0) / (e1 - e0)); return u * u * (3 - 2 * u); }
Vector3 V(Color c) => new Vector3(c.r, c.g, c.b);
var outPx = new Color32[RES * RES];
var o = V(ORANGE_OLD);
for (int i = 0; i < outPx.Length; i++)
{
    float hn = filled[i] ? (H[i] - hMin) / (hMax - hMin) : 0.5f;
    var bas = Color.Lerp(BOTTOM, TOP, Smooth(0.0f, 0.9f, hn));
    var p = V(old[i]);
    // melange orange -> couleur-cle la plus proche (bords anticrenelés)
    int kBest = -1; float tBest = 0, dBest = float.MaxValue;
    for (int k = 0; k < keysOld.Length; k++)
    {
        var seg = V(keysOld[k]) - o;
        float tk = Mathf.Clamp01(Vector3.Dot(p - o, seg) / seg.sqrMagnitude);
        float dk = (p - (o + tk * seg)).magnitude;
        if (dk < dBest) { dBest = dk; tBest = tk; kBest = k; }
    }
    Color col;
    if (dBest < 0.08f) col = Color.Lerp(bas, keysNew[kBest], tBest);
    else col = old[i];                     // couleur inconnue : gardee telle quelle
    outPx[i] = col;
}
var tex = new Texture2D(RES, RES, TextureFormat.RGB24, false);
tex.SetPixels32(outPx);
tex.Apply();
File.WriteAllBytes(texDir + outName, tex.EncodeToPNG());
Object.DestroyImmediate(tex);

// --- import et materiau
AssetDatabase.ImportAsset(texDir + outName, ImportAssetOptions.ForceSynchronousImport);
var ti = (TextureImporter)AssetImporter.GetAtPath(texDir + outName);
ti.textureType = TextureImporterType.Default;
ti.sRGBTexture = true;
ti.maxTextureSize = 2048;
ti.mipmapEnabled = true;
ti.textureCompression = TextureImporterCompression.CompressedHQ;
ti.SaveAndReimport();
var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + outName);
var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Characters/Materiau_Renard.mat");
mat.SetTexture("_BaseMap", albedo);
mat.SetTexture("_MainTex", albedo);
mat.SetColor("_BaseColor", Color.white);
mat.SetColor("_Color", Color.white);
mat.SetTexture("_BumpMap", null);
mat.DisableKeyword("_NORMALMAP");
mat.SetFloat("_Smoothness", 0.15f);
mat.SetFloat("_Metallic", 0f);
EditorUtility.SetDirty(mat);
AssetDatabase.SaveAssets();
return "texels ilots " + nFilled + ", hauteur " + hMin.ToString("F2") + " .. " + hMax.ToString("F2") + " ; Materiau_Renard : " + mat.GetTexture("_BaseMap").name + ", normal map " + (mat.GetTexture("_BumpMap") ? "oui" : "non");
