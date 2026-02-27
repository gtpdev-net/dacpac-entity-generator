window.dropdownInterop = (function () {
    const _listeners = new Map();

    return {
        addClickOutsideListener: function (element, dotNetRef) {
            // Remove any pre-existing listener for this element first
            this.removeClickOutsideListener(element);

            const handler = function (e) {
                if (!element.contains(e.target)) {
                    dotNetRef.invokeMethodAsync('Close');
                }
            };

            // Use capture phase so we catch the click before Blazor stop-propagation takes effect
            document.addEventListener('click', handler, true);
            _listeners.set(element, handler);
        },

        removeClickOutsideListener: function (element) {
            const handler = _listeners.get(element);
            if (handler) {
                document.removeEventListener('click', handler, true);
                _listeners.delete(element);
            }
        }
    };
})();
