export async function initPostEditor(wrapperId, hiddenTextareaId, formId) {
    const wrapperEl = document.getElementById(wrapperId);
    const hiddenTextareaEl = document.getElementById(hiddenTextareaId);
    const formEl = document.getElementById(formId);

    if (!wrapperEl || !hiddenTextareaEl || !formEl) return;

    const initialContent = hiddenTextareaEl.value || '';

    function syncToHidden(html) {
        hiddenTextareaEl.value = html;
        if (window.jQuery) {
            const $form = window.jQuery(formEl);
            if ($form.data('validator')) {
                window.jQuery(hiddenTextareaEl).valid();
            }
        }
    }

    function configureJQueryValidation() {
        if (window.jQuery && window.jQuery.validator) {
            window.jQuery.validator.setDefaults({
                ignore: ':hidden:not(#' + hiddenTextareaId + ')'
            });
        }
    }

    let tiptapLoaded = false;

    try {
        const [{ Editor }, { StarterKit }, { Placeholder }, { Image }] = await Promise.all([
            import('https://esm.sh/@tiptap/core@2'),
            import('https://esm.sh/@tiptap/starter-kit@2'),
            import('https://esm.sh/@tiptap/extension-placeholder@2'),
            import('https://esm.sh/@tiptap/extension-image@2')
        ]);

        tiptapLoaded = true;
        configureJQueryValidation();

        const editorContentEl = document.createElement('div');
        editorContentEl.className = 'post-editor-tiptap';
        wrapperEl.appendChild(editorContentEl);

        const toolbar = buildTiptapToolbar();
        wrapperEl.insertBefore(toolbar, editorContentEl);

        const editor = new Editor({
            element: editorContentEl,
            extensions: [StarterKit, Placeholder.configure({ placeholder: 'Write your post content here\u2026' }), Image.configure({ inline: false, allowBase64: false })],
            content: initialContent,
            onUpdate({ editor: e }) {
                syncToHidden(e.getHTML());
            },
            onSelectionUpdate({ editor: e }) {
                updateToolbarState(toolbar, e);
            },
            onTransaction({ editor: e }) {
                updateToolbarState(toolbar, e);
            }
        });

        syncToHidden(editor.getHTML());

        document.addEventListener('mediaSelected', function onEditorMediaSelected(e) {
            if (e.detail.targetInputId !== 'editorImageInsert') return;
            const src = e.detail.fullUrl ?? e.detail.url;
            const alt = e.detail.altText ?? '';
            if (src) {
                editor.chain().focus().setImage({ src, alt }).run();
            }
        });

        wireToolbarButtons(toolbar, editor);

        formEl.addEventListener('submit', function () {
            syncToHidden(editor.getHTML());
        });

    } catch (err) {
        if (tiptapLoaded) throw err;

        try {
            configureJQueryValidation();
            await loadQuillAssets();

            const quillWrapper = document.createElement('div');
            quillWrapper.className = 'post-editor-quill';
            wrapperEl.appendChild(quillWrapper);

            const quillEl = document.createElement('div');
            quillWrapper.appendChild(quillEl);

            const quill = new window.Quill(quillEl, {
                theme: 'snow',
                modules: {
                    toolbar: [
                        ['bold', 'italic', 'underline', 'strike'],
                        ['link', 'blockquote', 'code-block'],
                        [{ list: 'ordered' }, { list: 'bullet' }],
                        [{ header: 2 }, { header: 3 }]
                    ]
                }
            });

            if (initialContent) {
                quill.clipboard.dangerouslyPasteHTML(initialContent);
            }

            syncToHidden(quill.root.innerHTML);

            quill.on('text-change', function () {
                syncToHidden(quill.root.innerHTML);
            });

            formEl.addEventListener('submit', function () {
                syncToHidden(quill.root.innerHTML);
            });
        } catch (_quillErr) {
            hiddenTextareaEl.classList.remove('visually-hidden');
            hiddenTextareaEl.removeAttribute('aria-hidden');
            hiddenTextareaEl.removeAttribute('tabindex');
            hiddenTextareaEl.classList.add('form-control');
        }
    }
}

function buildTiptapToolbar() {
    const toolbar = document.createElement('div');
    toolbar.className = 'btn-toolbar border-bottom p-1 bg-light';
    toolbar.setAttribute('role', 'toolbar');
    toolbar.setAttribute('aria-label', 'Text formatting');

    const groups = [
        [
            { label: 'Bold', command: 'toggleBold', mark: 'bold' },
            { label: 'Italic', command: 'toggleItalic', mark: 'italic' },
            { label: 'Strike', command: 'toggleStrike', mark: 'strike' }
        ],
        [
            { label: 'H2', command: 'toggleHeading', attrs: { level: 2 }, node: 'heading', nodeAttrs: { level: 2 } },
            { label: 'H3', command: 'toggleHeading', attrs: { level: 3 }, node: 'heading', nodeAttrs: { level: 3 } }
        ],
        [
            { label: 'Blockquote', command: 'toggleBlockquote', node: 'blockquote' },
            { label: 'Code block', command: 'toggleCodeBlock', node: 'codeBlock' }
        ],
        [
            { label: 'Bullet list', command: 'toggleBulletList', node: 'bulletList' },
            { label: 'Ordered list', command: 'toggleOrderedList', node: 'orderedList' }
        ],
        [
            { label: 'Undo', command: 'undo' },
            { label: 'Redo', command: 'redo' }
        ],
        [
            { label: 'Insert image', command: 'insertImage' }
        ]
    ];

    groups.forEach(function (group) {
        const groupEl = document.createElement('div');
        groupEl.className = 'btn-group btn-group-sm me-1';
        groupEl.setAttribute('role', 'group');

        group.forEach(function (item) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn btn-outline-secondary';
            btn.setAttribute('aria-label', item.label);
            btn.textContent = item.label;
            btn.dataset.command = item.command;
            if (item.attrs) btn.dataset.attrs = JSON.stringify(item.attrs);
            if (item.mark) btn.dataset.mark = item.mark;
            if (item.node) btn.dataset.node = item.node;
            if (item.nodeAttrs) btn.dataset.nodeAttrs = JSON.stringify(item.nodeAttrs);
            groupEl.appendChild(btn);
        });

        toolbar.appendChild(groupEl);
    });

    return toolbar;
}

function wireToolbarButtons(toolbar, editor) {
    function runCommand(btn) {
        const command = btn.dataset.command;

        if (command === 'insertImage') {
            const modalEl = document.getElementById('editorImageInsert-modal');
            const bodyEl  = document.getElementById('editorImageInsert-modal-body');
            if (!modalEl || !bodyEl) return;

            window.htmx.ajax(
                'GET',
                '/admin/Media?handler=MediaPicker&targetInputId=editorImageInsert&modalId=editorImageInsert-modal',
                { target: '#editorImageInsert-modal-body', swap: 'innerHTML' }
            );
            window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
            return;
        }

        const attrs = btn.dataset.attrs ? JSON.parse(btn.dataset.attrs) : undefined;
        if (attrs !== undefined) {
            editor.chain().focus()[command](attrs).run();
        } else {
            editor.chain().focus()[command]().run();
        }
    }

    toolbar.addEventListener('mousedown', function (e) {
        const btn = e.target.closest('button[data-command]');
        if (!btn) return;
        e.preventDefault();
        btn._pointerActivated = true;
        runCommand(btn);
    });

    toolbar.addEventListener('click', function (e) {
        const btn = e.target.closest('button[data-command]');
        if (!btn) return;
        if (btn._pointerActivated) {
            btn._pointerActivated = false;
            return;
        }
        runCommand(btn);
    });
}

function updateToolbarState(toolbar, editor) {
    const buttons = toolbar.querySelectorAll('button[data-command]');
    buttons.forEach(function (btn) {
        const mark = btn.dataset.mark;
        const node = btn.dataset.node;
        const nodeAttrs = btn.dataset.nodeAttrs ? JSON.parse(btn.dataset.nodeAttrs) : undefined;

        let isActive = false;
        if (mark) {
            isActive = editor.isActive(mark);
        } else if (node && nodeAttrs) {
            isActive = editor.isActive(node, nodeAttrs);
        } else if (node) {
            isActive = editor.isActive(node);
        }

        if (isActive) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });
}

function loadQuillAssets() {
    return new Promise(function (resolve, reject) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = '/lib/quill/quill.snow.css';
        document.head.appendChild(link);

        const script = document.createElement('script');
        script.src = '/lib/quill/quill.js';
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}
