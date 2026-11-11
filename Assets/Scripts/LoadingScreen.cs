using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Écran de chargement de la partie : s'affiche dès que la génération du labyrinthe commence
/// (chez l'hôte au lancement, chez un client quand il rejoint) et disparaît quand le labyrinthe,
/// le terrain et le NavMesh / les créatures sont prêts. Le joueur local ne peut pas bouger pendant ce temps.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("UIDocument qui affiche LoadingScreen.uxml")]
    [SerializeField] private UIDocument document;
    [SerializeField] private MazeGenerator mazeGenerator;
    [SerializeField] private TerrainGenerator terrainGenerator;
    [Tooltip("Facultatif : sans lui, on n'attend pas le NavMesh ni l'apparition des créatures")]
    [SerializeField] private MazeNavMeshBuilder navMeshBuilder;

    [Header("Ambiance")]
    [Tooltip("Phrases d'ambiance : une est tirée au hasard à chaque chargement")]
    [SerializeField] private string[] loreLines =
    {
        "Le Fermier aiguise sa faux…",
        "Dix jours. Pas un de plus.",
        "Au village, on compte les absents chaque matin.",
        "Les gardiens ne dorment que la nuit.",
        "Les anciens bataillons ont laissé des traces. Ouvrez l'œil.",
        "Restez groupés : le labyrinthe préfère les retardataires.",
        "Un fragment, c'est lourd. Une clé entière, c'est la liberté."
    };

    [Header("Progression")]
    [Tooltip("Part de la barre réservée au labyrinthe")]
    [SerializeField] private float mazeWeight = 0.2f;
    [Tooltip("Part de la barre réservée au terrain (le plus long)")]
    [SerializeField] private float terrainWeight = 0.6f;
    [Tooltip("Part de la barre réservée au NavMesh et à l'apparition des créatures")]
    [SerializeField] private float creaturesWeight = 0.2f;

    [Tooltip("Durée maximum de l'écran (s) : au-delà, il se masque quoi qu'il arrive (sécurité)")]
    [SerializeField] private float maxLoadingSeconds = 120f;

    [Tooltip("Durée du fondu de sortie (s)")]
    [SerializeField] private float fadeOutDuration = 0.4f;

    /// <summary>Vrai tant que l'écran de chargement est affiché.</summary>
    public static bool IsLoading { get; private set; }

    private VisualElement _root;
    private VisualElement _fill;
    private Label _loreLabel;
    private Label _stepLabel;
    private Label _percentLabel;

    private float _startTime;
    private int _lastPercent = -1;
    private int _lastStep = -1;
    private int _lastTilesBuilt = -1;
    private Coroutine _fadeRoutine;

    private void OnEnable()
    {
        if (document != null)
        {
            VisualElement root = document.rootVisualElement;
            _root = root.Q<VisualElement>("LoadingScreen");
            _fill = root.Q<VisualElement>("LoadingFill");
            _loreLabel = root.Q<Label>("LoadingLore");
            _stepLabel = root.Q<Label>("LoadingStep");
            _percentLabel = root.Q<Label>("LoadingPercent");
        }
        SetVisible(false);

        if (mazeGenerator != null) mazeGenerator.OnGenerationStarted += Show;
        PlayerNetworkSetup.OnLocalPlayerSpawned += OnLocalPlayerSpawned;
    }

    private void OnDisable()
    {
        if (mazeGenerator != null) mazeGenerator.OnGenerationStarted -= Show;
        PlayerNetworkSetup.OnLocalPlayerSpawned -= OnLocalPlayerSpawned;
        IsLoading = false;
    }

    private void Start()
    {
        // La génération a pu démarrer avant notre abonnement.
        if (!IsLoading && mazeGenerator != null && mazeGenerator.Seed != 0 && !IsWorldReady()) Show(mazeGenerator.Seed);
    }

    private void Show(int seed)
    {
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        IsLoading = true;
        _startTime = Time.unscaledTime;
        _lastPercent = _lastStep = _lastTilesBuilt = -1;

        if (_loreLabel != null && loreLines != null && loreLines.Length > 0)
        {
            _loreLabel.text = "« " + loreLines[Random.Range(0, loreLines.Length)] + " »";
        }

        SetVisible(true);
        if (_root != null) _root.style.opacity = 1f;
        SetLocalPlayerInput(false);
    }

    private void Update()
    {
        if (!IsLoading) return;

        bool mazeReady = mazeGenerator == null || mazeGenerator.IsGenerated;
        bool terrainReady = terrainGenerator == null || terrainGenerator.IsReady;
        bool creaturesReady = navMeshBuilder == null || navMeshBuilder.IsReady;

        float terrainProgress = 1f;
        if (terrainGenerator != null && !terrainReady)
        {
            terrainProgress = terrainGenerator.TilesTotal > 0 ? (float)terrainGenerator.TilesBuilt / terrainGenerator.TilesTotal : 0f;
        }

        float totalWeight = Mathf.Max(0.0001f, mazeWeight + terrainWeight + creaturesWeight);
        float progress = ((mazeReady ? mazeWeight : 0f) + terrainProgress * terrainWeight + (creaturesReady ? creaturesWeight : 0f)) / totalWeight;
        UpdateDisplay(progress, mazeReady, terrainReady);

        bool timedOut = Time.unscaledTime - _startTime > maxLoadingSeconds;
        if (timedOut) Debug.LogWarning("Écran de chargement : délai maximum dépassé, il est masqué sans attendre la fin.");

        if ((mazeReady && terrainReady && creaturesReady) || timedOut) Hide();
    }

    private void UpdateDisplay(float progress, bool mazeReady, bool terrainReady)
    {
        if (_fill != null) _fill.style.width = Length.Percent(progress * 100f);

        // On ne réécrit les textes que s'ils changent (pas d'allocation à chaque frame).
        int percent = Mathf.RoundToInt(progress * 100f);
        if (percent != _lastPercent && _percentLabel != null)
        {
            _lastPercent = percent;
            _percentLabel.text = percent + " %";
        }

        int step = !terrainReady ? 0 : !mazeReady ? 1 : 2;
        int tilesBuilt = terrainGenerator != null ? terrainGenerator.TilesBuilt : 0;
        if (_stepLabel == null || (step == _lastStep && (step != 0 || tilesBuilt == _lastTilesBuilt))) return;

        _lastStep = step;
        _lastTilesBuilt = tilesBuilt;
        switch (step)
        {
            case 0: _stepLabel.text = $"Terrain… {tilesBuilt} / {terrainGenerator.TilesTotal}"; break;
            case 1: _stepLabel.text = "Construction du labyrinthe…"; break;
            default: _stepLabel.text = "Réveil des créatures…"; break;
        }
    }

    private void Hide()
    {
        IsLoading = false;
        SetLocalPlayerInput(true);
        _fadeRoutine = StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration && _root != null)
        {
            elapsed += Time.unscaledDeltaTime;
            _root.style.opacity = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
            yield return null;
        }
        SetVisible(false);
        _fadeRoutine = null;
    }

    private bool IsWorldReady()
    {
        return (mazeGenerator == null || mazeGenerator.IsGenerated)
            && (terrainGenerator == null || terrainGenerator.IsReady)
            && (navMeshBuilder == null || navMeshBuilder.IsReady);
    }

    private void OnLocalPlayerSpawned(PlayerNetworkSetup player)
    {
        // Le joueur peut apparaître pendant le chargement : il attend la fin pour bouger.
        if (IsLoading) player.SetInputEnabled(false);
    }

    private static void SetLocalPlayerInput(bool enabled)
    {
        if (PlayerNetworkSetup.LocalPlayer != null) PlayerNetworkSetup.LocalPlayer.SetInputEnabled(enabled);
    }

    private void SetVisible(bool visible)
    {
        if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
