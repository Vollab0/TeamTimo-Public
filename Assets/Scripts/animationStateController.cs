using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Pilote l'Animator du renard (PlayerAnimatorController) d'après l'état du PlayerController.
/// Booléens : isWalking, isSprinting, isJumping, isFalling, isGrounded, isAttacking, isHolding.
/// Déclencheurs : Attack (arme en main) + ToolSwing (coup d'outil, couche BrasObjet), Push (mains nues), Hit (perte de vie), Roll (roulade),
/// Downed (0 cœur : à terre), Die (fin du délai à terre), Revive (relevé par un allié). Booléen isDowned (ramper / au sol).
/// Clips : Renard.fbx (Repos = Idle) et Renard_Animations.fbx (ArtSource/Renard/renard_anim.py).
/// </summary>
public class AnimationStateController : MonoBehaviour
{
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsSprintingHash = Animator.StringToHash("isSprinting");
    private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
    private static readonly int IsFallingHash = Animator.StringToHash("isFalling");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsAttackingHash = Animator.StringToHash("isAttacking");
    private static readonly int IsHoldingHash = Animator.StringToHash("isHolding");
    private static readonly int IsDownedHash = Animator.StringToHash("isDowned");
    private const string AttackTrigger = "Attack";
    private const string PushTrigger = "Push";
    private const string HitTrigger = "Hit";
    private const string RollTrigger = "Roll";
    private const string DownedTrigger = "Downed";
    private const string DieTrigger = "Die";
    private const string ReviveTrigger = "Revive";
    private const string ToolSwingTrigger = "ToolSwing";

    [Header("Chute")]
    [Tooltip("Temps passé en l'air (s) avant de jouer l'animation de chute. Évite que le renard « tombe » une frame dans les pentes ou sur une marche.")]
    [SerializeField] private float fallDelay = 0.15f;

    [Header("Objet en main")]
    [Tooltip("Couche de l'Animator qui fige le bras droit en pose « objet en main » (masque : bras droit)")]
    [SerializeField] private string holdLayerName = "BrasObjet";

    [Tooltip("Couche de l'Animator qui ferme les doigts de la main droite autour de l'objet tenu (masque : doigts)")]
    [SerializeField] private string fistLayerName = "MainObjet";

    [Tooltip("Vitesse de fondu (par seconde) de la pose « objet en main » : elle s'efface pendant une poussée ou une roulade")]
    [Min(0.1f)]
    [SerializeField] private float holdBlendSpeed = 10f;

    [Tooltip("Durée (s) du coup d'outil joué sur la couche du bras (clip Renard_CoupOutil, calé sur l'animation Attaque de 0,6 s)")]
    [Min(0.1f)]
    [SerializeField] private float toolSwingDuration = 0.6f;

    // États du Base Layer où le bras garde la pose « objet en main » (tag posé dans l'Animator Controller).
    private static readonly int LocomotionTag = Animator.StringToHash("Locomotion");

    private Animator _animator;
    private NetworkAnimator _networkAnimator;
    private PlayerController _playerController;
    private GrabObject _grabObject;
    private PlayerHealth _playerHealth;
    private InventorySystem _inventory;
    private bool _wasAttacking;
    private int _lastHealth = -1;
    private float _airTime;
    private int _holdLayer = -1;
    private int _fistLayer = -1;
    private float _holdWeight;
    private float _fistWeight;
    private float _toolSwingUntil = -1f;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _holdLayer = _animator.GetLayerIndex(holdLayerName);
        _fistLayer = _animator.GetLayerIndex(fistLayerName);
        _networkAnimator = GetComponent<NetworkAnimator>();
        _playerController = GetComponentInParent<PlayerController>();
        _grabObject = GetComponentInParent<GrabObject>();
        _playerHealth = GetComponentInParent<PlayerHealth>();
    }

    private void Start()
    {
        // L'inventaire est un objet de la scène (même recherche que GrabObject)
        _inventory = FindFirstObjectByType<InventorySystem>();
    }

    private void OnEnable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnSanteChangee += OnHealthChanged;
            _playerHealth.OnDowned += OnDowned;
            _playerHealth.OnDied += OnDied;
            _playerHealth.OnRevived += OnRevived;
        }
        if (_playerController != null) _playerController.OnRoll += PlayRoll;
    }

    private void OnDisable()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnSanteChangee -= OnHealthChanged;
            _playerHealth.OnDowned -= OnDowned;
            _playerHealth.OnDied -= OnDied;
            _playerHealth.OnRevived -= OnRevived;
        }
        if (_playerController != null) _playerController.OnRoll -= PlayRoll;
    }

    private void Update()
    {
        if (_playerController == null) return;

        bool grounded = _playerController.IsGroundedNow;
        _airTime = grounded ? 0f : _airTime + Time.deltaTime;

        _animator.SetBool(IsWalkingHash, _playerController.IsWalking);
        _animator.SetBool(IsSprintingHash, _playerController.IsSprinting);
        _animator.SetBool(IsJumpingHash, _playerController.IsJumping);
        _animator.SetBool(IsFallingHash, _playerController.IsFalling && _airTime > fallDelay);
        _animator.SetBool(IsGroundedHash, grounded);
        _animator.SetBool(IsAttackingHash, _playerController.IsAttacking);
        _animator.SetBool(IsDownedHash, _playerController.IsDowned);
        bool holding = _grabObject != null && _grabObject.IsHolding();
        if (_grabObject != null) _animator.SetBool(IsHoldingHash, holding);

        // Début d'une attaque : coup d'outil si l'objet sélectionné est une arme, sinon poussée (GDD Combat_Attaque)
        bool attacking = _playerController.IsAttacking;
        if (attacking && !_wasAttacking)
        {
            bool armed = _inventory != null && _inventory.GetCurrentItem() is WeaponItem;
            Trigger(armed ? AttackTrigger : PushTrigger);
            // Le corps joue « Attaque » ; le bras droit joue le coup d'outil (lame devant à l'impact) sur la couche du bras.
            if (armed && holding)
            {
                Trigger(ToolSwingTrigger);
                _toolSwingUntil = Time.time + toolSwingDuration;
            }
        }
        _wasAttacking = attacking;

        UpdateHoldLayers(holding);
    }

    // Bras droit en pose « objet en main » pendant les déplacements (repos, marche, course, saut) et pendant
    // le coup d'outil : l'outil ne ballotte plus. La pose s'efface pour la poussée, la roulade, les dégâts, etc.
    // Les doigts restent fermés sur l'objet tant qu'il est en main.
    private void UpdateHoldLayers(bool holding)
    {
        float step = holdBlendSpeed * Time.deltaTime;

        if (_holdLayer >= 0)
        {
            AnimatorStateInfo state = _animator.IsInTransition(0)
                ? _animator.GetNextAnimatorStateInfo(0)
                : _animator.GetCurrentAnimatorStateInfo(0);
            bool swinging = Time.time < _toolSwingUntil;
            bool moving = state.tagHash == LocomotionTag && !_playerController.IsAttacking;
            float target = holding && (swinging || moving) ? 1f : 0f;
            // Le coup d'outil part tout de suite : pas de fondu à l'entrée.
            _holdWeight = swinging && holding ? 1f : Mathf.MoveTowards(_holdWeight, target, step);
            _animator.SetLayerWeight(_holdLayer, _holdWeight);
        }

        if (_fistLayer >= 0)
        {
            _fistWeight = Mathf.MoveTowards(_fistWeight, holding ? 1f : 0f, step);
            _animator.SetLayerWeight(_fistLayer, _fistWeight);
        }
    }

    /// <summary>Joue la roulade (abonné à PlayerController.OnRoll).</summary>
    public void PlayRoll()
    {
        Trigger(RollTrigger);
    }

    private void OnDowned() => Trigger(DownedTrigger);
    private void OnDied() => Trigger(DieTrigger);
    private void OnRevived() => Trigger(ReviveTrigger);

    private void OnHealthChanged(int health)
    {
        // Recul animé quand on perd de la vie sans tomber à 0 (à 0 : animation « à terre », voir OnDowned)
        if (_lastHealth >= 0 && health < _lastHealth && health > 0) Trigger(HitTrigger);
        _lastHealth = health;
    }

    private void Trigger(string trigger)
    {
        // En réseau, les déclencheurs passent par le NetworkAnimator pour être rejoués chez les autres joueurs
        if (_networkAnimator != null && _networkAnimator.IsSpawned) _networkAnimator.SetTrigger(trigger);
        else _animator.SetTrigger(trigger);
    }
}
