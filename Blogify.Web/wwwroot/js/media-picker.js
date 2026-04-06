// media-picker.js — Media picker selection logic
// Loaded once in BlogAdmin _Layout.cshtml via <script type="module">.
// Uses event delegation so no re-attachment is needed after HTMX swaps.

document.addEventListener('click', function (e) {
    // Media select button inside picker modal
    const selectBtn = e.target.closest('[data-media-select]');
    if (selectBtn) {
        const mediaId = selectBtn.dataset.mediaId;
        const url = selectBtn.dataset.url;
        const targetInputId = selectBtn.dataset.target;
        const modalId = selectBtn.dataset.modalId;

        document.dispatchEvent(new CustomEvent('mediaSelected', {
            detail: {
                targetInputId,
                mediaId,
                url,
                fullUrl: selectBtn.dataset.fullUrl ?? url,
                altText: selectBtn.dataset.altText ?? ''
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

    // Clear / remove button on picker field
    const clearBtn = e.target.closest('[data-picker-clear]');
    if (clearBtn) {
        const targetInputId = clearBtn.dataset.targetInputId;
        if (!targetInputId) { return; }

        const hiddenInput = document.getElementById(targetInputId);
        if (hiddenInput) { hiddenInput.value = ''; }

        const previewDiv = document.getElementById(targetInputId + '-preview');
        if (previewDiv) { previewDiv.classList.add('d-none'); }

        clearBtn.classList.add('d-none');
    }
});

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

    const clearBtn = document.getElementById(targetInputId + '-clear');
    if (clearBtn) { clearBtn.classList.remove('d-none'); }
});

