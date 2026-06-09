// media-picker-upload.js — Drag-and-drop upload for the media picker modal.
// Loaded once in BlogAdmin _Layout.cshtml via <script type="module">.
// Uses event delegation to support dynamically rendered upload zones
// (HTMX loads the picker modal body on show.bs.modal).

const PICKER_ALLOWED_TYPES = new Set(['image/jpeg', 'image/png', 'image/gif', 'image/webp']);
const PICKER_MAX_BYTES = 10 * 1024 * 1024;

function getAntiForgeryToken() {
    const hxHeaders = document.body.getAttribute('hx-headers');
    if (hxHeaders) {
        try { return JSON.parse(hxHeaders)['RequestVerificationToken'] ?? ''; } catch { return ''; }
    }
    return '';
}

function escapeHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// ── Queue item helpers ──────────────────────────────────────────────────────

function addPickerQueueItem(queue, name) {
    const li = document.createElement('li');
    li.className = 'list-group-item d-flex align-items-center gap-2 py-2';
    li.innerHTML = `
        <img class="picker-queue-thumb rounded flex-shrink-0"
             width="32" height="32" style="object-fit:cover;" src="" alt="" aria-hidden="true" />
        <div class="flex-grow-1 min-w-0">
            <div class="text-truncate small fw-semibold">${escapeHtml(name)}</div>
            <div class="progress mt-1" style="height:4px;" role="progressbar"
                 aria-valuemin="0" aria-valuemax="100" aria-valuenow="0">
                <div class="progress-bar"></div>
            </div>
            <div class="picker-queue-status small text-muted mt-1" aria-live="polite"></div>
        </div>`;
    queue.appendChild(li);
    queue.classList.remove('d-none');
    return li;
}

function setPickerProgress(item, pct) {
    const bar = item.querySelector('.progress-bar');
    if (bar) {
        bar.style.width = pct + '%';
        item.querySelector('.progress')?.setAttribute('aria-valuenow', pct);
    }
}

function setPickerDone(item) {
    const bar = item.querySelector('.progress-bar');
    if (bar) { bar.style.width = '100%'; bar.classList.add('bg-success'); }
    const status = item.querySelector('.picker-queue-status');
    if (status) { status.textContent = '✓ Uploaded'; status.className = 'picker-queue-status small text-success mt-1'; }
}

function setPickerError(item, msg) {
    item.querySelector('.progress')?.classList.add('d-none');
    const status = item.querySelector('.picker-queue-status');
    if (status) { status.textContent = '✗ ' + msg; status.className = 'picker-queue-status small text-danger mt-1'; }
}

// ── Core upload function ────────────────────────────────────────────────────

function uploadPickerFile(file, uploadUrl, queue, gridId, modalId, targetInputId) {
    const queueItem = addPickerQueueItem(queue, file.name);

    if (!PICKER_ALLOWED_TYPES.has(file.type)) {
        setPickerError(queueItem, 'Not allowed — only JPEG, PNG, GIF, WebP.');
        return;
    }
    if (file.size > PICKER_MAX_BYTES) {
        setPickerError(queueItem, 'Exceeds 10 MB limit.');
        return;
    }

    // Inline thumbnail preview
    const reader = new FileReader();
    reader.onload = function (ev) {
        const thumb = queueItem.querySelector('.picker-queue-thumb');
        if (thumb) { thumb.src = ev.target.result; }
    };
    reader.readAsDataURL(file);

    const formData = new FormData();
    formData.append('file', file);

    const token = getAntiForgeryToken();
    const xhr = new XMLHttpRequest();
    xhr.open('POST', uploadUrl);
    if (token) { xhr.setRequestHeader('RequestVerificationToken', token); }
    xhr.setRequestHeader('HX-Request', 'true');

    xhr.upload.addEventListener('progress', function (ev) {
        if (ev.lengthComputable) {
            setPickerProgress(queueItem, Math.round((ev.loaded / ev.total) * 100));
        }
    });

    xhr.addEventListener('load', function () {
        if (xhr.status >= 200 && xhr.status < 300 && xhr.responseText.trim()) {
            setPickerDone(queueItem);

            // Prepend the new card to the library grid
            const grid = document.getElementById(gridId);
            if (grid) {
                const wrapper = document.createElement('ul');
                wrapper.innerHTML = xhr.responseText.trim();
                const card = wrapper.firstElementChild;
                if (card) {
                    grid.prepend(card);

                    // Auto-select the new card (simulate a click on its preview button)
                    const previewBtn = card.querySelector('[data-media-preview]');
                    if (previewBtn) {
                        previewBtn.click();
                    }
                }
            }

            // Switch to Library tab after a short delay so user sees the queue update
            setTimeout(function () {
                const libTab = document.getElementById(modalId + '-library-tab');
                if (libTab) {
                    bootstrap.Tab.getOrCreateInstance(libTab).show();
                }
            }, 600);
        } else {
            setPickerError(queueItem, 'Upload failed. Please try again.');
        }
    });

    xhr.addEventListener('error', function () {
        setPickerError(queueItem, 'Network error. Please try again.');
    });

    xhr.send(formData);
}

function handlePickerFiles(files, uploadUrl, queue, gridId, modalId, targetInputId) {
    Array.from(files).forEach(function (f) {
        uploadPickerFile(f, uploadUrl, queue, gridId, modalId, targetInputId);
    });
}

// ── Event delegation for dynamically rendered upload zones ─────────────────

function initPickerDropZone(zone) {
    if (zone.dataset.pickerUploadInit) { return; }
    zone.dataset.pickerUploadInit = '1';

    const modalId = zone.dataset.modalId;
    const targetInputId = zone.dataset.targetInputId;
    const uploadUrl = zone.dataset.uploadUrl;
    const queue = document.getElementById(modalId + '-upload-queue');
    const gridId = modalId + '-grid';

    if (!queue) { return; }

    zone.addEventListener('dragover', function (e) {
        e.preventDefault();
        zone.classList.add('drag-over');
    });

    zone.addEventListener('dragleave', function (e) {
        if (!zone.contains(e.relatedTarget)) {
            zone.classList.remove('drag-over');
        }
    });

    zone.addEventListener('drop', function (e) {
        e.preventDefault();
        zone.classList.remove('drag-over');
        handlePickerFiles(e.dataTransfer.files, uploadUrl, queue, gridId, modalId, targetInputId);
    });

    zone.addEventListener('click', function (e) {
        // Only trigger file input when clicking the zone itself, not the label/button inside
        if (!e.target.closest('label') && !e.target.closest('input')) {
            const fileInput = zone.querySelector('.media-picker-file-input');
            if (fileInput) { fileInput.click(); }
        }
    });

    const fileInput = zone.querySelector('.media-picker-file-input');
    if (fileInput) {
        fileInput.addEventListener('change', function () {
            handlePickerFiles(fileInput.files, uploadUrl, queue, gridId, modalId, targetInputId);
            fileInput.value = '';
        });
    }
}

// Re-initialize after each HTMX swap (picker body is refreshed on modal open and search)
document.addEventListener('htmx:afterSwap', function () {
    document.querySelectorAll('.media-picker-upload-zone:not([data-picker-upload-init])').forEach(initPickerDropZone);
});

// Also init on DOMContentLoaded in case a zone is already present
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.media-picker-upload-zone').forEach(initPickerDropZone);
});
