using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ButDetection : MonoBehaviour
{
    void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.name == "Balle")
        {
            // Cherche l'objet "UIDocumentBut" dans la scène
            GameObject uiDocumentBut = GameObject.Find("UIDocumentBut");

            // Vérifie si l'objet a été trouvé
            if (uiDocumentBut != null)
            {
                // Active l'objet trouvé
                uiDocumentBut.GetComponent<UIDocument>().enabled = true;
                StartCoroutine(FermetureFenetre(uiDocumentBut));
            }
            else
            {
                Debug.LogError("Objet 'UIDocumentBut' introuvable dans la scène.");
            }
        }
    }

    private IEnumerator FermetureFenetre(GameObject gameObject)
    {
        yield return new WaitForSeconds(2);
        gameObject.GetComponent<UIDocument>().enabled = false;
    }
}
