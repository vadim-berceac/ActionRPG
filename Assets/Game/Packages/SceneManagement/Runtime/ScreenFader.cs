using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace Game
{
    public class ScreenFader : MonoBehaviour
    {
        public enum FadeType
        {
            Black, Loading, GameOver,
        }

        public static ScreenFader Instance
        {
            get
            {
                if (s_Instance != null)
                    return s_Instance;

                s_Instance = FindFirstObjectByType<ScreenFader>();

                if (s_Instance != null)
                    return s_Instance;

                Create();

                return s_Instance;
            }
        }

        public static bool IsFading
        {
            get { return Instance.m_IsFading; }
        }

        protected static ScreenFader s_Instance;

        public static void Create()
        {
            ScreenFader controllerPrefab = Resources.Load<ScreenFader>("ScreenFader");
            s_Instance = Instantiate(controllerPrefab);
        }

        public CanvasGroup faderCanvasGroup;
        public CanvasGroup loadingCanvasGroup;
        public CanvasGroup gameOverCanvasGroup;
        public float fadeDuration = 1f;

        protected bool m_IsFading;

        const int k_MaxSortingLayer = 32767;

        // Токен, привязанный к жизни объекта — гасит все фейды при уничтожении/смене сцены
        CancellationTokenSource m_LifetimeCts;

        void Awake()
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            m_LifetimeCts = new CancellationTokenSource();
        }

        void OnDestroy()
        {
            m_LifetimeCts?.Cancel();
            m_LifetimeCts?.Dispose();
            m_LifetimeCts = null;
        }

        protected async UniTask Fade(float finalAlpha, CanvasGroup canvasGroup, CancellationToken externalToken = default)
        {
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                m_LifetimeCts.Token, externalToken);
            CancellationToken token = linkedCts.Token;

            m_IsFading = true;
            canvasGroup.blocksRaycasts = true;

            try
            {
                float fadeSpeed = Mathf.Abs(canvasGroup.alpha - finalAlpha) / fadeDuration;

                while (!Mathf.Approximately(canvasGroup.alpha, finalAlpha))
                {
                    token.ThrowIfCancellationRequested();

                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, finalAlpha,
                        fadeSpeed * Time.deltaTime);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                canvasGroup.alpha = finalAlpha;
            }
            catch (System.OperationCanceledException)
            {
                // Объект уничтожен или фейд отменён — просто выходим,
                // не бросаем исключение дальше и не оставляем m_IsFading = true.
            }
            finally
            {
                m_IsFading = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public static void SetAlpha(float alpha)
        {
            Instance.faderCanvasGroup.alpha = alpha;
        }

        public static async UniTask FadeSceneIn(CancellationToken token = default)
        {
            CanvasGroup canvasGroup;
            if (Instance.faderCanvasGroup.alpha > 0.1f)
                canvasGroup = Instance.faderCanvasGroup;
            else if (Instance.gameOverCanvasGroup.alpha > 0.1f)
                canvasGroup = Instance.gameOverCanvasGroup;
            else
                canvasGroup = Instance.loadingCanvasGroup;

            await Instance.Fade(0f, canvasGroup, token);

            if (canvasGroup != null)
                canvasGroup.gameObject.SetActive(false);
        }

        public static async UniTask FadeSceneOut(FadeType fadeType = FadeType.Black, CancellationToken token = default)
        {
            CanvasGroup canvasGroup;
            switch (fadeType)
            {
                case FadeType.Black:
                    canvasGroup = Instance.faderCanvasGroup;
                    break;
                case FadeType.GameOver:
                    canvasGroup = Instance.gameOverCanvasGroup;
                    break;
                default:
                    canvasGroup = Instance.loadingCanvasGroup;
                    break;
            }

            canvasGroup.gameObject.SetActive(true);

            await Instance.Fade(1f, canvasGroup, token);
        }
    }
}