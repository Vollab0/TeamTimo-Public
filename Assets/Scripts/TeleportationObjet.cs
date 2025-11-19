
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
            other.GetComponent<CharacterController>().enabled = false;
            other.GetComponent<AttraperObjets>().LacherObjet();
            other.transform.position = new Vector3(0.0f, 5.5f, 0.0f);
            other.GetComponent<CharacterController>().enabled = true;
        }
        else
        {
            other.GetComponent<Rigidbody>().linearVelocity = new Vector3(0.0f, 0.0f, 0.0f);
            other.transform.rotation = new Quaternion(0.0f, 0.0f, 0.0f, 0.0f);
            other.transform.position = other.GetComponent<InitialisationPositionObjet>()._positionInitial;
        }
    }

}
