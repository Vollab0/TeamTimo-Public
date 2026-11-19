// ABANDONNE le 2026-11-19 : Materiau_Renard utilise maintenant Renard_Pelage_Uni.png (unity_renard_pelage_uni.cs).
// Relancer ce script remettrait le pelage marbre et la normal map.
// Materiau du renard (pelage cartoon), a rejouer apres renard_fur_texture.py :
//   unity command script eval --file ArtSource/Renard/unity_setup_renard_material.cs --project-path <projet>
// Regle l'import de Renard_Pelage.png (couleur) et Renard_Pelage_Normal.png (normal map), puis met a jour
// Materiau_Renard (URP Lit, mat : brillance 0,15 au lieu de 0,5) et accorde les paupieres / sourcils.
// L'ancienne texture Renard.png est gardee (pour revenir en arriere : la remettre dans _BaseMap).
using UnityEditor;
using UnityEngine;

const string texDir = "Assets/Art/Textures/Characters/Renard/";
const string matDir = "Assets/Art/Materials/Characters/";
AssetDatabase.Refresh();

var ta = (TextureImporter)AssetImporter.GetAtPath(texDir + "Renard_Pelage.png");
ta.textureType = TextureImporterType.Default;
ta.sRGBTexture = true;
ta.maxTextureSize = 2048;
ta.mipmapEnabled = true;
ta.textureCompression = TextureImporterCompression.CompressedHQ;
ta.SaveAndReimport();
var tn = (TextureImporter)AssetImporter.GetAtPath(texDir + "Renard_Pelage_Normal.png");
tn.textureType = TextureImporterType.NormalMap;
tn.maxTextureSize = 2048;
tn.mipmapEnabled = true;
tn.textureCompression = TextureImporterCompression.CompressedHQ;
tn.SaveAndReimport();

var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Renard_Pelage.png");
var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + "Renard_Pelage_Normal.png");
var mat = AssetDatabase.LoadAssetAtPath<Material>(matDir + "Materiau_Renard.mat");
mat.SetTexture("_BaseMap", albedo);
mat.SetTexture("_MainTex", albedo);
mat.SetColor("_BaseColor", Color.white);
mat.SetColor("_Color", Color.white);
mat.SetTexture("_BumpMap", normal);
mat.SetFloat("_BumpScale", 1f);
mat.EnableKeyword("_NORMALMAP");
mat.SetFloat("_Smoothness", 0.15f);
mat.SetFloat("_Metallic", 0f);
mat.SetFloat("_SpecularHighlights", 1f);
EditorUtility.SetDirty(mat);

// Paupieres : orange moyen du dessus de la tete dans la nouvelle texture (sRGB 0,768 / 0,371 / 0,160)
var lid = AssetDatabase.LoadAssetAtPath<Material>(matDir + "Materiau_Paupières.mat");
var lidCol = new Color(0.768f, 0.371f, 0.160f, 1f);
lid.SetColor("_BaseColor", lidCol);
lid.SetColor("_Color", lidCol);
lid.SetFloat("_Smoothness", 0.15f);
EditorUtility.SetDirty(lid);
// Sourcils : creme du pelage (sRGB 0,949 / 0,890 / 0,804)
var brow = AssetDatabase.LoadAssetAtPath<Material>(matDir + "Materiau_Sourcils.mat");
var browCol = new Color(0.949f, 0.890f, 0.804f, 1f);
brow.SetColor("_BaseColor", browCol);
brow.SetColor("_Color", browCol);
brow.SetFloat("_Smoothness", 0.15f);
EditorUtility.SetDirty(brow);
// Truffe : reste noire et brillante (humide), un peu moins que 0,5
var nose = AssetDatabase.LoadAssetAtPath<Material>(matDir + "Materiau_Museau.mat");
nose.SetFloat("_Smoothness", 0.6f);
EditorUtility.SetDirty(nose);

AssetDatabase.SaveAssets();
return "Materiau_Renard : " + mat.GetTexture("_BaseMap").name + " + " + mat.GetTexture("_BumpMap").name + ", smoothness " + mat.GetFloat("_Smoothness");
