using UnityEngine;

public class LinkController : MonoBehaviour
{
    [Header("Link Info")]
    public string linkName;
    public Vector3 originalLocalScale;
    public Vector3 originalWorldSize;

    [Header("Scale Multipliers")]
    [Range(0.1f, 3f)] public float scaleX = 1f;
    [Range(0.1f, 3f)] public float scaleY = 1f;
    [Range(0.1f, 3f)] public float scaleZ = 1f;

    [Header("State")]
    public bool hasError = false;
    public bool hasWarning = false;

    // Components
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Collider[] colliders;

    // Materials
    private Material originalMaterial;
    private Material currentMaterial;
    private Material normalMaterial;

    private bool isInitialized = false;

    public bool IsInitialized => isInitialized;
    public MeshRenderer Renderer => meshRenderer;

    public void Initialize(MeshRenderer renderer, Material defaultMaterial)
    {
        meshRenderer = renderer;
        meshFilter = renderer.GetComponent<MeshFilter>();
        colliders = GetComponents<Collider>();
        linkName = gameObject.name;
        normalMaterial = defaultMaterial;

        // Store original state
        originalLocalScale = transform.localScale;

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
            originalWorldSize = Vector3.Scale(meshSize, transform.lossyScale);
        }

        // Store original material
        if (renderer.sharedMaterial != null)
        {
            originalMaterial = renderer.sharedMaterial;
        }
        else
        {
            originalMaterial = defaultMaterial;
        }

        currentMaterial = originalMaterial;
        isInitialized = true;
    }

    public void SetScale(float x, float y, float z)
    {
        scaleX = Mathf.Clamp(x, 0.1f, 3f);
        scaleY = Mathf.Clamp(y, 0.1f, 3f);
        scaleZ = Mathf.Clamp(z, 0.1f, 3f);

        transform.localScale = new Vector3(
            originalLocalScale.x * scaleX,
            originalLocalScale.y * scaleY,
            originalLocalScale.z * scaleZ
        );
    }

    public void SetUniformScale(float scale)
    {
        SetScale(scale, scale, scale);
    }

    public void ResetScale()
    {
        SetScale(1f, 1f, 1f);
    }

    public void SetMaterial(Material mat)
    {
        if (meshRenderer != null && mat != null)
        {
            meshRenderer.material = mat;
            currentMaterial = mat;
        }
    }

    public void SetErrorState(bool error, bool warning = false)
    {
        hasError = error;
        hasWarning = warning;
    }

    public void ResetMaterial()
    {
        if (meshRenderer != null)
        {
            meshRenderer.material = originalMaterial;
            currentMaterial = originalMaterial;
        }
        hasError = false;
        hasWarning = false;
    }

    public void SetToNormalMaterial()
    {
        if (meshRenderer != null && normalMaterial != null)
        {
            meshRenderer.material = normalMaterial;
            currentMaterial = normalMaterial;
        }
        hasError = false;
        hasWarning = false;
    }

    public Vector3 GetWorldSize()
    {
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
            return Vector3.Scale(meshSize, transform.lossyScale);
        }
        return Vector3.zero;
    }

    public Bounds GetWorldBounds()
    {
        if (meshRenderer != null)
        {
            return meshRenderer.bounds;
        }
        return new Bounds(transform.position, Vector3.zero);
    }

    public float GetVolume()
    {
        Vector3 size = GetWorldSize();
        return size.x * size.y * size.z;
    }
}
