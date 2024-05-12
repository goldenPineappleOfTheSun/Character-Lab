using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using UnityEngine;
using TMPro;

public enum SprintingState
{
    walk,
    sprint,
    recovery,
    exhausted
}

public class Player : MonoBehaviour
{
    public float speed = 3f;
    public float sprintSpeed = 6f;
    public float exhaustedSpeed = 2f;
    public float climbingSpeed = 2f;
    public float breaks = 0.5f;
    public float airbreaks = 0.8f;
    public float jump = 10f;
    public float aircontrol = 0.01f;
    public Vector2 cameraRotation = Vector2.zero;
    public new Camera camera;
    public float sensitivity = 10f;
    public float staminaWaste = 0.01f;
    public float staminaRecovery = 0.01f;
    public Graphic healthBar;
    public TMP_Text debugText1;
    public float fieldOfView = 35f;
    public LayerMask groundMask;

    Rigidbody rb; 
    new CapsuleCollider collider;
    float stamina = 1f;
    SmoothValue fov = new SmoothValue(1f, 1f, 1.4f, 0.01f);
    SmoothValue crouch = new SmoothValue(1f, 0.5f, 1f, 0.05f);
    DateTime lastTimeOnGround = DateTime.Now;
    DateTime lastTimeJumped = DateTime.Now;
    bool ladder = false;
    Collider ladderCollider;
    int ladderMask;

    float height {
        get {
            return collider.height * transform.localScale.y;
        }
    }

    bool _groundHitCached = false;
    RaycastHit _groundHit;
    RaycastHit groundHit 
    {
        get {
            if (!_groundHitCached)
            {
                if (JumpTime() > 0.2f)
                {
                    Physics.SphereCast(transform.position, collider.radius, -transform.up, out _groundHit, height / 2 - collider.radius + 0.1f, groundMask);
                }
                else
                {
                   _groundHit = new RaycastHit(); 
                }
                _groundHitCached = true;
            }
            return _groundHit;
        }
    }

    bool _ceilingHitCached = false;
    RaycastHit _ceilingHit;
    RaycastHit ceilingHit 
    {
        get {
            if (!_ceilingHitCached)
            {
                Physics.SphereCast(transform.position, collider.radius, transform.up, out _ceilingHit, height / 2 - collider.radius + 0.1f, groundMask);
                _ceilingHitCached = true;
            }
            return _ceilingHit;
        }
    }

    bool _wallsHitCached = false;
    bool _wallsHit;
    bool wallsHit 
    {
        get {
            if (!_wallsHitCached)
            {
                var direction = transform.rotation * Vector3.forward;
                direction = new Vector3(direction.x, 0, direction.z) * collider.radius;
                var start = transform.position + collider.center + Vector3.up * height * 0.4f + direction;
                var end = transform.position + collider.center - Vector3.up * height * 0.1f + direction;
                _wallsHit = Physics.CheckCapsule(start, end, collider.radius, groundMask);
                _wallsHitCached = true;
            }
            return _wallsHit;
        }
    }

    bool _stairsHitCached = false;
    bool _stairsHit;
    bool stairsHit 
    {
        get {
            if (!_stairsHitCached)
            {
                _stairsHit = false;
                var direction = transform.rotation * Vector3.forward;
                direction = new Vector3(direction.x, 0, direction.z) * collider.radius;
                var origin = transform.position + collider.center + direction;
                RaycastHit lowhit;
                if (Physics.SphereCast(origin, collider.radius * 0.3f, -Vector3.up, out lowhit, height * 0.33f, groundMask))
                {
                    if (lowhit.distance < height / 2)
                    {
                        RaycastHit highHit;
                        origin = transform.position + collider.center + direction - Vector3.up * (lowhit.distance + 0.1f);
                        if (!Physics.SphereCast(origin, collider.radius * 0.3f, Vector3.up, out highHit, height, groundMask))
                        {
                            _stairsHit = true;
                        }
                    }
                }
                _stairsHitCached = true;
            }
            return _stairsHit;
        }
    }

    bool _sprintStateCached = false;
    SprintingState _sprintState = SprintingState.walk;
    SprintingState sprintState
    {
        get 
        {
            if (!_sprintStateCached)
            {
                if (_sprintState == SprintingState.sprint && stamina <= 0.1f)
                {
                    _sprintState = SprintingState.exhausted;
                    _sprintStateCached = true;
                    return _sprintState;
                }
                if (_sprintState == SprintingState.recovery && stamina < 1f)
                {
                    _sprintStateCached = true;
                    return _sprintState;
                }
                if (_sprintState == SprintingState.exhausted && stamina < 1f)
                {
                    _sprintStateCached = true;
                    return _sprintState;
                }
                if (_sprintState == SprintingState.recovery && stamina >= 1f)
                {
                    _sprintState = SprintingState.walk;
                    _sprintStateCached = true;
                    return _sprintState;
                }
                _sprintState = Input.GetKey(KeyCode.LeftShift) ? SprintingState.sprint : SprintingState.walk;
                _sprintStateCached = true;
            }
            return _sprintState;
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        collider = GetComponent<CapsuleCollider>();
        ladderMask = LayerMask.NameToLayer("Ladder");
    }

    void Update()
    {
        ClearCache();
        if (!ladder)
        {
            Movement();
        }
        else
        {
            Ladder();
        }
        CameraMovement();
        GUI();
    }

    void Movement()
    {
        var grounded = IsTouchingGround();
        var leaned = IsTouchingWall();
        var ducking = Input.GetKey(KeyCode.LeftControl) ? 0.5f : IsTouchingCeiling() ? crouch.value : 1f;
        var canjump =  JumpTime() > 0.3f && (grounded || AirTime() < 0.2f) && ducking > 0.9f;
        var stairs = stairsHit;
        var nomove = !Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.D); 
        var dx = 0f;
        var dy = 0f;

        rb.useGravity = true;

        if (Input.GetKey(KeyCode.W))
        {
            dy += 1;
        }

        if (Input.GetKey(KeyCode.A))
        {
            dx += -1;
        }

        if (Input.GetKey(KeyCode.S))
        {
            dy += -1;
        }

        if (Input.GetKey(KeyCode.D))
        {
            dx += 1;
        }

        if (dx != 0 && dy != 0)
        {
            var koeff = 1 / Mathf.Sqrt(dx*dx + dy*dy);
            dx *= koeff;
            dy *= koeff;
        }

        var sprintMultiplier = ducking > 0.8f && grounded
            ? (sprintState == SprintingState.sprint && grounded ? sprintSpeed / speed : sprintState == SprintingState.exhausted ? exhaustedSpeed / speed : 1f)
            :  exhaustedSpeed / speed;
        var move = transform.forward * dy * speed * sprintMultiplier + transform.right * dx * speed * sprintMultiplier;
        
        var staminaDelta = sprintState == SprintingState.sprint ? -staminaWaste : staminaRecovery;
        stamina = Mathf.Clamp(stamina + staminaDelta, 0f, 1f);

        if (grounded)
        {
            Approach(move.x, null, move.z);
        }
        else
        {
            Approach(move.x, null, move.z, 0.001f);
        }

        if (canjump && Input.GetKeyDown(KeyCode.Space))
        {
            Approach(null, jump, null);
            lastTimeJumped = DateTime.Now;
        }

        if (!grounded && JumpTime() < 1f && Input.GetKey(KeyCode.Space))
        {
            Apply(0, aircontrol, 0);
        }

        if (nomove && grounded && !Input.GetKey(KeyCode.Space))
        {
            Approach(rb.velocity.x * breaks, rb.velocity.y * breaks, rb.velocity.z * breaks);
        }

        rb.velocity = new Vector3(
            Mathf.Clamp(rb.velocity.x, -50f, 50f),
            Mathf.Clamp(rb.velocity.y, -50f, 50f),
            Mathf.Clamp(rb.velocity.z, -50f, 50f)
        );

        collider.material.dynamicFriction = stairs ? 0f : !grounded ? 0f : leaned ? 0.1f : 0.5f;
       // debugText1.text =  ladder.ToString();

        var dot = Vector3.Dot(camera.transform.forward.normalized, rb.velocity.normalized);
        camera.fieldOfView = fov.Update(0.5f * dot * rb.velocity.sqrMagnitude / (speed * speed), Time.deltaTime) * fieldOfView;

        transform.localScale = new Vector3(1f, crouch.Update(ducking, Time.deltaTime), 1f);
    }

    void Ladder()
    {
        if (IsTouchingGround() && !Input.GetKey(KeyCode.W))
        {
            Movement();
            return;
        }

        rb.useGravity = false;

        var dx = 0f;
        var dy = 0f;

        if (Input.GetKey(KeyCode.W))
        {
            dy += 1;
        }

        if (Input.GetKey(KeyCode.A))
        {
            dx -= 1;
        }

        if (Input.GetKey(KeyCode.S))
        {
            dy -= 1;
        }

        if (Input.GetKey(KeyCode.D))
        {
            dx += 1;
        }

        if (dx != 0 && dy != 0)
        {
            var koeff = 1 / Mathf.Sqrt(dx*dx + dy*dy);
            dx *= koeff;
            dy *= koeff;
        }

        var top = transform.position.y - ladderCollider.ClosestPoint(transform.position).y;
        var df = 0f;
        if (top > height / 4 && dy > 0)
        {
            df = dy;
            dy *= 0.1f;
        }

        rb.velocity = transform.up * dy * climbingSpeed + transform.right * dx * climbingSpeed + transform.forward * df * climbingSpeed;

        collider.material.dynamicFriction = 0.5f;
    }

    void CameraMovement()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;
        
        cameraRotation = new Vector2(
            cameraRotation.x + mouseX, 
            Mathf.Clamp(cameraRotation.y + mouseY, -90f, 90f)
        );

        this.transform.rotation = Quaternion.Euler(0f, cameraRotation.x, 0f);
        camera.transform.rotation = Quaternion.Euler(-cameraRotation.y, cameraRotation.x, 0f);
    }

    void OnTriggerStay(Collider other)
    {
        //debugText1.text = (transform.position.y - other.ClosestPoint(transform.position).y).ToString() + " " + (height / 2).ToString();
        if (other.gameObject.layer == ladderMask)
        {
            ladder = true;
            ladderCollider = other;
            return;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == ladderMask)
        {
            ladder = false;
        }
    }

    void GUI()
    {
        var s = healthBar.transform.localScale;
        healthBar.transform.localScale = new Vector2(stamina, s.y);
    }

    void Apply(float x, float y, float z)
    {
        rb.velocity = new Vector3(rb.velocity.x + x, rb.velocity.y + y, rb.velocity.z + z);
    }

    void Approach(float? x, float? y, float? z, float smoothness = 1)
    {
        var target = new Vector3(x ?? rb.velocity.x, y ?? rb.velocity.y, z ?? rb.velocity.z);
        rb.velocity = new Vector3(
            rb.velocity.x + (target.x - rb.velocity.x) * smoothness, 
            rb.velocity.y + (target.y - rb.velocity.y) * smoothness, 
            rb.velocity.z + (target.z - rb.velocity.z) * smoothness
        );
    }

    bool IsTouchingGround()
    {
        if (groundHit.transform != null)
        {
            lastTimeOnGround = DateTime.Now;
        }
        return groundHit.transform != null;
    }

    bool IsTouchingCeiling()
    {
        return ceilingHit.transform != null;
    }

    bool IsTouchingWall()
    {
        return wallsHit;
    }

    float AirTime()
    {
        return (float)((DateTime.Now - lastTimeOnGround).TotalMilliseconds) / 1000;
    }

    float JumpTime()
    {
        return (float)((DateTime.Now - lastTimeJumped).TotalMilliseconds) / 1000;
    }

    void ClearCache()
    {
        _groundHitCached = false;
        _ceilingHitCached = false;
        _wallsHitCached = false;
        _stairsHitCached = false;
        _sprintStateCached = false;
    }
}
