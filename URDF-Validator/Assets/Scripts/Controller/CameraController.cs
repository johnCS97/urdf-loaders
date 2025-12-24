using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public bool autoFindTarget = true;
    public string targetName = "";

    [Header("Orbit Settings")]
    public float orbitSpeed = 5f;
    public float orbitSmoothing = 10f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 10f;
    public float minDistance = 1f;
    public float maxDistance = 50f;
    public float zoomSmoothing = 10f;

    [Header("Pan Settings")]
    public float panSpeed = 0.5f;
    public float panSmoothing = 10f;

    [Header("Input Settings")]
    public KeyCode orbitKey = KeyCode.Mouse1;      // Right mouse button
    public KeyCode panKey = KeyCode.Mouse2;        // Middle mouse button
    public bool invertOrbitY = false;
    public bool invertPanY = false;

    [Header("Limits")]
    public float minVerticalAngle = -80f;
    public float maxVerticalAngle = 80f;

    [Header("Current State")]
    public float currentDistance = 10f;
    public Vector2 currentRotation = new Vector2(30f, 45f);

    // Internal
    private Vector3 targetPosition;
    private float targetDistance;
    private Vector2 targetRotation;
    private Vector3 panOffset;
    private bool isInitialized = false;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        if (autoFindTarget && target == null)
        {
            StartCoroutine(FindTargetCoroutine());
        }
        else if (target != null)
        {
            SetupCamera();
        }
    }

    System.Collections.IEnumerator FindTargetCoroutine()
    {
        float timeout = 30f;
        float elapsed = 0f;

        while (target == null && elapsed < timeout)
        {
            // Try to find by name
            if (!string.IsNullOrEmpty(targetName))
            {
                GameObject found = GameObject.Find(targetName);
                if (found != null)
                {
                    target = found.transform;
                    break;
                }
            }

            // Try to find robot by ArticulationBody
            var bodies = FindObjectsOfType<ArticulationBody>();
            foreach (var body in bodies)
            {
                var parent = body.transform.parent;
                if (parent == null || parent.GetComponent<ArticulationBody>() == null)
                {
                    target = body.transform;
                    break;
                }
            }

            // Try to find by mesh count
            if (target == null)
            {
                var allTransforms = FindObjectsOfType<Transform>();
                foreach (var t in allTransforms)
                {
                    if (t.parent != null) continue;
                    var meshes = t.GetComponentsInChildren<MeshRenderer>();
                    if (meshes.Length >= 5)
                    {
                        target = t;
                        break;
                    }
                }
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        if (target != null)
        {
            Debug.Log($"📷 Camera target found: {target.name}");
            SetupCamera();
        }
        else
        {
            Debug.LogWarning("📷 Camera: No target found, using origin");
            targetPosition = Vector3.zero;
            isInitialized = true;
        }
    }

    void SetupCamera()
    {
        // Calculate initial distance based on target bounds
        Bounds bounds = CalculateBounds(target);
        float size = bounds.size.magnitude;
        
        currentDistance = Mathf.Clamp(size * 2f, minDistance, maxDistance);
        targetDistance = currentDistance;
        targetPosition = bounds.center;
        panOffset = Vector3.zero;

        // Set initial rotation
        targetRotation = currentRotation;

        // Apply initial position
        UpdateCameraPosition(true);
        
        isInitialized = true;
        Debug.Log($"📷 Camera initialized. Distance: {currentDistance:F1}m");
    }

    Bounds CalculateBounds(Transform t)
    {
        var renderers = t.GetComponentsInChildren<MeshRenderer>();
        
        if (renderers.Length == 0)
        {
            return new Bounds(t.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        
        return bounds;
    }

    void Update()
    {
        if (!isInitialized) return;

        HandleInput();
        UpdateCameraPosition(false);
    }

    void HandleInput()
    {
        // ═══════════════════════════════════════════════
        // ORBIT (Right Mouse Button + Drag)
        // ═══════════════════════════════════════════════
        if (Input.GetKey(orbitKey))
        {
            float mouseX = Input.GetAxis("Mouse X") * orbitSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * orbitSpeed;

            if (invertOrbitY) mouseY = -mouseY;

            targetRotation.y += mouseX;
            targetRotation.x -= mouseY;

            // Clamp vertical rotation
            targetRotation.x = Mathf.Clamp(targetRotation.x, minVerticalAngle, maxVerticalAngle);
        }

        // ═══════════════════════════════════════════════
        // PAN (Middle Mouse Button + Drag)
        // ═══════════════════════════════════════════════
        if (Input.GetKey(panKey))
        {
            float mouseX = Input.GetAxis("Mouse X") * panSpeed * currentDistance * 0.1f;
            float mouseY = Input.GetAxis("Mouse Y") * panSpeed * currentDistance * 0.1f;

            if (invertPanY) mouseY = -mouseY;

            Vector3 right = transform.right;
            Vector3 up = transform.up;

            panOffset -= right * mouseX;
            panOffset -= up * mouseY;
        }

        // ═══════════════════════════════════════════════
        // ZOOM (Scroll Wheel)
        // ═══════════════════════════════════════════════
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            targetDistance -= scroll * zoomSpeed * (targetDistance * 0.3f);
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }

        // ═══════════════════════════════════════════════
        // KEYBOARD SHORTCUTS
        // ═══════════════════════════════════════════════
        
        // F - Focus/Frame target
        if (Input.GetKeyDown(KeyCode.F))
        {
            FocusOnTarget();
        }

        // R - Reset camera
        if (Input.GetKeyDown(KeyCode.R) && !Input.GetKey(KeyCode.LeftControl))
        {
            ResetCamera();
        }

        // Home - Reset view
        if (Input.GetKeyDown(KeyCode.Home))
        {
            ResetCamera();
        }

        // Arrow keys for rotation (when not in UI)
        float arrowRotateSpeed = 60f * Time.deltaTime;
        
        if (Input.GetKey(KeyCode.LeftArrow))
            targetRotation.y -= arrowRotateSpeed;
        if (Input.GetKey(KeyCode.RightArrow))
            targetRotation.y += arrowRotateSpeed;
        if (Input.GetKey(KeyCode.UpArrow))
            targetRotation.x -= arrowRotateSpeed;
        if (Input.GetKey(KeyCode.DownArrow))
            targetRotation.x += arrowRotateSpeed;

        targetRotation.x = Mathf.Clamp(targetRotation.x, minVerticalAngle, maxVerticalAngle);
    }

    void UpdateCameraPosition(bool instant)
    {
        // Smooth interpolation
        if (instant)
        {
            currentRotation = targetRotation;
            currentDistance = targetDistance;
        }
        else
        {
            currentRotation = Vector2.Lerp(currentRotation, targetRotation, Time.deltaTime * orbitSmoothing);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, Time.deltaTime * zoomSmoothing);
        }

        // Calculate camera position
        Quaternion rotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);
        Vector3 direction = rotation * Vector3.back;

        // Target center (follow target if it moves)
        Vector3 center = targetPosition;
        if (target != null)
        {
            Bounds bounds = CalculateBounds(target);
            center = bounds.center;
        }

        // Apply position
        transform.position = center + panOffset + direction * currentDistance;
        transform.LookAt(center + panOffset);
    }

    // ═══════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Focus camera on current target
    /// </summary>
    [ContextMenu("Focus on Target")]
    public void FocusOnTarget()
    {
        if (target != null)
        {
            Bounds bounds = CalculateBounds(target);
            targetPosition = bounds.center;
            targetDistance = bounds.size.magnitude * 1.5f;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
            panOffset = Vector3.zero;
            
            Debug.Log($"📷 Focused on {target.name}");
        }
    }

    /// <summary>
    /// Reset camera to initial state
    /// </summary>
    [ContextMenu("Reset Camera")]
    public void ResetCamera()
    {
        targetRotation = new Vector2(30f, 45f);
        panOffset = Vector3.zero;
        FocusOnTarget();
        
        Debug.Log("📷 Camera reset");
    }

    /// <summary>
    /// Set a new target
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            SetupCamera();
        }
    }

    /// <summary>
    /// Set camera to specific view angle
    /// </summary>
    public void SetView(float horizontal, float vertical, float distance)
    {
        targetRotation = new Vector2(vertical, horizontal);
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    // Preset views
    public void ViewFront() => SetView(0f, 0f, currentDistance);
    public void ViewBack() => SetView(180f, 0f, currentDistance);
    public void ViewLeft() => SetView(-90f, 0f, currentDistance);
    public void ViewRight() => SetView(90f, 0f, currentDistance);
    public void ViewTop() => SetView(0f, 89f, currentDistance);
    public void ViewBottom() => SetView(0f, -89f, currentDistance);
    public void ViewIsometric() => SetView(45f, 30f, currentDistance);

    // ═══════════════════════════════════════════════════════════
    // UI HELP
    // ═══════════════════════════════════════════════════════════

    void OnGUI()
    {
        // Draw help text at bottom of screen
        float helpHeight = 25f;
        float helpY = Screen.height - helpHeight - 10f;

        GUI.color = new Color(1, 1, 1, 0.8f);
        GUI.Label(
            new Rect(10, Screen.height-25, Screen.width - 20, helpHeight),  
            "MMB+Drag: Pan  |  Scroll: Zoom  |  F: Focus  |  R: Reset  |  Arrows: Rotate"
        );
        GUI.color = Color.white;
    }
}
