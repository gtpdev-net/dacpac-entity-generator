// splitterInterop.js — Draggable panel splitter for the Schema Browser.
// Uses the same IIFE/window pattern as dropdownInterop.js and progressLogInterop.js.

window.splitterInterop = (function () {
    // Map from containerId -> cleanup function
    const _cleanups = new Map();

    return {
        init: function (containerId, leftId, dividerId) {
            // Clean up any previous listener on this container (e.g. component remounted)
            this.dispose(containerId);

            const container = document.getElementById(containerId);
            const left      = document.getElementById(leftId);
            const divider   = document.getElementById(dividerId);

            if (!container || !left || !divider) return;

            let dragging   = false;
            let startX     = 0;
            let startWidth = 0;

            const MIN_PX = 80;

            function onPointerDown(e) {
                if (e.button !== 0) return; // left button only
                dragging   = true;
                startX     = e.clientX;
                startWidth = left.getBoundingClientRect().width;

                divider.setPointerCapture(e.pointerId);
                e.preventDefault();
            }

            function onPointerMove(e) {
                if (!dragging) return;

                const containerWidth = container.getBoundingClientRect().width;
                const dividerWidth   = divider.getBoundingClientRect().width;
                const delta          = e.clientX - startX;
                const maxWidth       = containerWidth - MIN_PX - dividerWidth;

                const newWidth = Math.min(Math.max(startWidth + delta, MIN_PX), maxWidth);
                left.style.flex = `0 0 ${newWidth}px`;
            }

            function onPointerUp() {
                dragging = false;
            }

            divider.addEventListener('pointerdown',   onPointerDown);
            divider.addEventListener('pointermove',   onPointerMove);
            divider.addEventListener('pointerup',     onPointerUp);
            divider.addEventListener('pointercancel', onPointerUp);

            _cleanups.set(containerId, function () {
                divider.removeEventListener('pointerdown',   onPointerDown);
                divider.removeEventListener('pointermove',   onPointerMove);
                divider.removeEventListener('pointerup',     onPointerUp);
                divider.removeEventListener('pointercancel', onPointerUp);
            });
        },

        dispose: function (containerId) {
            const cleanup = _cleanups.get(containerId);
            if (cleanup) {
                cleanup();
                _cleanups.delete(containerId);
            }
        }
    };
})();
