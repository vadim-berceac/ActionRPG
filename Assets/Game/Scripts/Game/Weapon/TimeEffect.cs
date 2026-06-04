using System.Collections;
using UnityEngine;

namespace Game
{
    public class TimeEffect : MonoBehaviour
    {
        public float duration;
        public Light staffLight;
        
        Animation m_Animation;

        void Awake()
        {
            m_Animation = GetComponent<Animation>();

            gameObject.SetActive(false);
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            staffLight.enabled = true;

            float time;
            if (m_Animation)
            {
                m_Animation.Play();
                time = m_Animation.clip.length;
            }
            else
            {
                time = duration;
            }
            StartCoroutine(DisableAtEndOfAnimation(time));
        }

        IEnumerator DisableAtEndOfAnimation(float time)
        {
            yield return new WaitForSeconds(time);

            gameObject.SetActive(false);
            staffLight.enabled = false;
        }
    } 
}
