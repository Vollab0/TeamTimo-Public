using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class HeartSystem : MonoBehaviour
{
    [Header("Paramètres Santé")]
    // 1 coeur = 2 points de vie (Style Minecraft)
    public int maxHealth = 20;
    public int currentHealth;

    [Header("UI Toolkit References")]
    public UIDocument uiDocument;

    [Header("Sprites des Cœurs")]
    public Sprite fullHeartSprite;
    public Sprite halfHeartSprite;
    public Sprite emptyHeartSprite;

    // Référence au conteneur dans le UXML
    private VisualElement heartContainer;

    void Start()
    {
        // Initialisation de la santé
        currentHealth = maxHealth;

        // Si l'UIDocument n'est pas assigné, on essaie de le trouver
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        if (uiDocument != null)
        {
            // Récupération de la racine de l'UI
            var root = uiDocument.rootVisualElement;

            // On cherche l'élément créé dans le UXML par son nom
            // Assure-toi que ton VisualElement s'appelle bien "HeartContainer" dans le UI Builder
            heartContainer = root.Q<VisualElement>("HeartContainer");

            // Premier affichage
            UpdateHeartsUI();
        }
        else
        {
            Debug.LogError("HeartSystem : UIDocument manquant !");
        }
    }

    // Fonction publique à appeler depuis tes scripts de dégâts (ex: ennemi, piège)
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth < 0) currentHealth = 0;
        UpdateHeartsUI();
    }

    // Fonction publique à appeler depuis tes items de soin
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        UpdateHeartsUI();
    }

    // Mise à jour visuelle des coeurs
    private void UpdateHeartsUI()
    {
        if (heartContainer == null) return;

        // 1. On nettoie le conteneur (supprime les anciens coeurs)
        heartContainer.Clear();

        // 2. Calcul du nombre total de coeurs à afficher
        int totalHearts = maxHealth / 2;

        for (int i = 0; i < totalHearts; i++)
        {
            // Création d'un nouvel élément visuel pour ce coeur
            VisualElement heart = new VisualElement();
            heart.AddToClassList("heart-icon"); // Ajoute le style CSS défini dans le USS

            // Logique pour déterminer l'image (Plein, Demi, Vide)
            int heartStatus = currentHealth - (i * 2);

            if (heartStatus >= 2)
            {
                // Coeur Plein
                heart.style.backgroundImage = new StyleBackground(fullHeartSprite);
            }
            else if (heartStatus == 1)
            {
                // Demi Coeur
                heart.style.backgroundImage = new StyleBackground(halfHeartSprite);
            }
            else
            {
                // Coeur Vide
                heart.style.backgroundImage = new StyleBackground(emptyHeartSprite);
            }

            // Ajouter le coeur au conteneur
            heartContainer.Add(heart);
        }
    }
}