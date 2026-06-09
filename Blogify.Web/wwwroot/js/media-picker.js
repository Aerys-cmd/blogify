// media-picker.js — Media picker selection, preview panel, and confirm flow.
// Loaded once in BlogAdmin _Layout.cshtml via <script type="module">.
// Uses event delegation so no re-attachment is needed after HTMX swaps.

// ── Card preview (click selects card, populates right preview panel) ──────────

document.addEventListener('click', function (e) {

    // ── Switch-tab helper button (empty state CTA + back-to-library) ────────
    const switchBtn = e.target.closest('[data-picker-switch-tab]');
    if (switchBtn) {
        const tabId = switchBtn.dataset.pickerSwitchTab;
        const tabEl = document.getElementById(tabId);
        if (tabEl) {
            bootstrap.Tab.getOrCreateInstance(tabEl).show();
        }
        return;
    }

    // ── Card preview button ──────────────────────────────────────────────────
    const previewBtn = e.target.closest('[data-media-preview]');
    if (previewBtn) {
        const modalId = previewBtn.dataset.modalId;
        if (!modalId) { return; }

        // Deselect previously selected card in this modal
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.querySelectorAll('[data-media-preview].selected').forEach(function (el) {
                el.classList.remove('selected');
                el.closest('.media-attachment')?.classList.remove('selected');
                el.setAttribute('aria-pressed', 'false');
            });
        }

        // Select this card
        previewBtn.classList.add('selected');
        previewBtn.closest('.media-attachment')?.classList.add('selected');
        previewBtn.setAttribute('aria-pressed', 'true');

        // Populate the preview panel
        const panelEl = document.getElementById(modalId + '-preview-panel');
        if (panelEl) {
            const emptyEl = panelEl.querySelector('.media-picker-preview-empty');
            const contentEl = panelEl.querySelector('.media-picker-preview-content');
            const imgEl = panelEl.querySelector('.media-picker-preview-img');
            const filenameEl = panelEl.querySelector('.media-picker-preview-filename');
            const metaEl = panelEl.querySelector('.media-picker-preview-meta');
            const altInput = panelEl.querySelector('.media-picker-preview-alt');

            if (emptyEl) { emptyEl.classList.add('d-none'); }
            if (contentEl) { contentEl.classList.remove('d-none'); }
            if (imgEl) {
                imgEl.src = previewBtn.dataset.url || '';
                imgEl.alt = previewBtn.dataset.filename || '';
            }
            if (filenameEl) { filenameEl.textContent = previewBtn.dataset.filename || ''; }
            if (metaEl) { metaEl.textContent = ''; }
            if (altInput) {
                altInput.value = previewBtn.dataset.altText || '';
                // Store the selected card's data on the input for the confirm handler
                altInput.dataset.selectedMediaId = previewBtn.dataset.mediaId || '';
                altInput.dataset.selectedUrl = previewBtn.dataset.url || '';
                altInput.dataset.selectedFullUrl = previewBtn.dataset.fullUrl || previewBtn.dataset.url || '';
                altInput.dataset.selectedTarget = previewBtn.dataset.target || '';
            }
        }

        // Enable the confirm button
        const confirmBtn = document.getElementById(modalId + '-confirm-btn');
        if (confirmBtn) {
            confirmBtn.disabled = false;
            confirmBtn.dataset.selectedMediaId = previewBtn.dataset.mediaId || '';
            confirmBtn.dataset.selectedUrl = previewBtn.dataset.url || '';
            confirmBtn.dataset.selectedFullUrl = previewBtn.dataset.fullUrl || previewBtn.dataset.url || '';
            confirmBtn.dataset.selectedTarget = previewBtn.dataset.target || '';
            confirmBtn.dataset.selectedFilename = previewBtn.dataset.filename || '';
        }

        // On mobile: show preview panel when a card is selected
        if (panelEl) { panelEl.classList.add('has-selection'); }
        return;
    }

    // ── Confirm / "Use this image" button ────────────────────────────────────
    const confirmBtn = e.target.closest('[data-picker-confirm]');
    if (confirmBtn && !confirmBtn.disabled) {
        const modalId = confirmBtn.dataset.modalId;
        const targetInputId = confirmBtn.dataset.targetInputId;

        // Read alt text from preview panel input (user may have edited it)
        let altText = confirmBtn.dataset.selectedFilename || '';
        if (modalId) {
            const panelEl = document.getElementById(modalId + '-preview-panel');
            const altInput = panelEl?.querySelector('.media-picker-preview-alt');
            if (altInput) { altText = altInput.value || altText; }
        }

        document.dispatchEvent(new CustomEvent('mediaSelected', {
            detail: {
                targetInputId,
                mediaId: confirmBtn.dataset.selectedMediaId,
                url: confirmBtn.dataset.selectedUrl,
                fullUrl: confirmBtn.dataset.selectedFullUrl,
                altText,
            }
        }));

        if (modalId) {
            const modalEl = document.getElementById(modalId);
            if (modalEl) {
                bootstrap.Modal.getOrCreateInstance(modalEl).hide();
            }
        }
        return;
    }

    // ── Clear / remove button on picker field ────────────────────────────────
    const clearBtn = e.target.closest('[data-picker-clear]');
    if (clearBtn) {
        const targetInputId = clearBtn.dataset.targetInputId;
        if (!targetInputId) { return; }

        const hiddenInput = document.getElementById(targetInputId);
        if (hiddenInput) { hiddenInput.value = ''; }

        const previewDiv = document.getElementById(targetInputId + '-preview');
        if (previewDiv) { previewDiv.classList.add('d-none'); }

        const emptyBtn = document.getElementById(targetInputId + '-empty-btn');
        if (emptyBtn) { emptyBtn.classList.remove('d-none'); }

        // Hide any standalone clear buttons
        document.querySelectorAll(`[data-picker-clear][data-target-input-id="${targetInputId}"]`).forEach(function (btn) {
            if (btn !== clearBtn || btn.id === targetInputId + '-clear') {
                btn.classList.add('d-none');
            }
        });
    }
});

// ── mediaSelected: update the field preview ──────────────────────────────────

document.addEventListener('mediaSelected', function (e) {
    const { targetInputId, mediaId, url } = e.detail;
    if (!targetInputId) { return; }

    const hiddenInput = document.getElementById(targetInputId);
    if (hiddenInput) { hiddenInput.value = mediaId; }

    const previewDiv = document.getElementById(targetInputId + '-preview');
    if (previewDiv) {
        const img = previewDiv.querySelector('img');
        if (img) { img.src = url; }
        previewDiv.classList.remove('d-none');
    }

    const emptyBtn = document.getElementById(targetInputId + '-empty-btn');
    if (emptyBtn) { emptyBtn.classList.add('d-none'); }

    const clearBtn = document.getElementById(targetInputId + '-clear');
    if (clearBtn) { clearBtn.classList.remove('d-none'); }
});

// ── Reset picker state when modal closes ──────────────────────────────────────

document.addEventListener('hidden.bs.modal', function (e) {
    const modal = e.target;
    if (!modal || !modal.id) { return; }
    const modalId = modal.id;

    // Deselect all cards
    modal.querySelectorAll('[data-media-preview].selected').forEach(function (el) {
        el.classList.remove('selected');
        el.closest('.media-attachment')?.classList.remove('selected');
        el.setAttribute('aria-pressed', 'false');
    });

    // Reset preview panel
    const panelEl = document.getElementById(modalId + '-preview-panel');
    if (panelEl) {
        const emptyEl = panelEl.querySelector('.media-picker-preview-empty');
        const contentEl = panelEl.querySelector('.media-picker-preview-content');
        if (emptyEl) { emptyEl.classList.remove('d-none'); }
        if (contentEl) { contentEl.classList.add('d-none'); }
        panelEl.classList.remove('has-selection');
    }

    // Disable confirm button
    const confirmBtn = document.getElementById(modalId + '-confirm-btn');
    if (confirmBtn) {
        confirmBtn.disabled = true;
    }
});

