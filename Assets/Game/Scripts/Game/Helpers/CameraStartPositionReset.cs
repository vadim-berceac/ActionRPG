using Unity.Cinemachine;
using UnityEngine;

public class CameraStartPositionReset : MonoBehaviour
{
    [SerializeField] private CinemachineCamera cam;
    [SerializeField] private float startDistance = 5f;
    [SerializeField] private float startHeight = 1.5f;

    private void OnEnable()
    {
        CinemachineCore.CameraActivatedEvent.AddListener(OnCameraActivated);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraActivatedEvent.RemoveListener(OnCameraActivated);
    }

    private void OnCameraActivated(ICinemachineCamera.ActivationEventParams evt)
    {
        if (evt.IncomingCamera != (ICinemachineCamera)cam)
        {
            return;
        }

        var target = cam.Follow;
        
        if (target == null)
        {
            return;
        }

        var desiredPos = target.position - target.forward * startDistance 
                         + Vector3.up * startHeight;

        var desiredRot = Quaternion.LookRotation(target.position - desiredPos);

        cam.ForceCameraPosition(desiredPos, desiredRot);
        cam.PreviousStateIsValid = false;
    }
}