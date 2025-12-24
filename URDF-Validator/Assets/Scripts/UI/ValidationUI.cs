using UnityEngine;

public class ValidationUI : MonoBehaviour
{
    [Header("References")]
    public URDFQualityValidator validator;

    [Header("UI Toggle")]
    public bool showUI = true;
    public KeyCode toggleKey = KeyCode.Tab;

    // Window state
    private Rect windowRect;
    private Vector2 scrollPosition;
    private int currentTab = 0;
    private readonly string[] tabNames = { "Joints", "Links", "Errors", "Info" };
    private int windowID = 12345;

    // Cached
    private GUIStyle errorStyle;
    private GUIStyle warningStyle;
    private GUIStyle successStyle;
    private bool stylesCreated = false;

    void Start()
    {
        if (validator == null)
            validator = FindObjectOfType<URDFQualityValidator>();

        // Initial window position (will be adjusted in OnGUI)
        windowRect = new Rect(20, 20, 350, 500);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            showUI = !showUI;

        if (Input.GetKeyDown(KeyCode.V) && validator != null)
            validator.RunValidation();
    }

    void CreateStyles()
    {
        if (stylesCreated) return;

        errorStyle = new GUIStyle(GUI.skin.box);
        errorStyle.normal.textColor = Color.red;

        warningStyle = new GUIStyle(GUI.skin.box);
        warningStyle.normal.textColor = Color.yellow;

        successStyle = new GUIStyle(GUI.skin.box);
        successStyle.normal.textColor = Color.green;

        stylesCreated = true;
    }

    void OnGUI()
    {
         // Help text at bottom
        GUI.Label(new Rect(10, Screen.height - 45, 400, 20),
            "[Tab] Toggle UI | [V] Validate");
        if (!showUI || validator == null) return;

        CreateStyles();

        // Calculate window size based on screen (responsive)
        float width = Mathf.Min(380f, Screen.width * 0.3f);
        float height = Mathf.Min(Screen.height - 40f, Screen.height * 0.9f);
        
        // Keep window on screen
        windowRect.width = width;
        windowRect.height = height;
        windowRect.x = Mathf.Clamp(windowRect.x, 0, Screen.width - width);
        windowRect.y = Mathf.Clamp(windowRect.y, 0, Screen.height - height);

        // Draw draggable window
        windowRect = GUI.Window(windowID, windowRect, DrawWindow, "URDF Quality Validator");

       
    }

    void DrawWindow(int id)
    {
        // Make window draggable by title bar
        GUI.DragWindow(new Rect(0, 0, windowRect.width - 30, 20));

        GUILayout.Space(5);

        // Status
        DrawStatus();

        // Tabs
        GUILayout.BeginHorizontal();
        for (int i = 0; i < tabNames.Length; i++)
        {
            if (GUILayout.Toggle(currentTab == i, tabNames[i], "Button"))
                currentTab = i;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Scrollable content
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        switch (currentTab)
        {
            case 0: DrawJoints(); break;
            case 1: DrawLinks(); break;
            case 2: DrawErrors(); break;
            case 3: DrawInfo(); break;
        }

        GUILayout.EndScrollView();

        // Footer
        GUILayout.Space(5);
        DrawFooter();
    }

    void DrawStatus()
    {
        string status;
        GUIStyle style;

        if (!validator.isInitialized)
        {
            status = validator.isDiscovering 
                ? $"🔍 Searching... {validator.discoveryProgress * 100:F0}%" 
                : "⏳ Initializing...";
            style = warningStyle;
        }
        else if (validator.errorCount > 0)
        {
            status = $"❌ {validator.errorCount} Errors, {validator.warningCount} Warnings";
            style = errorStyle;
        }
        else if (validator.warningCount > 0)
        {
            status = $"⚠️ {validator.warningCount} Warnings";
            style = warningStyle;
        }
        else
        {
            status = "✅ No Issues";
            style = successStyle;
        }

        GUILayout.Box(status, style);
    }

    void DrawJoints()
    {
        GUILayout.Label($"<b>Joints ({validator.joints.Count})</b>");

        if (validator.joints.Count == 0)
        {
            GUILayout.Label("No joints found.");
            return;
        }

        foreach (var joint in validator.joints)
        {
            GUILayout.BeginVertical("box");
            
            GUILayout.Label($"📐 {joint.jointName} [{joint.jointType}]");
            GUILayout.Label($"   Range: {joint.lowerLimit:F0}° to {joint.upperLimit:F0}°");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{joint.currentAngle:F1}°", GUILayout.Width(50));
            
            float newVal = GUILayout.HorizontalSlider(joint.normalizedPosition, 0f, 1f);
            if (Mathf.Abs(newVal - joint.normalizedPosition) > 0.001f)
                joint.SetNormalizedPosition(newVal);
            
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Min")) joint.SetToMin();
            if (GUILayout.Button("Mid")) joint.SetToMid();
            if (GUILayout.Button("Max")) joint.SetToMax();
            if (GUILayout.Button("Reset")) joint.ResetToOriginal();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        GUILayout.Space(10);
        GUILayout.Label("<b>Bulk Controls</b>");
        
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("All Min")) 
            foreach (var j in validator.joints) j.SetToMin();
        if (GUILayout.Button("All Mid")) 
            foreach (var j in validator.joints) j.SetToMid();
        if (GUILayout.Button("All Max")) 
            foreach (var j in validator.joints) j.SetToMax();
        if (GUILayout.Button("Reset All")) 
            validator.ResetAllJoints();
        GUILayout.EndHorizontal();
    }

    void DrawLinks()
    {
        GUILayout.Label($"<b>Links ({validator.links.Count})</b>");

        if (validator.links.Count == 0)
        {
            GUILayout.Label("No links found.");
            return;
        }

        foreach (var link in validator.links)
        {
            GUIStyle style = link.hasError ? errorStyle : 
                            link.hasWarning ? warningStyle : 
                            GUI.skin.box;

            GUILayout.BeginVertical(style);

            string icon = link.hasError ? "❌" : link.hasWarning ? "⚠️" : "📦";
            GUILayout.Label($"{icon} {link.linkName}");

            Vector3 size = link.GetWorldSize();
            GUILayout.Label($"   Size: {size.x:F2} × {size.y:F2} × {size.z:F2}m");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Scale:", GUILayout.Width(40));
            
            float avg = (link.scaleX + link.scaleY + link.scaleZ) / 3f;
            float newScale = GUILayout.HorizontalSlider(avg, 0.5f, 2f);
            GUILayout.Label($"{newScale:F2}", GUILayout.Width(35));
            
            if (Mathf.Abs(newScale - avg) > 0.01f)
                link.SetUniformScale(newScale);
            
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("X+")) link.SetScale(link.scaleX * 1.1f, link.scaleY, link.scaleZ);
            if (GUILayout.Button("Y+")) link.SetScale(link.scaleX, link.scaleY * 1.1f, link.scaleZ);
            if (GUILayout.Button("Z+")) link.SetScale(link.scaleX, link.scaleY, link.scaleZ * 1.1f);
            if (GUILayout.Button("Reset")) link.ResetScale();
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Reset All Scales"))
            validator.ResetAllScales();
    }

    void DrawErrors()
    {
        GUILayout.Label($"<b>Errors & Warnings ({validator.currentErrors.Count})</b>");

        if (validator.currentErrors.Count == 0)
        {
            GUILayout.Box("✅ No issues detected!", successStyle);
            return;
        }

        foreach (var error in validator.currentErrors)
        {
            GUIStyle style = error.severity == Severity.Error ? errorStyle : warningStyle;
            string icon = error.severity == Severity.Error ? "❌" : "⚠️";

            GUILayout.BeginVertical(style);
            GUILayout.Label($"{icon} {error.errorType}");
            GUILayout.Label($"   {error.message}");
            
            if (error.penetrationDepth > 0)
                GUILayout.Label($"   Depth: {error.penetrationDepth * 1000:F1}mm");

            if (error.affectedObjects != null && error.affectedObjects.Length > 0)
            {
                if (GUILayout.Button("Focus"))
                    FocusOnObject(error.affectedObjects[0]);
            }
            
            GUILayout.EndVertical();
        }
    }

    void DrawInfo()
    {
        GUILayout.Label("<b>Robot Info</b>");
        
        GUILayout.BeginVertical("box");
        
        if (validator.robotRoot != null)
        {
            GUILayout.Label($"Name: {validator.robotRoot.name}");
            GUILayout.Label($"Joints: {validator.joints.Count}");
            GUILayout.Label($"Links: {validator.links.Count}");
            GUILayout.Label($"Colliders: {validator.collisionReporters.Count}");
        }
        else
        {
            GUILayout.Label("Waiting for robot...");
        }
        
        GUILayout.EndVertical();

        GUILayout.Space(10);
        GUILayout.Label("<b>Settings</b>");
        
        GUILayout.BeginVertical("box");
        
        validator.continuousValidation = GUILayout.Toggle(
            validator.continuousValidation, 
            "Continuous Validation"
        );

        GUILayout.BeginHorizontal();
        GUILayout.Label("Interval:", GUILayout.Width(60));
        validator.validationInterval = GUILayout.HorizontalSlider(
            validator.validationInterval, 0.05f, 1f
        );
        GUILayout.Label($"{validator.validationInterval:F2}s", GUILayout.Width(40));
        GUILayout.EndHorizontal();
        
        GUILayout.EndVertical();

        GUILayout.Space(10);
        GUILayout.Label("<b>Camera Views</b>");
        
        CameraController cam = Camera.main?.GetComponent<CameraController>();
        if (cam != null)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Front")) cam.ViewFront();
            if (GUILayout.Button("Back")) cam.ViewBack();
            if (GUILayout.Button("Top")) cam.ViewTop();
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Left")) cam.ViewLeft();
            if (GUILayout.Button("Right")) cam.ViewRight();
            if (GUILayout.Button("Iso")) cam.ViewIsometric();
            GUILayout.EndHorizontal();
            
            if (GUILayout.Button("Focus on Robot"))
                cam.FocusOnTarget();
        }
    }

    void DrawFooter()
    {
        GUILayout.BeginHorizontal();
        
        if (GUILayout.Button("Validate", GUILayout.Height(30)))
            validator.RunValidation();
        
        if (GUILayout.Button("Reset All", GUILayout.Height(30)))
        {
            validator.ResetAllJoints();
            validator.ResetAllScales();
        }
        
        if (GUILayout.Button("Export", GUILayout.Height(30)))
            validator.ExportReport();
        
        GUILayout.EndHorizontal();
    }

    void FocusOnObject(GameObject obj)
    {
        CameraController cam = Camera.main?.GetComponent<CameraController>();
        if (cam != null && obj != null)
        {
            cam.SetTarget(obj.transform);
            cam.FocusOnTarget();
        }
    }
}
