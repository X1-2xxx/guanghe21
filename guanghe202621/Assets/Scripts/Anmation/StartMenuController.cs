using System;
using System.Collections;
using UnityEngine;

public class StartMenuController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float blendTime = 0.08f;

    private bool isLocked;
    private int selectedOption;

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("StartMenuController has no Animator assigned.", this);
            enabled = false;
            return;
        }

        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.Play("StartPose", 0, 0f);
    }

    public void SelectStart()
    {
        if (isLocked || selectedOption == 0)
            return;

        selectedOption = 0;
        animator.CrossFadeInFixedTime("MoveToStart", blendTime, 0, 0f);
    }

    public void SelectHome()
    {
        if (isLocked || selectedOption == 1)
            return;

        selectedOption = 1;
        animator.CrossFadeInFixedTime("MoveToHome", blendTime, 0, 0f);
    }

    public void PlayStartThen(Action onComplete)
    {
        PlayPressThen("PressStart", onComplete);
    }

    public void PlayExitThen(Action onComplete)
    {
        PlayPressThen("PressHome", onComplete);
    }

    public void CancelPress()
    {
        isLocked = false;
        selectedOption = 0;

        if (animator != null)
            animator.CrossFadeInFixedTime("StartPose", blendTime, 0, 0f);
    }

    private void PlayPressThen(string animationName, Action onComplete)
    {
        if (isLocked)
            return;

        if (animator == null)
        {
            Debug.LogError("StartMenuController has no Animator assigned.", this);
            return;
        }

        isLocked = true;
        StartCoroutine(PlayPressAnimation(animationName, onComplete));
    }

    private IEnumerator PlayPressAnimation(string animationName, Action onComplete)
    {
        const int layer = 0;
        int targetStateHash = Animator.StringToHash(animationName);

        animator.CrossFadeInFixedTime(animationName, blendTime, layer, 0f);
        yield return null;

        float stateEntryDeadline = Time.unscaledTime + 2f;
        while (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != targetStateHash ||
               animator.IsInTransition(layer))
        {
            if (Time.unscaledTime >= stateEntryDeadline)
            {
                Debug.LogError("Animator did not enter state: " + animationName, this);
                isLocked = false;
                yield break;
            }

            yield return null;
        }

        while (animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == targetStateHash &&
               animator.GetCurrentAnimatorStateInfo(layer).normalizedTime < 1f)
        {
            yield return null;
        }

        onComplete?.Invoke();
    }
}