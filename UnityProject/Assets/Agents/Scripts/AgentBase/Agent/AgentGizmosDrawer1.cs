using System.Collections.Generic;
using UnityEngine;
using static AgentSensorsManager1;

[RequireComponent(typeof(AgentSensorsManager1))]
public class AgentGizmosDrawer1 : MonoBehaviour
{
    public bool gizmosDrawer;
    public IAgentConstants constants;

    private List<(GizmosTag, Vector3)> wallsAndTargetsObservations = new List<(GizmosTag, Vector3)>();
    private List<(GizmosTag, Vector3)> wallsAndAgentsObservations = new List<(GizmosTag, Vector3)>();

    public void SetObservationsResults(List<(GizmosTag, Vector3)> wallsAndTargetsObservations, List<(GizmosTag, Vector3)> wallsAndAgentsObservations)
    {
        this.wallsAndTargetsObservations = wallsAndTargetsObservations;
        this.wallsAndAgentsObservations = wallsAndAgentsObservations;
    }

    private Dictionary<GizmosTag, Color> _tagColorDict = new Dictionary<GizmosTag, Color>()
    {
        { GizmosTag.Wall,       new Color(1, 1, 1, 0.05f) },
        { GizmosTag.Agent,      Color.cyan                },
        { GizmosTag.NewTarget,  Color.green               },
        { GizmosTag.TakenTarget,Color.red                 },
        { GizmosTag.Obiettivo,  Color.yellow              }  // colore per gli objectives
    };

    private AgentSensorsManager1 agentSensorsManager;

    private void Start()
    {
        agentSensorsManager = GetComponent<AgentSensorsManager1>();
    }

    private void OnDrawGizmos()
    {
        if (gizmosDrawer)
        {
            DrawObservationsGizmos();
            DrawGizmosProxemics();
        }
    }

    private void DrawObservationsGizmos()
    {
        if (constants == null) return;
        Vector3 newPosition = transform.position + (Vector3.up * constants.verticalRayOffset);
        newPosition.y += 1;

        for (int i = 0; i < wallsAndTargetsObservations.Count && i < wallsAndAgentsObservations.Count; i++)
        {
            (GizmosTag wallsAndTargetTag, Vector3 wallsAndTargetVector) = wallsAndTargetsObservations[i];
            (GizmosTag wallsAndAgentTag,  Vector3 wallsAndAgentVector)  = wallsAndAgentsObservations[i];

            float distTarget = Vector3.Distance(newPosition, wallsAndTargetVector);
            float distAgent  = Vector3.Distance(newPosition, wallsAndAgentVector);

            if (distTarget < distAgent)
            {
                Gizmos.color = _tagColorDict[wallsAndTargetTag];
                Gizmos.DrawLine(newPosition, wallsAndTargetVector);
                Gizmos.color = _tagColorDict[wallsAndAgentTag];
                Gizmos.DrawLine(newPosition, wallsAndAgentVector);
            }
            else
            {
                Gizmos.color = _tagColorDict[wallsAndAgentTag];
                Gizmos.DrawLine(newPosition, wallsAndAgentVector);
                Gizmos.color = _tagColorDict[wallsAndTargetTag];
                Gizmos.DrawLine(newPosition, wallsAndTargetVector);
            }
        }
    }

    private void DrawGizmosProxemics()
    {
        if (constants == null) return;
        Gizmos.color = Color.red;
        Vector3 newPosition = transform.position + (Vector3.up * constants.verticalRayOffset);
        newPosition.y += 1;

        if (agentSensorsManager != null)
        {
            foreach (Proxemic proxemic in constants.Proxemics)
            {
                float d = proxemic.Distance + constants.rayOffset;

                Gizmos.DrawRay(newPosition, agentSensorsManager.CalculateRayDirection(proxemic.RaysNumberPerSide,  1) * d);
                Gizmos.DrawRay(newPosition, agentSensorsManager.CalculateRayDirection(proxemic.RaysNumberPerSide, -1) * d);

                for (int i = 0; i < proxemic.RaysNumberPerSide; i++)
                {
                    Vector3 a1 = newPosition + (agentSensorsManager.CalculateRayDirection(i,     1) * d);
                    Vector3 b1 = newPosition + (agentSensorsManager.CalculateRayDirection(i + 1, 1) * d);
                    Vector3 a2 = newPosition + (agentSensorsManager.CalculateRayDirection(i,    -1) * d);
                    Vector3 b2 = newPosition + (agentSensorsManager.CalculateRayDirection(i + 1,-1) * d);

                    Gizmos.DrawLine(a1, b1);
                    Gizmos.DrawLine(a2, b2);
                }
            }
        }
    }
}