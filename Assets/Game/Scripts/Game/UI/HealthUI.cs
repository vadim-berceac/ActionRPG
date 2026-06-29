using UnityEngine;
using Zenject;

namespace Game
{
    public class HealthUI : MonoBehaviour
    {
        private Damageable _representedDamageable;
        public GameObject healthIconPrefab;

        protected Animator[] m_HealthIconAnimators;

        protected readonly int m_HashActivePara = Animator.StringToHash("Active");
        protected readonly int m_HashInactiveState = Animator.StringToHash("Inactive");
        protected const float k_HeartIconAnchorWidth = 0.041f;

        [Inject]
        private void Construct(PlayerTag playerTag)
        {
            _representedDamageable = playerTag.PlayerHealth;
        }

        private void Start()
        {
            m_HealthIconAnimators = new Animator[_representedDamageable.maxHitPoints];

            for (int i = 0; i < _representedDamageable.maxHitPoints; i++)
            {
                GameObject healthIcon = Instantiate(healthIconPrefab);
                healthIcon.transform.SetParent(transform);
                RectTransform healthIconRect = healthIcon.transform as RectTransform;
                healthIconRect.anchoredPosition = Vector2.zero;
                healthIconRect.sizeDelta = Vector2.zero;
                healthIconRect.anchorMin += new Vector2(k_HeartIconAnchorWidth, 0f) * i;
                healthIconRect.anchorMax += new Vector2(k_HeartIconAnchorWidth, 0f) * i;
                m_HealthIconAnimators[i] = healthIcon.GetComponent<Animator>();

                if (_representedDamageable.currentHitPoints < i + 1)
                {
                    m_HealthIconAnimators[i].Play(m_HashInactiveState);
                    m_HealthIconAnimators[i].SetBool(m_HashActivePara, false);
                }
            }

            _representedDamageable.OnReceiveDamage.AddListener(ChangeHitPointUI);
            _representedDamageable.OnResetDamage.AddListener(ChangeHitPointUI);
            _representedDamageable.OnDeath.AddListener(ChangeHitPointUI);
        }

        public void ChangeHitPointUI()
        {
            if (m_HealthIconAnimators == null)
                return;

            for (int i = 0; i < m_HealthIconAnimators.Length; i++)
            {
                m_HealthIconAnimators[i].SetBool(m_HashActivePara, _representedDamageable.currentHitPoints >= i + 1);
            }
        }

        private void OnDestroy()
        {
            _representedDamageable.OnReceiveDamage.RemoveListener(ChangeHitPointUI);
            _representedDamageable.OnResetDamage.RemoveListener(ChangeHitPointUI);
            _representedDamageable.OnDeath.RemoveListener(ChangeHitPointUI);
        }
    } 
}
