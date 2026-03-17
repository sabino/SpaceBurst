using SpaceBurst.RuntimeData;

namespace SpaceBurst
{
    sealed class OptionsData
    {
        public RetryMode RetryMode { get; set; } = RetryMode.ClassicStageRestart;
    }
}
