using System.Collections.Generic;

public class PickupPersistenceService
{
    private readonly HashSet<string> _pickedKeys = new();

    public bool IsPicked(string saveKey)
    {
        if (string.IsNullOrEmpty(saveKey)) return false;
        return _pickedKeys.Contains(saveKey);
    }

    public void MarkPicked(string saveKey)
    {
        if (string.IsNullOrEmpty(saveKey)) return;
        _pickedKeys.Add(saveKey);
    }

    public IReadOnlyCollection<string> GetPickedKeys() => _pickedKeys;

    public void RestorePickedKeys(IEnumerable<string> keys)
    {
        _pickedKeys.Clear();
        if (keys == null) return;

        foreach (var key in keys)
        {
            _pickedKeys.Add(key);
        }
    }
}