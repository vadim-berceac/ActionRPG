using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class HumanoidIK
    {
        private readonly Animator _animator;
        private readonly Dictionary<AvatarIKGoal, (Transform target, Transform hint, float weight)> _ikGoals = new();
        private Transform _lookAtTarget;
        private float _lookAtWeight;

        public HumanoidIK(Animator animator)
        {
            _animator = animator;
        }

        public void OnAnimatorIK()
        {
            foreach (var (goal, (target, hint, weight)) in _ikGoals)
            {
                _animator.SetIKPositionWeight(goal, weight);
                _animator.SetIKRotationWeight(goal, weight);
                _animator.SetIKPosition(goal, target.position);

                var handBone = _animator.GetBoneTransform(ToBone(goal));
                var toTarget = (target.position - handBone.position).normalized;

                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    var lookRotation = Quaternion.LookRotation(toTarget, target.up);
                    _animator.SetIKRotation(goal, lookRotation);
                }

                var ikHint = ToHint(goal);
                var hintPos = hint ? hint.position : GetAutoHintPosition(goal);
                if (hintPos == default) continue;

                _animator.SetIKHintPositionWeight(ikHint, weight);
                _animator.SetIKHintPosition(ikHint, hintPos);
            }

            if (_lookAtTarget)
            {
                _animator.SetLookAtWeight(
                    weight: _lookAtWeight,
                    bodyWeight: 0.3f,
                    headWeight: 0.6f,
                    eyesWeight: 0.9f,
                    clampWeight: 0.5f);

                _animator.SetLookAtPosition(_lookAtTarget.position);
            }
        }
        
        private static HumanBodyBones ToBone(AvatarIKGoal goal) => goal switch
        {
            AvatarIKGoal.LeftHand => HumanBodyBones.LeftHand,
            AvatarIKGoal.RightHand => HumanBodyBones.RightHand,
            AvatarIKGoal.LeftFoot => HumanBodyBones.LeftFoot,
            AvatarIKGoal.RightFoot => HumanBodyBones.RightFoot,
            _ => HumanBodyBones.Hips
        };

        public void SetLookAt(Transform target, float weight)
        {
            _lookAtTarget = target;
            _lookAtWeight = weight;
        }

        public void SetIKGoal(AvatarIKGoal goal, Transform target, Transform hint, float weight)
        {
            if (!target || weight <= 0f)
            {
                _ikGoals.Remove(goal);
                return;
            }

            _ikGoals[goal] = (target, hint, weight);
        }

        private Vector3 GetAutoHintPosition(AvatarIKGoal goal)
        {
            var bone = goal switch
            {
                AvatarIKGoal.LeftHand => HumanBodyBones.LeftLowerArm,
                AvatarIKGoal.RightHand => HumanBodyBones.RightLowerArm,
                AvatarIKGoal.LeftFoot => HumanBodyBones.LeftLowerLeg,
                AvatarIKGoal.RightFoot => HumanBodyBones.RightLowerLeg,
                _ => HumanBodyBones.Hips
            };

            var t = _animator.GetBoneTransform(bone);
            return t ? t.position : default;
        }

        private static AvatarIKHint ToHint(AvatarIKGoal goal) => goal switch
        {
            AvatarIKGoal.LeftHand => AvatarIKHint.LeftElbow,
            AvatarIKGoal.RightHand => AvatarIKHint.RightElbow,
            AvatarIKGoal.LeftFoot => AvatarIKHint.LeftKnee,
            AvatarIKGoal.RightFoot => AvatarIKHint.RightKnee,
            _ => default
        };
    }
}