// media-upload.js — Multi-file drag-and-drop upload with per-file progress.
// ES module: auto-initialises when the target elements exist on the page.

const ALLOWED_TYPES = new Set(['image/jpeg', 'image/png', 'image/gif', 'image/webp']);
const MAX_BYTES = 10 * 1024 * 1024;

function getAntiForgeryToken() {
    const hxHeaders = document.body.getAttribute('hx-headers');
    if (hxHeaders) {
        try {
            return JSON.parse(hxHeaders)['RequestVerificationToken'] ?? '';
        } catch {
            return '';
        }
    }
    return '';
}

function escapeHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function addQueueItem(queue, name) {
    const li = document.createElement('li');
    li.className = 'list-group-item d-flex align-items-center gap-2 py-2';
    li.innerHTML = `
        <img class="queue-thumb rounded flex-shrink-0"
             width="32" height="32" style="object-fit:cover;" src="" alt="" />
        <div class="flex-grow-1 min-w-0">
            <div class="text-truncate small fw-semibold">${escapeHtml(name)}</div>
            <div class="progress mt-1" style="height: 4px;">
                <div class="progress-bar" role="progressbar" style="width: 0%;"></div>
            </div>
            <div class="queue-status small text-muted mt-1"></div>
        </div>`;
    queue.appendChild(li);
    queue.classList.remove('d-none');
    return li;
}

function setProgress(item, pct) {
    const bar = item.querySelector('.progress-bar');
    if (bar) bar.style.width = pct + '%';
}

function setDone(item) {
    const bar = item.querySelector('.progress-bar');
    if (bar) { bar.style.width = '100%'; bar.classList.add('bg-success'); }
    const status = item.querySelector('.queue-status');
    if (status) { status.textContent = '✓ Uploaded'; status.className = 'queue-status small text-success mt-1'; }
}

function setError(item, msg) {
    const progress = item.querySelector('.progress');
    if (progress) progress.classList.add('d-none');
    const status = item.querySelector('.queue-status');
    if (status) { status.textContent = '✗ ' + msg; status.className = 'queue-status small text-danger mt-1'; }
}

function uploadFile(file, queue, gridContainerId) {
    const queueItem = addQueueItem(queue, file.name);

    if (!ALLOWED_TYPES.has(file.type)) {
        setError(queueItem, 'Not allowed — only JPEG, PNG, GIF, WebP.');
        return;
    }
    if (file.size > MAX_BYTES) {
        setError(queueItem, 'Exceeds 10 MB limit.');
        return;
    }

    // FileReader preview thumbnail
    const reader = new FileReader();
    reader.onload = (e) => {
        const thumb = queueItem.querySelector('.queue-thumb');
        if (thumb) thumb.src = e.target.result;
    };
    reader.readAsDataURL(file);

    // XHR upload with progress
    const formData = new FormData();
    formData.append('file', file);

    const token = getAntiForgeryToken();
    const xhr = new XMLHttpRequest();
    xhr.open('POST', '?handler=Upload');
    if (token) xhr.setRequestHeader('RequestVerificationToken', token);
    xhr.setRequestHeader('HX-Request', 'true');

    xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable) {
            setProgress(queueItem, Math.round((e.loaded / e.total) * 100));
        }
    });

    xhr.addEventListener('load', () => {
        if (xhr.status >= 200 && xhr.status < 300) {
            setDone(queueItem);
            // Prepend new card to grid
            const gridContainer = document.getElementById(gridContainerId);
            if (gridContainer && xhr.responseText.trim()) {
                const wrapper = document.createElement('div');
                wrapper.innerHTML = xhr.responseText.trim();
                const card = wrapper.firstElementChild;
                if (card) gridContainer.prepend(card);
            }
        } else {
            setError(queueItem, 'Upload failed. Please try again.');
        }
    });

    xhr.addEventListener('error', () => setError(queueItem, 'Network error. Please try again.'));

    xhr.send(formData);
}

function handleFiles(files, queue, gridContainerId) {
    Array.from(files).forEach((f) => uploadFile(f, queue, gridContainerId));
}

// Auto-initialise
const dropZone = document.getElementById('upload-drop-zone');
const queue = document.getElementById('upload-queue');
const fileInput = document.getElementById('upload-file-input');
const GRID_CONTAINER_ID = 'media-items-row';

if (dropZone && queue) {
    dropZone.addEventListener('dragover', (e) => {
        e.preventDefault();
        dropZone.classList.add('border-primary', 'bg-primary-subtle');
    });

    dropZone.addEventListener('dragleave', () => {
        dropZone.classList.remove('border-primary', 'bg-primary-subtle');
    });

    dropZone.addEventListener('drop', (e) => {
        e.preventDefault();
        dropZone.classList.remove('border-primary', 'bg-primary-subtle');
        handleFiles(e.dataTransfer.files, queue, GRID_CONTAINER_ID);
    });

    if (fileInput) {
        fileInput.addEventListener('change', () => {
            handleFiles(fileInput.files, queue, GRID_CONTAINER_ID);
            fileInput.value = '';
        });
    }
}

