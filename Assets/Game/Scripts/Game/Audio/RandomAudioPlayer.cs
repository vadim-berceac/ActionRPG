using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Game
{
    public class RandomAudioPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audiosource;
        
        [Serializable]
        public class MaterialAudioOverride
        {
            public Material[] materials;
            public SoundBank[] banks;
        }

        [Serializable]
        public class SoundBank
        {
            public string name;
            public AudioClip[] clips;
        }

        public bool randomizePitch = true;
        public float pitchRandomRange = 0.2f;
        public float playDelay = 0;
        public SoundBank defaultBank = new SoundBank();
        public MaterialAudioOverride[] overrides;

        [HideInInspector]
        public bool playing;
        [HideInInspector]
        public bool canPlay;
        
        protected Dictionary<Material, SoundBank[]> m_Lookup = new Dictionary<Material, SoundBank[]>();

        public AudioSource audioSource { get { return audiosource; } }

        public AudioClip clip { get; private set; }

        public bool IsPlaying => audiosource != null && audiosource.isPlaying;

        void Awake()
        {
            if (!audiosource)
            {
                audiosource = GetComponent<AudioSource>();
            }
            for (int i = 0; i < overrides.Length; i++)
            {
                foreach (var material in overrides[i].materials)
                    m_Lookup[material] = overrides[i].banks;
            }
        }

        public AudioClip PlayRandomClip(Material overrideMaterial, int bankId = 0)
        {
            if (!overrideMaterial) return null;
            return InternalPlayRandomClip(overrideMaterial, bankId);
        }

        public void PlayRandomClip()
        {
            clip = InternalPlayRandomClip(null, bankId: 0);
        }

        public void PlayRandomClipOneShot(Material overrideMaterial = null, int bankId = 0)
        {
            if (IsPlaying) return;
            clip = InternalPlayRandomClip(overrideMaterial, bankId);
        }

        private AudioClip InternalPlayRandomClip(Material overrideMaterial, int bankId)
        {
            var bank = defaultBank;
            if (overrideMaterial)
            {
                if (m_Lookup.TryGetValue(overrideMaterial, out var currentBanks))
                {
                    if (bankId < currentBanks.Length)
                    {
                        bank = currentBanks[bankId];
                    }
                }
            }
            if (bank.clips == null || bank.clips.Length == 0)
                return null;
            var clip = bank.clips[Random.Range(0, bank.clips.Length)];

            if (!clip) return null;

            audiosource.pitch = randomizePitch ? Random.Range(1.0f - pitchRandomRange, 1.0f + pitchRandomRange) : 1.0f;
            audiosource.clip = clip;
            audiosource.PlayDelayed(playDelay);

            return clip;
        }

    }
}
