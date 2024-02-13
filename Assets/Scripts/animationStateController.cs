using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationStateController : MonoBehaviour
{

    private Animator animator;
    bool[] stateArray = new bool[4];
    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        stateArray = GetComponentInParent<PlayerController>().GetState();
        animator.SetBool("isWalking", stateArray[0]);
        animator.SetBool("isSprinting", stateArray[1]);
        animator.SetBool("isJumping", stateArray[2]);
        animator.SetBool("isFalling", stateArray[3]);
        animator.SetBool("isHolding", GetComponentInParent<AttraperObjets>().IsHolding());
    }
}
