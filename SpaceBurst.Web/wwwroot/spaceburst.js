(function () {
    const storagePrefix = "spaceburst/";
    let instance = null;
    let animationHandle = 0;
    let tickInFlight = false;
    let handlersAttached = false;
    let canvas = null;

    function resizeCanvas() {
        const canvas = document.getElementById("theCanvas");
        const holder = document.getElementById("canvasHolder");
        if (!canvas || !holder) {
            return;
        }

        const width = Math.max(1, holder.clientWidth);
        const height = Math.max(1, holder.clientHeight);
        if (canvas.width !== width) {
            canvas.width = width;
        }
        if (canvas.height !== height) {
            canvas.height = height;
        }
    }

    function scheduleTick() {
        if (instance && animationHandle === 0) {
            animationHandle = window.requestAnimationFrame(tick);
        }
    }

    async function tick() {
        animationHandle = 0;
        if (!instance || document.hidden || tickInFlight) {
            scheduleTick();
            return;
        }

        tickInFlight = true;
        try {
            await instance.invokeMethodAsync("TickDotNet");
        } catch (error) {
            showFatalError("THE GAME LOOP STOPPED. RELOAD TO RESTART THE RUN.", error);
            instance = null;
        } finally {
            tickInFlight = false;
            scheduleTick();
        }
    }

    function preventContextMenu(event) {
        event.preventDefault();
    }

    function preventGameKeys(event) {
        if ([" ", "ArrowLeft", "ArrowUp", "ArrowRight", "ArrowDown"].includes(event.key)) {
            event.preventDefault();
        }
    }

    function preventCanvasWheel(event) {
        event.preventDefault();
    }

    function handleVisibilityChange() {
        if (!document.hidden) {
            scheduleTick();
        }
    }

    function dismissError() {
        const errorUi = document.getElementById("blazor-error-ui");
        if (errorUi) {
            errorUi.classList.remove("show");
            errorUi.setAttribute("aria-hidden", "true");
        }
    }

    function showFatalError(message, error) {
        const errorUi = document.getElementById("blazor-error-ui");
        const errorCopy = errorUi && errorUi.querySelector(".error-copy");
        if (errorCopy) {
            errorCopy.textContent = message;
        }
        if (errorUi) {
            errorUi.classList.add("show");
            errorUi.setAttribute("aria-hidden", "false");
        }
        if (error) {
            console.error("SpaceBurst browser host failure", error);
        }
    }

    function attachHandlers() {
        if (handlersAttached) {
            return;
        }

        canvas = document.getElementById("theCanvas");
        if (canvas) {
            canvas.addEventListener("contextmenu", preventContextMenu);
            canvas.addEventListener("wheel", preventCanvasWheel, { passive: false });
        }
        const dismiss = document.querySelector("#blazor-error-ui .dismiss");
        if (dismiss) {
            dismiss.addEventListener("click", dismissError);
        }
        window.addEventListener("resize", resizeCanvas);
        window.addEventListener("keydown", preventGameKeys, { passive: false });
        document.addEventListener("visibilitychange", handleVisibilityChange);
        handlersAttached = true;
    }

    function detachHandlers() {
        if (!handlersAttached) {
            return;
        }

        if (canvas) {
            canvas.removeEventListener("contextmenu", preventContextMenu);
            canvas.removeEventListener("wheel", preventCanvasWheel);
        }
        const dismiss = document.querySelector("#blazor-error-ui .dismiss");
        if (dismiss) {
            dismiss.removeEventListener("click", dismissError);
        }
        window.removeEventListener("resize", resizeCanvas);
        window.removeEventListener("keydown", preventGameKeys);
        document.removeEventListener("visibilitychange", handleVisibilityChange);
        canvas = null;
        handlersAttached = false;
    }

    window.spaceBurstHost = {
        detectTouchSupport: function () {
            return navigator.maxTouchPoints > 0 || window.matchMedia("(pointer: coarse)").matches;
        },
        initRender: function (dotNetInstance) {
            instance = dotNetInstance;
            resizeCanvas();
            attachHandlers();
            scheduleTick();
        },
        disposeRender: function () {
            instance = null;
            tickInFlight = false;
            if (animationHandle !== 0) {
                window.cancelAnimationFrame(animationHandle);
                animationHandle = 0;
            }
            detachHandlers();
        },
        storageExists: function (key) {
            return window.localStorage.getItem(storagePrefix + key) !== null;
        },
        storageRead: function (key) {
            return window.localStorage.getItem(storagePrefix + key) || "";
        },
        storageWrite: function (key, value) {
            window.localStorage.setItem(storagePrefix + key, value || "");
        },
        storageDelete: function (key) {
            window.localStorage.removeItem(storagePrefix + key);
        },
        storageList: function (prefix) {
            const fullPrefix = storagePrefix + (prefix || "");
            const keys = [];
            for (let index = 0; index < window.localStorage.length; index += 1) {
                const currentKey = window.localStorage.key(index);
                if (currentKey && currentKey.startsWith(fullPrefix)) {
                    keys.push(currentKey.substring(storagePrefix.length));
                }
            }

            return keys;
        }
    };
})();
