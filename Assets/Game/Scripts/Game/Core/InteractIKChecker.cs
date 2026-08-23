using Game;
using LegIK;
using UnityEngine;

public class InteractIKChecker : MonoBehaviour
{
    [SerializeField] private InteractAnimation trigger;
    [SerializeField] [Range(0, 1)] private float iKGlobalWeight;
    
    private LegIKController  _legIKController;
    private float _weightStartValue;

    private void OnEnable()
    {
       trigger.onInteractEnter.AddListener(OnInteractEnter);
       trigger.onInteractExit.AddListener(OnInteractExit);
    }

    private void OnDisable()
    {
        trigger.onInteractEnter.RemoveListener(OnInteractEnter);
        trigger.onInteractExit.RemoveListener(OnInteractExit);
    }

    private void OnInteractEnter(HumanoidController controller)
    {
        _legIKController = null;
        
        controller.TryGetComponent(out _legIKController);

        if (!_legIKController)
        {
            return;
        }
        
        _weightStartValue = _legIKController.GlobalWeight;
        _legIKController.SetGlobalWeight(iKGlobalWeight);
    }

    private void OnInteractExit(HumanoidController controller)
    {
        if (!_legIKController)
        {
            return;
        }
        
        _legIKController.SetGlobalWeight(_weightStartValue);
        
        _legIKController = null;
    }
}
