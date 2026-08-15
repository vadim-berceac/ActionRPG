using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class SaveableCharacter : MonoBehaviour
{
    public class CharacterState
    {
        public string SaveKey { get; set; }
        public Dictionary<string, JObject> Components { get; set; }
    }
    
    [field: SerializeField] public string SaveKey { get; set; }

    private ISaveable[] _saveables;
    private readonly JsonSerializer _serializer = JsonSerializer.Create(SaveJsonSettings.Settings);

    private void Awake()
    {
        _saveables = GetComponentsInChildren<ISaveable>();
    }

    public CharacterState Capture()
    {
        var state = new CharacterState
        {
            SaveKey = SaveKey,
            Components = new Dictionary<string, JObject>()
        };

        foreach (var saveable in _saveables)
        {
            state.Components[saveable.SaveKey] = JObject.FromObject(saveable.CaptureState(), _serializer);
        }
        return state;
    }

    public void Restore(CharacterState state)
    {
        foreach (var saveable in _saveables)
        {
            if (!state.Components.TryGetValue(saveable.SaveKey, out var token))
            {
                continue;
            }

            var stateType = saveable.CaptureState().GetType();
            var typedState = token.ToObject(stateType, _serializer);
            saveable.RestoreState(typedState);
        }
    }
}
