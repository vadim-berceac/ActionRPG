using System;
using Cinemachine;
using UnityEngine;

namespace Game
{
    public class CameraSettings : MonoBehaviour
    {
        public enum InputChoice
        {
            KeyboardAndMouse, Controller,
        }

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

        public Transform follow;
        public Transform lookAt;
        public CinemachineFreeLook keyboardAndMouseCamera;
        public CinemachineFreeLook controllerCamera;
        public InputChoice inputChoice;
        public InvertSettings keyboardAndMouseInvertSettings;
        public InvertSettings controllerInvertSettings;
        public AxisClampSettings keyboardAndMouseAxisClamp;
        public AxisClampSettings controllerAxisClamp;
        public bool allowRuntimeCameraSettingsChanges;
        public float xFactor;
        public float yFactor;

        public CinemachineFreeLook Current
        {
            get { return inputChoice == InputChoice.KeyboardAndMouse ? keyboardAndMouseCamera : controllerCamera; }
        }

        public void SetTarget(Transform followTo, Transform look)
        {
            follow = followTo;
            lookAt = look;
        }

        private void Awake()
        {
            UpdateCameraSettings();
            UpdateClampSettings();
        }

        private void LateUpdate()
        {
            if (allowRuntimeCameraSettingsChanges)
                UpdateCameraSettings();

            UpdateClampSettings();
            UpdateInputSettings();
        }

        private void UpdateCameraSettings()
        {
            keyboardAndMouseCamera.Follow = follow;
            keyboardAndMouseCamera.LookAt = lookAt;

            controllerCamera.m_XAxis.m_InvertInput = controllerInvertSettings.invertX;
            controllerCamera.m_YAxis.m_InvertInput = controllerInvertSettings.invertY;
            controllerCamera.Follow = follow;
            controllerCamera.LookAt = lookAt;

            keyboardAndMouseCamera.Priority = inputChoice == InputChoice.KeyboardAndMouse ? 1 : 0;
            controllerCamera.Priority = inputChoice == InputChoice.Controller ? 1 : 0;
        }

        private void UpdateClampSettings()
        {
            keyboardAndMouseCamera.m_YAxis.m_MinValue = keyboardAndMouseAxisClamp.minY;
            keyboardAndMouseCamera.m_YAxis.m_MaxValue = keyboardAndMouseAxisClamp.maxY;

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
