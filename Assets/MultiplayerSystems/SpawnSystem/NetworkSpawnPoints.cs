using System.Collections.Generic;
using UnityEngine;

public class NetworkSpawnPoints : MonoBehaviour
{
    [Tooltip("Radio para validar colisiones en el punto de spawn.")]
    public float checkRadius = 0.5f;

    [Tooltip("Capas que bloquean un spawn (paredes, jugadores, props, etc.).")]
    public LayerMask collisionMask;

    [Tooltip("Altura de cápsula para validar espacio (si usas CharacterController). 0 = desactivado.")]
    public float capsuleHeight = 1.8f;

    [Tooltip("Offset vertical para colocar al jugador.")]
    public float yOffset = 0.0f;

    [SerializeField] private List<Transform> points = new();

    public int Count => points.Count;
    public Vector3 GetPoint(int i) => points[i].position + Vector3.up * yOffset;
    public Quaternion GetRotation(int i) => points[i].rotation;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Autollenar con los hijos si está vacío
        if (points.Count == 0)
        {
            points.Clear();
            foreach (Transform child in transform)
                points.Add(child);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.65f);
        foreach (var t in points)
        {
            if (!t) continue;
            Gizmos.DrawWireSphere(t.position + Vector3.up * yOffset, checkRadius);
        }
    }
#endif

    public bool IsFree(int index)
    {
        if (index < 0 || index >= points.Count || points[index] == null) return false;

        Vector3 p = points[index].position + Vector3.up * yOffset;
        bool blocked = Physics.CheckSphere(p, checkRadius, collisionMask, QueryTriggerInteraction.Ignore);

        if (!blocked && capsuleHeight > 0f)
        {
            Vector3 p1 = p;
            Vector3 p2 = p + Vector3.up * capsuleHeight;
            blocked = Physics.CheckCapsule(p1, p2, checkRadius, collisionMask, QueryTriggerInteraction.Ignore);
        }
        return !blocked;
    }
}
