using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game
{
    public class WeaponInstance : MonoBehaviour
    {
        [System.Serializable]
        public class AttackPoint
        {
            public float radius;
            public Vector3 offset;
            public Transform attackRoot;

#if UNITY_EDITOR
            //editor only as it's only used in editor to display the path of the attack that is used by the raycast
            [NonSerialized] public List<Vector3> previousPositions = new List<Vector3>();
#endif

        }

        public GameObject view;
        public GameObject trail;
        public AttackPoint[] attackPoints = new AttackPoint[0];

        [Header("Audio")] public RandomAudioPlayer hitAudio;
        public RandomAudioPlayer attackAudio;

        public bool throwingHit
        {
            get { return m_IsThrowingHit; }
            set { m_IsThrowingHit = value; }
        }

        protected GameObject m_Owner;
        protected LayerMask m_targetLayers;
        protected WeaponData m_WeaponData;
        protected AnimatorStateCache m_AnimatorStateCache;
        protected CurveConstants m_CurveConstants;

        protected Vector3[] m_PreviousPos = null;
        protected Vector3 m_Direction;

        protected bool m_IsThrowingHit = false;
        protected bool m_InAttack = false;
        protected float m_KnockbackForce;

        const int PARTICLE_COUNT = 10;
        protected ParticleSystem[] m_ParticlesPool = new ParticleSystem[PARTICLE_COUNT];
        protected int m_CurrentParticle = 0;

        protected RaycastHit[] s_RaycastHitCache = new RaycastHit[32];
        protected Collider[] s_ColliderCache = new Collider[32];
        protected GameObject[] staticParts;

        // Коллайдеры, уже получившие урон в рамках текущего BeginAttack()..EndAttack().
        // Без этого свип SphereCast за несколько FixedUpdate-кадров бьёт (и спавнит партикл)
        // по одной и той же цели многократно за один удар.
        private readonly HashSet<Collider> m_HitTargetsThisAttack = new HashSet<Collider>();
        

        //whoever own the weapon is responsible for calling that. Allow to avoid "self harm"
        public void Initialize(GameObject owner, LayerMask layers, AnimatorStateCache animatorStateCache = null, CurveConstants curveConstants = null)
        {
            m_Owner = owner;
            m_targetLayers = layers;
            m_AnimatorStateCache = animatorStateCache;
            m_CurveConstants = curveConstants;
        }

        public void SetKnockbackForce(float force)
        {
            m_KnockbackForce = force;
        }

        public void SetViewParent(PropBones propBones, PropBoneSettings settings)
        {
            settings.SetPropBone(view.transform, propBones);
        }

        public void SetStaticParts(GameObject[] parts)
        {
            staticParts = parts;
        }

        public WeaponData WeaponData => m_WeaponData;

        public void SetWeaponData(WeaponData weaponData)
        {
            m_WeaponData = weaponData;
        }

        public void BeginAttack(bool thowingAttack)
        {
            if (attackAudio != null)
            {
                attackAudio.PlayRandomClip();
            }
            if (trail != null)
            {
                trail.SetActive(true);
            }
            throwingHit = thowingAttack;

            m_InAttack = true;
            m_HitTargetsThisAttack.Clear();

            m_PreviousPos = new Vector3[attackPoints.Length];

            for (int i = 0; i < attackPoints.Length; ++i)
            {
                Vector3 worldPos = attackPoints[i].attackRoot.position +
                                   attackPoints[i].attackRoot.TransformVector(attackPoints[i].offset);
                m_PreviousPos[i] = worldPos;

#if UNITY_EDITOR
                attackPoints[i].previousPositions.Clear();
                attackPoints[i].previousPositions.Add(m_PreviousPos[i]);
#endif
            }
        }

        public void EndAttack()
        {
            m_InAttack = false;
            m_HitTargetsThisAttack.Clear();

            if (trail != null)
            {
                trail.SetActive(false);
            }

#if UNITY_EDITOR
            for (int i = 0; i < attackPoints.Length; ++i)
            {
                attackPoints[i].previousPositions.Clear();
            }
#endif
        }

        private void FixedUpdate()
        {
            if (m_InAttack)
            {
                for (int i = 0; i < attackPoints.Length; ++i)
                {
                    AttackPoint pts = attackPoints[i];

                    Vector3 worldPos = pts.attackRoot.position + pts.attackRoot.TransformVector(pts.offset);
                    Vector3 attackVector = worldPos - m_PreviousPos[i];

                    if (attackVector.magnitude < 0.001f)
                    {
                        // A zero vector for the sphere cast don't yield any result, even if a collider overlap the "sphere" created by radius. 
                        // so we set a very tiny microscopic forward cast to be sure it will catch anything overlaping that "stationary" sphere cast
                        attackVector = Vector3.forward * 0.0001f;
                    }


                    Ray r = new Ray(worldPos, attackVector.normalized);

                    int contacts = Physics.SphereCastNonAlloc(r, pts.radius, s_RaycastHitCache, attackVector.magnitude,
                        ~0,
                        QueryTriggerInteraction.Collide);

                    for (int k = 0; k < contacts; ++k)
                    {
                        Collider col = s_RaycastHitCache[k].collider;

                        if (col != null)
                            CheckDamage(col, pts, s_RaycastHitCache[k].point);
                    }

                    m_PreviousPos[i] = worldPos;

#if UNITY_EDITOR
                    pts.previousPositions.Add(m_PreviousPos[i]);
#endif
                }
            }
        }

        private bool CheckDamage(Collider other, AttackPoint pts, Vector3 hitPoint)
        {
            var d = other.GetComponent<Damageable>();
            if (!d)
            {
                return false;
            }

            if (d.gameObject == m_Owner)
                return true; //ignore self harm, but do not end the attack (we don't "bounce" off ourselves)

            if ((m_targetLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                //hit an object that is not in our layer, this end the attack. we "bounce" off it
                return false;
            }

            // Эта цель уже получила урон в рамках текущего удара — не наносим повторно,
            // не спавним партикл и не проигрываем звук снова, но и не "отбиваем" атаку от неё.
            if (!m_HitTargetsThisAttack.Add(other))
            {
                return true;
            }

            if (hitAudio)
            {
                hitAudio.PlayRandomClip ();
            }

            if (m_WeaponData && m_WeaponData.hitParticlePrefab)
            {
                var hitEffect = Instantiate(m_WeaponData.hitParticlePrefab, hitPoint, Quaternion.identity);

                hitEffect.Play();

                Destroy(hitEffect.gameObject, hitEffect.main.duration);
            }

            Damageable.DamageMessage data;

            data.amount = m_WeaponData.Damage;
            data.damager = this;
            data.direction = m_Direction.normalized;
            data.damageSource = m_Owner.transform.position;
            data.throwing = m_IsThrowingHit;
            data.stopCamera = false;
            data.knockbackForce = m_KnockbackForce;

            d.ApplyDamage(data);
            
            m_AnimatorStateCache?.SetAnimationSpeedCurve(Constants.WeaponStuckTime, m_CurveConstants.HitStopCurve).Forget();

            return true;
        }
        
        public void DestroyInstance()
        {
            if (staticParts != null && staticParts.Length > 0)
            {
                foreach (var part in staticParts)
                {
                    Destroy(part);
                }
            }
            Destroy(view.gameObject);
            Destroy(gameObject);
        }

#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < attackPoints.Length; ++i)
            {
                AttackPoint pts = attackPoints[i];

                if (pts.attackRoot != null)
                {
                    Vector3 worldPos = pts.attackRoot.TransformVector(pts.offset);
                    Gizmos.color = new Color(1.0f, 1.0f, 1.0f, 0.4f);
                    Gizmos.DrawSphere(pts.attackRoot.position + worldPos, pts.radius);
                }

                if (pts.previousPositions.Count > 1)
                {
                    UnityEditor.Handles.DrawAAPolyLine(10, pts.previousPositions.ToArray());
                }
            }
        }

#endif
    }
}