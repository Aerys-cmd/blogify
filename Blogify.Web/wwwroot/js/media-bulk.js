// media-bulk.js — Checkbox selection and bulk actions toolbar.
// ES module: auto-initialises when #bulk-toolbar exists on the page.

const selected = new Set();

const toolbar = document.getElementById('bulk-toolbar');
const countLabel = document.getElementById('bulk-count-label');
const idsContainer = document.getElementById('bulk-ids-container');

if (!toolbar) {
    // Not on the media library page — nothing to do.
    // eslint-disable-next-line no-throw-literal
} else {
    // ── Checkbox change (event delegation on document) ──────────────
    document.addEventListener('change', function (e) {
        const checkbox = e.target.closest('.media-check');
        if (!checkbox) return;

        const mediaId = checkbox.dataset.mediaId;
        if (!mediaId) return;

        if (checkbox.checked) {
            selected.add(mediaId);
        } else {
            selected.delete(mediaId);
        }

        syncToolbar();
    });

    // ── Clear selection button ───────────────────────────────────────
    document.addEventListener('click', function (e) {
        if (e.target.closest('#clear-selection')) {
            clearSelection();
        }
    });

    // ── Re-sync after HTMX swap (grid content replaced) ─────────────
    document.addEventListener('htmx:afterSwap', function (e) {
        const target = e.detail ? e.detail.target : e.target;
        if (target && (target.id === 'media-content' || target.closest('#media-content'))) {
            syncCheckboxDom();
        }
    });

    function syncToolbar() {
        const count = selected.size;

        if (count > 0) {
            toolbar.classList.remove('d-none');
        } else {
            toolbar.classList.add('d-none');
        }

        if (countLabel) {
            countLabel.textContent = `${count} item${count !== 1 ? 's' : ''} selected`;
        }

        // Rebuild hidden id inputs for the bulk-delete form
        if (idsContainer) {
            idsContainer.innerHTML = '';
            selected.forEach(function (id) {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'ids';
                input.value = id;
                idsContainer.appendChild(input);
            });
        }

        syncCheckboxDom();
    }

    function syncCheckboxDom() {
        document.querySelectorAll('.media-check').forEach(function (cb) {
            cb.checked = selected.has(cb.dataset.mediaId);
        });
    }

    function clearSelection() {
        selected.clear();
        syncToolbar();
    }

    // Expose for external callers (e.g., after programmatic delete).
    window.mediaBulkClear = clearSelection;
}

