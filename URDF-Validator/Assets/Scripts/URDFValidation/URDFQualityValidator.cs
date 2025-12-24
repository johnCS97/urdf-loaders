using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class URDFQualityValidator : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    // CONFIGURATION
    // ══════════════════════════════════════════════════════════════

    [Header("Robot Discovery")]
    [Tooltip("Name of robot GameObject. Leave empty for auto-discovery.")]
    public string robotName = "";

    [Tooltip("Tag to search for. Leave empty to skip tag search.")]
    public string robotTag = "";

    [Tooltip("Maximum time to wait for robot to load.")]
    public float discoveryTimeout = 30f;

    [Tooltip("How often to check for robot during discovery.")]
    public float discoveryCheckInterval = 0.5f;

    [Header("Validation Settings")]
    public bool continuousValidation = true;
    public float validationInterval = 0.1f;

    [Tooltip("Minimum penetration depth to report (meters).")]
    public float penetrationThreshold = 0.0001f;

    [Tooltip("Gap threshold for disconnected parts warning (meters).")]
    public float maxAllowedGap = 0.05f;

    [Header("Visual Feedback")]
    public Color normalColor = new Color(0.4f, 0.8f, 0.4f, 1f);
    public Color errorColor = new Color(1f, 0.2f, 0.2f, 1f);
    public Color warningColor = new Color(1f, 0.7f, 0.2f, 1f);

    [Range(0f, 1f)]
    public float emissionIntensity = 0.5f;

    // ══════════════════════════════════════════════════════════════
    // RUNTIME STATE
    // ══════════════════════════════════════════════════════════════

    [Header("Status")]
    public bool isInitialized = false;
    public bool isDiscovering = false;
    public float discoveryProgress = 0f;
    public GameObject robotRoot;

    [Header("Discovered Components")]
    public List<JointController> joints = new List<JointController>();
    public List<LinkController> links = new List<LinkController>();
    public List<CollisionReporter> collisionReporters = new List<CollisionReporter>();

    [Header("Validation Results")]
    public List<ValidationError> currentErrors = new List<ValidationError>();
    public int errorCount = 0;
    public int warningCount = 0;

    // Materials
    [HideInInspector] public Material normalMaterial;
    [HideInInspector] public Material errorMaterial;
    [HideInInspector] public Material warningMaterial;

    // Events
    public event Action OnInitialized;
    public event Action<List<ValidationError>> OnValidationComplete;
    public event Action<ValidationError> OnErrorDetected;

    // Internal
    private float lastValidationTime;
    private HashSet<string> reportedCollisionPairs = new HashSet<string>();

    // ══════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ══════════════════════════════════════════════════════════════

    void Start()
    {
        StartCoroutine(InitializationSequence());
    }

    void Update()
    {
        if (!isInitialized) return;

        if (continuousValidation &&
            Time.time - lastValidationTime > validationInterval)
        {
            RunValidation();
            lastValidationTime = Time.time;
        }
    }

    void OnDestroy()
    {
        // Cleanup materials
        if (normalMaterial != null) Destroy(normalMaterial);
        if (errorMaterial != null) Destroy(errorMaterial);
        if (warningMaterial != null) Destroy(warningMaterial);
    }

    // ══════════════════════════════════════════════════════════════
    // INITIALIZATION
    // ══════════════════════════════════════════════════════════════

    IEnumerator InitializationSequence()
    {
        Debug.Log("╔════════════════════════════════════════╗");
        Debug.Log("║   URDF QUALITY VALIDATOR STARTING      ║");
        Debug.Log("╚════════════════════════════════════════╝");

        isDiscovering = true;

        // Step 1: Wait for robot to load
        yield return StartCoroutine(WaitForRobot());

        if (robotRoot == null)
        {
            Debug.LogError("❌ Failed to find robot. Validator disabled.");
            isDiscovering = false;
            enabled = false;
            yield break;
        }

        // Step 2: Wait for robot hierarchy to stabilize
        Debug.Log("Waiting for robot to stabilize...");
        yield return new WaitForSeconds(0.5f);
        yield return new WaitForFixedUpdate();

        // Step 3: Create materials
        CreateMaterials();

        // Step 4: Discover robot structure
        DiscoverRobotStructure();

        // Step 5: Setup collision detection
        SetupCollisionDetection();

        // Step 6: Run initial validation
        RunValidation();

        // Done!
        isDiscovering = false;
        isInitialized = true;

        Debug.Log("╔════════════════════════════════════════╗");
        Debug.Log("║   URDF VALIDATOR READY                 ║");
        Debug.Log($"║   Joints: {joints.Count,-5} Links: {links.Count,-5}         ║");
        Debug.Log("╚════════════════════════════════════════╝");

        OnInitialized?.Invoke();
    }

    IEnumerator WaitForRobot()
    {
        Debug.Log("🔍 Searching for robot...");
        float elapsed = 0f;

        while (robotRoot == null && elapsed < discoveryTimeout)
        {
            // Method 1: Search by name
            if (!string.IsNullOrEmpty(robotName))
            {
                robotRoot = GameObject.Find(robotName);
                if (robotRoot != null)
                {
                    Debug.Log($"   Found by name: {robotName}");
                }
            }

            // Method 2: Search by tag
            if (robotRoot == null && !string.IsNullOrEmpty(robotTag))
            {
                try
                {
                    robotRoot = GameObject.FindWithTag(robotTag);
                    if (robotRoot != null)
                    {
                        Debug.Log($"   Found by tag: {robotTag}");
                    }
                }
                catch { }
            }

            // Method 3: Auto-discover by ArticulationBody
            if (robotRoot == null)
            {
                robotRoot = FindRobotByArticulationBody();
                if (robotRoot != null)
                {
                    Debug.Log($"   Found by ArticulationBody: {robotRoot.name}");
                }
            }

            // Method 4: Auto-discover by mesh count (fallback)
            if (robotRoot == null)
            {
                robotRoot = FindRobotByMeshCount();
                if (robotRoot != null)
                {
                    Debug.Log($"   Found by mesh count: {robotRoot.name}");
                }
            }

            // Not found yet, wait and try again
            if (robotRoot == null)
            {
                elapsed += discoveryCheckInterval;
                discoveryProgress = elapsed / discoveryTimeout;

                if (elapsed % 2f < discoveryCheckInterval)
                {
                    Debug.Log($"   Still searching... ({elapsed:F1}s / {discoveryTimeout}s)");
                }

                yield return new WaitForSeconds(discoveryCheckInterval);
            }
        }

        discoveryProgress = 1f;

        if (robotRoot != null)
        {
            Debug.Log($"✅ Robot found: {robotRoot.name}");
        }
        else
        {
            Debug.LogError($"❌ Robot not found after {discoveryTimeout}s");
        }
    }

    GameObject FindRobotByArticulationBody()
    {
        var bodies = FindObjectsOfType<ArticulationBody>();

        foreach (var body in bodies)
        {
            // Root = has ArticulationBody but parent doesn't
            Transform parent = body.transform.parent;
            if (parent == null || parent.GetComponent<ArticulationBody>() == null)
            {
                return body.gameObject;
            }
        }

        return null;
    }

    GameObject FindRobotByMeshCount()
    {
        // Find root objects with multiple mesh children (likely a robot)
        var allTransforms = FindObjectsOfType<Transform>();

        foreach (var t in allTransforms)
        {
            // Only check root-level objects
            if (t.parent != null) continue;

            var meshes = t.GetComponentsInChildren<MeshRenderer>();
            if (meshes.Length >= 5) // Robot likely has multiple parts
            {
                return t.gameObject;
            }
        }

        return null;
    }

    // ══════════════════════════════════════════════════════════════
    // SETUP
    // ══════════════════════════════════════════════════════════════

    void CreateMaterials()
    {
        Debug.Log("🎨 Creating materials...");

        Shader shader = FindBestShader();

        normalMaterial = CreateMaterial(shader, normalColor, false);
        normalMaterial.name = "Validator_Normal";

        errorMaterial = CreateMaterial(shader, errorColor, true);
        errorMaterial.name = "Validator_Error";

        warningMaterial = CreateMaterial(shader, warningColor, true);
        warningMaterial.name = "Validator_Warning";

        Debug.Log("   ✓ Materials created");
    }

    Shader FindBestShader()
    {
        // Try URP
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null) return shader;

        // Try HDRP
        shader = Shader.Find("HDRP/Lit");
        if (shader != null) return shader;

        // Fallback to Standard
        shader = Shader.Find("Standard");
        if (shader != null) return shader;

        // Last resort
        return Shader.Find("Diffuse");
    }

    Material CreateMaterial(Shader shader, Color color, bool emission)
    {
        Material mat = new Material(shader);

        // Set base color (compatible with Standard, URP, HDRP)
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);

        // Set emission
        if (emission)
        {
            mat.EnableKeyword("_EMISSION");
            Color emissionColor = color * emissionIntensity;
            mat.SetColor("_EmissionColor", emissionColor);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        return mat;
    }

    void DiscoverRobotStructure()
    {
        Debug.Log("🔧 Discovering robot structure...");

        joints.Clear();
        links.Clear();

        // ─── Find Joints (ArticulationBody) ───
        var articulationBodies = robotRoot.GetComponentsInChildren<ArticulationBody>();

        foreach (var ab in articulationBodies)
        {
            if (ab.jointType != ArticulationJointType.FixedJoint)
            {
                JointController jc = ab.gameObject.GetComponent<JointController>();
                if (jc == null)
                {
                    jc = ab.gameObject.AddComponent<JointController>();
                }
                jc.Initialize(ab);
                joints.Add(jc);
            }
        }

        // ─── Find Joints (HingeJoint - alternative) ───
        var hingeJoints = robotRoot.GetComponentsInChildren<HingeJoint>();

        foreach (var hj in hingeJoints)
        {
            JointController jc = hj.gameObject.GetComponent<JointController>();
            if (jc == null)
            {
                jc = hj.gameObject.AddComponent<JointController>();
            }
            jc.Initialize(hj);
            joints.Add(jc);
        }

        Debug.Log($"   ✓ Found {joints.Count} joints");

        // ─── Find Links ───
        var renderers = robotRoot.GetComponentsInChildren<MeshRenderer>();

        foreach (var renderer in renderers)
        {
            LinkController lc = renderer.gameObject.GetComponent<LinkController>();
            if (lc == null)
            {
                lc = renderer.gameObject.AddComponent<LinkController>();
            }
            lc.Initialize(renderer, normalMaterial);
            links.Add(lc);
        }

        Debug.Log($"   ✓ Found {links.Count} links");
    }

    void SetupCollisionDetection()
    {
        Debug.Log("💥 Setting up collision detection...");

        collisionReporters.Clear();

        var colliders = robotRoot.GetComponentsInChildren<Collider>();

        foreach (var collider in colliders)
        {
            CollisionReporter reporter = collider.gameObject.GetComponent<CollisionReporter>();
            if (reporter == null)
            {
                reporter = collider.gameObject.AddComponent<CollisionReporter>();
            }
            reporter.validator = this;
            collisionReporters.Add(reporter);
        }

        Debug.Log($"   ✓ Setup {collisionReporters.Count} collision reporters");
    }

    // ══════════════════════════════════════════════════════════════
    // VALIDATION
    // ══════════════════════════════════════════════════════════════

    [ContextMenu("Run Validation")]
    public void RunValidation()
    {
        if (!isInitialized && robotRoot == null) return;

        currentErrors.Clear();
        reportedCollisionPairs.Clear();

        // Run all checks
        CheckSelfCollisions();
        CheckGeometryGaps();
        CheckScaleIssues();

        // Count errors/warnings
        errorCount = 0;
        warningCount = 0;

        foreach (var error in currentErrors)
        {
            if (error.severity == Severity.Error) errorCount++;
            else if (error.severity == Severity.Warning) warningCount++;
        }

        // Update visuals
        UpdateVisualFeedback();

        // Fire event
        OnValidationComplete?.Invoke(currentErrors);
    }

    void CheckSelfCollisions()
    {
        var colliders = robotRoot.GetComponentsInChildren<Collider>();

        for (int i = 0; i < colliders.Length; i++)
        {
            for (int j = i + 1; j < colliders.Length; j++)
            {
                Collider a = colliders[i];
                Collider b = colliders[j];

                // Skip adjacent parts (parent-child)
                if (AreAdjacent(a.transform, b.transform))
                    continue;

                // Skip if same object
                if (a.gameObject == b.gameObject)
                    continue;

                // Check collision pair ID to avoid duplicates
                string pairId = GetCollisionPairId(a, b);
                if (reportedCollisionPairs.Contains(pairId))
                    continue;

                // Check for overlap
                if (CollidersOverlap(a, b, out float penetration))
                {
                    reportedCollisionPairs.Add(pairId);

                    var error = new ValidationError
                    {
                        errorType = ErrorType.SelfCollision,
                        severity = penetration > 0.01f ? Severity.Error : Severity.Warning,
                        message = $"{a.name} ↔ {b.name}",
                        affectedObjects = new[] { a.gameObject, b.gameObject },
                        penetrationDepth = penetration
                    };

                    currentErrors.Add(error);
                    OnErrorDetected?.Invoke(error);
                }
            }
        }
    }

    void CheckGeometryGaps()
    {
        foreach (var joint in joints)
        {
            Transform child = joint.transform;
            Transform parent = child.parent;

            if (parent == null) continue;

            // Get renderers
            var childRenderer = child.GetComponentInChildren<MeshRenderer>();
            var parentRenderer = parent.GetComponentInChildren<MeshRenderer>();

            if (childRenderer == null || parentRenderer == null) continue;

            // Calculate gap between bounds
            float gap = CalculateGap(parentRenderer.bounds, childRenderer.bounds);

            if (gap > maxAllowedGap)
            {
                currentErrors.Add(new ValidationError
                {
                    errorType = ErrorType.GeometryGap,
                    severity = Severity.Warning,
                    message = $"Gap of {gap * 100:F1}cm between {parent.name} and {child.name}",
                    affectedObjects = new[] { parent.gameObject, child.gameObject },
                    jointName = joint.jointName
                });
            }
        }
    }

    void CheckScaleIssues()
    {
        foreach (var link in links)
        {
            Vector3 size = link.GetWorldSize();

            // Check for extremely large parts
            float maxDim = Mathf.Max(size.x, size.y, size.z);
            if (maxDim > 10f)
            {
                currentErrors.Add(new ValidationError
                {
                    errorType = ErrorType.InvalidScale,
                    severity = Severity.Error,
                    message = $"{link.linkName} is very large ({maxDim:F1}m). Check scale/units.",
                    affectedObjects = new[] { link.gameObject }
                });
            }

            // Check for extremely small parts
            float minDim = Mathf.Min(size.x, size.y, size.z);
            if (minDim > 0 && minDim < 0.001f)
            {
                currentErrors.Add(new ValidationError
                {
                    errorType = ErrorType.InvalidScale,
                    severity = Severity.Warning,
                    message = $"{link.linkName} is very small ({minDim * 1000:F2}mm)",
                    affectedObjects = new[] { link.gameObject }
                });
            }
        }
    }

    void UpdateVisualFeedback()
    {
        // Reset all links to normal
        foreach (var link in links)
        {
            link.SetToNormalMaterial();
        }

        // Highlight problematic links
        foreach (var error in currentErrors)
        {
            if (error.affectedObjects == null) continue;

            foreach (var obj in error.affectedObjects)
            {
                var link = obj.GetComponent<LinkController>();
                if (link != null)
                {
                    Material mat = error.severity == Severity.Error
                        ? errorMaterial
                        : warningMaterial;
                    link.SetMaterial(mat);
                    link.SetErrorState(
                        error.severity == Severity.Error,
                        error.severity == Severity.Warning
                    );
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // COLLISION CALLBACKS
    // ══════════════════════════════════════════════════════════════

    public void OnCollisionReported(CollisionReporter reporter, Collider other, Collision collision)
    {
        // Called by CollisionReporter when collision detected
    }

    public void OnCollisionEndedReport(CollisionReporter reporter, Collider other)
    {
        // Called when collision ends
    }

    // ══════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ══════════════════════════════════════════════════════════════

    bool AreAdjacent(Transform a, Transform b)
    {
        // Direct parent-child
        if (a.parent == b || b.parent == a)
            return true;

        // Siblings (same parent)
        if (a.parent == b.parent && a.parent != null)
            return true;

        // Grandparent-grandchild
        if (a.IsChildOf(b) || b.IsChildOf(a))
        {
            int depth = GetHierarchyDepth(a, b);
            return depth <= 2;
        }

        return false;
    }

    int GetHierarchyDepth(Transform a, Transform b)
    {
        int depth = 0;
        Transform current = a;

        while (current != null && current != b)
        {
            current = current.parent;
            depth++;
            if (depth > 10) break;
        }

        return current == b ? depth : 999;
    }

    bool CollidersOverlap(Collider a, Collider b, out float penetration)
    {
        penetration = 0f;
        Vector3 direction;

        bool overlap = Physics.ComputePenetration(
            a, a.transform.position, a.transform.rotation,
            b, b.transform.position, b.transform.rotation,
            out direction, out penetration
        );

        return overlap && penetration > penetrationThreshold;
    }

    float CalculateGap(Bounds a, Bounds b)
    {
        if (a.Intersects(b)) return 0f;

        Vector3 closestA = a.ClosestPoint(b.center);
        Vector3 closestB = b.ClosestPoint(a.center);

        return Vector3.Distance(closestA, closestB);
    }

    string GetCollisionPairId(Collider a, Collider b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();

        return idA < idB
            ? $"{idA}_{idB}"
            : $"{idB}_{idA}";
    }

    // ══════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Manually set the robot reference (skip auto-discovery)
    /// </summary>
    public void SetRobot(GameObject robot)
    {
        StopAllCoroutines();
        robotRoot = robot;
        StartCoroutine(ManualInitialize());
    }

    IEnumerator ManualInitialize()
    {
        yield return new WaitForSeconds(0.1f);

        CreateMaterials();
        DiscoverRobotStructure();
        SetupCollisionDetection();
        RunValidation();

        isInitialized = true;
        OnInitialized?.Invoke();

        Debug.Log("✅ Validator initialized via SetRobot()");
    }

    /// <summary>
    /// Reset all joints to original positions
    /// </summary>
    public void ResetAllJoints()
    {
        foreach (var joint in joints)
        {
            joint.ResetToOriginal();
        }
    }

    /// <summary>
    /// Reset all link scales
    /// </summary>
    public void ResetAllScales()
    {
        foreach (var link in links)
        {
            link.ResetScale();
        }
    }

    /// <summary>
    /// Get current validation report
    /// </summary>
    public ValidationReport GenerateReport()
    {
        var report = new ValidationReport
        {
            robotName = robotRoot != null ? robotRoot.name : "Unknown",
            totalJoints = joints.Count,
            totalLinks = links.Count,
            errors = new List<ValidationError>(currentErrors)
        };

        foreach (var joint in joints)
        {
            report.configuration.Add(new JointConfiguration(
                joint.jointName,
                joint.currentAngle
            ));
        }

        return report;
    }

    /// <summary>
    /// Export report to file
    /// </summary>
    public void ExportReport(string filename = "")
    {
        if (string.IsNullOrEmpty(filename))
        {
            filename = $"ValidationReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        }

        var report = GenerateReport();
        string path = System.IO.Path.Combine(Application.dataPath, filename);
        System.IO.File.WriteAllText(path, report.ToText());

        Debug.Log($"📄 Report saved: {path}");
    }
}
