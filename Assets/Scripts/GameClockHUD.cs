using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD de l'horloge (UI_UX) : compteur de jours « Jour x/10 » et temps restant avant le crépuscule ou l'aube.
/// Lit le GameClock de la scène ; chaque machine affiche l'heure de la partie partagée.
/// </summary>
public class GameClockHUD : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("UIDocument du HUD (contient GameClockPanel, DayLabel, PhaseLabel)")]
    [SerializeField] private UIDocument document;

    [Tooltip("Horloge de la partie ; si vide, prend GameClock.Instance")]
    [SerializeField] private GameClock gameClock;

    [Header("Textes")]
    [Tooltip("Texte avant le temps restant pendant le jour")]
    [SerializeField] private string dayPhaseText = "Nuit dans";

    [Tooltip("Texte avant le temps restant pendant la nuit")]
    [SerializeField] private string nightPhaseText = "Aube dans";

    [Tooltip("Texte affiché une fois le temps écoulé (coucher du soleil du dernier jour)")]
    [SerializeField] private string timeUpText = "La Moisson est terminée";

    private VisualElement _panel;
    private Label _dayLabel;
    private Label _phaseLabel;

    // On ne réécrit les textes que s'ils changent (une fois par seconde au plus).
    private int _shownDay = -1;
    private int _shownSeconds = -1;
    private int _shownState = -1; // 0 = jour, 1 = nuit, 2 = temps écoulé

    private void OnEnable()
    {
        if (document == null) return;
        VisualElement root = document.rootVisualElement;
        _panel = root.Q<VisualElement>("GameClockPanel");
        _dayLabel = root.Q<Label>("DayLabel");
        _phaseLabel = root.Q<Label>("PhaseLabel");
        SetVisible(false);
    }

    private void Update()
    {
        GameClock clock = gameClock != null ? gameClock : GameClock.Instance;
        bool visible = clock != null && clock.HasStarted;
        SetVisible(visible);
        if (!visible) return;

        int state = clock.IsTimeUp ? 2 : clock.IsNight ? 1 : 0;
        int day = clock.CurrentDay;
        int seconds = state == 2 ? 0 : Mathf.CeilToInt(clock.SecondsUntilNextPhase);

        if (day != _shownDay && _dayLabel != null)
        {
            _shownDay = day;
            _dayLabel.text = "Jour " + day + "/" + clock.TotalDays;
        }

        if ((seconds != _shownSeconds || state != _shownState) && _phaseLabel != null)
        {
            _shownSeconds = seconds;
            if (state != _shownState && _panel != null)
            {
                _panel.EnableInClassList("game-clock--night", state == 1);
            }
            _shownState = state;
            _phaseLabel.text = state == 2
                ? timeUpText
                : (state == 1 ? nightPhaseText : dayPhaseText) + " " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }
    }

    private void SetVisible(bool visible)
    {
        if (_panel == null) return;
        DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_panel.style.display != display) _panel.style.display = display;
    }
}
