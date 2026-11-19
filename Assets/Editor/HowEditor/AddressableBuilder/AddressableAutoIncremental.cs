#define EDITOR
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public class AddressableAutoIncremental : AssetPostprocessor
{
    private static readonly string RootFolder = "Assets/GameRes";

    static void OnPostprocessAllAssets(
        string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogWarning("⚠️ Addressable Settings 未找到，请先打开 Addressables 窗口创建 Settings.");
            return;
        }

        bool hasRelevantChange =
            importedAssets.Any(p => p.StartsWith(RootFolder)) ||
            deletedAssets.Any(p => p.StartsWith(RootFolder)) ||
            movedAssets.Any(p => p.StartsWith(RootFolder)) ||
            movedFromAssetPaths.Any(p => p.StartsWith(RootFolder));

        if (!hasRelevantChange) return;

        // ---------------- 新增/修改 ----------------
        foreach (var assetPath in importedAssets)
        {
            if (!assetPath.StartsWith(RootFolder)) continue;
            if (AssetDatabase.IsValidFolder(assetPath)) continue;
            if (assetPath.EndsWith(".meta") || assetPath.EndsWith(".cs") || assetPath.EndsWith(".dll")) continue;

            string groupName = GetGroupName(assetPath);
            var group = string.IsNullOrEmpty(groupName) ? settings.DefaultGroup : GetOrCreateGroup(settings, groupName);

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
                // 只给新加进来的资源起地址：已有的可能被手动 / 菜单改过（如背景音乐固定叫 bgm_main），
                // 改个导入设置触发重新导入时不能冲掉
                entry.address = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                // [ZombieGame] 新资源按文件夹名打标签（只限 LabelFolders，比如 Audio），GameInit 按标签预加载
                if (System.Array.IndexOf(LabelFolders, groupName) >= 0)
                {
                    settings.AddLabel(groupName);
                    entry.SetLabel(groupName, true, true, false);
                }
            }
            else if (entry.parentGroup != group)
            {
                settings.MoveEntry(entry, group, false);
            }
        }

        // ---------------- 删除 ----------------
        foreach (var assetPath in deletedAssets)
        {
            if (!assetPath.StartsWith(RootFolder)) continue;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) continue;

            var entry = settings.FindAssetEntry(guid);
            if (entry != null)
            {
                settings.RemoveAssetEntry(guid);
            }
        }

        // ---------------- 移动/重命名 ----------------
        for (int i = 0; i < movedAssets.Length; i++)
        {
            string newPath = movedAssets[i];
            string oldPath = movedFromAssetPaths[i];

            bool movedIntoRoot = !oldPath.StartsWith(RootFolder) && newPath.StartsWith(RootFolder);
            bool movedInsideRoot = oldPath.StartsWith(RootFolder) && newPath.StartsWith(RootFolder);

            if (!movedIntoRoot && !movedInsideRoot)
            {
                // [ZombieGame] 移出 GameRes = 不再动态加载：把 Addressables 条目一起删掉（原来不处理，会留下指向 GameRes 外的旧条目）
                if (oldPath.StartsWith(RootFolder))
                {
                    var movedOutGuid = AssetDatabase.AssetPathToGUID(newPath);
                    if (!string.IsNullOrEmpty(movedOutGuid) && settings.FindAssetEntry(movedOutGuid) != null)
                        settings.RemoveAssetEntry(movedOutGuid);
                }
                continue;
            }

            string guid = AssetDatabase.AssetPathToGUID(newPath);
            var entry = settings.FindAssetEntry(guid);

            string groupName = GetGroupName(newPath);
            var group = string.IsNullOrEmpty(groupName) ? settings.DefaultGroup : GetOrCreateGroup(settings, groupName);

            if (entry == null)
            {
                // 从外部拖入 GameRes 或全新资源
                entry = settings.CreateOrMoveEntry(guid, group, false, false);
            }
            else if (entry.parentGroup != group)
            {
                settings.MoveEntry(entry, group, false);
            }

            entry.address = System.IO.Path.GetFileNameWithoutExtension(newPath);
        }

        // ---------------- 保存修改 ----------------
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, new List<AddressableAssetEntry>(), true);
        AssetDatabase.SaveAssets();
        Debug.Log("✅ Addressables 增量同步完成（自动加入并简化地址）");
    }

    /// <summary>[ZombieGame] 这些文件夹下的新资源自动打上"文件夹名"标签（和 Game.Editor 的 AddressablesCleanup.LabelFolders 保持一致）</summary>
    public static readonly string[] LabelFolders = { "Audio", "Levels" }; // [BoxPusher] 关卡配置也按文件夹打标签

    private static string GetGroupName(string assetPath)
    {
        string relative = assetPath.Substring(RootFolder.Length + 1);
        string[] parts = relative.Split('/');
        return parts.Length > 1 ? parts[0] : null;
    }

    private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
    {
        var group = settings.FindGroup(groupName);
        if (group != null) return group;

        group = settings.CreateGroup(groupName, false, false, false, null,
            typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        var bundleSchema = group.GetSchema<BundledAssetGroupSchema>();
        if (bundleSchema != null)
            bundleSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;

        return group;
    }
}
#endif
