// media-library.js — WordPress-style Media Library interactions.
// Handles: click-to-select attachments, inline attachment details panel,
// view-mode persistence across filter changes, and panel close behaviour.

function escapeHtml(s) {
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

const panel = document.getElementById('attachment-details');
const panelContent = document.getElementById('attachment-details-content');
const activeViewInput = document.getElementById('active-view');
const viewGridBtn = document.getElementById('view-grid-btn');
const viewListBtn = document.getElementById('view-list-btn');

// ── Attachment selection ────────────────────────────────────────────

function selectAttachment(item) {
    document.querySelectorAll('.wp-attachment.selected').forEach(function (el) {
        el.classList.remove('selected');
    });
    item.classList.add('selected');
}

function deselectAll() {
    document.querySelectorAll('.wp-attachment.selected').forEach(function (el) {
        el.classList.remove('selected');
    });
}

// Click on attachment button → select it and open details panel
document.addEventListener('click', function (e) {
    const btn = e.target.closest('.wp-attachment-btn');
    if (!btn) return;
    const item = btn.closest('.wp-attachment');
    if (!item) return;
    selectAttachment(item);
    // HTMX will load the detail content; openAttachmentPanel is called
    // by the htmx:afterSwap listener below once the content arrives.
});

// Bulk-select checkbox click — toggle selection without opening detail
document.addEventListener('change', function (e) {
    const cb = e.target.closest('.wp-attachment-bulk-check');
    if (!cb) return;
    const item = cb.closest('.wp-attachment');
    if (!item) return;
    if (cb.checked) {
        item.classList.add('selected');
    } else {
        item.classList.remove('selected');
    }
    e.stopPropagation();
});

// ── Attachment details panel ────────────────────────────────────────

function openAttachmentPanel() {
    if (!panel) return;
    panel.classList.remove('closed');
}

function closeAttachmentPanel() {
    if (!panel) return;
    panel.classList.add('closed');
    deselectAll();
    // Reset panel content to the server-rendered placeholder (localised)
    if (panelContent) {
        const placeholderText = panel.dataset.placeholder ?? '';
        panelContent.innerHTML =
            `<div class="p-4 text-center text-muted"><p class="small mb-0">${escapeHtml(placeholderText)}</p></div>`;
    }
}

// Expose for inline hx-on handlers (e.g. after delete)
window.closeAttachmentPanel = closeAttachmentPanel;

// Close button inside the loaded panel content (event delegation)
document.addEventListener('click', function (e) {
    if (e.target.closest('#close-attachment-details')) {
        closeAttachmentPanel();
    }
});

// Open panel once HTMX has swapped in the detail content
document.addEventListener('htmx:afterSwap', function (e) {
    const target = e.detail ? e.detail.target : null;
    if (target && target.id === 'attachment-details-content') {
        openAttachmentPanel();
    }

    // After a grid refresh (filter/search swap), re-apply bulk checkbox states
    if (target && target.id === 'media-content') {
        deselectAll();
        closeAttachmentPanel();
    }
});

// ── View-mode hidden input — keeps view state across filter changes ─

if (viewGridBtn && activeViewInput) {
    viewGridBtn.addEventListener('click', function () {
        activeViewInput.value = 'grid';
    });
}

if (viewListBtn && activeViewInput) {
    viewListBtn.addEventListener('click', function () {
        activeViewInput.value = 'list';
    });
}
