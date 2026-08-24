using System;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Xna.Framework;

namespace SpaceBurst.Web.Pages
{
    public partial class Index : IDisposable
    {
        private Game game;
        private DotNetObjectReference<Index> renderReference;
        private bool initialized;
        private bool disposed;

        [Inject]
        private IJSRuntime JsRuntime { get; set; }

        private IJSInProcessRuntime SyncJsRuntime
        {
            get { return (IJSInProcessRuntime)JsRuntime; }
        }

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (!firstRender || initialized || disposed)
                return;

            initialized = true;
            bool supportsTouch = SyncJsRuntime.Invoke<bool>("spaceBurstHost.detectTouchSupport");
            PlatformServices.Initialize(
                new PlatformCapabilities(
                    supportsWindowedDisplayModes: false,
                    supportsTextInput: false,
                    supportsMouseCursor: true,
                    supportsTouch: supportsTouch,
                    supportsGamepad: true,
                    supportsApplicationExit: false,
                    audioRequiresUserGesture: true,
                    preferDepth16RenderTargets: true,
                    supportsScreenCapture: false),
                new BrowserLocalStorageBackend(SyncJsRuntime),
                PlatformServices.CreateDefaultTextAssetProvider(),
                new BrowserAudioStartGate());

            renderReference = DotNetObjectReference.Create(this);
            SyncJsRuntime.InvokeVoid("spaceBurstHost.initRender", renderReference);
        }

        [JSInvokable]
        public void TickDotNet()
        {
            if (disposed)
                return;

            if (game == null)
            {
                game = new Game1();
                game.Run();
            }

            game.Tick();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            try
            {
                SyncJsRuntime?.InvokeVoid("spaceBurstHost.disposeRender");
            }
            catch (JSException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            renderReference?.Dispose();
            renderReference = null;
            game?.Dispose();
            game = null;
        }
    }
}
