using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{

    static readonly int AnimatorHash = Animator.StringToHash("IsWalking");

    [SerializeField] PlayerBehaviour player;

    Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        animator.SetBool(AnimatorHash, player.IsWalking);
    }

}
