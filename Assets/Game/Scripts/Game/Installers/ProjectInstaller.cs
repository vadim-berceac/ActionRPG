using UnityEngine.InputSystem;
using Zenject;

public class ProjectInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container
            .Bind<InputActionAsset>()
            .FromScriptableObjectResource("Input/InputSystem_Actions")
            .AsSingle()
            .NonLazy();
           
        Container
            .Bind<IItemDatabase>()
            .To<ItemDatabase>()
            .FromScriptableObjectResource("Items/ItemDatabase")
            .AsSingle()
            .NonLazy();
           
        Container
            .Bind<SaveService>()
            .AsSingle()
            .NonLazy();
        
        Container
            .Bind<SaveGameController>()
            .AsSingle()
            .NonLazy();
    }
}