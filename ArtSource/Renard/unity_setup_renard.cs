// Configuration Unity des animations du renard, a rejouer apres un nouvel export de Renard_Animations.fbx :
//   unity command script eval --file ArtSource/Renard/unity_setup_renard.cs --project-path <projet>
// 1. Regle l'import de Renard_Animations.fbx (Generic, sans materiaux, clips renommes, boucles).
// 2. Reconstruit PlayerAnimatorController (meme asset : les references du prefab Player restent valides).
//    Idle = "Repos" et marche = "Marche" de Renard.fbx (anims de Gabin) ; le reste vient de Renard_Animations.fbx
//    (dont la course de Gabin avec un petit rebond, cf. renard_course_rebond.py).
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

var log = new System.Text.StringBuilder();
const string animFbx = "Assets/Art/Models/Characters/Renard_Animations.fbx";
const string modelFbx = "Assets/Art/Models/Characters/Renard.fbx";
const string ctrlPath = "Assets/Animations/Characters/PlayerAnimatorController.controller";
string[] loops = { "Course", "Chute", "AuSol", "Ramper" };

AssetDatabase.Refresh();

// ------------------------------------------------------------------ Import du FBX d'animations
var mi = (ModelImporter)AssetImporter.GetAtPath(animFbx);
mi.animationType = ModelImporterAnimationType.Generic;
mi.materialImportMode = ModelImporterMaterialImportMode.None;
mi.bakeAxisConversion = false;
mi.useFileScale = true;
mi.globalScale = 1f;
mi.importAnimation = true;
mi.resampleCurves = true;
mi.animationCompression = ModelImporterAnimationCompression.KeyframeReductionAndCompression;
var takes = mi.defaultClipAnimations;
var clipsCfg = new List<ModelImporterClipAnimation>();
foreach (var take in takes)
{
    string shortName = take.takeName.Contains("|") ? take.takeName.Substring(take.takeName.LastIndexOf('|') + 1) : take.takeName;
    take.name = shortName;
    take.loopTime = loops.Contains(shortName);
    take.loopPose = false;
    take.keepOriginalPositionY = true;
    take.keepOriginalOrientation = true;
    take.keepOriginalPositionXZ = true;
    clipsCfg.Add(take);
}
mi.clipAnimations = clipsCfg.ToArray();
mi.SaveAndReimport();

var clips = AssetDatabase.LoadAllAssetsAtPath(animFbx).OfType<AnimationClip>()
    .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
var modelClips = AssetDatabase.LoadAllAssetsAtPath(modelFbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
var idle = modelClips.First(c => c.name.EndsWith("|Repos"));
var walk = modelClips.First(c => c.name.EndsWith("|Marche"));
log.AppendLine("Clips : " + string.Join(", ", clips.Keys) + " + " + idle.name + ", " + walk.name);

// ------------------------------------------------------------------ Animator Controller
var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
var sm = ctrl.layers[0].stateMachine;
foreach (var st in sm.states.ToArray()) sm.RemoveState(st.state);
foreach (var t in sm.anyStateTransitions.ToArray()) sm.RemoveAnyStateTransition(t);
foreach (var t in sm.entryTransitions.ToArray()) sm.RemoveEntryTransition(t);
foreach (var p in ctrl.parameters.ToArray()) ctrl.RemoveParameter(p);
foreach (var n in new[] { "isWalking", "isSprinting", "isJumping", "isFalling", "isGrounded", "isAttacking", "isHolding", "isDowned" })
    ctrl.AddParameter(n, AnimatorControllerParameterType.Bool);
foreach (var n in new[] { "Attack", "Push", "Hit", "Roll", "Downed", "Die", "Revive" })
    ctrl.AddParameter(n, AnimatorControllerParameterType.Trigger);

// Vitesses de lecture d'origine (Gabin) : Marche x2, Course x1,5 sur 11 images a 24 i/s = 0,306 s par cycle.
// La course avec rebond dure 0,5 s (cycle complet de 12 images reechantillonne) : x1,64 garde le meme rythme.
var speeds = new Dictionary<string, float> { { "Marche", 2f }, { "Course", 1.64f } };
var states = new Dictionary<string, AnimatorState>();
AnimatorState Add(string name, Motion motion, Vector3 pos)
{
    var st = sm.AddState(name, pos);
    st.motion = motion;
    st.speed = speeds.TryGetValue(name, out float sp) ? sp : 1f;
    st.writeDefaultValues = true;
    states[name] = st;
    return st;
}
Add("Repos", idle, new Vector3(300, 0, 0));
Add("Marche", walk, new Vector3(550, -80, 0));
Add("Course", clips["Course"], new Vector3(550, 80, 0));
Add("Saut", clips["Saut"], new Vector3(300, 220, 0));
Add("Chute", clips["Chute"], new Vector3(550, 300, 0));
Add("Atterrissage", clips["Atterrissage"], new Vector3(800, 220, 0));
Add("Attaque", clips["Attaque"], new Vector3(50, -220, 0));
Add("Poussee", clips["Poussee"], new Vector3(250, -260, 0));
Add("Degats", clips["Degats"], new Vector3(450, -260, 0));
Add("Roulade", clips["Roulade"], new Vector3(650, -260, 0));
// A terre puis mort (GDD Sante_et_Degats) : effondrement, au sol / ramper pendant ~30 s, relever ou mourir
Add("Effondrement", clips["Effondrement"], new Vector3(1050, -100, 0));
Add("AuSol", clips["AuSol"], new Vector3(1300, -40, 0));
Add("Ramper", clips["Ramper"], new Vector3(1300, 120, 0));
Add("Mort", clips["Mort"], new Vector3(1550, 40, 0));
Add("Relever", clips["Relever"], new Vector3(1050, 120, 0));
sm.defaultState = states["Repos"];
sm.anyStatePosition = new Vector3(50, -60, 0);

AnimatorStateTransition Tr(string from, string to, float duration, float exitTime = -1f, params (string, bool)[] conds)
{
    var t = states[from].AddTransition(states[to]);
    t.hasFixedDuration = true;
    t.duration = duration;
    t.hasExitTime = exitTime >= 0f;
    if (exitTime >= 0f) t.exitTime = exitTime;
    // Interruption ordonnee par la source : seules les transitions declarees AVANT celle-ci (plus prioritaires)
    // peuvent la couper. D'ou Saut / Chute declares en premier ci-dessous.
    t.interruptionSource = TransitionInterruptionSource.Source;
    t.orderedInterruption = true;
    foreach (var (p, v) in conds) t.AddCondition(v ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p);
    return t;
}
void Any(string to, string trigger, float duration)
{
    var t = sm.AddAnyStateTransition(states[to]);
    t.hasFixedDuration = true;
    t.duration = duration;
    t.hasExitTime = false;
    t.canTransitionToSelf = false;
    t.AddCondition(AnimatorConditionMode.If, 0, trigger);
}

// Saut / chute AVANT la locomotion (2026-11-19) : en sprint, isSprinting repasse a faux des que le renard
// decolle (PlayerController.SprintSmoke) ; si Course->Marche passait avant, elle coupait Course->Saut et le
// renard restait en marche (bras le long du corps) en l'air avant de passer en Saut / Chute.
foreach (var g in new[] { "Repos", "Marche", "Course", "Atterrissage" }) Tr(g, "Saut", 0.05f, -1, ("isJumping", true));
foreach (var g in new[] { "Repos", "Marche", "Course" }) Tr(g, "Chute", 0.15f, -1, ("isFalling", true));
// Locomotion au sol
Tr("Repos", "Marche", 0.12f, -1, ("isWalking", true), ("isSprinting", false));
Tr("Repos", "Course", 0.10f, -1, ("isWalking", true), ("isSprinting", true));
Tr("Marche", "Repos", 0.15f, -1, ("isWalking", false));
Tr("Marche", "Course", 0.12f, -1, ("isSprinting", true));
Tr("Course", "Marche", 0.15f, -1, ("isSprinting", false), ("isWalking", true));
Tr("Course", "Repos", 0.15f, -1, ("isWalking", false));
// Atterrissage
Tr("Saut", "Atterrissage", 0.05f, -1, ("isGrounded", true), ("isJumping", false));
Tr("Saut", "Chute", 0.12f, -1, ("isFalling", true));
// Fin du clip Saut encore en l'air : Chute tout de suite, sans attendre le sommet du saut
Tr("Saut", "Chute", 0.10f, 0.9f, ("isGrounded", false));
Tr("Chute", "Atterrissage", 0.05f, -1, ("isGrounded", true));
Tr("Atterrissage", "Course", 0.15f, 0.35f, ("isSprinting", true));
Tr("Atterrissage", "Marche", 0.15f, 0.35f, ("isWalking", true));
Tr("Atterrissage", "Repos", 0.15f, 0.9f);
// Actions (depuis n'importe quel etat), puis retour a la locomotion
Any("Attaque", "Attack", 0.05f);
Any("Poussee", "Push", 0.05f);
Any("Degats", "Hit", 0.04f);
Any("Roulade", "Roll", 0.05f);
foreach (var a in new[] { "Attaque", "Poussee", "Degats", "Roulade" })
{
    float walkExit = a == "Degats" ? 0.6f : 0.75f;
    Tr(a, "Course", 0.15f, walkExit, ("isSprinting", true));
    Tr(a, "Marche", 0.15f, walkExit, ("isWalking", true));
    Tr(a, "Repos", 0.2f, 0.88f);
}

// A terre : declencheurs envoyes par AnimationStateController (evenements de PlayerHealth)
Any("Effondrement", "Downed", 0.1f);
Tr("Effondrement", "Ramper", 0.25f, 0.85f, ("isWalking", true));
Tr("Effondrement", "AuSol", 0.2f, 0.95f);
Tr("AuSol", "Ramper", 0.25f, -1, ("isWalking", true));
Tr("Ramper", "AuSol", 0.3f, -1, ("isWalking", false));
Any("Mort", "Die", 0.25f);
Any("Relever", "Revive", 0.15f);
Tr("Relever", "Marche", 0.15f, 0.8f, ("isWalking", true));
Tr("Relever", "Repos", 0.2f, 0.9f);

EditorUtility.SetDirty(ctrl);
AssetDatabase.SaveAssets();
log.AppendLine("Etats : " + string.Join(", ", sm.states.Select(s => s.state.name)));
log.AppendLine("Parametres : " + string.Join(", ", ctrl.parameters.Select(p => p.name)));
return log.ToString();
