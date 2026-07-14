using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [SerializeField] private InputActionReference raise;
    public Vector2 move { get; private set; }
    private Rigidbody2D _rb;

    private HangUpBox _hangUp;

    void Awake()
    {
        _hangUp = GetComponent<HangUpBox>();
    }
    
    private void OnMove(InputValue value)
    {
        move = value.Get<Vector2>();
    }

    private void OnRaise(InputValue value)
    {
        if (value.isPressed)
        {
            Debug.Log("x키가 정상작동 됐습니다");
            if (_hangUp.currentBox == null)
            {
                _hangUp.TryGrabBox();
            }
            else
            {
                _hangUp.ReleaseBox();
            }
        }
    }
}
