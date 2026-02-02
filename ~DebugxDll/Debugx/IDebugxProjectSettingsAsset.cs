namespace DebugxLog
{
    /// <summary>
    /// Debugx settings asset interface.
    /// Debugx设置资源接口。
    /// </summary>
    public interface IDebugxProjectSettingsAsset
    {
        /// <summary>
        /// Copy data.
        /// 复制数据。
        /// </summary>
        /// <param name="settings"></param>
        void ApplyTo(DebugxProjectSettings settings);
    }
}