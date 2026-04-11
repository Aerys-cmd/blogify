// media-library.js — Media Library interactions.
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

// ── Attachment selection (active = currently open in details panel) ─

function setActiveAttachment(item) {
    document.querySelectorAll('.media-attachment.active').forEach(function (el) {
        el.classList.remove('active');
    });
    item.classList.add('active');
}

function clearActiveAttachment() {
    document.querySelectorAll('.media-attachment.active').forEach(function (el) {
        el.classList.remove('active');
    });
}

// Click on attachment button → mark active and open details panel
document.addEventListener('click', function (e) {
    const btn = e.target.closest('.media-attachment-btn');
    if (!btn) return;
    const item = btn.closest('.media-attachment');
    if (!item) return;
    setActiveAttachment(item);
    // HTMX will load the detail content; openAttachmentPanel is called
    // by the htmx:afterSwap listener below once the content arrives.
});

// ── Attachment details panel ────────────────────────────────────────

function openAttachmentPanel() {
    if (!panel) return;
    panel.classList.remove('closed');
}

function closeAttachmentPanel() {
    if (!panel) return;
    panel.classList.add('closed');
    clearActiveAttachment();
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

    // After a grid refresh (filter/search swap), clear active + bulk selection
    if (target && target.id === 'media-content') {
        clearActiveAttachment();
        closeAttachmentPanel();
        if (typeof window.mediaBulkClear === 'function') {
            window.mediaBulkClear();
        }
    }
});

// ── Select-all (list view header checkbox) ──────────────────────────

document.addEventListener('change', function (e) {
    if (e.target.id !== 'select-all-check') return;
    document.querySelectorAll('.media-check').forEach(function (cb) {
        cb.checked = e.target.checked;
        cb.dispatchEvent(new Event('change', { bubbles: true }));
    });
});

// ── Copy-to-clipboard (event delegation for dynamically loaded content) ─

document.addEventListener('click', function (e) {
    const btn = e.target.closest('[data-copy-target]');
    if (!btn) return;
    const input = document.getElementById(btn.dataset.copyTarget);
    if (!input) return;
    navigator.clipboard.writeText(input.value).then(function () {
        const orig = btn.textContent.trim();
        btn.textContent = '✓';
        setTimeout(function () { btn.textContent = orig; }, 1500);
    });
});

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
