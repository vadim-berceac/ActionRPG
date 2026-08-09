using Game;
using UnityEngine;
using Zenject;

public class CameraSoundPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip audioClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private float maxDistanceToCamera;

    private CameraSettings _cameraSettings;

    [Inject]
    private void Construct(CameraSettings cameraSettings)
    {
        _cameraSettings = cameraSettings;
    }

    public void PlayClipByCamera()
    {
        if (!audioClip || !_cameraSettings || !_cameraSettings.mainCamera)
        {
            return;
        }

        var sqrDistance = (transform.position - _cameraSettings.mainCamera.transform.position).sqrMagnitude;
        if (sqrDistance > maxDistanceToCamera * maxDistanceToCamera)
        {
            return;
        }

        _cameraSettings.PlayAudioByCamera(audioClip, volume, false);
    }

    public void PlayLoopClipByCamera()
    {
        if (!audioClip || !_cameraSettings || !_cameraSettings.mainCamera)
        {
            return;
        }

        var sqrDistance = (transform.position - _cameraSettings.mainCamera.transform.position).sqrMagnitude;
        if (sqrDistance > maxDistanceToCamera * maxDistanceToCamera)
        {
            return;
        }

        _cameraSettings.PlayAudioByCamera(audioClip, volume, true);
    }

    public void StopClipByCamera()
    {
        if (!audioClip || !_cameraSettings || !_cameraSettings.mainCamera)
        {
            return;
        }
        
        _cameraSettings.StopAudioByCamera();
    }
}