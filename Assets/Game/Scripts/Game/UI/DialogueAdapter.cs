using Game;
using UnityEngine;
using Zenject;

public class DialogueAdapter : MonoBehaviour
{
    private DialogueCanvasController _dialogueCanvasController;

    [Inject]
    private void Construct(DialogueCanvasController dialogueCanvasController)
    {
        _dialogueCanvasController = dialogueCanvasController;
    }

    public void ActivateCanvasWithTranslatedText(string phraseKey)
    {
        if(!_dialogueCanvasController) return;
        _dialogueCanvasController.ActivateCanvasWithTranslatedText(phraseKey);
    }

    public void DeactivateCanvasWithDelay(float delay)
    {
        if(!_dialogueCanvasController) return;
        _dialogueCanvasController.DeactivateCanvasWithDelay(delay);
    }
}
