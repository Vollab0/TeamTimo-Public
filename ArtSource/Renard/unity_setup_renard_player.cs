// Prefab Player : roulade et ragdoll du renard (a rejouer si le prefab est reconstruit) :
//   unity command script eval --file ArtSource/Renard/unity_setup_renard_player.cs --project-path <projet>
// 1. PlayerInput : branche l'action Roll (Ctrl gauche / bouton Est) sur PlayerController.Roll.
// 2. Ragdoll : Rigidbody + collider + CharacterJoint sur 9 os (bassin, poitrine, tete, bras, cuisses, tibias).
//    RagdollController les garde inertes (cinematiques, colliders coupes) tant que le joueur vit ; apres
//    l'animation de mort, il les libere : le corps tombe et pourra etre porte par les allies.
//    Les positions sont donnees dans l'espace de l'armature Blender (X = gauche du renard, Y = arriere, Z = haut).
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.InputSystem;

const string prefabPath = "Assets/Prefabs/Characters/Player.prefab";
var log = new System.Text.StringBuilder();
AssetDatabase.Refresh();
var root = PrefabUtility.LoadPrefabContents(prefabPath);

// ------------------------------------------------------------------ 1. Roulade
var pi = root.GetComponent<PlayerInput>();
var pc = root.GetComponent<PlayerController>();
var rollAction = pi.actions.FindAction("Gameplay/Roll", true);
var events = pi.actionEvents.ToList();
var rollEvent = events.FirstOrDefault(e => e.actionId == rollAction.id.ToString());
if (rollEvent == null)
{
    rollEvent = new PlayerInput.ActionEvent(rollAction);
    events.Add(rollEvent);
}
for (int i = rollEvent.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(rollEvent, i);
UnityEventTools.AddPersistentListener(rollEvent, pc.Roll);
pi.actionEvents = events.ToArray();
log.AppendLine("Roll -> PlayerController.Roll (" + rollAction.bindings.Count + " touches)");

// ------------------------------------------------------------------ 2. Ragdoll
var renard = root.transform.Find("Renard");
Transform Bone(string n) => renard.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
// Espace de l'armature Blender -> monde (le renard regarde vers +Z dans Unity, l'armature est a 1,116 m du sol)
Vector3 A(float x, float y, float z) => renard.TransformPoint(new Vector3(-x, z + 1.116f, -y));

Rigidbody Body(string bone, float mass, Rigidbody parent, float twist, float swing)
{
    var t = Bone(bone);
    foreach (var j in t.GetComponents<CharacterJoint>()) Object.DestroyImmediate(j);
    foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
    if (!t.TryGetComponent<Rigidbody>(out var rb)) rb = t.gameObject.AddComponent<Rigidbody>();   // pas de ?? sur un objet Unity
    rb.mass = mass;
    rb.isKinematic = true;
    rb.interpolation = RigidbodyInterpolation.None;   // RagdollController passe en Interpolate a la mort seulement
    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    rb.linearDamping = 0.2f;
    rb.angularDamping = 0.6f;
    if (parent != null)
    {
        var j = t.gameObject.AddComponent<CharacterJoint>();
        j.connectedBody = parent;
        j.enableProjection = true;
        j.lowTwistLimit = new SoftJointLimit { limit = -twist };
        j.highTwistLimit = new SoftJointLimit { limit = twist };
        j.swing1Limit = new SoftJointLimit { limit = swing };
        j.swing2Limit = new SoftJointLimit { limit = swing };
    }
    return rb;
}
void Sphere(string bone, Vector3 worldCenter, float worldRadius)
{
    var t = Bone(bone);
    var c = t.gameObject.AddComponent<SphereCollider>();
    c.center = t.InverseTransformPoint(worldCenter);
    c.radius = worldRadius / t.lossyScale.x;
}
void Capsule(string bone, Vector3 worldEnd, float worldRadius)
{
    var t = Bone(bone);
    var c = t.gameObject.AddComponent<CapsuleCollider>();
    Vector3 b = t.InverseTransformPoint(worldEnd);
    Vector3 ab = new Vector3(Mathf.Abs(b.x), Mathf.Abs(b.y), Mathf.Abs(b.z));
    c.direction = ab.x >= ab.y && ab.x >= ab.z ? 0 : (ab.y >= ab.z ? 1 : 2);
    c.center = b * 0.5f;
    c.radius = worldRadius / t.lossyScale.x;
    c.height = b.magnitude + 2f * c.radius;
    var j = t.GetComponent<CharacterJoint>();
    if (j != null)
    {
        j.axis = b.normalized;
        j.swingAxis = Vector3.Cross(b.normalized, Mathf.Abs(b.normalized.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
    }
}

var hips = Body("Bassin", 10f, null, 0, 0);
Sphere("Bassin", A(0, 0.05f, 0.25f), 0.62f);
var chest = Body("Epaules", 8f, hips, 15, 25);
Sphere("Epaules", A(0, 0, 0.95f), 0.58f);
Body("Tete", 7f, chest, 25, 30);
Sphere("Tete", A(0, -0.15f, 2.15f), 0.98f);
foreach (var side in new[] { "L", "R" })
{
    Body("Epaule." + side, 2f, chest, 30, 70);
    Capsule("Epaule." + side, Bone("Majeur." + side).position, 0.24f);
    var thigh = Body("Cuisse." + side, 3f, hips, 20, 55);
    Capsule("Cuisse." + side, Bone("Tibias." + side).position, 0.27f);
    Body("Tibias." + side, 2f, thigh, 10, 50);
    Capsule("Tibias." + side, Bone("Doigts_Pied." + side).position, 0.25f);
}
log.AppendLine("Ragdoll : " + string.Join(", ", renard.GetComponentsInChildren<Rigidbody>(true).Select(r => r.name)));

PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
PrefabUtility.UnloadPrefabContents(root);
return log.ToString();
