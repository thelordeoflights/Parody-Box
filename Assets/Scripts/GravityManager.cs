using UnityEngine;
using Cinemachine;

/// <summary>
/// Singleton that manages gravity direction and smooth transitions.
/// Attach to any persistent GameObject in the scene.
/// </summary>
public class GravityManager : MonoBehaviour
{
    public static GravityManager Instance { get; private set; }

    [Header("Gravity Settings")]
    [Tooltip("Gravity strength in m/s²")]
    public float GravityStrength = 15f;

    [Tooltip("How fast gravity direction interpolates to the new direction")]
    public float TransitionSpeed = 4f;

    [Header("Camera")]
    [Tooltip("Assign a Transform whose Y-axis Cinemachine uses as world-up.\n" +
             "In CinemachineBrain, set 'World Up Override' to this transform.")]
    public Transform WorldUpReference;

    // ── Public accessors ────────────────────────────────────────────────────────
    /// <summary>Current (possibly mid-transition) gravity direction (unit vector).</summary>
    public Vector3 GravityDirection => _currentDirection;

    /// <summary>Opposite of GravityDirection — the player's "up".</summary>
    public Vector3 UpDirection => -_currentDirection;

    /// <summary>True while gravity is still rotating toward its target.</summary>
    public bool IsTransitioning => Vector3.Angle(_currentDirection, _targetDirection) > 0.5f;

    // ── Private state ────────────────────────────────────────────────────────────
    private Vector3 _targetDirection = Vector3.down;
    private Vector3 _currentDirection = Vector3.down;

    // ── Unity lifecycle ──────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        // Smoothly slerp current direction toward target
        if (IsTransitioning)
        {
            _currentDirection = Vector3.Slerp(
                _currentDirection, _targetDirection,
                Time.deltaTime * TransitionSpeed).normalized;
        }

        // Push to Unity physics
        Physics.gravity = _currentDirection * GravityStrength;

        // Keep Cinemachine world-up in sync
        if (WorldUpReference != null)
            WorldUpReference.up = UpDirection;
    }

    // ── Public API ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Change gravity to pull toward a surface.
    /// Pass the surface's hit.normal — gravity will point OPPOSITE to it.
    /// </summary>
    // public void SetGravityDirection(Vector3 newDirection)
    // {
    //     _targetDirection = newDirection.normalized;
    // }
    /// <summary>
/// Set gravity to pull in <paramref name="pullDirection"/>.
/// For normal floor gravity use Vector3.down.
/// For wall/ceiling walking use -hit.normal (i.e. into the surface).
/// </summary>
public void SetGravityDirection(Vector3 pullDirection)
{
    _targetDirection = pullDirection.normalized;
}

/// <summary>
/// Convenience wrapper: pass the RaycastHit/collision normal directly.
/// Gravity is automatically inverted so it pulls TOWARD the surface.
/// </summary>
public void SetGravityTowardSurface(Vector3 surfaceNormal)
{
    _targetDirection = (-surfaceNormal).normalized;
}
}