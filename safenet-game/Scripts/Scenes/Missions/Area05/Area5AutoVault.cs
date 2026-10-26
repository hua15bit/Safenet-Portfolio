using System.Collections;
using UnityEngine;

/// <summary>
/// Gives Area 5 its storyboard jump without adding another small mobile button.
/// Walking toward the privacy pillar triggers a controlled vault from either
/// side, preserving the existing left/right/interact control scheme.
/// </summary>
[DisallowMultipleComponent]
public sealed class Area5AutoVault : MonoBehaviour
{
    private PlayerMovement movement;
    private Rigidbody2D body;
    private Transform obstacle;
    private bool vaulting;

    public void Configure(PlayerMovement playerMovement, Rigidbody2D playerBody, Transform targetObstacle)
    {
        movement = playerMovement;
        body = playerBody;
        obstacle = targetObstacle;
    }

    private void Update()
    {
        if (vaulting || movement == null || body == null || obstacle == null ||
            DialogueController.IsDialogueActive || Time.timeScale <= 0f)
            return;

        float horizontal = Mathf.Abs(SafeNetMobileInput.Horizontal) > 0.01f
            ? SafeNetMobileInput.Horizontal
            : SafeNetDesktopInput.Horizontal;
        if (ShouldStartVault(horizontal))
            StartCoroutine(Vault(Mathf.Sign(horizontal)));
    }

    private bool ShouldStartVault(float horizontal)
    {
        float distance = obstacle.position.x - body.position.x;
        bool approachingFromLeft = horizontal > 0.1f && distance >= 0f && distance <= 1.55f;
        bool approachingFromRight = horizontal < -0.1f && distance <= 0f && distance >= -1.55f;
        return approachingFromLeft || approachingFromRight;
    }

    private IEnumerator Vault(float direction)
    {
        vaulting = true;
        movement.enabled = false;
        body.linearVelocity = Vector2.zero;

        Vector2 start = body.position;
        Vector2 end = new Vector2(obstacle.position.x + direction * 1.65f, start.y);
        const float duration = 0.72f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector2 position = Vector2.Lerp(start, end, progress);
            position.y += Mathf.Sin(progress * Mathf.PI) * 1.65f;
            body.MovePosition(position);
            yield return new WaitForFixedUpdate();
        }

        body.position = end;
        body.linearVelocity = Vector2.zero;
        movement.enabled = true;
        vaulting = false;
    }

    private void OnDisable()
    {
        if (vaulting && movement != null)
            movement.enabled = true;
        vaulting = false;
    }
}
