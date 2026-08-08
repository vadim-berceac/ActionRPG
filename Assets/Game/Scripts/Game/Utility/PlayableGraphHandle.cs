using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game
{
    public class PlayableGraphHandle
    {
        private const int ControllerSlot = 0;
        private const int ClipSlotA = 1;
        private const int ClipSlotB = 2;

        public PlayableGraph Graph { get; private set; }
        public bool IsValid => Graph.IsValid();

        private AnimationLayerMixerPlayable _mixer;
        private AnimatorControllerPlayable _controllerPlayable;
        private AnimationPlayableOutput _output;
        private bool _isOutputActive;

        private AnimationClipPlayable _clipPlayableA;
        private AnimationClipPlayable _clipPlayableB;
        private bool _clipAValid;
        private bool _clipBValid;

        private int _activeSlot; 
        private bool _isPlaying;
        private bool _isLoopingClip;
        private double _clipLength;

        private bool _isBlending;
        private float _blendDuration;
        private float _blendElapsed;
        private int _blendFromSlot;
        private int _blendToSlot;

        private PlayableGraphHandle()
        {
        }

        public static PlayableGraphHandle Create(Animator animator)
        {
            var handle = new PlayableGraphHandle();

            handle.Graph = PlayableGraph.Create("AnimationGraph");
            handle.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            handle._mixer = AnimationLayerMixerPlayable.Create(handle.Graph, 3);

            handle._controllerPlayable = AnimatorControllerPlayable.Create(handle.Graph, animator.runtimeAnimatorController);
            handle._mixer.ConnectInput(ControllerSlot, handle._controllerPlayable, 0, 1f);

            handle.Graph.Play();

            return handle;
        }

        public void PlayClip(Animator animator, AnimationClip clip, float blendLength)
        {
            if (!Graph.IsValid() || clip == null)
            {
                return;
            }

            EnsureOutput(animator);

            var targetSlot = _activeSlot == ClipSlotA ? ClipSlotB : ClipSlotA;
            var fromSlot = _isPlaying ? _activeSlot : ControllerSlot;

            ConnectClip(targetSlot, clip);

            _activeSlot = targetSlot;
            _isPlaying = true;
            _isLoopingClip = clip.isLooping;
            _clipLength = clip.length;

            StartBlend(fromSlot, targetSlot, Mathf.Max(blendLength, 0f));
        }

        public void Stop()
        {
            if (!_isPlaying)
            {
                return;
            }

            _isBlending = false;

            _mixer.SetInputWeight(ControllerSlot, 1f);
            _mixer.SetInputWeight(ClipSlotA, 0f);
            _mixer.SetInputWeight(ClipSlotB, 0f);

            DisconnectSlot(ClipSlotA);
            DisconnectSlot(ClipSlotB);

            if (_isOutputActive)
            {
                _output.SetSourcePlayable(Playable.Null);
                Graph.DestroyOutput(_output);
                _isOutputActive = false;
            }

            _isPlaying = false;
            _isLoopingClip = false;
            _activeSlot = 0;
        }

        public void Evaluate(float deltaTime)
        {
            UpdateBlend(deltaTime);

            Graph.Evaluate(deltaTime);

            if (_isPlaying && _isLoopingClip && _clipLength > 0d)
            {
                var activeClip = GetActiveClipPlayable();

                if (activeClip.IsValid())
                {
                    var time = activeClip.GetTime();

                    if (time >= _clipLength)
                    {
                        activeClip.SetTime(time % _clipLength);
                    }
                }
            }
        }

        public void Destroy()
        {
            Stop();

            if (Graph.IsValid())
            {
                Graph.Destroy();
            }
        }

        private void EnsureOutput(Animator animator)
        {
            if (_isOutputActive)
            {
                return;
            }

            _output = AnimationPlayableOutput.Create(Graph, "Animation", animator);
            _output.SetSourcePlayable(_mixer);
            _isOutputActive = true;
        }

        private void ConnectClip(int slot, AnimationClip clip)
        {
            DisconnectSlot(slot);

            var clipPlayable = AnimationClipPlayable.Create(Graph, clip);
            clipPlayable.SetDuration(clip.length);
            clipPlayable.SetTime(0);
            clipPlayable.Play();

            _mixer.ConnectInput(slot, clipPlayable, 0, 0f);

            if (slot == ClipSlotA)
            {
                _clipPlayableA = clipPlayable;
                _clipAValid = true;
            }
            else
            {
                _clipPlayableB = clipPlayable;
                _clipBValid = true;
            }
        }

        private void DisconnectSlot(int slot)
        {
            if (slot == ClipSlotA && _clipAValid)
            {
                _mixer.DisconnectInput(ClipSlotA);
                _clipPlayableA.Destroy();
                _clipAValid = false;
            }
            else if (slot == ClipSlotB && _clipBValid)
            {
                _mixer.DisconnectInput(ClipSlotB);
                _clipPlayableB.Destroy();
                _clipBValid = false;
            }
        }

        private AnimationClipPlayable GetActiveClipPlayable()
        {
            if (_activeSlot == ClipSlotA)
            {
                return _clipPlayableA;
            }

            if (_activeSlot == ClipSlotB)
            {
                return _clipPlayableB;
            }

            return default;
        }

        private void StartBlend(int fromSlot, int toSlot, float duration)
        {
            if (_isBlending)
            {
                _mixer.SetInputWeight(_blendToSlot, 1f);
                _mixer.SetInputWeight(_blendFromSlot, 0f);
            }

            _blendFromSlot = fromSlot;
            _blendToSlot = toSlot;
            _blendDuration = duration;
            _blendElapsed = 0f;
            _isBlending = true;

            if (duration <= 0f)
            {
                _mixer.SetInputWeight(toSlot, 1f);
                _mixer.SetInputWeight(fromSlot, 0f);
                _isBlending = false;
            }
        }

        private void UpdateBlend(float deltaTime)
        {
            if (!_isBlending)
            {
                return;
            }

            _blendElapsed += deltaTime;
            var t = _blendDuration > 0f ? Mathf.Clamp01(_blendElapsed / _blendDuration) : 1f;

            _mixer.SetInputWeight(_blendToSlot, t);
            _mixer.SetInputWeight(_blendFromSlot, 1f - t);

            if (t >= 1f)
            {
                _isBlending = false;
            }
        }
    }
}