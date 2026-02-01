using UnityEditor;
using UnityEngine;

namespace DebugxLog.Editor
{
    public static class DebugxEditorLibrary
    {
        /// <summary>
        /// Get the default configuration.
        /// 获取默认配置。
        /// </summary>
        /// <typeparam name="T">Type parameter. 类型参数。</typeparam>
        /// <param name="path">Full path. 全路径。</param>
        /// <param name="createOnNotFound">Automatically create if not found. 未找到时自动创建。</param>
        /// <returns>Returns the configuration object. 返回配置对象。</returns>
        public static T GetConfigDefault<T>(string path, bool createOnNotFound = true) where T : ScriptableObject
        {
            return GetConfigDefault<T>(path, out _, createOnNotFound);
        }
        
        /// <summary>
        /// Get the default configuration.
        /// 获取默认配置。
        /// </summary>
        /// <typeparam name="T">Type parameter. 类型参数。</typeparam>
        /// <param name="path">Full path. 全路径。</param>
        /// <param name="createOnNotFound">Automatically create if not found. 未找到时自动创建。</param>
        /// <param name="createNew">Automatically create new if not found. 未找到时自动创建。</param>
        /// <returns>Returns the configuration object. 返回配置对象。</returns>
        private static T GetConfigDefault<T>(string path, out bool createNew, bool createOnNotFound = true) where T : ScriptableObject
        {
            createNew = false;
            T config = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!config && createOnNotFound)
            {
                createNew = true;
                config = ScriptableObject.CreateInstance(typeof(T)) as T;
                AssetDatabase.CreateAsset(config, path);
            }

            return config;
        }
    }
}