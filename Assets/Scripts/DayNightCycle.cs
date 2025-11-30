using System;
using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    #region --- SETTINGS ---

    [Header("1. Temps")]
    public float dayDuration = 600f; 
    public float nightDuration = 600f;
    
    [Header("Info Debug")]
    [SerializeField] private int _currentDay = 1; 

    [Header("2. Soleil (Objet)")]
    [SerializeField] private Light _sunLight;

    [Header("3. Skybox Custom")]
    [Tooltip("Glissez ici votre matériau MAT_MySkybox")]
    [SerializeField] private Material _mySkyboxMaterial; 

    [Header("4. Réglages Transition")]
    [Tooltip("Courbe pour gérer l'apparition des étoiles (0 le jour, 1 la nuit)")]
    [SerializeField] private AnimationCurve _starVisibilityCurve;
    
    [Header("5. Eclairage Ambiant")]
    [SerializeField] private Color _dayAmbient = Color.gray;
    [SerializeField] private Color _nightAmbient = new Color(0.1f, 0.1f, 0.3f);

    #endregion

    #region --- EVENTS ---
    public static event Action OnDayStarted;
    public static event Action OnNightStarted;
    public static event Action<int> OnNewDay; 
    #endregion

    #region --- PRIVATE VARS ---
    private float _currentTime;
    private bool _isNight = false;
    private float _totalCycleDuration;
    
    // IDs pour optimiser l'envoi des données au shader
    private int _sunDirID;
    private int _blendID;
    private int _starIntID;
    #endregion

    public int GetCurrentDay() { return _currentDay; }

    void Start()
    {
        _totalCycleDuration = dayDuration + nightDuration;
        
        // Récupération des IDs
        _sunDirID = Shader.PropertyToID("_SunDirection");
        _blendID = Shader.PropertyToID("_DayNightBlend");
        _starIntID = Shader.PropertyToID("_StarIntensity");

        // Init
        if(OnDayStarted != null) OnDayStarted.Invoke();
        if(OnNewDay != null) OnNewDay.Invoke(_currentDay);
    }

    void Update()
    {
        _currentTime += Time.deltaTime;
        if (_currentTime >= _totalCycleDuration) _currentTime %= _totalCycleDuration;

        float timePercent = _currentTime / _totalCycleDuration;

        // 1. Rotation physique du Soleil
        float sunAngle = timePercent * 360f - 90f; 
        _sunLight.transform.rotation = Quaternion.Euler(sunAngle, 30f, 0);

        // 2. Mise à jour du Shader et de l'ambiance
        UpdateSkyboxShader(timePercent);
        UpdateAmbientLight(timePercent);
        CheckPhaseTransition();
    }

    private void UpdateSkyboxShader(float percent)
    {
        if (_mySkyboxMaterial == null || _sunLight == null) return;

        // A. Direction du soleil (inchangé)
        _mySkyboxMaterial.SetVector(_sunDirID, -_sunLight.transform.forward);

        // B. Calculer le Blend Jour/Nuit --- LOGIQUE ADAPTEE ---
        
        // DEMANDE : 0 = Midi (Jour), 1 = Minuit (Nuit)
        
        // percent = 0.0 (Minuit) -> Abs(-0.5) * 2 = 1.0 (Nuit)
        // percent = 0.5 (Midi)   -> Abs(0.0)  * 2 = 0.0 (Jour)
        // percent = 1.0 (Minuit) -> Abs(0.5)  * 2 = 1.0 (Nuit)
        
        float blend = Mathf.Abs(percent - 0.5f) * 2f;

        _mySkyboxMaterial.SetFloat(_blendID, blend);

        // C. Gérer les étoiles
        float starIntensity = _starVisibilityCurve.Evaluate(percent);
        _mySkyboxMaterial.SetFloat(_starIntID, starIntensity);
    }

    private void UpdateAmbientLight(float percent)
    {
        // Bascule simple jour/nuit pour la lumière ambiante au sol
        bool isNightVisually = percent > 0.75f || percent < 0.25f; 
        RenderSettings.ambientLight = isNightVisually ? _nightAmbient : _dayAmbient;
    }

    private void CheckPhaseTransition()
    {
        // Logique simplifiée : Jour dans la moitié centrale, nuit sur les bords
        if (_currentTime > _totalCycleDuration * 0.25f && _currentTime < _totalCycleDuration * 0.75f)
        {
             if (_isNight) // Transition Nuit -> Jour
            {
                _isNight = false;
                _currentDay++;
                if (OnDayStarted != null) OnDayStarted.Invoke();
                if (OnNewDay != null) OnNewDay.Invoke(_currentDay);
            }
        }
        else // Période Nuit
        {
           if (!_isNight) // Transition Jour -> Nuit
            {
                _isNight = true;
                if (OnNightStarted != null) OnNightStarted.Invoke();
            }
        }
    }
}