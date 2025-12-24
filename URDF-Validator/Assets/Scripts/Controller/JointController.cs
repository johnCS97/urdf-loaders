using UnityEngine;

public class JointController : MonoBehaviour
{
    [Header("Joint Info")]
    public string jointName;
    public JointControllerType jointType;

    [Header("Limits (Degrees)")]
    public float lowerLimit;
    public float upperLimit;
    public float currentAngle;

    [Header("Effort/Velocity Limits")]
    public float maxEffort;
    public float maxVelocity;

    [Header("UI Control")]
    [Range(0f, 1f)]
    public float normalizedPosition = 0.5f;

    // Internal
    private ArticulationBody articulationBody;
    private HingeJoint hingeJoint;
    private float originalAngle;
    private bool isInitialized = false;

    public bool IsInitialized => isInitialized;

    public void Initialize(ArticulationBody ab)
    {
        articulationBody = ab;
        jointName = ab.name;

        // Get limits
        var drive = ab.xDrive;
        lowerLimit = drive.lowerLimit * Mathf.Rad2Deg;
        upperLimit = drive.upperLimit * Mathf.Rad2Deg;

        // Get effort/velocity from drive
        maxEffort = drive.forceLimit;
        maxVelocity = drive.targetVelocity;

        // Determine joint type
        switch (ab.jointType)
        {
            case ArticulationJointType.RevoluteJoint:
                jointType = JointControllerType.Revolute;
                break;
            case ArticulationJointType.PrismaticJoint:
                jointType = JointControllerType.Prismatic;
                break;
            case ArticulationJointType.SphericalJoint:
                jointType = JointControllerType.Spherical;
                break;
            default:
                jointType = JointControllerType.Fixed;
                break;
        }

        originalAngle = GetCurrentAngle();
        currentAngle = originalAngle;
        UpdateNormalizedPosition();

        isInitialized = true;
    }

    public void Initialize(HingeJoint hj)
    {
        hingeJoint = hj;
        jointName = hj.name;
        jointType = JointControllerType.Revolute;

        if (hj.useLimits)
        {
            var limits = hj.limits;
            lowerLimit = limits.min;
            upperLimit = limits.max;
        }
        else
        {
            lowerLimit = -180f;
            upperLimit = 180f;
        }

        originalAngle = GetCurrentAngle();
        currentAngle = originalAngle;
        UpdateNormalizedPosition();

        isInitialized = true;
    }

    public void SetAngle(float angleDegrees)
    {
        if (!isInitialized) return;

        currentAngle = Mathf.Clamp(angleDegrees, lowerLimit, upperLimit);

        if (articulationBody != null)
        {
            var drive = articulationBody.xDrive;
            drive.target = currentAngle * Mathf.Deg2Rad;
            articulationBody.xDrive = drive;

            // Force immediate update
            articulationBody.jointPosition = new ArticulationReducedSpace(currentAngle * Mathf.Deg2Rad);
        }
        else if (hingeJoint != null)
        {
            var motor = hingeJoint.motor;
            motor.targetVelocity = 0;
            hingeJoint.motor = motor;
        }

        UpdateNormalizedPosition();
    }

    public void SetNormalizedPosition(float normalized)
    {
        normalizedPosition = Mathf.Clamp01(normalized);
        float angle = Mathf.Lerp(lowerLimit, upperLimit, normalizedPosition);
        SetAngle(angle);
    }

    public float GetCurrentAngle()
    {
        if (articulationBody != null && articulationBody.jointPosition.dofCount > 0)
        {
            return articulationBody.jointPosition[0] * Mathf.Rad2Deg;
        }
        else if (hingeJoint != null)
        {
            return hingeJoint.angle;
        }
        return 0f;
    }

    void UpdateNormalizedPosition()
    {
        float range = upperLimit - lowerLimit;
        if (Mathf.Abs(range) > 0.001f)
        {
            normalizedPosition = (currentAngle - lowerLimit) / range;
        }
        else
        {
            normalizedPosition = 0.5f;
        }
    }

    public void ResetToOriginal()
    {
        SetAngle(originalAngle);
    }

    public void SetToMin()
    {
        SetAngle(lowerLimit);
    }

    public void SetToMax()
    {
        SetAngle(upperLimit);
    }

    public void SetToMid()
    {
        SetAngle((lowerLimit + upperLimit) / 2f);
    }

    public float GetRange()
    {
        return upperLimit - lowerLimit;
    }

    public enum JointControllerType
    {
        Fixed,
        Revolute,
        Prismatic,
        Continuous,
        Spherical
    }
}
