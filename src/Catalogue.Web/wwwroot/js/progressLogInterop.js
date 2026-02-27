// progressLogInterop.js — Auto-scroll helpers for progress log panels.
// Uses the same IIFE/window pattern as dropdownInterop.js and sqlHighlight.js.

window.progressLog = (function () {
    // Per-element state: { autoScroll: bool, listener: function }
    const _state = new Map();
    const THRESHOLD = 10; // px from bottom that still counts as "at bottom"

    return {
        // Attach scroll listener and enable auto-scroll for the given element.
        init: function (elementId) {
            const el = document.getElementById(elementId);
            if (!el) return;

            // Clean up any previous listener (e.g. component remounted after clear)
            this.dispose(elementId);

            const listener = function () {
                const atBottom = el.scrollTop + el.clientHeight >= el.scrollHeight - THRESHOLD;
                const state = _state.get(elementId);
                if (state) state.autoScroll = atBottom;
            };

            el.addEventListener('scroll', listener);
            _state.set(elementId, { autoScroll: true, listener });
        },

        // Scroll to the bottom only when auto-scroll is enabled.
        scrollToBottom: function (elementId) {
            const state = _state.get(elementId);
            if (!state || !state.autoScroll) return;

            const el = document.getElementById(elementId);
            if (el) el.scrollTop = el.scrollHeight;
        },

        // Remove the scroll listener and clean up state.
        dispose: function (elementId) {
            const state = _state.get(elementId);
            if (state) {
                const el = document.getElementById(elementId);
                if (el) el.removeEventListener('scroll', state.listener);
                _state.delete(elementId);
            }
        }
    };
})();
