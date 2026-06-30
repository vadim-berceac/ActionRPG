using System;
using Cinemachine;
using UnityEngine;

namespace Game
{
    public class CameraSettings : MonoBehaviour
    {
        [Serializable]
        public struct InvertSettings
        {
            public bool invertX;
            public bool invertY;
        }
        
        [Serializable]
        public struct AxisClampSettings
        {
            [Range(0f, 1f)] public float minY;
            [Range(0f, 1f)] public float maxY;
        }

        public CinemachineFreeLook controllerCamera;
        public InvertSettings controllerInvertSettings;
        public AxisClampSettings controllerAxisClamp;
        public float xFactor;
        public float yFactor;

        public CinemachineFreeLook Current => controllerCamera;

        public void SetTarget(Transform followTo, Transform look)
        {
            controllerCamera.Follow = followTo;
            controllerCamera.LookAt = look;
        }

        private void Awake()
        {
            UpdateCameraSettings();
            UpdateClampSettings();
        }

        private void LateUpdate()
        {
            UpdateCameraSettings();
            UpdateClampSettings();
            UpdateInputSettings();
        }

        private void UpdateCameraSettings()
        {
            controllerCamera.m_XAxis.m_InvertInput = controllerInvertSettings.invertX;
            controllerCamera.m_YAxis.m_InvertInput = controllerInvertSettings.invertY;
        }

        private void UpdateClampSettings()
        {
            controllerCamera.m_YAxis.m_MinValue = controllerAxisClamp.minY;
            controllerCamera.m_YAxis.m_MaxValue = controllerAxisClamp.maxY;
        }

        private void UpdateInputSettings()
        {
            var input = CharacterInput.Instance;
            if (input == null) return;

            var look = input.CameraInput;

            Current.m_XAxis.m_InputAxisValue = look.x * xFactor;
            Current.m_YAxis.m_InputAxisValue = look.y * yFactor;
        }
    } 
}
