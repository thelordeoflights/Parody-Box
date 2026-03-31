using UnityEngine;

public class HologramHandler : MonoBehaviour
{
    [SerializeField] GameObject holoGameObject;

    [Tooltip("The player transform used to anchor the hologram above their head")]
    [SerializeField] Transform playerTransform;

    [Tooltip("How far above the player's local origin the hologram floats")]
    [SerializeField] float heightAbovePlayer = 2.2f;

    // Cached so we can update position every frame while active
    private Vector3 _currentUpVector;
    private bool    _isActive;

    void Start()
    {
        if (holoGameObject == null)
            holoGameObject = GameObject.FindGameObjectWithTag("hologram");

        holoGameObject.SetActive(false);
    }

    void LateUpdate()
    {
        // Keep the hologram floating above the player's head every frame,
        // but never inherit the player's rotation — only the surface-normal orientation.
        if (_isActive && playerTransform != null)
        {
            Vector3 gravityUp = GravityManager.Instance != null
                ? GravityManager.Instance.UpDirection
                : Vector3.up;

            holoGameObject.transform.position =
                playerTransform.position + gravityUp * heightAbovePlayer;

            // Lock orientation to surface normal — completely ignores player rotation
            holoGameObject.transform.rotation =
                Quaternion.FromToRotation(Vector3.up, _currentUpVector);
        }
    }

    /// <summary>
    /// Call this whenever a new surface is selected.
    /// <paramref name="upVector"/> is the surface normal (outward).
    /// The <paramref name="position"/> parameter is intentionally ignored —
    /// the hologram always appears above the player's head.
    /// </summary>
    public void setPosition(Vector3 upVector, Vector3 position)
    {
        _currentUpVector = upVector;
        // position is discarded; LateUpdate handles placement each frame
    }

    public void setActive(bool val)
    {
        _isActive = val;
        holoGameObject.SetActive(val);
    }
}