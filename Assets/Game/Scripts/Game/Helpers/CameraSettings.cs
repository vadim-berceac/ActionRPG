using System;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;
using Zenject;

namespace Game
{
    public class CameraSettings : MonoBehaviour
    {
        public enum CameraType
        {
            Exploration,
            Bow
        }
        
        [Serializable]
        public struct CameraParts
        {
            public CinemachineCamera controllerCamera;
            public CinemachineOrbitalFollow orbitalFollow;
            [Header("Rotate Speed")]
            public float xFactor;
            public float yFactor;
        }
        
        [Serializable]
        public struct InvertSettings
        {
            public bool invertX;
            public bool invertY;
        }

        [Serializable]
        public struct SmoothingSettings
        {
            [Tooltip("Время сглаживания инпута по X, сек. Чем больше — тем инертнее камера")]
            public float smoothTimeX;
            [Tooltip("Время сглаживания инпута по Y, сек")]
            public float smoothTimeY;
        }

        [Serializable]
        public struct RecenterSettings
        {
            public bool enabled;
            [Tooltip("Задержка перед началом рецентровки после последнего инпута, сек")]
            public float wait;
            [Tooltip("Длительность рецентровки, сек")]
            public float time;
        }

        public float CurrentYaw
        {
            get
            {
                var active = _currentCamera == CameraType.Bow ? bowCamera : explorationCamera;
                return active.orbitalFollow.HorizontalAxis.Value;
            }
        }
        
        public CameraParts explorationCamera;
        public CameraParts bowCamera;
        public CinemachineImpulseSource impulseSource;

        public InvertSettings controllerInvertSettings;
        public SmoothingSettings controllerSmoothing;
        public RecenterSettings controllerRecentering;

        [Tooltip("Минимальный угол Pitch (в градусах). Отрицательное значение — камера выше персонажа")]
        public float pitchMin = -30f;
        [Tooltip("Максимальный угол Pitch (в градусах). Ограничивает, чтобы камера не смотрела снизу")]
        public float pitchMax = 70f;

        public CinemachineCamera Current => _currentCamera == CameraType.Bow ? bowCamera.controllerCamera : explorationCamera.controllerCamera;
        public CameraType ActiveCamera => _currentCamera;

        [Header("Events")]
        public UnityEvent<CameraType> onCameraSwitched;

        private float m_CurrentX;
        private float m_VelocityX;
        private float m_CurrentY;
        private float m_VelocityY;
        private PlayerInputHandlerService _playerInputHandler;

        private CameraType _currentCamera = CameraType.Exploration;

        [Inject]
        private void Construct(PlayerInputHandlerService playerInputHandler)
        {
            _playerInputHandler = playerInputHandler;
        }

        public void Shake(float amplitude, float duration, CinemachineImpulseDefinition.ImpulseShapes mode, Vector3? velocity = null)
        {
            impulseSource.ImpulseDefinition.ImpulseDuration = duration;
            impulseSource.ImpulseDefinition.ImpulseShape = mode;

            if (velocity.HasValue)
            {
                impulseSource.GenerateImpulseWithVelocity(velocity.Value * amplitude);
                return;
            }
            impulseSource.GenerateImpulseWithForce(amplitude);
        }

        public void Shake()
        {
            impulseSource.GenerateImpulse();
        }
        
        private void Awake()
        {
            explorationCamera.controllerCamera.gameObject.SetActive(true);
            bowCamera.controllerCamera.gameObject.SetActive(true);
            
            explorationCamera.controllerCamera.Priority = 20;
            bowCamera.controllerCamera.Priority = 10;
            
            UpdateClampSettings();
            UpdateRecenterSettings();
        }
        
        public void SetTarget(Transform followTo, Transform lookAt)
        {
            explorationCamera.controllerCamera.Follow = followTo;
            explorationCamera.controllerCamera.LookAt = lookAt;
            bowCamera.controllerCamera.Follow = followTo;
            bowCamera.controllerCamera.LookAt = lookAt;
        }

        public void SwitchCamera(CameraType targetCamera)
        {
            if (_currentCamera == targetCamera) return;

            CameraParts from, to;
            
            if (targetCamera == CameraType.Bow)
            {
                from = explorationCamera;
                to = bowCamera;
            }
            else
            {
                from = bowCamera;
                to = explorationCamera;
            }

            CopyCameraState(to, from);
            ApplySettingsToCamera(to);
            
            from.controllerCamera.Priority = 10;
            to.controllerCamera.Priority = 20;
            _currentCamera = targetCamera;

            ResetSmoothInput();
            
            onCameraSwitched?.Invoke(targetCamera);
        }

        private static void CopyCameraState(CameraParts target, CameraParts source)
        {
            var targetHorizontal = target.orbitalFollow.HorizontalAxis;
            targetHorizontal.Value = source.orbitalFollow.HorizontalAxis.Value;
            target.orbitalFollow.HorizontalAxis = targetHorizontal;

            var targetVertical = target.orbitalFollow.VerticalAxis;
            targetVertical.Value = source.orbitalFollow.VerticalAxis.Value;
            targetVertical.Range = source.orbitalFollow.VerticalAxis.Range;
            target.orbitalFollow.VerticalAxis = targetVertical;
        }

        private void ResetSmoothInput()
        {
            m_CurrentX = 0f;
            m_VelocityX = 0f;
            m_CurrentY = 0f;
            m_VelocityY = 0f;
        }

        private void ApplySettingsToCamera(CameraParts camera)
        {
            var vertical = camera.orbitalFollow.VerticalAxis;
            vertical.Range = new Vector2(pitchMin, pitchMax);
            vertical.Value = Mathf.Clamp(vertical.Value, pitchMin, pitchMax);
            camera.orbitalFollow.VerticalAxis = vertical;

            var horizontal = camera.orbitalFollow.HorizontalAxis;
            var recentering = horizontal.Recentering;
            recentering.Enabled = controllerRecentering.enabled;
            recentering.Wait = controllerRecentering.wait;
            recentering.Time = controllerRecentering.time;
            horizontal.Recentering = recentering;
            camera.orbitalFollow.HorizontalAxis = horizontal;
        }

        private void LateUpdate()
        {
            UpdateClampSettings();
            UpdateInputSettings();
        }

        private void UpdateClampSettings()
        {
            var active = _currentCamera == CameraType.Bow ? bowCamera : explorationCamera;
            
            var vertical = active.orbitalFollow.VerticalAxis;
            vertical.Range = new Vector2(pitchMin, pitchMax);
            vertical.Value = Mathf.Clamp(vertical.Value, pitchMin, pitchMax);
            active.orbitalFollow.VerticalAxis = vertical;
        }

        private void UpdateRecenterSettings()
        {
            var active = _currentCamera == CameraType.Bow ? bowCamera : explorationCamera;
            
            var horizontal = active.orbitalFollow.HorizontalAxis;
            var recentering = horizontal.Recentering;
            recentering.Enabled = controllerRecentering.enabled;
            recentering.Wait = controllerRecentering.wait;
            recentering.Time = controllerRecentering.time;
            horizontal.Recentering = recentering;
            active.orbitalFollow.HorizontalAxis = horizontal;
        }

        private void UpdateInputSettings()
        {
            var active = _currentCamera == CameraType.Bow ? bowCamera : explorationCamera;
            
            var look = _playerInputHandler.CameraInput;
            if (controllerInvertSettings.invertX) look.x = -look.x;
            if (controllerInvertSettings.invertY) look.y = -look.y;

            var targetX = look.x * active.yFactor;
            var targetY = look.y * active.xFactor;

            m_CurrentX = Mathf.SmoothDamp(m_CurrentX, targetX, ref m_VelocityX, Mathf.Max(controllerSmoothing.smoothTimeX, 0.0001f));
            m_CurrentY = Mathf.SmoothDamp(m_CurrentY, targetY, ref m_VelocityY, Mathf.Max(controllerSmoothing.smoothTimeY, 0.0001f));

            var horizontal = active.orbitalFollow.HorizontalAxis;
            horizontal.Value += m_CurrentX * Time.deltaTime;
            active.orbitalFollow.HorizontalAxis = horizontal;

            var vertical = active.orbitalFollow.VerticalAxis;
            vertical.Value = Mathf.Clamp(vertical.Value + m_CurrentY * Time.deltaTime, pitchMin, pitchMax);
            active.orbitalFollow.VerticalAxis = vertical;
        }
    }
}