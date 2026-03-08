using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;

public enum CrouchInput
{
    None,
    Toggle
}

public enum Stance
{
    Stand, Crouch
}
public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public CrouchInput Crouch;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform cameraTarget;
    [Space]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;
    [Space]
    [SerializeField] private float jumpSpeed = 20f;
    [SerializeField] private float gravity = -90f;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float crouchCameraTargetHeight = 0.7f;
    private Stance _stance;
    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedCrouch;

    public void Initialize()
    {
        _stance = Stance.Stand;
        motor.CharacterController = this;

    }

    public void UpdateInput(CharacterInput input)
    {
        // Implement your character's input handling logic here
        _requestedRotation = input.Rotation;
        //take the 2d input vector and create a 3d movement vector on the XZ plane
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        //clamp the movement vector to a magnitude of 1 to prevent faster diagonal movement
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
        //orient the input so its relative to the direction the player is facing
        _requestedMovement = input.Rotation * _requestedMovement;
        _requestedJump = _requestedJump || input.Jump;
        _requestedCrouch = input.Crouch switch
        {
            CrouchInput.Toggle => !_requestedCrouch,
            CrouchInput.None => _requestedCrouch,
            _ => _requestedCrouch
            //if want hold instead of toggle
            //CrouchInput.Crouch => true,
            //CrouchInput.Uncrouch => false,
        };
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        if (motor.GroundingStatus.IsStableOnGround)
        {
            //snap the requested movement direction to the angle of the surface
            //the character is currently walking on
            var groundedMovement = motor.GetDirectionTangentToSurface(
                direction: _requestedMovement,
                surfaceNormal: motor.GroundingStatus.GroundNormal
            ) * _requestedMovement.magnitude;

            var speed = _stance is Stance.Stand ? walkSpeed : crouchSpeed;
            // and move along the ground in that direction
            currentVelocity = groundedMovement * speed;
        }
        else //else in the air
        {
            //gravity
            currentVelocity += motor.CharacterUp * gravity * deltaTime;
        }

        if (_requestedJump)
        {
            _requestedJump = false;
            // unstick from ground before applying jump
            motor.ForceUnground(time: 0f);
            //set min vertical speed to the jump speed
            var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
            //add the difference between current and target vertical speed to the current velocity
            currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);

        }

    }
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        // Implement your character's rotation update logic here
        //requested camera rotation
        var forward = Vector3.ProjectOnPlane(
            _requestedRotation * Vector3.forward,
            motor.CharacterUp
        );
        if (forward != Vector3.zero)
        {
            currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        // Implement any logic that needs to happen before the character update here

        //crouch
        if (_requestedCrouch && _stance is Stance.Stand)
        {
            _stance = Stance.Crouch;
            motor.SetCapsuleDimensions(height: crouchHeight, radius: motor.Capsule.radius, yOffset: crouchHeight * 0.5f);
        }
    }
    public void PostGroundingUpdate(float deltaTime)
    {
        // Implement any logic that needs to happen after grounding updates here
    }
    public void AfterCharacterUpdate(float deltaTime)
    {
        // Implement any logic that needs to happen after the character update here
        //uncrouch
        if (!_requestedCrouch && _stance is not Stance.Stand)
        {
            _stance = Stance.Stand;
            motor.SetCapsuleDimensions(height: standHeight, radius: motor.Capsule.radius, yOffset: standHeight * 0.5f);
        }
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        // Implement any logic that needs to happen when the character hits the ground here
    }
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        // Implement any logic that needs to happen when the character hits something while moving here
    }
    public bool IsColliderValidForCollisions(Collider coll) => true;

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {
        // Implement any logic that needs to happen when a discrete collision is detected here
    }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 hitVelocity, Quaternion hitRotation, ref HitStabilityReport hitStabilityReport)
    {
        // Implement any logic that needs to happen when processing a hit stability report here
    }

    public Transform GetCameraTarget() => cameraTarget;
}
