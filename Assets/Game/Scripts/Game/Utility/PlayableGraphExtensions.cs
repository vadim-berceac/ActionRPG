using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Game
{
    public class PlayableGraphHandle
    {
        public PlayableGraph Graph { get; private set; }
        public bool IsValid => Graph.IsValid();

        private AnimationLayerMixerPlayable _mixer;
        private AnimationClipPlayable _clipPlayable;
        private AnimatorControllerPlayable _controllerPlayable;
        private AnimationPlayableOutput _output;
        private bool _isOutputActive;
        private bool _isPlaying;

        private PlayableGraphHandle()
        {
        }

        public static PlayableGraphHandle Create(Animator animator)
        {
            var handle = new PlayableGraphHandle();

            handle.Graph = PlayableGraph.Create("AnimationGraph");
            handle.Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            handle._mixer = AnimationLayerMixerPlayable.Create(handle.Graph, 2);

            handle._controllerPlayable = AnimatorControllerPlayable.Create(handle.Graph, animator.runtimeAnimatorController);
            handle._mixer.ConnectInput(0, handle._controllerPlayable, 0, 1f);

            handle.Graph.Play();

            return handle;
        }

        public void PlayClip(Animator animator, AnimationClip clip)
        {
            Stop();

            _output = AnimationPlayableOutput.Create(Graph, "Animation", animator);
            _output.SetSourcePlayable(_mixer);
            _isOutputActive = true;

            _clipPlayable = AnimationClipPlayable.Create(Graph, clip);
            _clipPlayable.SetDuration(clip.length);
            _clipPlayable.SetTime(0);
            _clipPlayable.Play();
            _mixer.ConnectInput(1, _clipPlayable, 0, 1f);

            _mixer.SetInputWeight(0, 0f);
            _mixer.SetInputWeight(1, 1f);

            _isPlaying = true;
        }

        public void Stop()
        {
            if (!_isPlaying) return;

            _mixer.SetInputWeight(0, 1f);
            _mixer.SetInputWeight(1, 0f);

            if (_clipPlayable.IsValid())
            {
                _mixer.DisconnectInput(1);
                _clipPlayable.Destroy();
            }

            if (_isOutputActive)
            {
                _output.SetSourcePlayable(Playable.Null);
                Graph.DestroyOutput(_output);
                _isOutputActive = false;
            }

            _isPlaying = false;
        }

        public void Evaluate(float deltaTime)
        {
            Graph.Evaluate(deltaTime);
        }

        public void Destroy()
        {
            Stop();

            if (Graph.IsValid())
            {
                Graph.Destroy();
            }
        }
    }
}