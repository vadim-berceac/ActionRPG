using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class SaveService
{
    private const string SaveExtension = ".json";
    private readonly JsonSerializerSettings _settings = SaveJsonSettings.Settings;

    public async UniTask SaveAsync(SaveFile saveFile, string path, CancellationToken ct = default)
    {
        var json = JsonConvert.SerializeObject(saveFile, _settings);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, json, ct);
    }

    public async UniTask<SaveFile> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = await File.ReadAllTextAsync(path, ct);
        return JsonConvert.DeserializeObject<SaveFile>(json, _settings);
    }

    public bool SaveExists(string slotName) => File.Exists(GetPath(slotName));

    public void Delete(string slotName)
    {
        var path = GetPath(slotName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public IReadOnlyList<string> GetAllSlotNames()
    {
        var directory = Application.persistentDataPath;
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(directory, $"*{SaveExtension}")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();
    }

    public async UniTask<List<SaveSlotInfo>> GetAllSlotInfosAsync(CancellationToken ct = default)
    {
        var result = new List<SaveSlotInfo>();

        foreach (var slotName in GetAllSlotNames())
        {
            var file = await LoadAsync(GetPath(slotName), ct);
            result.Add(file.SlotInfo);
        }

        return result.OrderByDescending(i => i.SavedAt).ToList();
    }

    public string GetPath(string slotName) => Path.Combine(Application.persistentDataPath, $"{slotName}{SaveExtension}");
}

public static class SaveJsonSettings
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        Converters = { new Vector3Converter(), new QuaternionConverter() },
        Formatting = Formatting.Indented
    };
}