using Game;
using Zenject;

public class SceneInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
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