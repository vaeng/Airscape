using UnityEngine;

/// <summary>
/// ScriptableObject holding player movement tuning values.
/// </summary>
[CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "U6-Multiplayer/Player Movement Settings")]
public class PlayerMovementSettings : ScriptableObject
{
    [Header("Speed")]
    public float walkSpeed = 10f;
    public float sprintSpeed = 20f;

    [Header("Jump")]
    public float jumpForce = 20f;
    public float coyoteTime = 0.3f;
    public float jumpBufferTime = 0.3f;

    [Header("Air Movement")]
    public float airSpeedMax = 20f;

    [Header("Gravity")]
    public float gravityScale = 2.99f;
    public float fallGravityMult = 1.25f;

    [Header("Slam")]
    public float slamForce = 28f;

    [Header("Look")]
    public float lookSensitivity = 0.1f;

    [Header("Interact")]
    public float interactRange = 15f;
}
