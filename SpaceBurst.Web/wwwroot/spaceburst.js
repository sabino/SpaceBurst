(function () {
    const storagePrefix = "spaceburst/";
    let instance = null;
    let animationHandle = 0;

    function resizeCanvas() {
        const canvas = document.getElementById("theCanvas");
        const holder = document.getElementById("canvasHolder");
        if (!canvas || !holder) {
            return;
        }

        canvas.width = holder.clientWidth;
        canvas.height = holder.clientHeight;
    }

    function tick() {
        if (instance) {
            instance.invokeMethodAsync("TickDotNet");
        }

        animationHandle = window.requestAnimationFrame(tick);
    }

    window.spaceBurstHost = {
        detectTouchSupport: function () {
            return navigator.maxTouchPoints > 0 || window.matchMedia("(pointer: coarse)").matches;
        },
        initRender: function (dotNetInstance) {
            instance = dotNetInstance;
            resizeCanvas();

            const canvas = document.getElementById("theCanvas");
            if (canvas) {
                canvas.addEventListener("contextmenu", function (event) {
                    event.preventDefault();
                });
            }

            window.addEventListener("resize", resizeCanvas);
            window.addEventListener("keydown", function (event) {
                if ([32, 37, 38, 39, 40].indexOf(event.keyCode) > -1) {
                    event.preventDefault();
                }
            });
            window.addEventListener("wheel", function (event) {
                event.preventDefault();
            }, { passive: false });

            if (animationHandle === 0) {
                animationHandle = window.requestAnimationFrame(tick);
            }
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
