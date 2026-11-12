using System;
using UnityEngine;

/// <summary>
/// Variante de nuit d'un mob (Bestiaire, Cycle_Jour_Nuit) : plus rapide, plus fort, aggro plus large,
/// et un visuel différent (assombrissement + objets « de nuit » comme des yeux rouges).
/// Écoute GameClock sur chaque machine : les bonus de stats ne servent que chez l'hôte (là où tourne l'IA),
/// le visuel s'applique partout. Dans une scène sans GameClock, le mob reste en version de jour.
/// L'IA du mob (BullAI, EnemySight…) lit les multiplicateurs ci-dessous et s'abonne à OnNightChanged.
/// </summary>
public class EnemyNightMode : MonoBehaviour
{
    [Header("Bonus de nuit (multiplicateurs)")]
    [Tooltip("Vitesse la nuit (1,3 = +30 %), en patrouille comme en poursuite / charge")]
    [Min(0f)]
    [SerializeField] private float speedMultiplier = 1.3f;

    [Tooltip("Dégâts la nuit (1,5 = +50 %). Les dégâts sont des demi-cœurs entiers, arrondis au-dessus : 1 → 2")]
    [Min(0f)]
    [SerializeField] private float damageMultiplier = 1.5f;

    [Tooltip("Portée de détection la nuit (1,5 = +50 %) : rayon de vision de l'araignée, largeur de détection du taureau")]
    [Min(0f)]
    [SerializeField] private float aggroMultiplier = 1.5f;

    [Header("Visuel de nuit")]
    [Tooltip("Couleur multipliée à la couleur de base des matériaux la nuit (blanc = inchangé, gris = plus sombre)")]
    [SerializeField] private Color nightTint = new Color(0.45f, 0.4f, 0.5f, 1f);

    [Tooltip("Renderers à assombrir la nuit ; si vide, tous les renderers du mob")]
    [SerializeField] private Renderer[] tintedRenderers;

    [Tooltip("Matériaux remplacés la nuit (ex. pelage sombre). Ces renderers ne reçoivent pas la teinte ci-dessus")]
    [SerializeField] private NightMaterialSwap[] nightMaterialSwaps;

    [Tooltip("Objets affichés seulement la nuit (ex. yeux rouges lumineux)")]
    [SerializeField] private GameObject[] nightOnlyObjects;

    [Tooltip("Objets affichés seulement le jour")]
    [SerializeField] private GameObject[] dayOnlyObjects;

    /// <summary>Un renderer et les matériaux qu'il porte la nuit (même ordre que ses matériaux de jour).</summary>
    [Serializable]
    private class NightMaterialSwap
    {
        [Tooltip("Renderer dont les matériaux changent la nuit")]
        [SerializeField] private Renderer renderer;

        [Tooltip("Matériaux de nuit, dans le même ordre que les matériaux de jour du renderer")]
        [SerializeField] private Material[] nightMaterials;

        private Material[] _dayMaterials;

        public Renderer Renderer => renderer;

        /// <summary>Mémorise les matériaux de jour (à appeler une fois, avant tout changement).</summary>
        public void CacheDayMaterials()
        {
            if (renderer != null) _dayMaterials = renderer.sharedMaterials;
        }

        public void Apply(bool night)
        {
            if (renderer == null || _dayMaterials == null) return;
            if (nightMaterials == null || nightMaterials.Length == 0) return;
            renderer.sharedMaterials = night ? nightMaterials : _dayMaterials;
        }
    }

    /// <summary>Vrai quand le mob est en version de nuit.</summary>
    public bool IsNight { get; private set; }

    /// <summary>Multiplicateurs à appliquer par l'IA (1 le jour).</summary>
    public float SpeedMultiplier => IsNight ? speedMultiplier : 1f;
    public float DamageMultiplier => IsNight ? damageMultiplier : 1f;
    public float AggroMultiplier => IsNight ? aggroMultiplier : 1f;

    /// <summary>Passage jour → nuit (true) ou nuit → jour (false).</summary>
    public event Action<bool> OnNightChanged;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private GameClock _clock;
    private MaterialPropertyBlock _block;
    private bool _visualApplied;

    /// <summary>Dégâts de nuit d'une attaque (en demi-cœurs, arrondis au-dessus).</summary>
    public int ScaleDamage(int baseDamage)
    {
        return IsNight ? Mathf.CeilToInt(baseDamage * damageMultiplier) : baseDamage;
    }

    /// <summary>Raccourci pour l'IA : dégâts de nuit si le composant existe, sinon dégâts de base.</summary>
    public static int ScaleDamage(EnemyNightMode nightMode, int baseDamage)
    {
        return nightMode != null ? nightMode.ScaleDamage(baseDamage) : baseDamage;
    }

    private void Awake()
    {
        if (nightMaterialSwaps != null)
        {
            foreach (NightMaterialSwap swap in nightMaterialSwaps) swap.CacheDayMaterials();
        }

        if (tintedRenderers == null || tintedRenderers.Length == 0)
        {
            // Par défaut : tous les renderers du mob, sauf ceux qui ont déjà leurs matériaux de nuit.
            var toTint = new System.Collections.Generic.List<Renderer>(GetComponentsInChildren<Renderer>(true));
            if (nightMaterialSwaps != null)
            {
                foreach (NightMaterialSwap swap in nightMaterialSwaps) toTint.Remove(swap.Renderer);
            }
            tintedRenderers = toTint.ToArray();
        }
        _block = new MaterialPropertyBlock();
        ApplyVisual(false);
    }

    private void OnEnable()
    {
        _clock = GameClock.Instance;
        if (_clock == null) return;

        _clock.OnDayStarted += HandleDayStarted;
        _clock.OnNightStarted += HandleNightStarted;
        _clock.OnTimeUp += HandleTimeUp;

        // Mob apparu en pleine nuit : il ne recevra pas le crépuscule.
        SetNight(_clock.HasStarted && _clock.IsNight);
    }

    private void OnDisable()
    {
        if (_clock == null) return;
        _clock.OnDayStarted -= HandleDayStarted;
        _clock.OnNightStarted -= HandleNightStarted;
        _clock.OnTimeUp -= HandleTimeUp;
        _clock = null;
    }

    private void HandleDayStarted(int day) => SetNight(false);
    private void HandleNightStarted(int day) => SetNight(true);
    private void HandleTimeUp() => SetNight(false);

    /// <summary>Force la version de jour ou de nuit (appelé par GameClock ; utile aussi pour tester).</summary>
    public void SetNight(bool night)
    {
        if (night == IsNight && _visualApplied) return;
        bool changed = night != IsNight;
        IsNight = night;
        ApplyVisual(night);
        if (changed) OnNightChanged?.Invoke(night);
    }

    [ContextMenu("Debug : basculer jour / nuit")]
    private void ToggleNightDebug() => SetNight(!IsNight);

    private void ApplyVisual(bool night)
    {
        _visualApplied = true;

        foreach (GameObject go in nightOnlyObjects)
        {
            if (go != null) go.SetActive(night);
        }
        foreach (GameObject go in dayOnlyObjects)
        {
            if (go != null) go.SetActive(!night);
        }

        if (nightMaterialSwaps != null)
        {
            foreach (NightMaterialSwap swap in nightMaterialSwaps) swap.Apply(night);
        }

        foreach (Renderer rend in tintedRenderers)
        {
            if (rend == null) continue;
            if (!night)
            {
                // Le jour : on retire la teinte, le renderer reprend ses matériaux tels quels.
                rend.SetPropertyBlock(null);
                continue;
            }

            Material mat = rend.sharedMaterial;
            if (mat == null) continue;
            int colorId = mat.HasProperty(BaseColorId) ? BaseColorId : (mat.HasProperty(ColorId) ? ColorId : -1);
            if (colorId == -1) continue;

            _block.Clear();
            _block.SetColor(colorId, mat.GetColor(colorId) * nightTint);
            rend.SetPropertyBlock(_block);
        }
    }
}
