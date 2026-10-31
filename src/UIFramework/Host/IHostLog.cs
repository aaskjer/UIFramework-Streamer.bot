namespace UIFramework
{
    /// <summary>
    /// Where the framework writes its log lines. The stub forwards to CPH.LogInfo/LogWarn/LogError; the preview
    /// writes to the console. The framework never talks to Streamer.bot directly.
    /// </summary>
    public interface IHostLog
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
