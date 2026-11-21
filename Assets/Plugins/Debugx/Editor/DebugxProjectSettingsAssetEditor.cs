using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DebugxLog.Editor
{
    public static class DebugxMemberInfoAssetEditor
    {
        private static readonly Dictionary<int, bool> _fadeAreaOpenCached = new Dictionary<int, bool>();
        private const string FadeAreaPrefsKeyPrefix = "DebugxMemberInfoAssetEditor.fadeAreaOpenCached.";

        public static bool GetFadeAreaOpenCached(int key)
        {
            if (!_fadeAreaOpenCached.ContainsKey(key))
            {
                _fadeAreaOpenCached.Add(
                    key,
                    DebugxStaticData.PlayerPrefsGetBool($"{FadeAreaPrefsKeyPrefix}{key}", true));
            }
            
            return _fadeAreaOpenCached[key];
        }
        
        public static bool SetFadeAreaOpenCached(int key, bool value)
        {
            _fadeAreaOpenCached[key] = value;
            DebugxStaticData.PlayerPrefsSetBool($"{FadeAreaPrefsKeyPrefix}{key}", value);
            return _fadeAreaOpenCached[key];
        }

        public static void DeleteFadeAreaOpenCached(int key)
        {
            _fadeAreaOpenCached.Remove(key);
            DebugxStaticData.PlayerPrefsDeleteKey($"{FadeAreaPrefsKeyPrefix}{key}");
        }
    }
    
    public static class DebugxProjectSettingsAssetEditor
    {
        public static void OnInitializeOnLoadMethod()
        {
            //项目启动时确认配置资源是否存在
            CheckDebugxProjectSettingsAsset();
            DebugxLoggerCodeGenerator.MigrateLegacyLocation();

            // Duplicate keys typically arrive through a merge, without anyone opening the settings page, so report on load.
            // 重复 key 通常来自合并分支，没人会去打开设置页，故在加载时报告。
            string duplicateKeys = DescribeDuplicateMemberKeys(DebugxProjectSettingsAsset.Instance);
            if (duplicateKeys != null)
                Debug.LogWarning($"[Debugx] Duplicate member keys: {duplicateKeys}. Give each member its own key in Project Settings > Debugx. 成员 Key 重复，请在 Project Settings > Debugx 中为每个成员设置不同的 Key。");
        }

        /// <summary>
        /// Describe the member keys used by more than one member, e.g. "2 (Alice, Bob)"; null when every key is unique.
        /// 描述被多个成员共用的 key，如 "2 (Alice, Bob)"；全部唯一时返回 null。
        /// </summary>
        public static string DescribeDuplicateMemberKeys(DebugxProjectSettingsAsset asset)
        {
            if (!asset) return null;

            var namesByKey = new Dictionary<int, List<string>>();
            void Collect(DebugxMemberInfoAsset[] members)
            {
                if (members == null) return;
                foreach (DebugxMemberInfoAsset m in members)
                {
                    if (!namesByKey.TryGetValue(m.key, out List<string> names)) namesByKey[m.key] = names = new List<string>();
                    names.Add(string.IsNullOrEmpty(m.signature) ? $"Member {m.key}" : m.signature);
                }
            }

            Collect(asset.defaultMemberAssets);
            Collect(asset.customMemberAssets);

            var parts = new List<string>();
            foreach (KeyValuePair<int, List<string>> pair in namesByKey)
                if (pair.Value.Count > 1) parts.Add($"{pair.Key} ({string.Join(", ", pair.Value)})");

            return parts.Count == 0 ? null : string.Join("; ", parts);
        }
        
        /// <summary>
        /// 确认配置资源是否存在
        /// </summary>
        public static void CheckDebugxProjectSettingsAsset()
        {
            //在代码编译时走到这里，无法通过instance = Resources.Load<DebugxProjectSettingsAsset>(DebugxProjectSettings.fileName);方式获取到实际存在的文件
            //这会导致每次代码重编译时都创建了新的DebugxProjectSettingsAsset并覆盖了旧的

            // Only the project's own copy counts: under Assets/, in a Resources folder, named DebugxProjectSettings. A copy
            // inside an installed package is read-only. Any folder works, so an asset created by an older version
            // (Assets/Plugins/Debugx/Resources/) keeps being used.
            // 只认项目自己的那份：位于 Assets/ 下的 Resources 目录、名为 DebugxProjectSettings。已安装包内的副本是只读的。
            // 目录不限，因此旧版本创建的资源（Assets/Plugins/Debugx/Resources/）会继续使用。
            List<string> found = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(DebugxProjectSettingsAsset)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/") && path.Contains("/Resources/")
                    && System.IO.Path.GetFileNameWithoutExtension(path) == DebugxProjectSettings.FileName)
                {
                    AssetDatabase.LoadAssetAtPath<DebugxProjectSettingsAsset>(path);
                    found.Add(path);
                }
            }

            // Resources.Load picks one of several same-named assets arbitrarily, so builds could use the wrong one.
            // Resources.Load 在多个同名资源中任取一个，构建可能用错。
            if (found.Count > 1)
                Debug.LogWarning("[Debugx] Found more than one DebugxProjectSettings asset; keep one and delete the others. 发现多个 DebugxProjectSettings 资源，请只保留一个：\n" + string.Join("\n", found));

            if (found.Count == 0) CreateDebugxProjectSettingsAsset();
        }
        
        /// <summary>
        /// 创建配置资源
        /// </summary>
        public static void CreateDebugxProjectSettingsAsset()
        {
            //在编辑器启动或代码编译时，创建Asset后直接Resources.Load此资源会导致一个堆栈溢出的Bug。所以我们使用DebugxProjectSettings.ApplyBy接口进行保存，避开会调用到Resources.Load的流程
            //调用Resources.Load加载已经存在的资源则不会有此问题，应该是Editor启动时创建的Asset资源还未成功进行保存

            string path = $"{DebugxStaticData.ResourcesPathRelative}/{DebugxProjectSettings.FileName}.asset";

            if (DebugxProjectSettingsAsset.Instance) AssetDatabase.DeleteAsset(path);//移除旧资源

            DebugxProjectSettingsAsset.Instance = ScriptableObject.CreateInstance(typeof(DebugxProjectSettingsAsset)) as DebugxProjectSettingsAsset;

            //确认文件夹是否存在，否则创建
            if (!System.IO.Directory.Exists(DebugxStaticData.ResourcesPathAbsolute))
                System.IO.Directory.CreateDirectory(DebugxStaticData.ResourcesPathAbsolute);

            AssetDatabase.CreateAsset(DebugxProjectSettingsAsset.Instance, path);

            DebugxProjectSettingsAsset.Instance?.ResetMembers();

            EditorUtility.SetDirty(DebugxProjectSettingsAsset.Instance);
            AssetDatabase.SaveAssetIfDirty(DebugxProjectSettingsAsset.Instance);
            DebugxProjectSettings.ApplyBy(DebugxProjectSettingsAsset.Instance);
            
            // Generate Debugx class with member-specific Log methods.
            // 生成包含成员专用 Log 方法的 Debugx 类。
            DebugxLoggerCodeGenerator.GenerateDebugxLoggerClass();
        }
    }
}
