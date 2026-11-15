using UnityEngine;

/// <summary>
/// Particules d'ambiance pilotées par le cycle jour / nuit : poussières dorées en suspension le jour,
/// lucioles la nuit. En Play Mode, la zone d'émission suit la caméra principale.
/// </summary>
[ExecuteAlways]
public class AmbientParticles : MonoBehaviour
{
    [Header("Cycle jour / nuit")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Poussières (jour)")]
    [SerializeField] private ParticleSystem dust;
    [Tooltip("Particules émises par seconde en plein jour")]
    [SerializeField] private float dustRate = 18f;

    [Header("Lucioles (nuit)")]
    [SerializeField] private ParticleSystem fireflies;
    [Tooltip("Lucioles émises par seconde en pleine nuit")]
    [SerializeField] private float firefliesRate = 10f;

    [Header("Suivi")]
    [Tooltip("En Play Mode, la zone d'émission suit la caméra principale")]
    [SerializeField] private bool followMainCamera = true;

    private Transform _camera;

    void OnEnable()
    {
        if (dayNightCycle == null) return;
        dayNightCycle.OnLightingUpdated += HandleLightingUpdated;
        HandleLightingUpdated(dayNightCycle.SunHeight01);
    }

    void OnDisable()
    {
        if (dayNightCycle != null) dayNightCycle.OnLightingUpdated -= HandleLightingUpdated;
    }

    void HandleLightingUpdated(float sunHeight01)
    {
        DayNightLightingState s = dayNightCycle.CurrentState;
        SetRate(dust, dustRate * s.DustAmount);
        SetRate(fireflies, firefliesRate * s.FirefliesAmount);
    }

    static void SetRate(ParticleSystem system, float rate)
    {
        if (system == null) return;
        var emission = system.emission;
        emission.rateOverTime = rate;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || !followMainCamera) return;
        if (_camera == null)
        {
            Camera main = Camera.main;
            if (main == null) return;
            _camera = main.transform;
        }
        transform.position = _camera.position;
    }
}
