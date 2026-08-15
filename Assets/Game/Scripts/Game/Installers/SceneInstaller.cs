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
            .Bind<DialogueCanvasController>()
            .FromComponentInNewPrefabResource("UI/DialogueCanvas")
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
        
        Container
            .BindInterfacesAndSelfTo<PlayerNewInput>()
            .AsSingle()
            .NonLazy();
        
        Container
            .BindInterfacesAndSelfTo<PlayerInputHandlerService>()
            .AsSingle()
            .NonLazy();
        
        Container
            .BindInterfacesAndSelfTo<PickupSelectionService>()
            .AsSingle()
            .NonLazy();
        
        Container
            .Bind<PlayerTag>()
            .FromComponentInHierarchy()
            .AsSingle()
            .NonLazy();
    }
}