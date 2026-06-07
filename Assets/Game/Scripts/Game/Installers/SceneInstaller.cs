using Game;
using UnityEngine.InputSystem;
using Zenject;

public class SceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container
            .Bind<InputActionAsset>()
            .FromScriptableObjectResource("Input/InputSystem_Actions")
            .AsSingle()
            .NonLazy();
        
        Container
            .Bind<CameraSettings>()
            .FromComponentInNewPrefabResource("Camera/CameraRig")
            .AsSingle()
            .NonLazy();
        
        Container
            .Bind<HealthUI>()
            .FromComponentInNewPrefabResource("UI/HealthCanvas")
            .AsSingle()
            .NonLazy();
        
        Container
            .BindInterfacesAndSelfTo<PlayerNewInput>()
            .AsSingle()
            .NonLazy();
        
        Container.
            Bind<PlayerController>()
            .FromComponentInHierarchy()
            .AsSingle()
            .NonLazy();
    }
}