using UnityEngine;
using System; // Nécessaire pour les Actions (Events)

public class PlayerHealth : MonoBehaviour
{
    [Header("Configuration")]
    public int nombreDeCoeurs = 3;
    
    // --- LE SIGNAL RADIO ---
    // Les autres scripts peuvent s'abonner à cet évènement pour savoir quand la vie change.
    public event Action<int> OnSanteChangee;

    private int vieActuelle;

    void Start()
    {
        vieActuelle = nombreDeCoeurs * 2;
        // On envoie le signal dès le début pour initialiser l'UI
        NotifierLeChangement();
    }

    void Update()
    {
        // Tests (Debug)
        if (Input.GetKeyDown(KeyCode.K)) Blesser(1);
        if (Input.GetKeyDown(KeyCode.H)) Soigner(1);
    }

    public void Blesser(int degats)
    {
        vieActuelle -= degats;
        if (vieActuelle < 0) vieActuelle = 0;
        
        NotifierLeChangement(); // On prévient l'UI !
    }

    public void Soigner(int soin)
    {
        vieActuelle += soin;
        int maxVie = nombreDeCoeurs * 2;
        if (vieActuelle > maxVie) vieActuelle = maxVie;

        NotifierLeChangement(); // On prévient l'UI !
    }

    private void NotifierLeChangement()
    {
        // Si quelqu'un écoute (l'UI), on lui envoie la nouvelle valeur de vie
        OnSanteChangee?.Invoke(vieActuelle);
    }
}