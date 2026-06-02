using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// This class is used to move gameobjects from one position to another in the scene.
    /// </summary>
    public class GameObjectTeleporter : MonoBehaviour
    {
        public static GameObjectTeleporter Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindFirstObjectByType<GameObjectTeleporter>();

                if (instance != null)
                    return instance;

                GameObject gameObjectTeleporter = new GameObject("GameObjectTeleporter");
                instance = gameObjectTeleporter.AddComponent<GameObjectTeleporter>();

                return instance;
            }
        }

        public static bool Transitioning
        {
            get { return Instance.m_Transitioning; }
        }

        protected static GameObjectTeleporter instance;

        protected CharacterInput MCharacterInput;
        protected bool m_Transitioning;

        void Awake()
        {
            if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            MCharacterInput = FindFirstObjectByType<CharacterInput>();
        }

        public static void Teleport(TransitionPoint transitionPoint)
        {
            Transform destinationTransform = Instance.GetDestination(transitionPoint.transitionDestinationTag).transform;
            Instance.StartCoroutine(Instance.Transition(transitionPoint.transitioningGameObject, true, destinationTransform.position, true));
        }

        public static void Teleport(GameObject transitioningGameObject, Transform destination)
        {
            Instance.StartCoroutine(Instance.Transition(transitioningGameObject, false, destination.position, false));
        }

        public static void Teleport(GameObject transitioningGameObject, Vector3 destinationPosition)
        {
            Instance.StartCoroutine(Instance.Transition(transitioningGameObject, false, destinationPosition, false));
        }

        protected IEnumerator Transition(GameObject transitioningGameObject, bool releaseControl, Vector3 destinationPosition, bool fade)
        {
            m_Transitioning = true;

            if (releaseControl)
            {
                if (MCharacterInput == null)
                    MCharacterInput = FindFirstObjectByType<CharacterInput>();
                MCharacterInput.ReleaseControl();
            }

            if (fade)
                yield return StartCoroutine(ScreenFader.FadeSceneOut());

            transitioningGameObject.transform.position = destinationPosition;

            if (fade)
                yield return StartCoroutine(ScreenFader.FadeSceneIn());

            if (releaseControl)
            {
                MCharacterInput.GainControl();
            }

            m_Transitioning = false;
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
    }
}