// Coup d'outil du renard (clip Renard_CoupOutil, couche BrasObjet), revu le 2026-11-19 après les retours de Gabin
// (« la pelle a une course trop grande, le renard se tord le poignet »). À lancer dans l'éditeur ouvert :
//   unity command script eval --project-path <projet> --file ArtSource/Renard/unity_coup_outil.cs
// Ne touche qu'au clip Renard_CoupOutil (la prise, la pose tenue et l'Animator viennent de unity_setup_prise_outil.cs).
//
// Principe : le poignet garde la pose « objet en main » (Renard_TenirObjet) et ne fait que se plier dans le plan
// avant-bras / manche (flexion limitée à ±30°, aucune torsion). C'est l'avant-bras qui donne la direction de l'outil :
// manche ≈ perpendiculaire à l'avant-bras (prise en poing), donc lame = angle de l'avant-bras + 90° + flexion.
// Angles dans le plan vertical avant / arrière du renard : 0 = devant, 90 = en haut, 180 = derrière, -90 = en bas.
// Arc de la lame : ≈ 150° (armé, au-dessus de l'épaule) → ≈ -10° (impact, devant) → ≈ -20° (accompagnement),
// au lieu d'environ 235° (lame dans le dos → 25° sous l'horizontale) avant.
// save = false : ne modifie rien, rend des aperçus dans le dossier temporaire et mesure la flexion du poignet.
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
bool save = false;
string outDir = Path.Combine(Path.GetTempPath(), "coup_outil");
var sb = new StringBuilder();
const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
const string Dir = "Assets/Animations/Characters/";
var allClips = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Models/Characters/Renard_Animations.fbx").OfType<AnimationClip>().Concat(AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Models/Characters/Renard.fbx").OfType<AnimationClip>()).Where(c => !c.name.StartsWith("__preview")).ToList();
AnimationClip Clip(string n) => allClips.First(c => c.name == n);
var attaque = Clip("Attaque"); var repos = Clip("Armature|Repos");
var holdClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "Renard_TenirObjet.anim");
var fistClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "Renard_MainFermee.anim");
var shovelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/P_Item_Shovel.prefab");

// Clés du coup : (temps, haut du bras, angle de l'avant-bras, flexion du poignet). Temps calés sur l'animation Attaque
// du corps (impact à 0,27 s = PlayerMeleeAttack.weaponHitDelay).
var keys = new (float t, Vector3 up, float fore, float flex)[] {
  (0.18f, new Vector3(0.55f, 0.40f, 0.72f), 50f, 12f),    // armé : main à côté du visage, lame en haut / arrière (≈ 150°, revu le 2026-11-19)
  (0.27f, new Vector3(0.20f, -0.50f, 0.85f), -65f, -30f), // impact : lame devant, presque à l'horizontale (≈ -5°)
  (0.40f, new Vector3(0.15f, -0.75f, 0.60f), -80f, -25f), // accompagnement : la lame continue vers le sol (≈ -20°)
};
const float ForeSide = 0.05f;  // avant-bras légèrement vers l'extérieur (bras droit)
const float BladeSide = 0.08f; // la lame reste dans le plan avant / arrière, un peu vers l'extérieur (roulis de l'avant-bras)
const float MaxRoll = 70f;     // roulis maximal de l'avant-bras pour y arriver

GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
root.hideFlags = HideFlags.DontSave;
Camera cam = null; RenderTexture rt = null;
try {
  root.transform.position = new Vector3(0, 500, 0); root.transform.rotation = Quaternion.identity;
  var anim = root.GetComponentInChildren<Animator>();
  var so = new SerializedObject(root.GetComponent<GrabObject>());
  var socket = (Transform)so.FindProperty("objectHolder").objectReferenceValue;
  var arm = anim.transform.Find("Armature/Bassin/Epaules/Epaule.R"); var fore = arm.Find("Avant_Bras.R"); var wrist = fore.Find("Poignet.R");
  string P(Transform t) => AnimationUtility.CalculateTransformPath(t, anim.transform);
  var sh = (GameObject)Object.Instantiate(shovelPrefab, socket);
  sh.transform.localPosition = so.FindProperty("handOffset").vector3Value;
  sh.transform.localEulerAngles = so.FindProperty("handRotation").vector3Value;
  foreach (var c in sh.GetComponentsInChildren<Collider>()) c.enabled = false;

  // 1) Pose tenue (bras, avant-bras, poignet) et axe de flexion du poignet
  repos.SampleAnimation(anim.gameObject, 0f); holdClip.SampleAnimation(anim.gameObject, 0f);
  var holdPose = new[]{ arm.localRotation, fore.localRotation, wrist.localRotation };
  Vector3 foreDir0 = (wrist.position - fore.position).normalized;
  Vector3 axisW = Vector3.Cross(foreDir0, sh.transform.up).normalized;
  Quaternion w0 = wrist.rotation;
  wrist.rotation = Quaternion.AngleAxis(20f, axisW) * w0;
  // flexion positive = lame qui part vers l'arrière de l'avant-bras (armé)
  if (Vector3.Dot(sh.transform.up, -foreDir0) < 0f) axisW = -axisW;
  wrist.rotation = w0;
  Vector3 axisLocal = Quaternion.Inverse(w0) * axisW;
  float bladeHold = Mathf.Atan2(Vector3.Dot(sh.transform.up, Vector3.up), Vector3.Dot(sh.transform.up, Vector3.forward)) * Mathf.Rad2Deg;
  float foreHold = Mathf.Atan2(foreDir0.y, foreDir0.z) * Mathf.Rad2Deg;
  sb.AppendLine($"tenue : avant-bras {foreHold:F0}°, lame {bladeHold:F0}° (écart {bladeHold - foreHold:F0}°)");

  Vector3 R(Vector3 v) => root.transform.TransformDirection(v.normalized);
  Vector3 ForeVec(float deg) => new Vector3(ForeSide, Mathf.Sin(deg * Mathf.Deg2Rad), Mathf.Cos(deg * Mathf.Deg2Rad));
  float lastRoll = 0f;
  void Pose(Vector3 upperDir, Vector3 foreDir, float flex) {
    // part de la pose tenue (même roulis), oriente le bras puis l'avant-bras au plus court, plie le poignet
    arm.localRotation = holdPose[0]; fore.localRotation = holdPose[1];
    arm.rotation = Quaternion.FromToRotation((fore.position - arm.position).normalized, upperDir) * arm.rotation;
    fore.rotation = Quaternion.FromToRotation((wrist.position - fore.position).normalized, foreDir) * fore.rotation;
    wrist.localRotation = holdPose[2] * Quaternion.AngleAxis(flex, axisLocal);
    // roulis de l'avant-bras (pronation / supination) : garde la lame dans le plan du coup au lieu de la tordre au poignet
    Quaternion f0 = fore.rotation; float best = 0f, bestErr = 999f;
    for (float r = -MaxRoll; r <= MaxRoll; r += 1f) {
      fore.rotation = Quaternion.AngleAxis(r, foreDir) * f0;
      float err = Mathf.Abs(root.transform.InverseTransformDirection(sh.transform.up).x - BladeSide) + Mathf.Abs(r) * 0.0005f;
      if (err < bestErr) { bestErr = err; best = r; }
    }
    fore.rotation = Quaternion.AngleAxis(best, foreDir) * f0;
    lastRoll = best;
  }

  // 2) Clip : tenue → armé → impact → accompagnement → tenue
  var times = new List<float>{ 0f }; var poses = new List<Quaternion[]>{ (Quaternion[])holdPose.Clone() };
  foreach (var k in keys) {
    attaque.SampleAnimation(anim.gameObject, k.t);
    Pose(R(k.up), R(ForeVec(k.fore)), k.flex);
    Vector3 b = sh.transform.up; Vector3 fd = (wrist.position - fore.position).normalized;
    sb.AppendLine($"t={k.t:F2} : avant-bras {Mathf.Atan2(fd.y, fd.z) * Mathf.Rad2Deg:F0}°, lame {Mathf.Atan2(b.y, b.z) * Mathf.Rad2Deg:F0}° (côté {b.x:F2}), roulis avant-bras {lastRoll:F0}°");
    times.Add(k.t); poses.Add(new[]{ arm.localRotation, fore.localRotation, wrist.localRotation });
  }
  times.Add(0.6f); poses.Add((Quaternion[])holdPose.Clone());
  var coupClip = new AnimationClip { frameRate = 30 };
  var bones = new[]{ arm, fore, wrist };
  for (int b = 0; b < 3; b++) {
    var cs = new[]{ new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
    Quaternion prev = poses[0][b];
    for (int i = 0; i < times.Count; i++) {
      Quaternion q = poses[i][b]; if (Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); prev = q;
      cs[0].AddKey(times[i], q.x); cs[1].AddKey(times[i], q.y); cs[2].AddKey(times[i], q.z); cs[3].AddKey(times[i], q.w);
    }
    string[] comp = { "x", "y", "z", "w" };
    for (int c = 0; c < 4; c++) {
      for (int i = 0; i < cs[c].length; i++) { AnimationUtility.SetKeyLeftTangentMode(cs[c], i, AnimationUtility.TangentMode.ClampedAuto); AnimationUtility.SetKeyRightTangentMode(cs[c], i, AnimationUtility.TangentMode.ClampedAuto); }
      coupClip.SetCurve(P(bones[b]), typeof(Transform), "localRotation." + comp[c], cs[c]);
    }
  }
  coupClip.EnsureQuaternionContinuity();

  // 3) Mesure sur tout le clip : flexion et torsion du poignet par rapport à la pose tenue, angle de la lame
  float maxBend = 0f, maxTwist = 0f, bMin = 999f, bMax = -999f;
  for (float t = 0f; t <= 0.6001f; t += 1f / 60f) {
    attaque.SampleAnimation(anim.gameObject, t); coupClip.SampleAnimation(anim.gameObject, t);
    Quaternion d = Quaternion.Inverse(holdPose[2]) * wrist.localRotation;
    d.ToAngleAxis(out float ang, out Vector3 ax); if (ang > 180f) ang -= 360f;
    Vector3 axn = ax.normalized;
    float twist = Mathf.Abs(ang) * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow(Vector3.Dot(axn, axisLocal), 2)));
    maxBend = Mathf.Max(maxBend, Mathf.Abs(ang)); maxTwist = Mathf.Max(maxTwist, twist);
    Vector3 bl = sh.transform.up; float ba = Mathf.Atan2(bl.y, bl.z) * Mathf.Rad2Deg; if (ba < -120f) ba += 360f;
    bMin = Mathf.Min(bMin, ba); bMax = Mathf.Max(bMax, ba);
  }
  sb.AppendLine($"poignet : flexion max {maxBend:F0}°, hors axe max {maxTwist:F1}° ; lame de {bMin:F0}° à {bMax:F0}° (arc {bMax - bMin:F0}°)");

  if (!save) {
    Directory.CreateDirectory(outDir);
    rt = new RenderTexture(360, 360, 24);
    var cgo = new GameObject("TmpPreviewCam"); cgo.hideFlags = HideFlags.HideAndDontSave;
    cam = cgo.AddComponent<Camera>(); cam.targetTexture = rt; cam.fieldOfView = 40; cam.enabled = false;
    var tex = new Texture2D(360, 360, TextureFormat.RGB24, false);
    var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "Renard_CoupOutil.anim");
    void Shot(AnimationClip arms, float t, Vector3 off, Vector3 look, string name) {
      attaque.SampleAnimation(anim.gameObject, t); arms.SampleAnimation(anim.gameObject, t); fistClip.SampleAnimation(anim.gameObject, 0f);
      cam.transform.position = look + off; cam.transform.LookAt(look);
      cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 360, 360), 0, 0); tex.Apply(); RenderTexture.active = null;
      File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
    }
    Vector3 body = root.transform.position + Vector3.up * 2.8f;
    foreach (var (c, tag) in new[]{ (old, "avant"), (coupClip, "apres") })
      foreach (float t in new[]{ 0.10f, 0.18f, 0.22f, 0.27f, 0.40f })
        Shot(c, t, new Vector3(9f, 0.5f, 1.5f), body, $"{tag}_cote_{(int)(t * 100):00}");
    foreach (var (c, tag) in new[]{ (old, "avant"), (coupClip, "apres") })
      foreach (float t in new[]{ 0.18f, 0.27f })
        Shot(c, t, new Vector3(1.5f, 1.5f, 9f), body, $"{tag}_face_{(int)(t * 100):00}");
    foreach (var (c, tag) in new[]{ (old, "avant"), (coupClip, "apres") })
      foreach (float t in new[]{ 0.18f, 0.27f }) {
        attaque.SampleAnimation(anim.gameObject, t); c.SampleAnimation(anim.gameObject, t);
        Vector3 w = wrist.position;
        Shot(c, t, new Vector3(2.8f, 0.6f, 1.2f), w, $"{tag}_poignet_{(int)(t * 100):00}");
      }
    sb.AppendLine("aperçus : " + outDir);
  } else {
    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "Renard_CoupOutil.anim");
    if (existing == null) AssetDatabase.CreateAsset(coupClip, Dir + "Renard_CoupOutil.anim");
    else { EditorUtility.CopySerialized(coupClip, existing); existing.name = "Renard_CoupOutil"; EditorUtility.SetDirty(existing); }
    AssetDatabase.SaveAssets();
    sb.AppendLine("Renard_CoupOutil enregistré");
  }
} finally {
  if (cam) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
  if (rt) Object.DestroyImmediate(rt);
  if (root) Object.DestroyImmediate(root);
}
return sb.ToString();
