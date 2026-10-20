using System;
using System.Collections.Generic;

/// <summary>
/// Shared animation action struct used by both RLAgent and RLAgent1.
/// Estratto dalla classe RLAgent per evitare conflitti di tipo.
/// </summary>
[Serializable]
public struct AgentAnimationAction
{
    public string animationName;
    public float delay;
}