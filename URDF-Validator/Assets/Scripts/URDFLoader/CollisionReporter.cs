using UnityEngine;
using System.Collections.Generic;

public class CollisionReporter : MonoBehaviour
{
    [Header("Reference")]
    public URDFQualityValidator validator;

    [Header("State")]
    public List<Collider> currentCollisions = new List<Collider>();
    public int collisionCount => currentCollisions.Count;

    [Header("Settings")]
    public bool reportTriggers = true;
    public bool reportCollisions = true;

    private Collider myCollider;

    void Awake()
    {
        myCollider = GetComponent<Collider>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!reportCollisions) return;

        if (!currentCollisions.Contains(collision.collider))
        {
            currentCollisions.Add(collision.collider);
            OnCollisionDetected(collision.collider, collision);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        if (!reportCollisions) return;

        if (!currentCollisions.Contains(collision.collider))
        {
            currentCollisions.Add(collision.collider);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (!reportCollisions) return;

        currentCollisions.Remove(collision.collider);
        OnCollisionEnded(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!reportTriggers) return;

        if (!currentCollisions.Contains(other))
        {
            currentCollisions.Add(other);
            OnCollisionDetected(other, null);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!reportTriggers) return;

        if (!currentCollisions.Contains(other))
        {
            currentCollisions.Add(other);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!reportTriggers) return;

        currentCollisions.Remove(other);
        OnCollisionEnded(other);
    }

    void OnCollisionDetected(Collider other, Collision collision)
    {
        if (validator != null)
        {
            validator.OnCollisionReported(this, other, collision);
        }
    }

    void OnCollisionEnded(Collider other)
    {
        if (validator != null)
        {
            validator.OnCollisionEndedReport(this, other);
        }
    }

    public bool IsCollidingWith(Collider other)
    {
        return currentCollisions.Contains(other);
    }

    public bool IsCollidingWithAny()
    {
        return currentCollisions.Count > 0;
    }

    public void ClearCollisions()
    {
        currentCollisions.Clear();
    }
}
