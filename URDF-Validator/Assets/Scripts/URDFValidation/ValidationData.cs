using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class ValidationError
{
    public ErrorType errorType;
    public Severity severity;
    public string message;
    public GameObject[] affectedObjects;
    public float penetrationDepth;
    public string jointName;
    public float jointAngle;
    public DateTime timestamp;

    public ValidationError()
    {
        timestamp = DateTime.Now;
    }

    public override string ToString()
    {
        string icon = severity == Severity.Error ? "❌" : "⚠️";
        return $"{icon} [{errorType}] {message}";
    }
}

public enum ErrorType
{
    SelfCollision,
    GeometryGap,
    UnreachableConfiguration,
    ScaleMismatch,
    JointLimitCollision,
    MissingMesh,
    InvalidScale
}

public enum Severity
{
    Info,
    Warning,
    Error
}

[Serializable]
public class JointConfiguration
{
    public string jointName;
    public float angle;

    public JointConfiguration(string name, float angle)
    {
        this.jointName = name;
        this.angle = angle;
    }
}

[Serializable]
public class ValidationReport
{
    public string robotName;
    public DateTime timestamp;
    public int totalJoints;
    public int totalLinks;
    public List<ValidationError> errors;
    public List<JointConfiguration> configuration;

    public ValidationReport()
    {
        timestamp = DateTime.Now;
        errors = new List<ValidationError>();
        configuration = new List<JointConfiguration>();
    }

    public string ToText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("╔════════════════════════════════════════════╗");
        sb.AppendLine("║       URDF QUALITY VALIDATION REPORT       ║");
        sb.AppendLine("╚════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"Robot: {robotName}");
        sb.AppendLine($"Time: {timestamp}");
        sb.AppendLine($"Joints: {totalJoints}");
        sb.AppendLine($"Links: {totalLinks}");
        sb.AppendLine();
        sb.AppendLine($"═══ ERRORS ({errors.Count}) ═══");

        if (errors.Count == 0)
        {
            sb.AppendLine("No errors found ✓");
        }
        else
        {
            foreach (var error in errors)
            {
                sb.AppendLine(error.ToString());
                if (error.penetrationDepth > 0)
                {
                    sb.AppendLine($"    Penetration: {error.penetrationDepth * 1000:F2}mm");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("═══ JOINT CONFIGURATION ═══");
        foreach (var joint in configuration)
        {
            sb.AppendLine($"  {joint.jointName}: {joint.angle:F2}°");
        }

        return sb.ToString();
    }
}
