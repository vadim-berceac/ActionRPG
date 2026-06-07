
public class PickupSelectionService
{
    private PickupItem _selected;

    public void Select(PickupItem item)
    {
        if (_selected == item) return;
        _selected = item;
    }

    public void Deselect(PickupItem item)
    {
        if (_selected != item) return;
        _selected = null;
    }

    public bool IsSelected(PickupItem item) => _selected == item;
}