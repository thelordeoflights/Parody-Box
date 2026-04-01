using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Gravity/Rotate Environment")]
public class RotateEnvironment : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject player;
    [SerializeField] private Transform castPoint;
    [SerializeField] private HologramHandler hologramHandler;
    [SerializeField] private PlayerInput playerInput;

    [Header("Detection Settings")]
    [SerializeField] private LayerMask wallMask;
    [SerializeField] private float castDistance = 20f;

    [Header("Teleport Settings")]
    [SerializeField] private float playerStandHeight = 1.0f;
    [SerializeField] private float transitionLockTime = 0.6f;

    // State tracking
    private bool _hasSelection;
    private bool _isTransitioning;
    private RaycastHit _currentHit;
    private Vector3 _pendingGravityDirection;

    // Input Actions
    private InputAction _hologramUp;
    private InputAction _hologramDown;
    private InputAction _hologramLeft;
    private InputAction _hologramRight;
    private InputAction _changeGravity;

    #region Unity Lifecycle

    private void Start()
    {
        InitializeReferences();
        InitializeInputs();
    }

    private void Update()
    {
        if (ShouldBlockInput()) return;

        HandleInput();
    }

    #endregion

    #region Initialization

    private void InitializeReferences()
    {
        if (player == null) player = gameObject;
        
        if (hologramHandler != null)
            hologramHandler.setActive(false);
    }

    private void InitializeInputs()
    {
        if (playerInput == null) return;

        _hologramUp    = playerInput.actions["HologramUP"];
        _hologramDown  = playerInput.actions["HologramDOWN"];
        _hologramLeft  = playerInput.actions["HologramLEFT"];
        _hologramRight = playerInput.actions["HologramRIGHT"];
        _changeGravity = playerInput.actions["ChangeGravity"];
    }

    #endregion

    #region Logic Execution

    private bool ShouldBlockInput()
    {
        return _isTransitioning || (GameManager.instance != null && GameManager.instance.isGameOver);
    }

    private void HandleInput()
    {
        // 1. Check for Gravity Confirmation
        if (_changeGravity.IsPressed() && _hasSelection)
        {
            StartCoroutine(ApplyGravityChange());
            return;
        }

        // 2. Check for Directional Selection
        if (_hologramLeft.IsPressed())       TrySelect(-transform.right);
        else if (_hologramRight.IsPressed()) TrySelect(transform.right);
        else if (_hologramUp.IsPressed())    TrySelect(transform.forward);
        else if (_hologramDown.IsPressed())  TrySelect(-transform.forward);
    }

    private void TrySelect(Vector3 direction)
    {
        // Project direction onto gravity plane to maintain orientation consistency
        Vector3 gravityUp = GravityManager.Instance.UpDirection;
        direction = Vector3.ProjectOnPlane(direction, gravityUp);

        // Fallback if looking straight up/down
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = gravityUp;
        }

        direction.Normalize();

        Vector3 origin = castPoint != null ? castPoint.position : transform.position;
        
        if (Physics.Raycast(origin, direction, out RaycastHit hit, castDistance, wallMask))
        {
            UpdateSelection(hit);
        }
        else
        {
            ClearSelection();
        }
    }

    private void UpdateSelection(RaycastHit hit)
    {
        _hasSelection = true;
        _currentHit = hit;
        _pendingGravityDirection = -hit.normal;

        if (hologramHandler != null)
        {
            hologramHandler.setActive(true);
            hologramHandler.setPosition(hit.normal, hit.point);
        }
    }

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
        if (hologramHandler != null)
            hologramHandler.setActive(false);
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        if (!_hasSelection) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_currentHit.point, 0.2f);
        Gizmos.DrawRay(_currentHit.point, _currentHit.normal * 0.5f);
    }

    #endregion
}