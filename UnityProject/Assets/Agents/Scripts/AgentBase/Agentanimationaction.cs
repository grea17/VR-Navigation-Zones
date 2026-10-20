using System;
using System.Collections.Generic;

/// <summary>
/// Shared animation action struct used by both RLAgent and RLAgent1.
/// Extracted from the RLAgent class to avoid type conflicts.
/// </summary>
[Serializable]
public struct AgentAnimationAction
{
    public string animationName;
    public float delay;
}