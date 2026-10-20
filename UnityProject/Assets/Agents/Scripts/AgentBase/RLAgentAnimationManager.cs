using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extension methods for IAgentRL
/// </summary>
public static class IAgentRLExtensions
{
    public static void SetAnimationSequenceMode(this IAgentRL agent, bool isPlaying)
    {
        if (agent is RLAgent rlAgent)
            rlAgent.SetAnimationSequenceMode(isPlaying);
        else if (agent is RLAgent1 rlAgent1)
            rlAgent1.SetAnimationSequenceMode(isPlaying);
    }
    
    public static void MoveToNextTarget(this IAgentRL agent)
    {
        if (agent is RLAgent rlAgent)
            rlAgent.MoveToNextTarget();
        else if (agent is RLAgent1 rlAgent1)
            rlAgent1.MoveToNextTarget();
    }
}

/// <summary>
/// Extension of AgentAnimationManager for RL agents.
/// Adds RL-specific functionality like delayed movement and animation sequences.
/// </summary>
public class RLAgentAnimationManager : AgentAnimationManager
{
    private IAgentRL agent;

    protected override void Awake()
    {
        base.Awake();
        agent = GetComponent<IAgentRL>();
        if (agent == null)
            Debug.LogWarning("IAgentRL not found on " + gameObject.name);
    }

    public void MoveToNextTargetWithDelay(float delay)
    {
        if (agent != null)
            agent.StartCoroutine(MoveToNextTargetWithDelayCoroutine(delay));
    }

    private IEnumerator MoveToNextTargetWithDelayCoroutine(float delay)
    {
        yield return new WaitUntil(() => animator != null);

        if (delay > 0)
        {
            agent.SetWalking(false);
            agent.GetRigidBody().velocity = Vector3.zero;
            yield return new WaitForSeconds(delay);
        }
        this.SetWalking(true);
        agent.SetWalking(true);
        if (agent is RLAgent rlAgent)
            rlAgent.MoveToNextTarget();
        else if (agent is RLAgent1 rlAgent1)
            rlAgent1.MoveToNextTarget();
    }

    public void PlayActionTriggerInSequence(string triggerName)
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError("Animator not found!");
                return;
            }
        }

        if (string.IsNullOrEmpty(triggerName)) return;

        if (triggerName == "isWalking")
        {
            this.SetWalking(true);
            return;
        }
        else if (triggerName == "isIdle")
        {
            SetIdle(true);
            return;
        }

        bool triggerExists = HasParameter(triggerName, AnimatorControllerParameterType.Trigger);
        if (!triggerExists)
        {
            Debug.LogWarning($"Trigger '{triggerName}' not found in animator controller");
            return;
        }

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
        Debug.Log($"Animation trigger '{triggerName}' set in sequence");
    }

    // -------------------------------------------------------
    // Overload per RLAgent (originale) — invariato
    // -------------------------------------------------------
    public IEnumerator PlayAnimationsSequence(List<RLAgent.AnimationAction> animations, IAgentRL agent)
    {
        yield return new WaitUntil(() => animator != null);

        agent.SetAnimationSequenceMode(true);
        agent.SetWalking(false);
        agent.GetRigidBody().velocity = Vector3.zero;

        foreach (var anim in animations)
        {
            Debug.Log($"Playing animation: {anim.animationName} with delay: {anim.delay}");
            animator.SetBool("isIdle", false);
            animator.SetBool("isWalking", false);
            PlayActionTriggerInSequence(anim.animationName);
            yield return new WaitForSeconds(anim.delay);
        }

        agent.SetAnimationSequenceMode(false);
        this.SetWalking(true);
        agent.SetWalking(true);
        agent.MoveToNextTarget();
    }

    // -------------------------------------------------------
    // Overload per RLAgent1 — usa AgentAnimationAction condiviso
    // -------------------------------------------------------
    public IEnumerator PlayAnimationsSequence(List<AgentAnimationAction> animations, IAgentRL agent)
    {
        yield return new WaitUntil(() => animator != null);

        agent.SetAnimationSequenceMode(true);
        agent.SetWalking(false);
        agent.GetRigidBody().velocity = Vector3.zero;

        foreach (var anim in animations)
        {
            Debug.Log($"Playing animation: {anim.animationName} with delay: {anim.delay}");
            animator.SetBool("isIdle", false);
            animator.SetBool("isWalking", false);
            PlayActionTriggerInSequence(anim.animationName);
            yield return new WaitForSeconds(anim.delay);
        }

        agent.SetAnimationSequenceMode(false);
        this.SetWalking(true);
        agent.SetWalking(true);
        agent.MoveToNextTarget();
    }
}