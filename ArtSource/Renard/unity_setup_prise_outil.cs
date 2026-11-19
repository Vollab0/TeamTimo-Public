// Prise des objets en main du renard (2026-11-18), à lancer dans l'éditeur ouvert (puis unity_coup_outil.cs pour le coup d'outil) :
//   unity command script eval --project-path <projet> --file ArtSource/Renard/unity_setup_prise_outil.cs
// Calcule et enregistre : GrabObject.handOffset / handRotation (manche en travers de la paume, lame côté pouce),
// clips Renard_TenirObjet (bras, outil droit), Renard_CoupOutil (bras pendant l'attaque : armé derrière la tête,
// lame devant à l'impact 0,27 s), Renard_MainFermee (doigts), masques Renard_BrasDroit / Renard_DoigtsDroits,
// couches BrasObjet / MainObjet de PlayerAnimatorController, et relie l'action Attack (clic gauche) à PlayerController.Attack.
// Réglages : Curl (pliage des doigts), GripAlong / GripPalm (position du manche dans la patte), clés du coup (tableau keys).
// save = false : ne modifie rien, rend des gros plans de la main dans le dossier temporaire.
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
// SAVE et C:/Users/gabin/AppData/Local/Temp/claude/5d366edec4d2ca75/a18e491f-95f5-44e9-ba89-bc0be14be1af/scratchpad remplacés par sed avant exécution
bool save = true;
string outDir = System.IO.Path.GetTempPath(); // aperçus (mode save = false seulement)
var sb = new StringBuilder();
const string PlayerPath = "Assets/Prefabs/Characters/Player.prefab";
const string Dir = "Assets/Animations/Characters/";
const float Curl = 80f, GripAlong = 0.30f, GripPalm = -0.12f;
var allClips = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Models/Characters/Renard_Animations.fbx").OfType<AnimationClip>().Concat(AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Models/Characters/Renard.fbx").OfType<AnimationClip>()).Where(c => !c.name.StartsWith("__preview")).ToList();
AnimationClip Clip(string n) => allClips.First(c => c.name == n);
var shovelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/P_Item_Shovel.prefab");
var attaque = Clip("Attaque"); var repos = Clip("Armature|Repos");

GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
root.hideFlags = HideFlags.DontSave;
Camera cam = null; RenderTexture rt = null;
try {
  root.transform.position = new Vector3(0, 500, 0); root.transform.rotation = Quaternion.identity;
  var anim = root.GetComponentInChildren<Animator>();
  var so = new SerializedObject(root.GetComponent<GrabObject>());
  var socket = (Transform)so.FindProperty("objectHolder").objectReferenceValue;
  var arm = anim.transform.Find("Armature/Bassin/Epaules/Epaule.R"); var fore = arm.Find("Avant_Bras.R"); var wrist = fore.Find("Poignet.R");
  Transform idx = wrist.Find("Index.R"), maj = wrist.Find("Majeur.R"), ann = wrist.Find("Annulaire.R"), pou = wrist.Find("Pouce.R");
  string P(Transform t) => AnimationUtility.CalculateTransformPath(t, anim.transform);
  var sh = (GameObject)Object.Instantiate(shovelPrefab, socket);

  // 1) Prise anatomique (pose Repos) : manche en travers de la paume, lame côté pouce
  repos.SampleAnimation(anim.gameObject, 0f);
  Vector3 fdir = maj.up;
  Vector3 across = (ann.position - idx.position).normalized;
  // côté paume = sens dans lequel le bout du majeur part quand on le plie (Z local négatif, comme dans le Repos)
  Quaternion majBase = maj.localRotation;
  Vector3 tip0 = maj.position + maj.up * 0.3f;
  maj.localRotation = majBase * Quaternion.AngleAxis(-30f, Vector3.forward);
  Vector3 palm = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(maj.position + maj.up * 0.3f - tip0, fdir), across).normalized;
  maj.localRotation = majBase;
  Vector3 shaftUp = -across; // lame côté pouce / index
  Vector3 face0 = Vector3.ProjectOnPlane(fdir, shaftUp).normalized;
  // manche contre la paume (face intérieure à +0,10 m), au niveau de la base des doigts (0,31 m du poignet)
  sh.transform.SetPositionAndRotation(wrist.position + fdir * GripAlong + palm * GripPalm, Quaternion.LookRotation(face0, shaftUp));
  Vector3 handOffset = sh.transform.localPosition, handRotation = sh.transform.localEulerAngles;
  Quaternion shInWrist = Quaternion.Inverse(wrist.rotation) * sh.transform.rotation;
  sb.AppendLine("handOffset=" + handOffset.ToString("F4") + " handRotation=" + handRotation.ToString("F2"));

  // Doigts repliés autour du manche (Z local négatif), pouce un peu refermé
  var fingerPose = new Dictionary<Transform, Quaternion>();
  foreach (var f in new[]{ idx, maj, ann }) fingerPose[f] = f.localRotation * Quaternion.AngleAxis(-Curl, Vector3.forward);
  fingerPose[pou] = pou.localRotation * Quaternion.AngleAxis(-Curl * 0.5f, Vector3.forward);
  void ApplyFingers() { foreach (var kv in fingerPose) kv.Key.localRotation = kv.Value; }

  // 2) Solveur de pose du bras : directions visées dans le repère du joueur
  Vector3 R(Vector3 v) => root.transform.TransformDirection(v.normalized);
  Quaternion holdWristLocal = Quaternion.identity; bool haveHold = false;
  void Solve(Vector3 upperDir, Vector3 foreDir, Vector3 shaftDir) {
    arm.rotation = Quaternion.FromToRotation((fore.position - arm.position).normalized, upperDir) * arm.rotation;
    fore.rotation = Quaternion.FromToRotation((wrist.position - fore.position).normalized, foreDir) * fore.rotation;
    // roulis du poignet : le moins de torsion possible par rapport à la pose tenue (ou à l'avant-bras)
    Quaternion best = wrist.localRotation; float bestA = 999f;
    Vector3 n0 = Vector3.ProjectOnPlane(Vector3.up, shaftDir).sqrMagnitude > 0.01f ? Vector3.ProjectOnPlane(Vector3.up, shaftDir).normalized : Vector3.ProjectOnPlane(root.transform.forward, shaftDir).normalized;
    Quaternion refLocal = haveHold ? holdWristLocal : wrist.localRotation;
    for (int r = 0; r < 360; r += 3) {
      Vector3 n = Quaternion.AngleAxis(r, shaftDir) * n0;
      Quaternion wl = Quaternion.Inverse(fore.rotation) * (Quaternion.LookRotation(n, shaftDir) * Quaternion.Inverse(shInWrist));
      float a = Quaternion.Angle(wl, refLocal);
      if (a < bestA) { bestA = a; best = wl; }
    }
    wrist.localRotation = best;
  }
  // Pose tenue : bras un peu en avant, avant-bras devant, manche vertical, face de la lame vers l'avant
  void HoldPose() {
    Vector3 upper = Quaternion.AngleAxis(-20f, root.transform.right) * (fore.position - arm.position).normalized;
    arm.rotation = Quaternion.FromToRotation((fore.position - arm.position).normalized, upper) * arm.rotation;
    fore.rotation = Quaternion.FromToRotation((wrist.position - fore.position).normalized, R(new Vector3(0.25f, 0.05f, 1f))) * fore.rotation;
    wrist.rotation = Quaternion.FromToRotation(sh.transform.up, Vector3.up) * wrist.rotation;
    Vector3 fc = Vector3.ProjectOnPlane(sh.transform.forward, Vector3.up);
    wrist.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(fc, root.transform.forward, Vector3.up), Vector3.up) * wrist.rotation;
  }
  repos.SampleAnimation(anim.gameObject, 0f); HoldPose();
  holdWristLocal = wrist.localRotation; haveHold = true;
  var holdPose = new[]{ arm.localRotation, fore.localRotation, wrist.localRotation };

  // 3) Coup d'outil : clés (temps, haut du bras, avant-bras, manche vers la lame)
  var keys = new (float t, Vector3 up, Vector3 fo, Vector3 sd)[] {
    (0.18f, new Vector3(0.3f, 1f, -0.2f),  new Vector3(0.05f, 0.5f, -1f),  new Vector3(0f, -0.6f, -1f)),   // armé : main derrière la tête, lame dans le dos
    (0.27f, new Vector3(0.15f, -0.35f, 1f), new Vector3(0.05f, -0.55f, 1f), new Vector3(0f, -0.47f, 1f)),  // impact : lame devant, 25° sous l'horizontale
    (0.40f, new Vector3(0.15f, -0.7f, 0.7f), new Vector3(0.05f, -0.85f, 0.5f), new Vector3(0f, -0.8f, 0.6f)), // accompagnement
  };
  var coupClip = new AnimationClip { frameRate = 30 };
  var bones = new[]{ arm, fore, wrist };
  var times = new List<float>{ 0f }; var poses = new List<Quaternion[]>{ (Quaternion[])holdPose.Clone() };
  foreach (var k in keys) {
    attaque.SampleAnimation(anim.gameObject, k.t);
    Solve(R(k.up), R(k.fo), R(k.sd));
    times.Add(k.t); poses.Add(new[]{ arm.localRotation, fore.localRotation, wrist.localRotation });
  }
  times.Add(0.6f); poses.Add((Quaternion[])holdPose.Clone());
  for (int b = 0; b < 3; b++) {
    var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
    Quaternion prev = poses[0][b];
    for (int i = 0; i < times.Count; i++) {
      Quaternion q = poses[i][b]; if (Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); prev = q;
      cx.AddKey(times[i], q.x); cy.AddKey(times[i], q.y); cz.AddKey(times[i], q.z); cw.AddKey(times[i], q.w);
    }
    foreach (var c in new[]{cx, cy, cz, cw}) for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
    foreach (var c in new[]{cx, cy, cz, cw}) for (int i = 0; i < c.length; i++) AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
    coupClip.SetCurve(P(bones[b]), typeof(Transform), "localRotation.x", cx);
    coupClip.SetCurve(P(bones[b]), typeof(Transform), "localRotation.y", cy);
    coupClip.SetCurve(P(bones[b]), typeof(Transform), "localRotation.z", cz);
    coupClip.SetCurve(P(bones[b]), typeof(Transform), "localRotation.w", cw);
  }
  coupClip.EnsureQuaternionContinuity();

  // 4) Aperçu : corps = Attaque, bras = coup d'outil, doigts fermés
  rt = new RenderTexture(300, 300, 24);
  var cgo = new GameObject("TmpPreviewCam"); cgo.hideFlags = HideFlags.DontSave;
  cam = cgo.AddComponent<Camera>(); cam.targetTexture = rt; cam.fieldOfView = 38; cam.enabled = false;
  var tex = new Texture2D(300, 300, TextureFormat.RGB24, false); int shot = 0;
  void Shot(Vector3 off, Vector3 look, string tag) {
    cam.transform.position = root.transform.position + off; cam.transform.LookAt(look);
    cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 300, 300), 0, 0); tex.Apply(); RenderTexture.active = null;
    File.WriteAllBytes(Path.Combine(outDir, "tool_" + (shot++).ToString("00") + "_" + tag + ".png"), tex.EncodeToPNG());
  }
  Vector3 center = root.transform.position + Vector3.up * 3f;
  if (!save) {
    repos.SampleAnimation(anim.gameObject, 0f);
    arm.localRotation = holdPose[0]; fore.localRotation = holdPose[1]; wrist.localRotation = holdPose[2]; ApplyFingers();
    Vector3 g = sh.transform.position;
    sb.AppendLine("grip-wrist dist=" + Vector3.Distance(g, wrist.position).ToString("F2") + " fingerW=" + Vector3.Distance(idx.position, ann.position).ToString("F2"));
    foreach (var v in new[]{ new Vector3(2.6f, 0.3f, 0.3f), new Vector3(0.4f, 0.4f, 2.6f), new Vector3(-1.2f, 0.4f, 2.4f), new Vector3(1.6f, 1.8f, -1.2f) }) Shot(g - root.transform.position + v, g, "hand");
    attaque.SampleAnimation(anim.gameObject, 0.27f); coupClip.SampleAnimation(anim.gameObject, 0.27f); ApplyFingers();
    g = sh.transform.position;
    Shot(g - root.transform.position + new Vector3(2.6f, 0.6f, 0.3f), g, "impact");
    repos.SampleAnimation(anim.gameObject, 0f); ApplyFingers(); g = sh.transform.position; Shot(g - root.transform.position + new Vector3(2.6f, 0.3f, 0.6f), g, "repos");
  }
  if (save) {
    // Clips
    AnimationClip SaveClip(string name, AnimationClip src) {
      string path = Dir + name + ".anim";
      var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
      if (existing == null) { AssetDatabase.CreateAsset(src, path); return src; }
      EditorUtility.CopySerialized(src, existing); return existing;
    }
    AnimationClip ConstPose(Dictionary<Transform, Quaternion> pose, bool loop) {
      var c = new AnimationClip { frameRate = 30 };
      foreach (var kv in pose) {
        var q = kv.Value; string p = P(kv.Key);
        c.SetCurve(p, typeof(Transform), "localRotation.x", AnimationCurve.Constant(0f, 1f / 30f, q.x));
        c.SetCurve(p, typeof(Transform), "localRotation.y", AnimationCurve.Constant(0f, 1f / 30f, q.y));
        c.SetCurve(p, typeof(Transform), "localRotation.z", AnimationCurve.Constant(0f, 1f / 30f, q.z));
        c.SetCurve(p, typeof(Transform), "localRotation.w", AnimationCurve.Constant(0f, 1f / 30f, q.w));
      }
      var s = AnimationUtility.GetAnimationClipSettings(c); s.loopTime = loop; AnimationUtility.SetAnimationClipSettings(c, s);
      return c;
    }
    var holdClip = SaveClip("Renard_TenirObjet", ConstPose(new Dictionary<Transform, Quaternion>{ { arm, holdPose[0] }, { fore, holdPose[1] }, { wrist, holdPose[2] } }, true));
    // Le coup d'outil est calculé par unity_coup_outil.cs depuis le 2026-11-19 (arc réduit, poignet sans torsion) : ne pas l'écraser.
    coupClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Dir + "Renard_CoupOutil.anim") ?? SaveClip("Renard_CoupOutil", coupClip);
    var fistClip = SaveClip("Renard_MainFermee", ConstPose(fingerPose, true));
    // Masques
    var allPaths = anim.GetComponentsInChildren<Transform>(true).Where(t => t != anim.transform && !t.IsChildOf(socket)).Select(P).ToList();
    AvatarMask SaveMask(string name, HashSet<string> active) {
      string path = Dir + name + ".mask";
      var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
      if (m == null) { m = new AvatarMask(); AssetDatabase.CreateAsset(m, path); }
      m.transformCount = allPaths.Count;
      for (int i = 0; i < allPaths.Count; i++) { m.SetTransformPath(i, allPaths[i]); m.SetTransformActive(i, active.Contains(allPaths[i])); }
      EditorUtility.SetDirty(m); return m;
    }
    var armMask = SaveMask("Renard_BrasDroit", new HashSet<string>{ P(arm), P(fore), P(wrist) });
    var fistMask = SaveMask("Renard_DoigtsDroits", new HashSet<string>(fingerPose.Keys.Select(P)));
    // Animator
    var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Dir + "PlayerAnimatorController.controller");
    if (!ac.parameters.Any(p => p.name == "ToolSwing")) ac.AddParameter("ToolSwing", AnimatorControllerParameterType.Trigger);
    AnimatorControllerLayer GetLayer(string name) {
      var ls = ac.layers; var l = ls.FirstOrDefault(x => x.name == name);
      if (l == null) { ac.AddLayer(name); l = ac.layers.Last(); }
      return l;
    }
    void SetLayer(AnimatorControllerLayer l) { var ls = ac.layers; int i = System.Array.FindIndex(ls, x => x.name == l.name); ls[i] = l; ac.layers = ls; }
    var bras = GetLayer("BrasObjet");
    bras.avatarMask = armMask; bras.blendingMode = AnimatorLayerBlendingMode.Override; bras.defaultWeight = 0f;
    var sm = bras.stateMachine;
    var tenir = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "TenirObjet") ?? sm.AddState("TenirObjet");
    tenir.motion = holdClip; tenir.writeDefaultValues = true; sm.defaultState = tenir;
    var coup = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "CoupOutil") ?? sm.AddState("CoupOutil");
    coup.motion = coupClip; coup.writeDefaultValues = true; coup.tag = "CoupOutil";
    foreach (var t in sm.anyStateTransitions.Where(t => t.destinationState == coup).ToArray()) sm.RemoveAnyStateTransition(t);
    var any = sm.AddAnyStateTransition(coup); any.AddCondition(AnimatorConditionMode.If, 0, "ToolSwing"); any.duration = 0.05f; any.hasExitTime = false; any.canTransitionToSelf = true;
    foreach (var t in coup.transitions.ToArray()) coup.RemoveTransition(t);
    var back = coup.AddTransition(tenir); back.hasExitTime = true; back.exitTime = 1f; back.duration = 0f;
    SetLayer(bras);
    var main = GetLayer("MainObjet");
    main.avatarMask = fistMask; main.blendingMode = AnimatorLayerBlendingMode.Override; main.defaultWeight = 0f;
    var fistState = main.stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == "MainFermee") ?? main.stateMachine.AddState("MainFermee");
    fistState.motion = fistClip; fistState.writeDefaultValues = true; main.stateMachine.defaultState = fistState;
    SetLayer(main);
    EditorUtility.SetDirty(ac);
    // Prefab : prise + action Attack reliée au clic gauche
    var pr = PrefabUtility.LoadPrefabContents(PlayerPath);
    try {
      var so2 = new SerializedObject(pr.GetComponent<GrabObject>());
      so2.FindProperty("handOffset").vector3Value = handOffset;
      so2.FindProperty("handRotation").vector3Value = handRotation;
      so2.ApplyModifiedPropertiesWithoutUndo();
      var pi = pr.GetComponent<UnityEngine.InputSystem.PlayerInput>();
      var pc = pr.GetComponent<PlayerController>();
      var evt = pi.actionEvents.First(e => e.actionName.StartsWith("Gameplay/Attack"));
      bool bound = false;
      for (int i = 0; i < evt.GetPersistentEventCount(); i++) if (evt.GetPersistentTarget(i) == pc && evt.GetPersistentMethodName(i) == "Attack") bound = true;
      if (!bound) UnityEditor.Events.UnityEventTools.AddPersistentListener(evt, (UnityEngine.Events.UnityAction<UnityEngine.InputSystem.InputAction.CallbackContext>)System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<UnityEngine.InputSystem.InputAction.CallbackContext>), pc, "Attack"));
      sb.AppendLine("attack bound=" + (!bound ? "ajouté" : "déjà"));
      PrefabUtility.SaveAsPrefabAsset(pr, PlayerPath);
    } finally { PrefabUtility.UnloadPrefabContents(pr); }
    AssetDatabase.SaveAssets();
    sb.AppendLine("layers=" + string.Join(",", ac.layers.Select(l => l.name)));
  }
} finally {
  if (cam) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
  if (rt) Object.DestroyImmediate(rt);
  if (root) Object.DestroyImmediate(root);
}
return sb.ToString();
