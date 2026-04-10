(function () {
    var loader = document.getElementById('ba-loader');
    var activeRequests = 0;

    function start() {
        activeRequests += 1;
        if (loader) {
            loader.classList.remove('ba-loader-done');
            loader.classList.add('ba-loader-running');
        }
    }

    function finish() {
        activeRequests = Math.max(0, activeRequests - 1);
        if (activeRequests > 0) { return; }
        if (loader) {
            loader.classList.remove('ba-loader-running');
            loader.classList.add('ba-loader-done');
            // 450ms = transform 0.15s (150ms) + opacity 0.3s delayed 0.15s (300ms total)
            setTimeout(function () { loader.classList.remove('ba-loader-done'); }, 450);
        }
    }

    document.addEventListener('htmx:beforeRequest', start);
    document.addEventListener('htmx:afterRequest', finish);

    window.addEventListener('beforeunload', start);
    window.addEventListener('pageshow', function (e) {
        if (e.persisted) { finish(); }
    });

    // Delete confirmation via data-confirm-message attribute (event delegation)
    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!form || !form.dataset.confirmMessage) { return; }
        if (!window.confirm(form.dataset.confirmMessage)) {
            e.preventDefault();
        }
    });
}());
