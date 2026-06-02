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

        public Transform follow;
        public Transform lookAt;
        public CinemachineFreeLook keyboardAndMouseCamera;
        public CinemachineFreeLook controllerCamera;
        public InputChoice inputChoice;
        public InvertSettings keyboardAndMouseInvertSettings;
        public InvertSettings controllerInvertSettings;
        public bool allowRuntimeCameraSettingsChanges;
        public float xFactor;
        public float yFactor;

        public CinemachineFreeLook Current
        {
            get { return inputChoice == InputChoice.KeyboardAndMouse ? keyboardAndMouseCamera : controllerCamera; }
        }

        private void Awake()
        {
            UpdateCameraSettings();
        }

        private void LateUpdate()
        {
            if (allowRuntimeCameraSettingsChanges)
                UpdateCameraSettings();

            var input = CharacterInput.Instance;
            if (input == null) return;

            var look = input.CameraInput;

            Current.m_XAxis.m_InputAxisValue = look.x * xFactor;
            Current.m_YAxis.m_InputAxisValue = look.y * yFactor;
        }

        private void UpdateCameraSettings()
        {
            keyboardAndMouseCamera.Follow = follow;
            keyboardAndMouseCamera.LookAt = lookAt;
            keyboardAndMouseCamera.m_XAxis.m_InvertInput = keyboardAndMouseInvertSettings.invertX;
            keyboardAndMouseCamera.m_YAxis.m_InvertInput = keyboardAndMouseInvertSettings.invertY;

            controllerCamera.m_XAxis.m_InvertInput = controllerInvertSettings.invertX;
            controllerCamera.m_YAxis.m_InvertInput = controllerInvertSettings.invertY;
            controllerCamera.Follow = follow;
            controllerCamera.LookAt = lookAt;

            keyboardAndMouseCamera.Priority = inputChoice == InputChoice.KeyboardAndMouse ? 1 : 0;
            controllerCamera.Priority = inputChoice == InputChoice.Controller ? 1 : 0;
        }
    } 
}
