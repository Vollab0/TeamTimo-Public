using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CubesDetection : MonoBehaviour
{
    public int nbrCubes = 0;
    void OnTriggerEnter(Collider collider)
    {
        if (collider.CompareTag("Ramassable"))
        {
            nbrCubes++;
        }
    }

    void OnTriggerExit(Collider collider)
    {
        if (collider.CompareTag("Ramassable"))
        {
            nbrCubes--;
        }
    }
}
