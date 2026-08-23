using System.Collections.Generic;
using Game;
using LegIK;
using UnityEngine;

public class InteractIKChecker : MonoBehaviour
{
    [SerializeField] private InteractAnimation trigger;
    [SerializeField] [Range(0, 1)] private float iKGlobalWeight;
    
    private readonly Dictionary<LegIKController, float> _startWeights = new();

    private void OnEnable()
    {
       trigger.onInteractEnter.AddListener(OnInteractEnter);
       trigger.onInteractExit.AddListener(OnInteractExit);
    }

    private void OnDisable()
    {
        trigger.onInteractEnter.RemoveListener(OnInteractEnter);
        trigger.onInteractExit.RemoveListener(OnInteractExit);

        foreach (var kvp in _startWeights)
        {
            kvp.Key.SetGlobalWeight(kvp.Value);
        }
        
        _startWeights.Clear();
    }

    private void OnInteractEnter(HumanoidController controller)
    {
        if (!controller.LegIK || _startWeights.ContainsKey(controller.LegIK))
        {
            return;
        }
       
        _startWeights.TryAdd(controller.LegIK, controller.LegIK.GlobalWeight);
        controller.LegIK.SetGlobalWeight(iKGlobalWeight);
    }

    private void OnInteractExit(HumanoidController controller)
    {
        if (!controller.LegIK || !_startWeights.TryGetValue(controller.LegIK, out var weight))
        {
            return;
        }
        
        controller.LegIK.SetGlobalWeight(weight);
        _startWeights.Remove(controller.LegIK);
    }
}
