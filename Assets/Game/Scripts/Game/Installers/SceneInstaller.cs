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
            .BindInterfacesAndSelfTo<PlayerNewInput>()
            .AsSingle()
            .NonLazy();
    }
}