using System.Collections;
using UnityEngine;

namespace Game
{
    public class HealthUI : MonoBehaviour
    {
        public Damageable representedDamageable;
        public GameObject healthIconPrefab;

        protected Animator[] m_HealthIconAnimators;

        protected readonly int m_HashActivePara = Animator.StringToHash("Active");
        protected readonly int m_HashInactiveState = Animator.StringToHash("Inactive");
        protected const float k_HeartIconAnchorWidth = 0.041f;

        IEnumerator Start()
        {
            if (representedDamageable == null)
                yield break;

            yield return null;

            m_HealthIconAnimators = new Animator[representedDamageable.maxHitPoints];

            for (int i = 0; i < representedDamageable.maxHitPoints; i++)
            {
                GameObject healthIcon = Instantiate(healthIconPrefab);
                healthIcon.transform.SetParent(transform);
                RectTransform healthIconRect = healthIcon.transform as RectTransform;
                healthIconRect.anchoredPosition = Vector2.zero;
                healthIconRect.sizeDelta = Vector2.zero;
                healthIconRect.anchorMin += new Vector2(k_HeartIconAnchorWidth, 0f) * i;
                healthIconRect.anchorMax += new Vector2(k_HeartIconAnchorWidth, 0f) * i;
                m_HealthIconAnimators[i] = healthIcon.GetComponent<Animator>();

                if (representedDamageable.currentHitPoints < i + 1)
                {
                    m_HealthIconAnimators[i].Play(m_HashInactiveState);
                    m_HealthIconAnimators[i].SetBool(m_HashActivePara, false);
                }
            }

            representedDamageable.OnReceiveDamage.AddListener(ChangeHitPointUI);
            representedDamageable.OnResetDamage.AddListener(ChangeHitPointUI);
            representedDamageable.OnDeath.AddListener(ChangeHitPointUI);
        }

        public void ChangeHitPointUI()
        {
            if (m_HealthIconAnimators == null)
                return;

            for (int i = 0; i < m_HealthIconAnimators.Length; i++)
            {
                m_HealthIconAnimators[i].SetBool(m_HashActivePara, representedDamageable.currentHitPoints >= i + 1);
            }
        }

        private void OnDestroy()
        {
            representedDamageable.OnReceiveDamage.RemoveListener(ChangeHitPointUI);
            representedDamageable.OnResetDamage.RemoveListener(ChangeHitPointUI);
            representedDamageable.OnDeath.RemoveListener(ChangeHitPointUI);
        }
    } 
}
