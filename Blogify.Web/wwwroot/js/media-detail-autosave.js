// media-detail-autosave.js — Debounced autosave for media detail metadata fields.
// Loaded once in BlogAdmin _Layout.cshtml via <script type="module">.
// Uses event delegation to support fields injected via HTMX.

const AUTOSAVE_DELAY_MS = 800;
const saveTimers = new Map();

function getAntiForgeryToken() {
    const hxHeaders = document.body.getAttribute('hx-headers');
    if (hxHeaders) {
        try { return JSON.parse(hxHeaders)['RequestVerificationToken'] ?? ''; } catch { return ''; }
    }
    return '';
}

function setStatus(statusEl, text, className) {
    if (!statusEl) { return; }
    statusEl.textContent = text;
    statusEl.className = 'small media-detail-save-status ' + className;
}

function saveMetadata(mediaId) {
    const altInput = document.getElementById('meta-alt-' + mediaId);
    const titleInput = document.getElementById('meta-title-' + mediaId);
    const descInput = document.getElementById('meta-desc-' + mediaId);
    const statusEl = document.getElementById('meta-save-status-' + mediaId);

    if (!altInput && !titleInput && !descInput) { return; }

    setStatus(statusEl, 'Saving\u2026', 'text-muted');

    const params = new URLSearchParams();
    params.set('id', mediaId);
    if (altInput) { params.set('altText', altInput.value); }
    if (titleInput) { params.set('title', titleInput.value); }
    if (descInput) { params.set('description', descInput.value); }

    const token = getAntiForgeryToken();

    fetch('?handler=UpdateMetadata', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/x-www-form-urlencoded',
            'HX-Request': 'true',
            ...(token ? { 'RequestVerificationToken': token } : {}),
        },
        body: params.toString(),
    })
        .then(function (res) {
            if (res.ok) {
                setStatus(statusEl, 'Saved \u2713', 'text-success');
                setTimeout(function () {
                    setStatus(statusEl, '', 'text-muted');
                }, 2500);
            } else {
                setStatus(statusEl, 'Save failed', 'text-danger');
            }
        })
        .catch(function () {
            setStatus(statusEl, 'Save failed', 'text-danger');
        });
}

// ── Character counter ────────────────────────────────────────────────────────

function updateCharCount(input) {
    const countEl = document.querySelector(`.media-char-count[data-input="${input.id}"]`);
    if (!countEl) { return; }
    const max = parseInt(countEl.dataset.max, 10) || 500;
    const len = input.value.length;
    countEl.textContent = len + '/' + max;
    countEl.className = 'form-text small media-char-count' + (len > max * 0.9 ? ' text-warning' : '');
}

// ── Event delegation ─────────────────────────────────────────────────────────

document.addEventListener('input', function (e) {
    const field = e.target.closest('.media-detail-autosave');
    if (!field) { return; }

    const mediaId = field.dataset.mediaId;
    if (!mediaId) { return; }

    updateCharCount(field);

    const statusEl = document.getElementById('meta-save-status-' + mediaId);
    setStatus(statusEl, 'Unsaved\u2026', 'text-muted');

    // Debounce: cancel any pending save for this media item
    if (saveTimers.has(mediaId)) {
        clearTimeout(saveTimers.get(mediaId));
    }
    saveTimers.set(mediaId, setTimeout(function () {
        saveTimers.delete(mediaId);
        saveMetadata(mediaId);
    }, AUTOSAVE_DELAY_MS));
});

// Immediate save on blur (don't wait for debounce timer)
document.addEventListener('blur', function (e) {
    const field = e.target.closest('.media-detail-autosave');
    if (!field) { return; }

    const mediaId = field.dataset.mediaId;
    if (!mediaId) { return; }

    if (saveTimers.has(mediaId)) {
        clearTimeout(saveTimers.get(mediaId));
        saveTimers.delete(mediaId);
    }
    saveMetadata(mediaId);
}, true);
