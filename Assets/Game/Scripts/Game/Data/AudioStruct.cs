using UnityEngine;

[System.Serializable]
public struct AudioStruct
{
    [field: SerializeField] public AudioClip AudioClip  { get; set; }
    [field: SerializeField] public float Volume { get; set; }
}
