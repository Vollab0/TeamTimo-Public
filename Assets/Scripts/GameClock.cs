using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Horloge des 10 jours de la Moisson (Cycle_de_Partie, Cycle_Jour_Nuit).
/// L'hôte est l'autorité (ADR 002) : il ne réplique qu'un « point d'ancrage » (temps serveur + temps écoulé),
/// et chaque machine recalcule l'heure en continu à partir du temps serveur de Netcode.
/// Les événements (aube, crépuscule, fin du temps) sont déclenchés localement sur chaque machine.
/// Un jour commence à l'aube : [0 ; dayRatio[ = jour, [dayRatio ; 1[ = nuit.
/// À placer dans la scène sur un objet avec un NetworkObject.
/// </summary>
public class GameClock : NetworkBehaviour
{
    /// <summary>État répliqué de l'horloge : tout change d'un bloc, en un seul message.</summary>
    private struct ClockState : INetworkSerializeByMemcpy, IEquatable<ClockState>
    {
        public double AnchorServerTime; // temps serveur Netcode au moment de l'ancrage
        public double ElapsedAtAnchor;  // secondes de partie écoulées à ce moment-là
        public float Speed;             // 0 = arrêtée, 1 = temps réel, > 1 = accéléré (debug)
        public bool Started;

        public bool Equals(ClockState other)
        {
            return AnchorServerTime == other.AnchorServerTime && ElapsedAtAnchor == other.ElapsedAtAnchor
                && Speed == other.Speed && Started == other.Started;
        }
    }

    [Header("Durée de la partie")]
    [Tooltip("Nombre de jours de la partie (la Moisson). Fin de partie au coucher du soleil du dernier jour.")]
    [SerializeField] private int totalDays = 10;

    [Tooltip("Durée d'un jour complet, jour + nuit, en secondes (1080 = 18 min)")]
    [SerializeField] private float dayDuration = 1080f;

    [Tooltip("Part du jour dans un cycle (0,7 = 70 % de jour, 30 % de nuit)")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float dayRatio = 0.7f;

    [Header("Démarrage")]
    [Tooltip("L'hôte attend la fin de l'écran de chargement avant de lancer le jour 1")]
    [SerializeField] private bool waitForLoading = true;

    [Header("Debug")]
    [Tooltip("Accélère le temps (hôte uniquement, au démarrage) : 60 = un jour de 18 min passe en 18 s. Laisser à 1 en jeu.")]
    [Min(0f)]
    [SerializeField] private float debugTimeScale = 1f;

    [Tooltip("Écrit dans la console chaque aube, crépuscule et la fin du temps")]
    [SerializeField] private bool logPhaseChanges = true;

    /// <summary>Horloge de la partie en cours (une seule par scène).</summary>
    public static GameClock Instance { get; private set; }

    /// <summary>Aube : début d'un nouveau jour (numéro du jour, à partir de 1).</summary>
    public event Action<int> OnDayStarted;
    /// <summary>Crépuscule : début de la nuit (numéro du jour).</summary>
    public event Action<int> OnNightStarted;
    /// <summary>Coucher du soleil du dernier jour : le temps est écoulé (Game Over si les joueurs ne sont pas sortis).</summary>
    public event Action OnTimeUp;

    private readonly NetworkVariable<ClockState> _state = new NetworkVariable<ClockState>();

    // Dernier état connu localement, pour ne déclencher chaque événement qu'une fois.
    private int _lastDay;
    private bool _lastIsNight;
    private bool _timeUpRaised;

    // --- Lecture de l'heure (valable sur toutes les machines) ---

    /// <summary>Vrai une fois le jour 1 commencé.</summary>
    public bool HasStarted => IsSpawned && _state.Value.Started;
    /// <summary>Secondes de partie écoulées depuis l'aube du jour 1.</summary>
    public double Elapsed => ComputeElapsed();
    /// <summary>Jour en cours, de 1 à <see cref="TotalDays"/>.</summary>
    public int CurrentDay => Mathf.Clamp((int)(Elapsed / dayDuration) + 1, 1, totalDays);
    /// <summary>Avancement du jour en cours : 0 = aube, dayRatio = crépuscule, 1 = aube suivante.</summary>
    public float DayProgress => IsTimeUp ? dayRatio : (float)((Elapsed % dayDuration) / dayDuration);
    /// <summary>Vrai pendant la nuit.</summary>
    public bool IsNight => !IsTimeUp && DayProgress >= dayRatio;
    /// <summary>Vrai une fois passé le coucher du soleil du dernier jour.</summary>
    public bool IsTimeUp => HasStarted && Elapsed >= TimeLimit;
    /// <summary>Secondes réelles avant le prochain changement (crépuscule ou aube).</summary>
    public float SecondsUntilNextPhase
    {
        get
        {
            float progress = DayProgress;
            float target = progress < dayRatio ? dayRatio : 1f;
            float speed = _state.Value.Speed > 0f ? _state.Value.Speed : 1f;
            return (target - progress) * dayDuration / speed;
        }
    }

    public int TotalDays => totalDays;
    public float DayRatio => dayRatio;
    public float DayDuration => dayDuration;

    /// <summary>Durée totale de la partie : jusqu'au crépuscule du dernier jour.</summary>
    private double TimeLimit => (totalDays - 1) * (double)dayDuration + dayRatio * dayDuration;

    // --- Cycle de vie ---

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameClock : une seule horloge par scène, celle-ci est ignorée.", this);
            enabled = false;
            return;
        }
        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkDespawn()
    {
        _lastDay = 0;
        _lastIsNight = false;
        _timeUpRaised = false;
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            if (!_state.Value.Started && (!waitForLoading || !LoadingScreen.IsLoading)) StartClock();
            else if (_state.Value.Started && _state.Value.Speed > 0f && Elapsed >= TimeLimit) StopAt(TimeLimit);
        }

        if (HasStarted) RaiseEvents();
    }

    // --- Commandes de l'hôte ---

    /// <summary>Lance le jour 1 à l'aube (hôte uniquement, appelé automatiquement après le chargement).</summary>
    public void StartClock()
    {
        if (!IsServer || _state.Value.Started) return;
        _state.Value = new ClockState
        {
            AnchorServerTime = ServerNow(),
            ElapsedAtAnchor = 0d,
            Speed = debugTimeScale,
            Started = true,
        };
    }

    /// <summary>Met l'horloge en pause ou la relance (hôte uniquement).</summary>
    public void SetPaused(bool paused)
    {
        if (!IsServer || !_state.Value.Started || IsTimeUp) return;
        Reanchor(Elapsed, paused ? 0f : Mathf.Max(debugTimeScale, 0.0001f));
    }

    /// <summary>Saute directement à l'aube suivante — dormir à la grange (hôte uniquement). Sans effet le dernier jour.</summary>
    public void SkipToNextDawn()
    {
        if (!IsServer || !_state.Value.Started || IsTimeUp || CurrentDay >= totalDays) return;
        Reanchor(CurrentDay * (double)dayDuration, _state.Value.Speed);
    }

    /// <summary>Saute au prochain changement de phase (debug : tester la nuit sans attendre).</summary>
    [ContextMenu("Debug : passer à la phase suivante")]
    public void SkipToNextPhase()
    {
        if (!IsServer || !_state.Value.Started || IsTimeUp) return;
        double dayStart = (CurrentDay - 1) * (double)dayDuration;
        double target = IsNight ? dayStart + dayDuration : dayStart + dayRatio * dayDuration;
        Reanchor(Math.Min(target, TimeLimit), _state.Value.Speed);
    }

    private void StopAt(double elapsed) => Reanchor(elapsed, 0f);

    private void Reanchor(double elapsed, float speed)
    {
        _state.Value = new ClockState
        {
            AnchorServerTime = ServerNow(),
            ElapsedAtAnchor = elapsed,
            Speed = speed,
            Started = true,
        };
    }

    // --- Calcul ---

    private double ServerNow() => NetworkManager.ServerTime.Time;

    private double ComputeElapsed()
    {
        if (!IsSpawned) return 0d;
        ClockState state = _state.Value;
        if (!state.Started) return 0d;
        double elapsed = state.ElapsedAtAnchor + Math.Max(0d, ServerNow() - state.AnchorServerTime) * state.Speed;
        return Math.Min(elapsed, TimeLimit);
    }

    private void RaiseEvents()
    {
        if (_timeUpRaised) return;

        if (IsTimeUp)
        {
            _timeUpRaised = true;
            if (logPhaseChanges) Debug.Log("GameClock : coucher du soleil du jour " + totalDays + ", le temps est écoulé.");
            OnTimeUp?.Invoke();
            return;
        }

        int day = CurrentDay;
        bool isNight = IsNight;

        // Nouveau jour (ou premier état connu, ex. un joueur qui rejoint en cours de partie).
        if (day != _lastDay)
        {
            _lastDay = day;
            _lastIsNight = false;
            if (logPhaseChanges) Debug.Log("GameClock : aube du jour " + day + "/" + totalDays + ".");
            OnDayStarted?.Invoke(day);
        }

        if (isNight && !_lastIsNight)
        {
            _lastIsNight = true;
            if (logPhaseChanges) Debug.Log("GameClock : crépuscule du jour " + day + ".");
            OnNightStarted?.Invoke(day);
        }
    }

    private void OnValidate()
    {
        totalDays = Mathf.Max(1, totalDays);
        dayDuration = Mathf.Max(1f, dayDuration);
    }
}
