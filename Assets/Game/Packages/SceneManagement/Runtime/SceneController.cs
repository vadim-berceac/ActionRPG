using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Game
{
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindFirstObjectByType<SceneController>();

                if (instance != null)
                    return instance;

                Create();

                return instance;
            }
        }

        public static bool Transitioning
        {
            get { return Instance.m_Transitioning; }
        }

        protected static SceneController instance;

        public static SceneController Create()
        {
            GameObject sceneControllerGameObject = new GameObject("SceneController");
            instance = sceneControllerGameObject.AddComponent<SceneController>();

            return instance;
        }

        public SceneTransitionDestination initialSceneTransitionDestination;

        protected Scene m_CurrentZoneScene;
        protected SceneTransitionDestination.DestinationTag m_ZoneRestartDestinationTag;
        protected PlayerNewInput MCharacterInput;
        protected bool m_Transitioning;
        
        CancellationTokenSource m_LifetimeCts;

        [Inject]
        private void Construct(PlayerNewInput mCharacterInput)
        {
            MCharacterInput = mCharacterInput;
        }

        private void Awake()
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            m_LifetimeCts = new CancellationTokenSource();

            if (initialSceneTransitionDestination != null)
            {
                SetEnteringGameObjectLocation(initialSceneTransitionDestination);
                ScreenFader.SetAlpha(1f);
                InitialFadeIn().Forget();
                initialSceneTransitionDestination.OnReachDestination.Invoke();
            }
            else
            {
                m_CurrentZoneScene = SceneManager.GetActiveScene();
                m_ZoneRestartDestinationTag = SceneTransitionDestination.DestinationTag.A;
            }
        }

        private async UniTaskVoid InitialFadeIn()
        {
            try
            {
                await ScreenFader.FadeSceneIn(m_LifetimeCts.Token);
            }
            catch (OperationCanceledException)
            {
                
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        void OnDestroy()
        {
            m_LifetimeCts?.Cancel();
            m_LifetimeCts?.Dispose();
            m_LifetimeCts = null;
        }

        public static void RestartZone(bool resetHealth = true)
        {
            Instance.Transition(Instance.m_CurrentZoneScene.name, Instance.m_ZoneRestartDestinationTag).Forget();
        }

        public static void RestartZoneWithDelay(float delay, bool resetHealth = true)
        {
            Instance.CallWithDelay(delay, RestartZone, resetHealth).Forget();
        }

        public static void TransitionToScene(TransitionPoint transitionPoint)
        {
            Instance.Transition(transitionPoint.newSceneName, transitionPoint.transitionDestinationTag, transitionPoint.transitionType).Forget();
        }

        protected async UniTaskVoid Transition(string newSceneName, SceneTransitionDestination.DestinationTag destinationTag, TransitionPoint.TransitionType transitionType = TransitionPoint.TransitionType.DifferentZone)
        {
            var token = m_LifetimeCts.Token;

            m_Transitioning = true;

            try
            {
                //PersistentDataManager.SaveAllData();

                //MCharacterInput.Enable(false);
                await ScreenFader.FadeSceneOut(ScreenFader.FadeType.Loading, token);

                //PersistentDataManager.ClearPersisters();
                await SceneManager.LoadSceneAsync(newSceneName).ToUniTask(cancellationToken: token);

                //MCharacterInput.ReleaseControl();
                //PersistentDataManager.LoadAllData();
                var entrance = GetDestination(destinationTag);
                SetEnteringGameObjectLocation(entrance);
                SetupNewScene(transitionType, entrance);
                if (entrance)
                {
                    entrance.OnReachDestination.Invoke();
                }

                await ScreenFader.FadeSceneIn(token);
                //MCharacterInput.Enable(true);
            }
            finally
            {
                m_Transitioning = false;
            }
        }
        
        public static async UniTask RunWithLoadingFade(Func<UniTask> action, CancellationToken cancellationToken = default)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                Instance.m_LifetimeCts.Token, cancellationToken);
            var token = linkedCts.Token;

            //Instance.MCharacterInput.Enable(false);
            await ScreenFader.FadeSceneOut(ScreenFader.FadeType.Loading, token);

            try
            {
                await action();
            }
            finally
            {
                await ScreenFader.FadeSceneIn(token);
                //Instance.MCharacterInput.Enable(true);
            }
        }

        protected SceneTransitionDestination GetDestination(SceneTransitionDestination.DestinationTag destinationTag)
        {
            SceneTransitionDestination[] entrances = FindObjectsByType<SceneTransitionDestination>(FindObjectsSortMode.None);
            for (int i = 0; i < entrances.Length; i++)
            {
                if (entrances[i].destinationTag == destinationTag)
                    return entrances[i];
            }
            Debug.LogWarning("No entrance was found with the " + destinationTag + " tag.");
            return null;
        }

        protected void SetEnteringGameObjectLocation(SceneTransitionDestination entrance)
        {
            if (entrance == null)
            {
                Debug.LogWarning("Entering Transform's location has not been set.");
                return;
            }
            var entranceLocation = entrance.transform;
            var enteringTransform = entrance.transitioningGameObject.transform;
            enteringTransform.position = entranceLocation.position;
            enteringTransform.rotation = entranceLocation.rotation;
        }

        protected void SetupNewScene(TransitionPoint.TransitionType transitionType, SceneTransitionDestination entrance)
        {
            if (entrance == null)
            {
                Debug.LogWarning("Restart information has not been set.");
                return;
            }

            if (transitionType == TransitionPoint.TransitionType.DifferentZone)
                SetZoneStart(entrance);
        }

        protected void SetZoneStart(SceneTransitionDestination entrance)
        {
            m_CurrentZoneScene = entrance.gameObject.scene;
            m_ZoneRestartDestinationTag = entrance.destinationTag;
        }

        async UniTaskVoid CallWithDelay<T>(float delay, Action<T> call, T parameter)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: m_LifetimeCts.Token);
            call(parameter);
        }
    }
}