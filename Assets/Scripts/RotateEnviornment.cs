using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [SerializeField] PlayerInput playerInput;
    InputAction HologramUP, HologramDOWN, HologramLEFT, HologramRIGHT, ChangeGravity;


    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        if (player == null)
            player = gameObject; // assume script is on the player

        hologramHandler.setActive(false);
        SetActions();
    }


    void SetActions()
    {

        HologramUP = playerInput.actions["HologramUP"];
        HologramDOWN = playerInput.actions["HologramDOWN"];
        HologramLEFT = playerInput.actions["HologramLEFT"];
        HologramRIGHT = playerInput.actions["HologramRIGHT"];
        ChangeGravity = playerInput.actions["ChangeGravity"];
    }



    private void Update()
    {
        if (_isTransitioning) return;
        
        if(GameManager.instance.isGameOver) return;
        
        // Confirm selection
        if (ChangeGravity.IsPressed() && _hasSelection)
        {
            StartCoroutine(ApplyGravityChange());
            return;
        }

        if (HologramLEFT.IsPressed())
            TrySelect(-transform.right);

        else if (HologramRIGHT.IsPressed())
            TrySelect(transform.right);

        else if (HologramUP.IsPressed())
            TrySelect(transform.forward);

        else if (HologramDOWN.IsPressed())
            TrySelect(-transform.forward);

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