using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportationObjet : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Le joueur tombe !");
            other.transform.position = new Vector3(0.0f, 5.5f, 0.0f); // Ne fonctionne pas...
        }
        else
        {
            other.GetComponent<Rigidbody>().velocity = new Vector3(0.0f, 0.0f, 0.0f);
            other.transform.position = other.GetComponent<InitialisationPositionObjet>()._positionInitial;
        }
    }

}
