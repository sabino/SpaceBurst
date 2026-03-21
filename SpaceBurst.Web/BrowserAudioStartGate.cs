namespace SpaceBurst
{
    internal sealed class BrowserAudioStartGate : IAudioStartGate
    {
        private bool isReady;

        public bool RequiresUserGesture
        {
            get { return true; }
        }

        public bool IsReady
        {
            get { return isReady; }
        }

        public void NotifyPrimaryGesture()
        {
            isReady = true;
        }
    }
}
