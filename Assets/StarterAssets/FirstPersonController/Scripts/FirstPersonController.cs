﻿using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System.Collections;
using Random = UnityEngine.Random;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED


#endif

[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
[RequireComponent(typeof(PlayerInput))]
#endif
public class FirstPersonController : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Move speed of the character in m/s")]
    public float MoveSpeed = 4.0f;
    [Tooltip("Sprint speed of the character in m/s")]
    public float SprintSpeed = 6.0f;
    [Tooltip("Rotation speed of the character")]
    public float RotationSpeed = 1.0f;
    [Tooltip("Acceleration and deceleration")]
    public float SpeedChangeRate = 10.0f;

    [Space(10)]
    [Tooltip("The height the player can jump")]
    public float JumpHeight = 1.2f;
    [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
    public float Gravity = -15.0f;

    [Space(10)]
    [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
    public float JumpTimeout = 0.1f;
    [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
    public float FallTimeout = 0.15f;

    [Header("Movement States")]
    [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
    public bool Grounded = true;
    public bool Submerged = false;
    public bool Swimming = false;
    public bool FlyMode = false;
    public Voxel.Material GroundMaterial;
    [Tooltip("Useful for rough ground")]
    public float GroundedOffset = -0.14f;
    [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
    public float GroundedRadius = 0.5f;
    [Tooltip("What layers the character uses as ground")]
    public LayerMask GroundLayers;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;
    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 90.0f;
    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -90.0f;

    // cinemachine
    private float _cinemachineTargetPitch;

    // player
    private float _speed;
    private float _rotationVelocity;
    public float _verticalVelocity;
    private float _terminalVelocity = 53.0f;

    // timeout deltatime
    private float _jumpTimeoutDelta;
    private float _fallTimeoutDelta;

    private CharacterController _controller;
    private StarterAssetsInputs _input;
    private GameObject _mainCamera;
    private SculptingController _interactionManager;

    private const float _threshold = 0.01f;

    private World world;

    bool alive = true;

    private GameManager gameManager;
    private UIManager UIManager;

    private void Awake()
    {
        // get a reference to our main camera
        if (_mainCamera == null)
        {
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }
    }

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<StarterAssetsInputs>();
        _interactionManager = GetComponent<SculptingController>();
        _interactionManager.enabled = true;

        world = GameObject.FindGameObjectWithTag("World").GetComponent<World>();
        UIManager = GameObject.FindGameObjectWithTag("Canvas").GetComponent<UIManager>();
        gameManager = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GameManager>();

        // reset our timeouts on start
        _jumpTimeoutDelta = JumpTimeout;
        _fallTimeoutDelta = FallTimeout;
    }

    private void Update()
    {
        if (!FlyMode)
        {
            JumpAndGravity();
            Move();
        }
        else Fly();

        GroundedCheck();
    }

    public void OnTeleport()
    {
        transform.position = new Vector3(0f, gameManager.world.chunkHeight, 0f);

    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    public Voxel.Material GetGroundMaterial()
    {
        return world.GetVoxel(transform.position + new Vector3(0, -1, 0)).material;
    }

    private void GroundedCheck()
    {
        // set sphere position, with offset
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
        bool touchingGround = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

        if (!Grounded && touchingGround)
        {
            Grounded = true;
        }
        else if (Grounded && !touchingGround)
        {
            Grounded = false;
        }

        //Check if swimming
        Swimming = transform.position.y < 8.7f;
    }

    private void CameraRotation()
    {

        // if there is an input
        if (_input.look.sqrMagnitude >= _threshold)
        {
            _cinemachineTargetPitch += _input.look.y * RotationSpeed * Time.deltaTime;
            _rotationVelocity = _input.look.x * RotationSpeed * Time.deltaTime;

            // clamp our pitch rotation
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

            // Update Cinemachine camera target pitch
            CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

            // rotate the player left and right
            transform.Rotate(Vector3.up * _rotationVelocity);
        }
    }

    private void Move()
    {
        // set target speed based on move speed, sprint speed and if sprint is pressed
        float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;
        // Adjust speed when swimming
        if (Swimming) targetSpeed /= 2;

        // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

        // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
        // if there is no input, set the target speed to 0
        if (_input.move == Vector2.zero)
        {
            targetSpeed = 0.0f;
        }

        // a reference to the players current horizontal velocity
        float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

        float speedOffset = 0.1f;
        float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

        // accelerate or decelerate to target speed
        if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            // creates curved result rather than a linear one giving a more organic speed change
            // note T in Lerp is clamped, so we don't need to clamp our speed
            _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

            // round speed to 3 decimal places
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = targetSpeed;
        }

        // normalise input direction
        Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

        // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
        // if there is a move input rotate player when the player is moving
        if (_input.move != Vector2.zero)
        {
            // move
            inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
        }

        // move the player
        _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
    }

    private void Fly()
    {
        // set target speed based on move speed, sprint speed and if sprint is pressed
        float targetSpeed = (_input.sprint ? SprintSpeed : MoveSpeed) * 5f;

        _verticalVelocity = 0f;

        // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

        if (_input.move == Vector2.zero)
        {
            targetSpeed = 0.0f;
        }

        // a reference to the players current horizontal velocity
        float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

        float speedOffset = 0.1f;
        float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

        // accelerate or decelerate to target speed
        if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            // creates curved result rather than a linear one giving a more organic speed change
            // note T in Lerp is clamped, so we don't need to clamp our speed
            _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

            // round speed to 3 decimal places
            _speed = Mathf.Round(_speed * 1000f) / 1000f;
        }
        else
        {
            _speed = targetSpeed;
        }

        // normalise input direction
        Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

        // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
        // if there is a move input rotate player when the player is moving
        if (_input.move != Vector2.zero)
        {
            // move
            inputDirection = transform.right * _input.move.x + CinemachineCameraTarget.transform.forward * _input.move.y;
        }

        // move the player
        _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
    }

    public void OnToggleFlyMode()
    {
        FlyMode = !FlyMode;
    }

    private void JumpAndGravity()
    {

        if (Grounded)
        {
            // reset the fall timeout timer
            _fallTimeoutDelta = FallTimeout;

            // stop our velocity dropping infinitely when grounded
            if (_verticalVelocity < 0.0f)
            {
                _verticalVelocity = -2f;
            }

            // Jump
            if (_input.jump && _jumpTimeoutDelta <= 0.0f)
            {
                // the square root of H * -2 * G = how much velocity needed to reach desired height
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            }

            // jump timeout
            if (_jumpTimeoutDelta >= 0.0f)
            {
                _jumpTimeoutDelta -= Time.deltaTime;
            }
        }
        else
        {
            // reset the jump timeout timer
            _jumpTimeoutDelta = JumpTimeout;

            // fall timeout
            if (_fallTimeoutDelta >= 0.0f)
            {
                _fallTimeoutDelta -= Time.deltaTime;
            }

            if (Swimming) _verticalVelocity = -2f;

            // if we are not grounded, do not jump
            //_input.jump = false;
        }

        //apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
        if (_verticalVelocity > -_terminalVelocity)
        {
            _verticalVelocity += Gravity * Time.deltaTime;
        }
        /*
		if (Grounded && !_input.jump)
        {
			_verticalVelocity = -2f;
		}
		if (Grounded && _input.jump && _jumpTimeoutDelta <= 0.0f)
		{
			_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
		}

		if (!Grounded && _input.jump && _verticalVelocity < _terminalVelocity) _verticalVelocity += 20f * Time.deltaTime;

		print(_verticalVelocity);
		*/
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }

    private void OnDrawGizmosSelected()
    {
        Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
        Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

        if (Grounded) Gizmos.color = transparentGreen;
        else Gizmos.color = transparentRed;

        // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
        Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == 4)
        {
            Submerged = true;
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == 4)
        {
            Submerged = false;
        }
    }


    void Die()
    {
        alive = false;
        print("Dead :(");
    }
}
