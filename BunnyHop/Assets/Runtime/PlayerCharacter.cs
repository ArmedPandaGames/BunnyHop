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
    Stand, Crouch, Slide
}

public struct CharacterState
{
    public bool Grounded;
    public Stance Stance;
}
public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public CrouchInput Crouch;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform root;
    [SerializeField] private Transform cameraTarget;
    [Space]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;
    [SerializeField] private float walkResponse = 25f; //how quickly the character accelerates and decelerates to changes in movement input
    [SerializeField] private float crouchResponse = 20f; //how quickly the character accelerates and decelerates to changes in movement input while crouching. Generally should be lower than walkResponse to feel better to crouch
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float airAcceleration = 70f;
    [Space]
    [SerializeField] private float jumpSpeed = 20f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpSustainGravity = 0.4f;
    [SerializeField] private float gravity = -90f;
    [Space]
    [SerializeField] private float slideStartSpeed = 25f;
    [SerializeField] private float slideEndSpeed = 15f; //if speed lower than this player will no longer slide
    [SerializeField] private float slideFriction = 0.8f;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponse = 15f; //how quickly the character capsule changes height when crouching/uncrouching
    [Range(0f, 1f)]
    [SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0f, 1f)]
    [SerializeField] private float crouchCameraTargetHeight = 0.7f;
    private CharacterState _state;
    private CharacterState _lastState;
    private CharacterState _tempState;
    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedSustainedJump;
    private bool _requestedCrouch;
    private Collider[] _uncrouchOverlapResults;

    public void Initialize()
    {
        _state.Stance = Stance.Stand;
        _lastState = _state;

        _uncrouchOverlapResults = new Collider[8];
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
        _requestedSustainedJump = input.JumpSustain;
        _requestedCrouch = input.Crouch switch
        {
            //toggle
            /*
            CrouchInput.Toggle => !_requestedCrouch,
            CrouchInput.None => _requestedCrouch,
            _ => _requestedCrouch
            */
            //if want hold instead of toggle
            CrouchInput.Toggle => true,
            CrouchInput.None => false,
            _ => _requestedCrouch

        };
    }

    public void UpdateBody(float deltaTime)
    {
        var currentHeight = motor.Capsule.height;
        var normalizedHeight = currentHeight / standHeight;
        var cameraTargetHeight = currentHeight * (
            _state.Stance is Stance.Stand ? standCameraTargetHeight : crouchCameraTargetHeight
        );
        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);
        cameraTarget.localPosition = Vector3.Lerp(
            a: cameraTarget.localPosition,
            b: new Vector3(0f, cameraTargetHeight, 0f),
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );
        root.localScale = Vector3.Lerp(
            a: root.localScale,
            b: rootTargetScale,
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );
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

            //start sliding
            {
                var moving = groundedMovement.sqrMagnitude > 0f;
                var crouching = _state.Stance is Stance.Crouch;
                var wasStanding = _lastState.Stance is Stance.Stand;
                var wasInAir = !_lastState.Grounded;
                if (moving && crouching && (wasStanding || wasInAir))
                {
                    _state.Stance = Stance.Slide;
                    var slideSpeed = Mathf.Max(slideStartSpeed, currentVelocity.magnitude);
                    currentVelocity = motor.GetDirectionTangentToSurface
                    (
                        direction: currentVelocity,
                        surfaceNormal: motor.GroundingStatus.GroundNormal
                    ) * slideSpeed;
                }
            }
            //Move
            if (_state.Stance is Stance.Stand or Stance.Crouch)
            {
                //calculate target velocity based on move speed, then smooth towards it based on acceleration speed
                var speed = _state.Stance is Stance.Stand ? walkSpeed : crouchSpeed;
                var response = _state.Stance is Stance.Stand ? walkResponse : crouchResponse;
                // and move along the ground in that direction
                var targetVelocity = groundedMovement * speed;
                currentVelocity = Vector3.Lerp(
                    a: currentVelocity,
                    b: targetVelocity,
                    t: 1f - Mathf.Exp(-response * deltaTime)
                    );

            }
            else //continue sliding
            {
                //friction
                currentVelocity -= currentVelocity * (slideFriction * Time.deltaTime);
                //stop
                if (currentVelocity.magnitude < slideEndSpeed)
                {
                    _state.Stance = Stance.Crouch;
                }
            }
        }
        else //else in the air
        {
            //move
            if (_requestedMovement.sqrMagnitude > 0f)
            {
                //requested movement projected onto movement plane
                var planarMovement = Vector3.ProjectOnPlane(
                    vector: _requestedMovement,
                    planeNormal: motor.CharacterUp
                ) * _requestedMovement.magnitude;

                //current velocity on movement plane
                var currentPlanarVelocity = Vector3.ProjectOnPlane
                (
                    vector: currentVelocity,
                    planeNormal: motor.CharacterUp
                );

                //calculate movement
                var movementForce = planarMovement * airAcceleration * deltaTime;
                //add it to the current planar velocity for target velocity
                var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                //limit target velocity to air speed
                targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                //stear towards current velocity
                currentVelocity += targetPlanarVelocity - currentPlanarVelocity;
            }
            //gravity
            var effectiveGravity = gravity;
            var verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            if (_requestedSustainedJump && verticalSpeed > 0f)
                effectiveGravity *= jumpSustainGravity;
            currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime;
        }

        if (_requestedJump)
        {
            _requestedJump = false;
            _requestedCrouch = false;
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
        _tempState = _state;
        //crouch
        if (_requestedCrouch && _state.Stance is Stance.Stand)
        {
            _state.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions(height: crouchHeight, radius: motor.Capsule.radius, yOffset: crouchHeight * 0.5f);
        }
    }
    public void PostGroundingUpdate(float deltaTime)
    {
        // Implement any logic that needs to happen after grounding updates here
        if (!motor.GroundingStatus.IsStableOnGround && _state.Stance is Stance.Slide)
        {
            _state.Stance = Stance.Crouch;
        }
    }
    public void AfterCharacterUpdate(float deltaTime)
    {
        // Implement any logic that needs to happen after the character update here
        //uncrouch
        if (!_requestedCrouch && _state.Stance is not Stance.Stand)
        {
            // tentatively stand up the character capsule
            motor.SetCapsuleDimensions(height: standHeight, radius: motor.Capsule.radius, yOffset: standHeight * 0.5f);
            //then see if that would cause a collision with anything
            var pos = motor.TransientPosition;
            var rot = motor.TransientRotation;
            var mask = motor.CollidableLayers;
            if (motor.CharacterOverlap(pos, rot, _uncrouchOverlapResults, mask, QueryTriggerInteraction.Ignore) > 0)
            {
                //if there was a collision, crouch back down
                motor.SetCapsuleDimensions(height: crouchHeight, radius: motor.Capsule.radius, yOffset: crouchHeight * 0.5f);
            }
            else
            {
                //if there was no collision, finalize standing up
                _state.Stance = Stance.Stand;
            }
        }
        //update state to reflect relevant motor properties
        _state.Grounded = motor.GroundingStatus.IsStableOnGround;
        _lastState = _tempState;
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

    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        motor.SetPosition(position);
        if (killVelocity)
            motor.BaseVelocity = Vector3.zero;
    }
}
