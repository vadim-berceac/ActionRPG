using System;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace Game
{
    public class CameraSettings : MonoBehaviour
    {
        [Serializable]
        public struct CameraParts
        {
            public CinemachineCamera controllerCamera;
            public CinemachineOrbitalFollow orbitalFollow;
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

        public float CurrentYaw => explorationCamera.orbitalFollow.HorizontalAxis.Value;
        public CameraParts explorationCamera;
        public CameraParts bowCamera;
        public CinemachineImpulseSource impulseSource;

        public InvertSettings controllerInvertSettings;
        public SmoothingSettings controllerSmoothing;
        public RecenterSettings controllerRecentering;

        public float xFactor;
        public float yFactor;

        [Tooltip("Минимальный угол Pitch (в градусах). Отрицательное значение — камера выше персонажа")]
        public float pitchMin = -30f;
        [Tooltip("Максимальный угол Pitch (в градусах). Ограничивает, чтобы камера не смотрела снизу")]
        public float pitchMax = 70f;

        public CinemachineCamera Current => explorationCamera.controllerCamera;

        private float m_CurrentX;
        private float m_VelocityX;
        private float m_CurrentY;
        private float m_VelocityY;
        private PlayerInputHandlerService _playerInputHandler;

        [Inject]
        private void Construct(PlayerInputHandlerService playerInputHandler)
        {
            _playerInputHandler = playerInputHandler;
        }

        public void Shake(float amplitude, float duration, CinemachineImpulseDefinition.ImpulseShapes mode,  Vector3? velocity = null)
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
            UpdateClampSettings();
            UpdateRecenterSettings();
        }
        
        public void SetTarget(Transform followTo, Transform look)
        {
            explorationCamera.controllerCamera.Follow = followTo;
            explorationCamera.controllerCamera.LookAt = look;
        }

        private void LateUpdate()
        {
            UpdateClampSettings();
            UpdateInputSettings();
        }

        private void UpdateClampSettings()
        {
            var vertical = explorationCamera.orbitalFollow.VerticalAxis;
            vertical.Range = new Vector2(pitchMin, pitchMax);
            vertical.Value = Mathf.Clamp(vertical.Value, pitchMin, pitchMax);
            explorationCamera.orbitalFollow.VerticalAxis = vertical;
        }

        private void UpdateRecenterSettings()
        {
            var horizontal = explorationCamera.orbitalFollow.HorizontalAxis;
            var recentering = horizontal.Recentering;
            recentering.Enabled = controllerRecentering.enabled;
            recentering.Wait = controllerRecentering.wait;
            recentering.Time = controllerRecentering.time;
            horizontal.Recentering = recentering;
            explorationCamera.orbitalFollow.HorizontalAxis = horizontal;
        }

        private void UpdateInputSettings()
        {
            var look = _playerInputHandler.CameraInput;
            if (controllerInvertSettings.invertX) look.x = -look.x;
            if (controllerInvertSettings.invertY) look.y = -look.y;

            var targetX = look.x * xFactor;
            var targetY = look.y * yFactor;

            m_CurrentX = Mathf.SmoothDamp(m_CurrentX, targetX, ref m_VelocityX, Mathf.Max(controllerSmoothing.smoothTimeX, 0.0001f));
            m_CurrentY = Mathf.SmoothDamp(m_CurrentY, targetY, ref m_VelocityY, Mathf.Max(controllerSmoothing.smoothTimeY, 0.0001f));

            var horizontal = explorationCamera.orbitalFollow.HorizontalAxis;
            horizontal.Value += m_CurrentX * Time.deltaTime;
            explorationCamera.orbitalFollow.HorizontalAxis = horizontal;

            var vertical = explorationCamera.orbitalFollow.VerticalAxis;
            vertical.Value = Mathf.Clamp(vertical.Value + m_CurrentY * Time.deltaTime, pitchMin, pitchMax);
            explorationCamera.orbitalFollow.VerticalAxis = vertical;
        }
    }
}