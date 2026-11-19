using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Coup porté par le joueur (GDD Combat_Attaque) : au début de l'attaque (PlayerController.OnAttack),
/// attend l'image d'impact de l'animation puis touche les ennemis (EnemyHealth) dans une sphère devant lui.
/// Arme en main (WeaponItem) : dégâts de l'arme + recul. Mains nues : poussée, recul sans dégâts.
/// Tourne seulement chez le joueur local (l'événement OnAttack vient de son input) ;
/// EnemyHealth transmet le coup à l'hôte en réseau.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerMeleeAttack : MonoBehaviour
{
    [Header("Coup d'arme (bâton, pelle, fourche)")]
    [Tooltip("Délai (s) entre le début de l'attaque et l'impact : image où l'outil s'abat dans l'animation Attaque (0,6 s)")]
    [Min(0f)]
    [SerializeField] private float weaponHitDelay = 0.27f;

    [Tooltip("Distance (m) entre le joueur et le centre de la zone touchée")]
    [Min(0f)]
    [SerializeField] private float weaponReach = 3.5f;

    [Tooltip("Rayon (m) de la zone touchée")]
    [Min(0.1f)]
    [SerializeField] private float weaponHitRadius = 2.5f;

    [Tooltip("Vitesse de recul (m/s) donnée à l'ennemi touché")]
    [Min(0f)]
    [SerializeField] private float weaponKnockback = 16f;

    [Header("Poussée (mains nues)")]
    [Tooltip("Délai (s) entre le début de la poussée et l'impact (animation Poussee, 0,53 s)")]
    [Min(0f)]
    [SerializeField] private float pushHitDelay = 0.2f;

    [Tooltip("Distance (m) entre le joueur et le centre de la zone poussée")]
    [Min(0f)]
    [SerializeField] private float pushReach = 2.5f;

    [Tooltip("Rayon (m) de la zone poussée")]
    [Min(0.1f)]
    [SerializeField] private float pushHitRadius = 2f;

    [Tooltip("Vitesse de recul (m/s) de la poussée (aucun dégât)")]
    [Min(0f)]
    [SerializeField] private float pushKnockback = 22f;

    [Header("Zone de frappe")]
    [Tooltip("Hauteur (m) du centre de la zone au-dessus des pieds du joueur")]
    [SerializeField] private float hitHeight = 2f;

    [Tooltip("Calques que le coup peut toucher (ennemis)")]
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Ressenti")]
    [Tooltip("Gel de l'animation du joueur (s) quand le coup porte : donne du poids à l'impact")]
    [Min(0f)]
    [SerializeField] private float hitStopDuration = 0.08f;

    [Tooltip("Secousse de caméra au contact ; vide = pas de secousse (la caméra doit avoir une CinemachineImpulseListener)")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Tooltip("Force de la secousse de caméra")]
    [Min(0f)]
    [SerializeField] private float impulseForce = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showHitGizmo = true;

    private PlayerController _controller;
    private Animator _animator;
    private InventorySystem _inventory;

    private readonly Collider[] _hitBuffer = new Collider[32];
    private readonly List<EnemyHealth> _hitEnemies = new List<EnemyHealth>(8);
    private Coroutine _hitStopRoutine;

    // Dernière zone testée (gizmo).
    private Vector3 _lastHitCenter;
    private float _lastHitRadius;
    private float _lastHitTime = -10f;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _animator = GetComponentInChildren<Animator>();
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();
    }

    private void Start()
    {
        // L'inventaire est un objet de la scène (même recherche que GrabObject).
        _inventory = FindFirstObjectByType<InventorySystem>();
    }

    private void OnEnable()
    {
        _controller.OnAttack += HandleAttack;
    }

    private void OnDisable()
    {
        _controller.OnAttack -= HandleAttack;
        if (_hitStopRoutine != null && _animator != null) _animator.speed = 1f;
        _hitStopRoutine = null;
    }

    private void HandleAttack()
    {
        // Arme = l'objet sélectionné est un WeaponItem (même règle que l'animation dans AnimationStateController).
        WeaponItem weapon = _inventory != null ? _inventory.GetCurrentItem() as WeaponItem : null;
        StartCoroutine(HitRoutine(weapon));
    }

    private IEnumerator HitRoutine(WeaponItem weapon)
    {
        bool armed = weapon != null;
        yield return new WaitForSeconds(armed ? weaponHitDelay : pushHitDelay);

        // Coup interrompu (roulade, à terre) : rien ne part.
        if (_controller.IsDowned || _controller.IsRolling) yield break;

        float reach = armed ? weaponReach : pushReach;
        float radius = armed ? weaponHitRadius : pushHitRadius;
        int damage = armed ? weapon.damage : 0;
        float knockback = armed ? weaponKnockback : pushKnockback;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 center = transform.position + Vector3.up * hitHeight + forward * reach;

        _lastHitCenter = center;
        _lastHitRadius = radius;
        _lastHitTime = Time.time;

        int count = Physics.OverlapSphereNonAlloc(center, radius, _hitBuffer, hitMask, QueryTriggerInteraction.Ignore);
        _hitEnemies.Clear();
        for (int i = 0; i < count; i++)
        {
            Collider hit = _hitBuffer[i];
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || _hitEnemies.Contains(enemy)) continue;
            _hitEnemies.Add(enemy);

            Vector3 hitPoint = hit.ClosestPoint(center);
            enemy.TakeHit(damage, transform.position, hitPoint, knockback);
        }

        if (_hitEnemies.Count == 0) yield break;

        if (hitStopDuration > 0f && _animator != null)
        {
            if (_hitStopRoutine != null) StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = StartCoroutine(HitStopRoutine());
        }
        if (impulseSource != null && impulseForce > 0f) impulseSource.GenerateImpulse(impulseForce * (armed ? 1f : 0.5f));
    }

    private IEnumerator HitStopRoutine()
    {
        _animator.speed = 0f;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        _animator.speed = 1f;
        _hitStopRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showHitGizmo) return;

        // Zones théoriques de l'arme (rouge) et de la poussée (bleu).
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 basePosition = transform.position + Vector3.up * hitHeight;
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(basePosition + forward * weaponReach, weaponHitRadius);
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireSphere(basePosition + forward * pushReach, pushHitRadius);

        // Dernier coup porté (jaune, 0,5 s).
        if (Application.isPlaying && Time.time - _lastHitTime < 0.5f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_lastHitCenter, _lastHitRadius);
        }
    }
}
