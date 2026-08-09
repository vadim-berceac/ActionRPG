using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game
{
    public class PlayableGraphHandle
    {
        private class ClipLayer
        {
            public AnimationClipPlayable Playable;
            public bool Connected;
            public float Weight;
            public bool IsLooping;
            public double ClipLength;
        }
        
        private const int ControllerSlot = 0;
        private const int ClipSlotCount = 4; 
        private const float FullyFadedThreshold = 0.0001f;
        
        public bool IsValid => _graph.IsValid();

        private readonly AnimationLayerMixerPlayable _mixer;
        private readonly PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private bool _isOutputActive;

        private readonly ClipLayer[] _clipSlots = new ClipLayer[ClipSlotCount];
        private float _controllerWeight = 1f;

        private ClipLayer _activeLayer;
        private bool _isPlaying;

        private bool _isBlending;
        private float _blendDuration;
        private float _blendElapsed;
        private readonly Dictionary<int, float> _fadeStartWeights = new(); 
        private int _fadingInSlot;

        public PlayableGraphHandle(Animator animator)
        {
            _graph = PlayableGraph.Create("AnimationGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            _mixer = AnimationLayerMixerPlayable.Create(_graph, 1 + ClipSlotCount);

            var controllerPlayable = AnimatorControllerPlayable.Create(_graph, animator.runtimeAnimatorController);
            _mixer.ConnectInput(ControllerSlot, controllerPlayable, 0, 1f);
            _controllerWeight = 1f;

            _graph.Play();
        }

        public void PlayClip(Animator animator, AnimationClip clip, float blendLength)
        {
            if (!_graph.IsValid() || clip == null)
            {
                return;
            }

            EnsureOutput(animator);

            var slot = AcquireFreeSlot();
            var layer = ConnectClip(slot, clip);

            _activeLayer = layer;
            _isPlaying = true;

            StartBlend(slot, Mathf.Max(blendLength, 0f));
        }

        public void Stop(float blendLength = 0f)
        {
            if (!_isPlaying)
            {
                return;
            }

            StartBlend(-1, Mathf.Max(blendLength, 0f)); 

            _isPlaying = false;
            _activeLayer = null;
        }

        public void Evaluate(float deltaTime)
        {
            UpdateBlend(deltaTime);

            _graph.Evaluate(deltaTime);

            if (_isPlaying && _activeLayer != null && _activeLayer.IsLooping && _activeLayer.ClipLength > 0d)
            {
                var clipPlayable = _activeLayer.Playable;

                if (clipPlayable.IsValid())
                {
                    var time = clipPlayable.GetTime();

                    if (time >= _activeLayer.ClipLength)
                    {
                        clipPlayable.SetTime(time % _activeLayer.ClipLength);
                    }
                }
            }
        }

        public void Destroy()
        {
            if (_graph.IsValid())
            {
                for (var i = 0; i < ClipSlotCount; i++)
                {
                    DisconnectSlot(i);
                }

                if (_isOutputActive)
                {
                    _graph.DestroyOutput(_output);
                    _isOutputActive = false;
                }

                _graph.Destroy();
            }
        }

        private void EnsureOutput(Animator animator)
        {
            if (_isOutputActive)
            {
                return;
            }

            _output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            _output.SetSourcePlayable(_mixer);
            _isOutputActive = true;
        }

        private int AcquireFreeSlot()
        {
            var bestSlot = 0;
            var bestWeight = float.MaxValue;

            for (var i = 0; i < ClipSlotCount; i++)
            {
                var layer = _clipSlots[i];
                var weight = layer?.Weight ?? 0f;

                if (weight <= FullyFadedThreshold)
                {
                    return i;
                }

                if (weight < bestWeight)
                {
                    bestWeight = weight;
                    bestSlot = i;
                }
            }

            return bestSlot;
        }

        private ClipLayer ConnectClip(int slot, AnimationClip clip)
        {
            DisconnectSlot(slot);

            var clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            clipPlayable.SetDuration(clip.length);
            clipPlayable.SetTime(0);
            clipPlayable.Play();

            var mixerSlot = 1 + slot; 
            _mixer.ConnectInput(mixerSlot, clipPlayable, 0, 0f);

            var layer = new ClipLayer
            {
                Playable = clipPlayable,
                Connected = true,
                Weight = 0f,
                IsLooping = clip.isLooping,
                ClipLength = clip.length
            };

            _clipSlots[slot] = layer;

            return layer;
        }

        private void DisconnectSlot(int slot)
        {
            var layer = _clipSlots[slot];

            if (layer is not { Connected: true })
            {
                return;
            }

            var mixerSlot = 1 + slot;
            _mixer.DisconnectInput(mixerSlot);
            layer.Playable.Destroy();
            layer.Connected = false;
            _clipSlots[slot] = null;
        }

        private void StartBlend(int fadingInSlot, float duration)
        {
            _fadeStartWeights.Clear();

            if (_controllerWeight > FullyFadedThreshold)
            {
                _fadeStartWeights[ControllerSlot] = _controllerWeight;
            }

            for (var i = 0; i < ClipSlotCount; i++)
            {
                var layer = _clipSlots[i];

                if (layer is { Connected: true } && layer.Weight > FullyFadedThreshold && i != fadingInSlot)
                {
                    _fadeStartWeights[1 + i] = layer.Weight;
                }
            }

            _fadingInSlot = fadingInSlot;
            _blendDuration = duration;
            _blendElapsed = 0f;
            _isBlending = true;

            if (duration <= 0f)
            {
                UpdateBlend(0f, forceComplete: true);
            }
        }

        private void UpdateBlend(float deltaTime)
        {
            UpdateBlend(deltaTime, forceComplete: false);
        }

        private void UpdateBlend(float deltaTime, bool forceComplete)
        {
            if (!_isBlending)
            {
                return;
            }

            _blendElapsed += deltaTime;
            var t = forceComplete || _blendDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_blendElapsed / _blendDuration);

            var growingMixerSlot = _fadingInSlot < 0 ? ControllerSlot : 1 + _fadingInSlot;
            var growingWeight = t;

            _controllerWeight = growingMixerSlot == ControllerSlot ? growingWeight : 0f;
            _mixer.SetInputWeight(ControllerSlot, 0f); 

            foreach (var kvp in _fadeStartWeights)
            {
                var mixerSlot = kvp.Key;
                var startWeight = kvp.Value;
                var fadedWeight = startWeight * (1f - t);

                if (mixerSlot == ControllerSlot)
                {
                    _controllerWeight = fadedWeight;
                    _mixer.SetInputWeight(ControllerSlot, fadedWeight);
                }
                else
                {
                    var slot = mixerSlot - 1;
                    var layer = _clipSlots[slot];

                    if (layer != null)
                    {
                        layer.Weight = fadedWeight;
                        _mixer.SetInputWeight(mixerSlot, fadedWeight);

                        if (fadedWeight <= FullyFadedThreshold)
                        {
                            DisconnectSlot(slot);
                        }
                    }
                }
            }

            if (growingMixerSlot == ControllerSlot)
            {
                _controllerWeight = growingWeight;
                _mixer.SetInputWeight(ControllerSlot, growingWeight);
            }
            else
            {
                var slot = growingMixerSlot - 1;
                var layer = _clipSlots[slot];

                if (layer != null)
                {
                    layer.Weight = growingWeight;
                    _mixer.SetInputWeight(growingMixerSlot, growingWeight);
                }
            }

            if (t >= 1f)
            {
                _isBlending = false;
                _fadeStartWeights.Clear();
            }
        }
    }
}