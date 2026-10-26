using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Applies final framing inside Cinemachine's pipeline, so camera damping cannot
/// move the view outside the artwork. Artwork itself is never stretched.
/// </summary>
[DisallowMultipleComponent]
public sealed class SafeNetCameraBounds : CinemachineExtension
{
    private SafeNetLevelBounds level;
    public void Configure(SafeNetLevelBounds value) => level = value;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || level == null || !level.IsReady) return;
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        state.Lens.OrthographicSize = level.CameraSizeForAspect(aspect);
        Vector3 position = state.RawPosition + state.PositionCorrection;
        Vector3 confined = level.ConfineCamera(position, state.Lens.OrthographicSize, aspect, level.PlayerBounds);
        state.PositionCorrection += confined - position;
    }
}
