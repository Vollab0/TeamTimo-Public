// Configuration Unity du taureau, a rejouer apres un nouvel export de Bull.fbx :
//   unity command script eval --file ArtSource/Taureau/unity_setup_bull.cs --project-path <projet>
// Regle l'import du FBX et des textures, cree les materiaux jour / nuit, l'Animator Controller,
// puis installe le modele (enfant "Model") et la fumee de charge (enfant "ChargeSmoke") dans Bull.prefab.
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

var log = new System.Text.StringBuilder();
const string fbxPath = "Assets/Art/Models/Characters/Bull.fbx";
const string texDir = "Assets/Art/Textures/Characters/Taureau/";
const string matDir = "Assets/Art/Materials/Characters/Taureau";
const string ctrlPath = "Assets/Animations/Characters/Bull.controller";
const string prefabPath = "Assets/Prefabs/Characters/Bull.prefab";
const string smokeMatPath = "Assets/VFX/BullChargeSmoke.mat";

AssetDatabase.Refresh();

// ------------------------------------------------------------------ Textures
foreach (string n in new[] { "Bull_Albedo_Day", "Bull_Albedo_Night", "Bull_Emission_Night" })
{
    var ti = (TextureImporter)AssetImporter.GetAtPath(texDir + n + ".png");
    ti.textureType = TextureImporterType.Default;
    ti.sRGBTexture = true;
    ti.maxTextureSize = 2048;
    ti.mipmapEnabled = true;
    ti.textureCompression = TextureImporterCompression.CompressedHQ;
    ti.SaveAndReimport();
}

// ------------------------------------------------------------------ Materiaux
if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder("Assets/Art/Materials/Characters", "Taureau");
System.Func<string, string, string, Material> makeMat = (name, albedo, emission) =>
{
    string path = matDir + "/" + name + ".mat";
    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (mat == null)
    {
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        AssetDatabase.CreateAsset(mat, path);
    }
    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + albedo + ".png");
    mat.SetTexture("_BaseMap", tex);
    mat.SetTexture("_MainTex", tex);
    mat.SetColor("_BaseColor", Color.white);
    mat.SetFloat("_Smoothness", 0.2f);
    if (emission != null)
    {
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texDir + emission + ".png"));
        mat.SetColor("_EmissionColor", new Color(2.5f, 2.5f, 2.5f, 1f));
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    }
    EditorUtility.SetDirty(mat);
    return mat;
};
Material matDay = makeMat("Bull_Day", "Bull_Albedo_Day", null);
Material matNight = makeMat("Bull_Night", "Bull_Albedo_Night", "Bull_Emission_Night");

// ------------------------------------------------------------------ Import du FBX
var imp = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
imp.globalScale = 4f;                       // modele a taille "reelle" (2,2 m) -> echelle du jeu
imp.bakeAxisConversion = false;             // sinon le taureau regarde vers -Z
imp.importBlendShapes = false;
imp.importCameras = false;
imp.importLights = false;
imp.animationType = ModelImporterAnimationType.Generic;
imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Bull_Day"), matDay);
imp.importAnimation = true;
var loops = new HashSet<string> { "Idle", "Walk", "Turn", "Prepare", "Charge", "Stun" };
// "Base" ne sert qu'a donner au FBX sa pose par defaut (Idle) : pas de clip
var clipDefs = imp.defaultClipAnimations
    .Where(c => c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) != "Base").ToArray();
foreach (var c in clipDefs)
{
    c.name = c.takeName.Substring(c.takeName.LastIndexOf('|') + 1);
    c.loopTime = loops.Contains(c.name);
}
imp.clipAnimations = clipDefs;
imp.SaveAndReimport();

var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
    .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
log.AppendLine("clips: " + string.Join(", ", clips.Values.Select(c => c.name + " " + c.length.ToString("0.00") + "s" + (c.isLooping ? " boucle" : ""))));

// ------------------------------------------------------------------ Animator Controller
if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null) AssetDatabase.DeleteAsset(ctrlPath);
var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
ctrl.AddParameter("Turning", AnimatorControllerParameterType.Bool);
ctrl.AddParameter("WallImpact", AnimatorControllerParameterType.Bool);
ctrl.AddParameter("Preparing", AnimatorControllerParameterType.Bool);
ctrl.AddParameter("Charging", AnimatorControllerParameterType.Bool);
ctrl.AddParameter("Stunned", AnimatorControllerParameterType.Bool);
ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
var sm = ctrl.layers[0].stateMachine;

BlendTree tree;
AnimatorState loco = ctrl.CreateBlendTreeInController("Locomotion", out tree, 0);
tree.blendParameter = "Speed";
tree.useAutomaticThresholds = false;
tree.AddChild(clips["Idle"], 0f);
tree.AddChild(clips["Walk"], 2f);
tree.AddChild(clips["Walk"], 8f);
tree.AddChild(clips["Walk"], 11f);
var kids = tree.children;
// La meme marche (cycle de 0,67 s), plus ou moins rapide selon la vitesse de deplacement.
// Patrouille : 8 m/s le jour (vitesse 1), 10,4 m/s la nuit.
kids[1].timeScale = 0.45f;
kids[2].timeScale = 1.0f;
kids[3].timeScale = 1.3f;
tree.children = kids;

AnimatorState prepare = sm.AddState("Prepare"); prepare.motion = clips["Prepare"];
AnimatorState charge = sm.AddState("Charge"); charge.motion = clips["Charge"];
AnimatorState impact = sm.AddState("Impact"); impact.motion = clips["Impact"];
AnimatorState stun = sm.AddState("Stun"); stun.motion = clips["Stun"];
AnimatorState hit = sm.AddState("Hit"); hit.motion = clips["Hit"];
AnimatorState die = sm.AddState("Die"); die.motion = clips["Die"];
AnimatorState turn = sm.AddState("Turn"); turn.motion = clips["Turn"];
AnimatorState brake = sm.AddState("Brake"); brake.motion = clips["Brake"];
AnimatorState wake = sm.AddState("WakeUp"); wake.motion = clips["WakeUp"];
sm.defaultState = loco;

System.Func<AnimatorState, AnimatorState, float, AnimatorStateTransition> link = (from, to, duration) =>
{
    var t = from.AddTransition(to);
    t.hasExitTime = false;
    t.hasFixedDuration = true;
    t.duration = duration;
    return t;
};
AnimatorStateTransition tr;
// Arret : Impact (contre un mur, ou pare au bouclier) ou Brake (freine sur un joueur / en terrain ouvert), puis Stun
System.Action<AnimatorState, float> toStops = (from, duration) =>
{
    var t1 = link(from, impact, duration);
    t1.AddCondition(AnimatorConditionMode.If, 0, "Stunned");
    t1.AddCondition(AnimatorConditionMode.If, 0, "WallImpact");
    var t2 = link(from, brake, duration);
    t2.AddCondition(AnimatorConditionMode.If, 0, "Stunned");
    t2.AddCondition(AnimatorConditionMode.IfNot, 0, "WallImpact");
};
tr = link(loco, turn, 0.15f); tr.AddCondition(AnimatorConditionMode.If, 0, "Turning");
tr = link(turn, loco, 0.15f); tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Turning");
foreach (var from in new[] { loco, turn })
{
    tr = link(from, prepare, 0.12f); tr.AddCondition(AnimatorConditionMode.If, 0, "Preparing");
    tr = link(from, charge, 0.10f); tr.AddCondition(AnimatorConditionMode.If, 0, "Charging");
    toStops(from, 0.05f);
}
tr = link(prepare, charge, 0.08f); tr.AddCondition(AnimatorConditionMode.If, 0, "Charging");
toStops(prepare, 0.05f);
tr = link(prepare, loco, 0.20f);
tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Preparing");
tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Charging");
tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
toStops(charge, 0.03f);
tr = link(charge, loco, 0.15f);
tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Charging");
tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
tr = link(impact, stun, 0.05f); tr.hasExitTime = true; tr.exitTime = 0.95f;
tr = link(brake, stun, 0.05f); tr.hasExitTime = true; tr.exitTime = 0.95f;
// Reveil : secoue la tete quand l'etourdissement se termine, puis repart
foreach (var from in new[] { impact, brake, stun })
{
    tr = link(from, wake, 0.20f); tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");
}
tr = link(wake, loco, 0.15f); tr.hasExitTime = true; tr.exitTime = 0.95f;
toStops(wake, 0.05f);
tr = link(wake, prepare, 0.12f); tr.AddCondition(AnimatorConditionMode.If, 0, "Preparing");
tr = link(hit, stun, 0.10f); tr.hasExitTime = true; tr.exitTime = 0.9f; tr.AddCondition(AnimatorConditionMode.If, 0, "Stunned");
tr = link(hit, loco, 0.10f); tr.hasExitTime = true; tr.exitTime = 0.9f; tr.AddCondition(AnimatorConditionMode.IfNot, 0, "Stunned");

var anyDie = sm.AddAnyStateTransition(die);
anyDie.hasExitTime = false; anyDie.hasFixedDuration = true; anyDie.duration = 0.1f; anyDie.canTransitionToSelf = false;
anyDie.AddCondition(AnimatorConditionMode.If, 0, "Dead");
var anyHit = sm.AddAnyStateTransition(hit);
anyHit.hasExitTime = false; anyHit.hasFixedDuration = true; anyHit.duration = 0.05f; anyHit.canTransitionToSelf = true;
anyHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
anyHit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
EditorUtility.SetDirty(ctrl);

// ------------------------------------------------------------------ Materiau de la fumee de charge
// Meme style que la fumee de course du renard (SprintSmoke.vfx : spheres blanches eclairees).
// Le shader Smoke.shadergraph ne sert qu'au VFX Graph ; ici, l'equivalent pour un ParticleSystem.
var smokeMat = AssetDatabase.LoadAssetAtPath<Material>(smokeMatPath);
if (smokeMat == null)
{
    smokeMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
    AssetDatabase.CreateAsset(smokeMat, smokeMatPath);
}
else
{
    // repart d'un materiau propre (efface les reglages d'un ancien shader)
    var fresh = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
    smokeMat.shader = fresh.shader;
    smokeMat.CopyPropertiesFromMaterial(fresh);
    Object.DestroyImmediate(fresh);
}
smokeMat.SetColor("_BaseColor", Color.white);
smokeMat.SetFloat("_Smoothness", 0f);
smokeMat.SetFloat("_Metallic", 0f);
smokeMat.enableInstancing = true;
EditorUtility.SetDirty(smokeMat);

// ------------------------------------------------------------------ Prefab
var root = PrefabUtility.LoadPrefabContents(prefabPath);
foreach (string n in new[] { "Cube", "Capsule", "Model", "ChargeSmoke" })
{
    var old = root.transform.Find(n);
    if (old != null)
    {
        log.AppendLine("supprime l'enfant " + n + " [" + string.Join(", ", old.GetComponents<Component>().Select(c => c.GetType().Name)) + "]");
        Object.DestroyImmediate(old.gameObject);
    }
}
float rootScale = root.transform.localScale.x;
var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
model.name = "Model";
model.transform.localPosition = Vector3.zero;
model.transform.localRotation = Quaternion.identity;
model.transform.localScale = Vector3.one / rootScale;       // la racine est a l'echelle 5 : le modele reste a sa taille d'import
model.transform.SetAsFirstSibling();
var animator = model.GetComponent<Animator>();
if (animator == null) animator = model.AddComponent<Animator>();
animator.runtimeAnimatorController = ctrl;
animator.applyRootMotion = false;
animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

// Le FBX arrive dans la pose d'une autre animation : on pose le modele sur la premiere image de l'Idle
// (pose visible dans l'editeur ; en jeu, l'Animator pilote tous les os).
clips["Idle"].SampleAnimation(model, 0f);
// Verification : les animations sont bien differentes les unes des autres
var hipsT = model.GetComponentsInChildren<Transform>().First(t => t.name == "Hips");
var probe = new System.Text.StringBuilder("inclinaison du bassin (deg) : ");
foreach (string cn in new[] { "Idle", "Walk", "Prepare", "Charge", "Stun", "Die" })
{
    clips[cn].SampleAnimation(model, clips[cn].length * (cn == "Die" ? 1f : 0f));
    probe.Append(cn + " " + Vector3.Angle(hipsT.up, model.transform.up).ToString("0") + "  ");
}
log.AppendLine(probe.ToString());
clips["Idle"].SampleAnimation(model, 0f);

var renderers = model.GetComponentsInChildren<Renderer>(true);
foreach (var r in renderers) r.sharedMaterial = matDay;
var skinned = model.GetComponentInChildren<SkinnedMeshRenderer>();
skinned.updateWhenOffscreen = false;
log.AppendLine("bounds corps (local) : " + skinned.localBounds.size.ToString("0.00"));

var ai = root.GetComponent<BullAI>();
var soAi = new SerializedObject(ai);
soAi.FindProperty("animator").objectReferenceValue = animator;
soAi.FindProperty("stopOnFirstPlayerHit").boolValue = false;    // decide par Gabin le 2026-11-15 : il fonce jusqu'au mur
soAi.ApplyModifiedPropertiesWithoutUndo();

var night = root.GetComponent<EnemyNightMode>();
var soNight = new SerializedObject(night);
soNight.FindProperty("nightTint").colorValue = Color.white;       // le taureau a son propre materiau de nuit
var swaps = soNight.FindProperty("nightMaterialSwaps");
swaps.arraySize = renderers.Length;
for (int i = 0; i < renderers.Length; i++)
{
    var e = swaps.GetArrayElementAtIndex(i);
    e.FindPropertyRelative("renderer").objectReferenceValue = renderers[i];
    var mats = e.FindPropertyRelative("nightMaterials");
    mats.arraySize = 1;
    mats.GetArrayElementAtIndex(0).objectReferenceValue = matNight;
}
soNight.ApplyModifiedPropertiesWithoutUndo();

// Gabarit : le modele fait ~8,8 m de haut cornes comprises (placeholder : 10 m)
var capsule = root.GetComponent<CapsuleCollider>();
if (capsule != null) { capsule.height = 1.76f; capsule.center = new Vector3(0f, 0.88f, 0f); }
var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
if (agent != null) agent.height = 1.76f;
var transformSmoke = root.transform.Find("TransformSmoke");
if (transformSmoke != null) { transformSmoke.localPosition = new Vector3(0f, 0.9f, 0f); transformSmoke.localScale = Vector3.one * 0.55f; }

// Fumee de charge : systeme de particules en espace monde, alimente par BullChargeSmoke
var smokeGo = new GameObject("ChargeSmoke");
smokeGo.transform.SetParent(root.transform, false);
smokeGo.transform.localScale = Vector3.one / rootScale;     // echelle monde 1 : les tailles des bouffees sont en metres
var ps = smokeGo.AddComponent<ParticleSystem>();
var main = ps.main;
main.loop = true;
main.playOnAwake = true;
main.simulationSpace = ParticleSystemSimulationSpace.World;
main.scalingMode = ParticleSystemScalingMode.Hierarchy;
main.startSpeed = 0f;
main.startLifetime = 0.9f;
main.startSize = 4f;
main.maxParticles = 2000;
main.gravityModifier = 0f;
var emission = ps.emission;
emission.enabled = true;
emission.rateOverTime = 0f;                               // tout est emis par script (ParticleSystem.Emit)
var shape = ps.shape;
shape.enabled = false;
var limit = ps.limitVelocityOverLifetime;
limit.enabled = true;
limit.dampen = 0f;
limit.limit = 1000f;
limit.drag = 10f;
limit.multiplyDragByParticleSize = false;
limit.multiplyDragByParticleVelocity = false;
// Couleur et taille sur la vie : memes courbes que SprintSmoke.vfx (gonfle vite, retrecit, fonce a la fin)
var col = ps.colorOverLifetime;
col.enabled = true;
var grad = new Gradient();
grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 0.22f), new GradientColorKey(new Color(0.11f, 0.11f, 0.11f), 1f) },
             new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
col.color = grad;
var size = ps.sizeOverLifetime;
size.enabled = true;
size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.2f, 0.9f, 0f, 0f), new Keyframe(1f, 0f, 0f, 0f)));
var psr = smokeGo.GetComponent<ParticleSystemRenderer>();
psr.renderMode = ParticleSystemRenderMode.Mesh;
psr.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
psr.alignment = ParticleSystemRenderSpace.World;
psr.enableGPUInstancing = true;
psr.sharedMaterial = smokeMat;
psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
psr.receiveShadows = true;

var chargeSmoke = root.GetComponent<BullChargeSmoke>();
if (chargeSmoke == null) chargeSmoke = root.AddComponent<BullChargeSmoke>();
var soSmoke = new SerializedObject(chargeSmoke);
soSmoke.FindProperty("bull").objectReferenceValue = ai;
soSmoke.FindProperty("smoke").objectReferenceValue = ps;
soSmoke.FindProperty("sizeRange").vector2Value = new Vector2(1f, 2.2f);      // 2,5 - 5 m jugees beaucoup trop grosses
soSmoke.FindProperty("impactBurstCount").intValue = 30;
soSmoke.FindProperty("impactBurstSpeed").floatValue = 35f;
soSmoke.ApplyModifiedPropertiesWithoutUndo();

log.AppendLine("enfants : " + string.Join(", ", Enumerable.Range(0, root.transform.childCount).Select(i => root.transform.GetChild(i).name)));
log.AppendLine("composants racine : " + string.Join(", ", root.GetComponents<Component>().Select(c => c.GetType().Name)));
var muzzle = model.GetComponentsInChildren<Transform>().First(t => t.name == "Muzzle");
var head = model.GetComponentsInChildren<Transform>().First(t => t.name == "Head");
log.AppendLine("museau (local racine) : " + root.transform.InverseTransformPoint(muzzle.position).ToString("0.00") + " ; tete : " + root.transform.InverseTransformPoint(head.position).ToString("0.00"));

PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
PrefabUtility.UnloadPrefabContents(root);
AssetDatabase.SaveAssets();
return log.ToString();
