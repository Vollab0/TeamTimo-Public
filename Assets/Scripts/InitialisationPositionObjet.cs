using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InitialisationPositionObjet : MonoBehaviour
{
    [HideInInspector] public Vector3 _positionInitial;
    
    void Awake()
    {
        _positionInitial = transform.position;
    }

}
