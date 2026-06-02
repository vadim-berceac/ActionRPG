using UnityEngine;
using UnityEngine.Playables;
using Zenject;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game
{
    public class StartUI : MonoBehaviour
    {
        public bool alwaysDisplayMouse;
        public GameObject pauseCanvas;
        public GameObject optionsCanvas;
        public GameObject controlsCanvas;
        public GameObject audioCanvas;

        protected bool m_InPause;
        protected PlayableDirector[] m_Directors;
        private PlayerNewInput _playerNewInput;

        [Inject]
        private void Construct(PlayerNewInput playerNewInput)
        {
            _playerNewInput = playerNewInput;
        }
        
        private void Start()
        {
            if (!alwaysDisplayMouse)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            m_Directors = FindObjectsByType<PlayableDirector> (FindObjectsSortMode.None);

            _playerNewInput.Pause += SwitchPauseState;
        }

        private void OnDestroy()
        {
            _playerNewInput.Pause -= SwitchPauseState;
        }

        public void Quit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
		    Application.Quit();
#endif
        }

        public void ExitPause()
        {
            m_InPause = true;
            SwitchPauseState();
        }

        public void RestartLevel()
        {
            m_InPause = true;
            SwitchPauseState();
            SceneController.RestartZone();
        }

        protected void SwitchPauseState()
        {
            if (m_InPause && Time.timeScale > 0 || !m_InPause && ScreenFader.IsFading)
                return;

            if (!alwaysDisplayMouse)
            {
                Cursor.lockState = m_InPause ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !m_InPause;
            }

            for (int i = 0; i < m_Directors.Length; i++)
            {
                if (m_Directors[i].state == PlayState.Playing && !m_InPause)
                {
                    m_Directors[i].Pause ();
                }
                else if(m_Directors[i].state == PlayState.Paused && m_InPause)
                {
                    m_Directors[i].Resume ();
                }
            }
            
            if(!m_InPause)
                CameraShake.Stop ();

            if (m_InPause)
                CharacterInput.Instance.GainControl();
            else
                CharacterInput.Instance.ReleaseControl();

            Time.timeScale = m_InPause ? 1 : 0;

            if (pauseCanvas)
                pauseCanvas.SetActive(!m_InPause);

            if (optionsCanvas)
                optionsCanvas.SetActive(false);

            if (controlsCanvas)
                controlsCanvas.SetActive(false);

            if (audioCanvas)
                audioCanvas.SetActive(false);

            m_InPause = !m_InPause;
        }
    }
}
