using System;
using System.Collections.Generic;

public class SaveFile
{
    public int Version { get; set; }
    public SaveSlotInfo SlotInfo { get; set; }
    public List<SaveableCharacter.CharacterState> Characters { get; set; }
}

public class SaveSlotInfo
{
    public string SlotName { get; set; }
    public DateTime SavedAt { get; set; }
    public string DisplayName { get; set; }
}
