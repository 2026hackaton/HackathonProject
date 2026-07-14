using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private PlayerInput _pI;
    private Rigidbody2D _rb;
    [SerializeField] float speed = 8f;
    private void Awake()
    {
        _pI = GetComponent<PlayerInput>();
        _rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = _pI.move * speed;
    }
}
