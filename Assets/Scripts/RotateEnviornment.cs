using System.Collections;
using UnityEngine;

/// <summary>
/// Detects surfaces in four directions and lets the player change gravity toward them.
/// 
/// Controls (default):
///   Arrow keys  — preview a surface in that direction
///   Enter       — confirm and apply gravity change
/// </summary>
public class RotateEnvironment : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player GameObject (with ThirdPersonController)")]
    public GameObject player;

    [Tooltip("Origin of the surface-detection raycasts")]
    public Transform castPoint;

    [Tooltip("HologramHandler for showing the target surface preview")]
    public HologramHandler hologramHandler;

    [Header("Detection")]
    [Tooltip("Layer(s) that count as selectable surfaces (your 'wall' layer)")]
    public LayerMask wallMask;

    [Tooltip("Max distance to scan for surfaces")]
    public float castDistance = 20f;

    [Header("Teleport")]
    [Tooltip("How far above the new surface to place the player on gravity change")]
    public float playerStandHeight = 1.0f;

    [Tooltip("Duration of the camera/transition lock after gravity changes")]
    public float transitionLockTime = 0.6f;

    // ── Private state ─────────────────────────────────────────────────────────
    private bool      _hasSelection;
    private RaycastHit _currentHit;
    private Vector3   _pendingGravityDirection;
    private bool      _isTransitioning;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        if (player == null)
            player = gameObject; // assume script is on the player

        hologramHandler.setActive(false);
    }

    private void Update()
    {
        if (_isTransitioning) return;

        // Confirm selection
        if (Input.GetKeyDown(KeyCode.Return) && _hasSelection)
        {
            StartCoroutine(ApplyGravityChange());
            return;
        }

        // ── Arrow keys → cast in four directions relative to current gravity plane ──
        // Left / Right : player's local left/right (projected onto current gravity plane)
        // Up arrow     : player's local forward (projected onto current gravity plane)
        // Down arrow   : opposite of player's forward (behind the player)
        //
        // Tip: gravity direction itself (straight down) can be selected by simply jumping —
        // the ground is always in that direction.

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            TrySelect(-transform.right);

        else if (Input.GetKeyDown(KeyCode.RightArrow))
            TrySelect(transform.right);

        else if (Input.GetKeyDown(KeyCode.UpArrow))
            TrySelect(transform.forward);

        else if (Input.GetKeyDown(KeyCode.DownArrow))
            TrySelect(-transform.forward);

        // Optional: also let the player scan directly above (current ceiling)
        else if (Input.GetKeyDown(KeyCode.Space) && Input.GetKey(KeyCode.LeftShift))
            TrySelect(GravityManager.Instance.UpDirection); // toward current ceiling
    }

    // ── Surface detection ─────────────────────────────────────────────────────

    private void TrySelect(Vector3 direction)
    {
        // Project direction onto the gravity plane so we don't accidentally shoot
        // straight up or straight down unintentionally.
        direction = Vector3.ProjectOnPlane(direction, GravityManager.Instance.UpDirection);

        if (direction.sqrMagnitude < 0.001f)
        {
            // Direction was parallel to gravity (e.g. player pressed up while looking straight up).
            // Fall back to the raw direction.
            direction = GravityManager.Instance.UpDirection;
        }

        direction.Normalize();

        Vector3 origin = castPoint != null ? castPoint.position : transform.position;
        Debug.DrawRay(origin, direction * castDistance, Color.yellow, 1f);

        if (Physics.Raycast(origin, direction, out RaycastHit hit, castDistance, wallMask))
        {
            _hasSelection            = true;
            _currentHit              = hit;

            // Gravity will pull TOWARD this surface (opposite of its outward normal)
            _pendingGravityDirection = -hit.normal;

            // Show hologram aligned with the surface
            hologramHandler.setActive(true);
            hologramHandler.setPosition(hit.normal, hit.point);
        }
        else
        {
            ClearSelection();
        }
    }

    // ── Gravity application ───────────────────────────────────────────────────

    private IEnumerator ApplyGravityChange()
    {
        _isTransitioning = true;

        GravityManager.Instance.SetGravityDirection(_pendingGravityDirection);

        if (player != null)
        {
            Vector3 landPosition = _currentHit.point + _currentHit.normal * playerStandHeight;
            player.transform.position = landPosition;

            // ❌ REMOVE this line — it was fighting GravityManager's slerp
            // player.transform.up = _currentHit.normal;  
        }

        ClearSelection();
        yield return new WaitForSeconds(transitionLockTime);
        _isTransitioning = false;
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        hologramHandler.setActive(false);
    }

    // ── Debug visualization ───────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (!_hasSelection) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_currentHit.point, 0.2f);
        Gizmos.DrawRay(_currentHit.point, _currentHit.normal * 0.5f);
    }
}