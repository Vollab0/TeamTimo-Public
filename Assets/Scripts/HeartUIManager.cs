using UnityEngine;
using UnityEngine.UIElements;

public class HeartUIManager : MonoBehaviour
{
    [Header("Mes Images")]
    public Sprite imageCoeurPlein;
    public Sprite imageCoeurDemi;
    public Sprite imageCoeurVide;

    [Header("Liaison UI")]
    public UIDocument monDocumentUI;

    private VisualElement conteneurCoeurs;
    private PlayerHealth scriptDuJoueur;

void OnEnable()
    {
        var racine = monDocumentUI.rootVisualElement;
        conteneurCoeurs = racine.Q<VisualElement>("HeartsContainer");

        // Scène sans réseau : le joueur est déjà posé dans la scène.
        // En réseau, il apparaît plus tard et PlayerNetworkSetup appelle SetPlayer().
        PlayerHealth joueurDeLaScene = FindFirstObjectByType<PlayerHealth>();
        if (joueurDeLaScene != null) SetPlayer(joueurDeLaScene);
    }

    /// <summary>Branche l'affichage des cœurs sur un joueur (appelé pour le joueur local en réseau).</summary>
    public void SetPlayer(PlayerHealth joueur)
    {
        if (scriptDuJoueur != null) scriptDuJoueur.OnSanteChangee -= MettreAJourLesCoeurs;

        scriptDuJoueur = joueur;
        if (scriptDuJoueur == null) return;

        scriptDuJoueur.OnSanteChangee += MettreAJourLesCoeurs;
        MettreAJourLesCoeurs(scriptDuJoueur.VieActuelle);
    }

    void OnDisable()
    {
        // IMPORTANT : On se désabonne quand l'objet est désactivé pour éviter des erreurs
        if (scriptDuJoueur != null)
        {
            scriptDuJoueur.OnSanteChangee -= MettreAJourLesCoeurs;
        }
    }

    // Cette fonction est appelée automatiquement par le signal du Player
    void MettreAJourLesCoeurs(int vieActuelle)
    {
        if (conteneurCoeurs == null) return;

        conteneurCoeurs.Clear();

        // On récupère le nombre de cœurs max depuis le script du joueur
        int nombreDeCoeurs = scriptDuJoueur.nombreDeCoeurs;

        for (int i = 0; i < nombreDeCoeurs; i++)
        {
            VisualElement coeur = new VisualElement();
            coeur.AddToClassList("heart");

            int viePourCeCoeur = vieActuelle - (i * 2);
            Sprite spriteChoisi = null;

            if (viePourCeCoeur >= 2) spriteChoisi = imageCoeurPlein;
            else if (viePourCeCoeur == 1) spriteChoisi = imageCoeurDemi;
            else spriteChoisi = imageCoeurVide;

            if (spriteChoisi != null)
            {
                coeur.style.backgroundImage = new StyleBackground(spriteChoisi);
            }

            conteneurCoeurs.Add(coeur);
        }
    }
}