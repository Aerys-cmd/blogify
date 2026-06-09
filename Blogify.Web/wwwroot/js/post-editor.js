const SLASH_ITEMS = [
    { title: 'Text',          icon: 'T',     desc: 'Normal paragraph',       command: ({ editor, range }) => editor.chain().focus().deleteRange(range).setNode('paragraph').run() },
    { title: 'Heading 1',    icon: 'H1',    desc: 'Large section heading',   command: ({ editor, range }) => editor.chain().focus().deleteRange(range).setHeading({ level: 1 }).run() },
    { title: 'Heading 2',    icon: 'H2',    desc: 'Medium section heading',  command: ({ editor, range }) => editor.chain().focus().deleteRange(range).setHeading({ level: 2 }).run() },
    { title: 'Heading 3',    icon: 'H3',    desc: 'Small section heading',   command: ({ editor, range }) => editor.chain().focus().deleteRange(range).setHeading({ level: 3 }).run() },
    { title: 'Bullet List',  icon: '\u2022', desc: 'Unordered list',         command: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleBulletList().run() },
    { title: 'Numbered List',icon: '1.',    desc: 'Ordered list',            command: ({ editor, range }) => editor.chain().focus().deleteRange(range).toggleOrderedList().run() },
    { title: 'Blockquote',   icon: '\u201c', desc: 'Callout quote',          command: ({ editor, range }) => editor.chain().focus().deleteRange(range).setBlockquote().run() },
    { title: 'Code Block',   icon: '</>',   desc: 'Code snippet',            command: ({ editor, range }) => editor.chain().focus().deleteRange(range).setCodeBlock().run() },
    { title: 'Divider',      icon: '\u2014', desc: 'Horizontal separator',   command: ({ editor, range }) => editor.chain().focus().deleteRange(range).setHorizontalRule().run() },
    { title: 'Image',        icon: '\u{1f5bc}', desc: 'Image from library', command: ({ editor, range }) => { editor.chain().focus().deleteRange(range).run(); openEditorImageModal(); } },
];

export async function initPostEditor(wrapperId, hiddenTextareaId, formId) {
    const wrapperEl        = document.getElementById(wrapperId);
    const hiddenTextareaEl = document.getElementById(hiddenTextareaId);
    const formEl           = document.getElementById(formId);

    if (!wrapperEl || !hiddenTextareaEl || !formEl) return;

    const rawContent = hiddenTextareaEl.value || '';
    let initialContent = null;
    if (rawContent) {
        try { initialContent = JSON.parse(rawContent); } catch { initialContent = null; }
    }

    const [{ Editor, Extension }, { StarterKit }, { Placeholder }, { Image }] = await Promise.all([
        import('https://esm.sh/@tiptap/core@2'),
        import('https://esm.sh/@tiptap/starter-kit@2'),
        import('https://esm.sh/@tiptap/extension-placeholder@2'),
        import('https://esm.sh/@tiptap/extension-image@2'),
    ]);

    let tableExtensions = [];
    try {
        const [{ Table }, { TableRow }, { TableCell }, { TableHeader }] = await Promise.all([
            import('https://esm.sh/@tiptap/extension-table@2'),
            import('https://esm.sh/@tiptap/extension-table-row@2'),
            import('https://esm.sh/@tiptap/extension-table-cell@2'),
            import('https://esm.sh/@tiptap/extension-table-header@2'),
        ]);
        tableExtensions = [Table.configure({ resizable: false }), TableRow, TableHeader, TableCell];
        SLASH_ITEMS.splice(SLASH_ITEMS.length - 1, 0, {
            title: 'Table', icon: '\u229e', desc: '3\u00d73 grid',
            command: ({ editor, range }) => editor.chain().focus().deleteRange(range).insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(),
        });
    } catch { /* tables unavailable */ }

    if (window.jQuery && window.jQuery.validator) {
        window.jQuery.validator.setDefaults({ ignore: ':hidden:not(#' + hiddenTextareaId + ')' });
    }

    function syncToHidden(json) {
        hiddenTextareaEl.value = JSON.stringify(json);
        if (window.jQuery) {
            const $form = window.jQuery(formEl);
            if ($form.data('validator')) window.jQuery(hiddenTextareaEl).valid();
        }
    }

    const editorContentEl = document.createElement('div');
    editorContentEl.className = 'post-editor-tiptap';
    wrapperEl.appendChild(editorContentEl);

    const blockHandleEl = buildBlockHandleEl();
    const slashMenuEl   = buildSlashMenuEl();
    const bubbleMenuEl  = buildBubbleMenuEl();
    document.body.appendChild(blockHandleEl);
    document.body.appendChild(slashMenuEl);
    document.body.appendChild(bubbleMenuEl);

    const slash = { active: false, items: [], selectedIndex: 0, triggerFrom: -1, triggerTo: -1 };

    window.addEventListener('scroll', () => closeSlashMenu(slash, slashMenuEl), { passive: true });

    const SlashCommand = Extension.create({
        name: 'slashCommand',
        addKeyboardShortcuts() {
            return {
                ArrowDown: () => {
                    if (!slash.active || !slash.items.length) return false;
                    slash.selectedIndex = (slash.selectedIndex + 1) % slash.items.length;
                    renderSlashItems(slashMenuEl, slash);
                    return true;
                },
                ArrowUp: () => {
                    if (!slash.active || !slash.items.length) return false;
                    slash.selectedIndex = (slash.selectedIndex - 1 + slash.items.length) % slash.items.length;
                    renderSlashItems(slashMenuEl, slash);
                    return true;
                },
                Enter: () => {
                    if (!slash.active) return false;
                    executeSlashItem(this.editor, slash, slashMenuEl);
                    return true;
                },
                Escape: () => {
                    if (!slash.active) return false;
                    closeSlashMenu(slash, slashMenuEl);
                    return true;
                },
            };
        },
        onUpdate()          { detectSlashTrigger(this.editor, slash, slashMenuEl); },
        onSelectionUpdate() {
            if (!slash.active) return;
            if (this.editor.state.selection.$from.pos < slash.triggerFrom) {
                closeSlashMenu(slash, slashMenuEl);
            }
        },
        onBlur() { setTimeout(() => closeSlashMenu(slash, slashMenuEl), 150); },
    });

    const editor = new Editor({
        element: editorContentEl,
        extensions: [
            StarterKit,
            Placeholder.configure({ placeholder: "Type '/' for commands\u2026" }),
            Image.configure({ inline: false, allowBase64: false }),
            ...tableExtensions,
            SlashCommand,
        ],
        content: initialContent,
        onUpdate({ editor: e })          { syncToHidden(e.getJSON()); },
        onSelectionUpdate({ editor: e }) { updateBubbleMenu(bubbleMenuEl, e); },
        onBlur()                         { setTimeout(() => hideBubbleMenu(bubbleMenuEl), 150); },
    });

    syncToHidden(editor.getJSON());
    initBlockHandle(editor, blockHandleEl);
    wireBubbleMenuButtons(bubbleMenuEl, editor);

    formEl.addEventListener('submit', () => syncToHidden(editor.getJSON()));

    document.addEventListener('mediaSelected', function onEditorMediaSelected(e) {
        if (e.detail.targetInputId !== 'editorImageInsert') return;
        const src = e.detail.fullUrl ?? e.detail.url;
        const alt = e.detail.altText ?? '';
        if (src) editor.chain().focus().setImage({ src, alt }).run();
    });

    editor.on('destroy', () => {
        blockHandleEl.remove();
        slashMenuEl.remove();
        bubbleMenuEl.remove();
    });
}

// ---------------------------------------------------------------------------
// Block handle — hover "+" (insert) and drag handle (reorder)
// ---------------------------------------------------------------------------

function buildBlockHandleEl() {
    const el = document.createElement('div');
    el.className = 'post-editor-block-handle';

    const addBtn = document.createElement('button');
    addBtn.type = 'button';
    addBtn.className = 'post-editor-handle-btn';
    addBtn.setAttribute('aria-label', 'Add block below');
    addBtn.setAttribute('data-handle-add', '');
    addBtn.innerHTML =
        '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round">' +
        '<line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>';

    const dragBtn = document.createElement('button');
    dragBtn.type = 'button';
    dragBtn.className = 'post-editor-handle-btn post-editor-handle-drag';
    dragBtn.setAttribute('aria-label', 'Drag to reorder');
    dragBtn.setAttribute('data-handle-drag', '');
    dragBtn.innerHTML =
        '<svg width="10" height="14" viewBox="0 0 10 16" fill="currentColor">' +
        '<circle cx="2" cy="2" r="1.5"/><circle cx="8" cy="2" r="1.5"/>' +
        '<circle cx="2" cy="7" r="1.5"/><circle cx="8" cy="7" r="1.5"/>' +
        '<circle cx="2" cy="12" r="1.5"/><circle cx="8" cy="12" r="1.5"/></svg>';

    el.appendChild(addBtn);
    el.appendChild(dragBtn);
    return el;
}

function initBlockHandle(editor, handleEl) {
    const pmEl = editor.view.dom;
    let activeNodeEl = null;
    let hideTimer    = null;

    // ---- show / hide ----

    function show(nodeEl) {
        clearTimeout(hideTimer);
        activeNodeEl = nodeEl;
        const rect = nodeEl.getBoundingClientRect();
        const lh   = Math.min(Math.max(parseInt(getComputedStyle(nodeEl).lineHeight) || 28, 20), 36);
        handleEl.style.top           = (rect.top + lh / 2 - 12) + 'px';
        handleEl.style.left          = Math.max(4, rect.left - 58) + 'px';
        handleEl.style.opacity       = '1';
        handleEl.style.pointerEvents = 'all';
    }

    function hide(immediate = false) {
        clearTimeout(hideTimer);
        if (immediate) {
            handleEl.style.opacity       = '0';
            handleEl.style.pointerEvents = 'none';
            return;
        }
        hideTimer = setTimeout(() => {
            handleEl.style.opacity       = '0';
            handleEl.style.pointerEvents = 'none';
        }, 250);
    }

    pmEl.addEventListener('mousemove', e => {
        let target = e.target;
        while (target && target.parentElement !== pmEl) target = target.parentElement;
        if (!target || target === pmEl) { hide(); return; }
        if (target !== activeNodeEl) show(target);
        else clearTimeout(hideTimer);
    });

    pmEl.addEventListener('mouseleave', () => hide());
    window.addEventListener('scroll', () => hide(true), { passive: true });
    handleEl.addEventListener('mouseenter', () => clearTimeout(hideTimer));
    handleEl.addEventListener('mouseleave', () => hide());

    // ---- "+" button: insert new block and open slash menu ----

    handleEl.querySelector('[data-handle-add]').addEventListener('mousedown', e => {
        e.preventDefault();
        if (!activeNodeEl) return;

        const rect    = activeNodeEl.getBoundingClientRect();
        const posInfo = editor.view.posAtCoords({ left: rect.left + 2, top: rect.top + 2 });
        if (!posInfo) return;

        try {
            const $pos   = editor.state.doc.resolve(posInfo.pos);
            const endPos = $pos.depth >= 1 ? $pos.after(1) : editor.state.doc.content.size;
            editor.chain()
                .focus()
                .insertContentAt(endPos, { type: 'paragraph', content: [{ type: 'text', text: '/' }] })
                .setTextSelection(endPos + 2)
                .run();
        } catch {
            editor.commands.focus('end');
        }
        hide(true);
    });

    // ---- Drag handle: custom mouse-driven block reorder ----

    handleEl.querySelector('[data-handle-drag]').addEventListener('mousedown', e => {
        e.preventDefault();
        if (!activeNodeEl) return;

        const srcRect = activeNodeEl.getBoundingClientRect();
        const posInfo = editor.view.posAtCoords({ left: srcRect.left + 2, top: srcRect.top + 2 });
        if (!posInfo) return;

        let fromPos, fromNode;
        try {
            const $pos = editor.state.doc.resolve(posInfo.pos);
            if ($pos.depth < 1) return;
            fromPos  = $pos.before(1);
            fromNode = $pos.node(1);
        } catch { return; }

        // Ghost — a small floating preview of the block being dragged.
        const ghost = document.createElement('div');
        ghost.className = 'post-editor-drag-ghost';
        ghost.style.left  = srcRect.left + 'px';
        ghost.style.top   = srcRect.top  + 'px';
        ghost.style.width = srcRect.width + 'px';
        ghost.textContent = activeNodeEl.textContent.slice(0, 80) || '\u00a0';
        document.body.appendChild(ghost);

        // Blue drop-indicator line.
        const indicator = document.createElement('div');
        indicator.className = 'post-editor-drag-indicator';
        document.body.appendChild(indicator);

        activeNodeEl.classList.add('post-editor-dragging-source');

        const startX = e.clientX;
        const startY = e.clientY;
        let isDragging  = false;
        let insertPos   = null; // doc position where node should be inserted

        function onMouseMove(ev) {
            const dy = ev.clientY - startY;
            const dx = ev.clientX - startX;

            if (!isDragging && (Math.abs(dy) > 4 || Math.abs(dx) > 4)) {
                isDragging = true;
                hide(true);
            }
            if (!isDragging) return;

            // Follow the cursor.
            ghost.style.top  = (srcRect.top  + dy) + 'px';
            ghost.style.left = (srcRect.left + dx * 0.15) + 'px'; // subtle horizontal lean

            // Find the nearest sibling block in the ProseMirror container.
            const siblings = Array.from(pmEl.children);
            let bestEl    = null;
            let bestDist  = Infinity;
            let bestBefore = true;

            siblings.forEach(child => {
                if (child === activeNodeEl) return;
                const cr  = child.getBoundingClientRect();
                const mid = cr.top + cr.height / 2;
                // Distance to the insertion line (top edge if before, bottom edge if after).
                const before = ev.clientY <= mid;
                const lineY  = before ? cr.top : cr.bottom;
                const dist   = Math.abs(ev.clientY - lineY);
                if (dist < bestDist) {
                    bestDist   = dist;
                    bestEl     = child;
                    bestBefore = before;
                }
            });

            if (!bestEl) {
                indicator.style.display = 'none';
                insertPos = null;
                return;
            }

            // Resolve the insert position in the ProseMirror document.
            const cr = bestEl.getBoundingClientRect();
            try {
                const pi = editor.view.posAtCoords({ left: cr.left + 2, top: cr.top + 2 });
                if (pi) {
                    const $p = editor.state.doc.resolve(pi.pos);
                    if ($p.depth >= 1) {
                        insertPos = bestBefore ? $p.before(1) : $p.after(1);
                    }
                }
            } catch { insertPos = null; }

            // Draw the blue indicator line.
            const lineY = bestBefore ? cr.top : cr.bottom;
            indicator.style.cssText =
                'left:' + cr.left + 'px;' +
                'top:'  + (lineY - 1) + 'px;' +
                'width:' + cr.width + 'px;' +
                'display:block;';
        }

        function onMouseUp() {
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup',   onMouseUp);

            ghost.remove();
            indicator.remove();
            activeNodeEl.classList.remove('post-editor-dragging-source');

            if (!isDragging || insertPos == null) return;

            const fromEnd  = fromPos + fromNode.nodeSize;

            // Skip no-ops (inserting right before or right after the source).
            if (insertPos === fromPos || insertPos === fromEnd) return;

            try {
                const tr       = editor.state.tr;
                const nodeSize = fromNode.nodeSize;

                if (insertPos < fromPos) {
                    // Insert before source: positions after insertPos shift up by nodeSize.
                    tr.insert(insertPos, fromNode);
                    tr.delete(fromPos + nodeSize, fromEnd + nodeSize);
                } else {
                    // Insert after source: delete first, then insert at adjusted position.
                    tr.delete(fromPos, fromEnd);
                    tr.insert(insertPos - nodeSize, fromNode);
                }

                editor.view.dispatch(tr);
            } catch { /* leave document unchanged on error */ }
        }

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup',   onMouseUp);
    });
}

// ---------------------------------------------------------------------------
// Slash command menu
// ---------------------------------------------------------------------------

function buildSlashMenuEl() {
    const el = document.createElement('div');
    el.className = 'post-editor-slash-menu';
    el.setAttribute('role', 'listbox');
    el.setAttribute('aria-label', 'Insert block');
    el.style.display = 'none';
    return el;
}

function detectSlashTrigger(editor, slash, slashMenuEl) {
    const { from, empty } = editor.state.selection;
    if (!empty) { closeSlashMenu(slash, slashMenuEl); return; }

    const $from = editor.state.selection.$from;
    if ($from.parent.type.name !== 'paragraph') { closeSlashMenu(slash, slashMenuEl); return; }

    const textBefore = $from.parent.textContent.slice(0, $from.parentOffset);
    const match      = /(?:^|\s)\/([\w]*)$/.exec(textBefore);
    if (!match) { closeSlashMenu(slash, slashMenuEl); return; }

    const query       = match[1] ?? '';
    const slashOffset = textBefore.lastIndexOf('/');
    const triggerFrom = $from.start() + slashOffset;
    const triggerTo   = from;

    const items = SLASH_ITEMS
        .filter(item => item.title.toLowerCase().includes(query.toLowerCase()))
        .slice(0, 10);

    if (!items.length) { closeSlashMenu(slash, slashMenuEl); return; }

    const wasActive   = slash.active;
    slash.active      = true;
    slash.items       = items;
    slash.triggerFrom = triggerFrom;
    slash.triggerTo   = triggerTo;
    if (!wasActive || slash.selectedIndex >= items.length) slash.selectedIndex = 0;

    renderSlashItems(slashMenuEl, slash);
    positionSlashMenu(slashMenuEl, editor, triggerFrom);
}

function renderSlashItems(menuEl, slash) {
    menuEl.innerHTML = '';
    slash.items.forEach((item, i) => {
        const btn = document.createElement('button');
        btn.type      = 'button';
        btn.className = 'post-editor-slash-item' + (i === slash.selectedIndex ? ' is-selected' : '');
        btn.setAttribute('role', 'option');
        btn.setAttribute('aria-selected', String(i === slash.selectedIndex));
        btn.innerHTML =
            '<span class="post-editor-slash-icon" aria-hidden="true">' + item.icon + '</span>' +
            '<span class="post-editor-slash-label">' +
            '<span class="post-editor-slash-title">' + item.title + '</span>' +
            '<span class="post-editor-slash-desc">' + item.desc  + '</span>' +
            '</span>';
        btn.addEventListener('mousedown', e => {
            e.preventDefault();
            const ed = window.__blogifyEditor;
            if (ed) item.command({ editor: ed, range: { from: slash.triggerFrom, to: slash.triggerTo } });
            closeSlashMenu(slash, menuEl);
        });
        menuEl.appendChild(btn);
    });
    menuEl.style.display = 'block';
}

function positionSlashMenu(menuEl, editor, triggerFrom) {
    window.__blogifyEditor = editor;
    const coords     = editor.view.coordsAtPos(triggerFrom);
    const spaceBelow = window.innerHeight - coords.bottom;
    menuEl.style.left = Math.max(4, Math.min(coords.left, window.innerWidth - 300)) + 'px';
    if (spaceBelow >= 320 || spaceBelow >= coords.top) {
        menuEl.style.top    = coords.bottom + window.scrollY + 6 + 'px';
        menuEl.style.bottom = 'auto';
    } else {
        menuEl.style.top    = 'auto';
        menuEl.style.bottom = window.innerHeight - coords.top + window.scrollY + 6 + 'px';
    }
}

function executeSlashItem(editor, slash, slashMenuEl) {
    const item = slash.items[slash.selectedIndex];
    if (!item) return;
    item.command({ editor, range: { from: slash.triggerFrom, to: slash.triggerTo } });
    closeSlashMenu(slash, slashMenuEl);
}

function closeSlashMenu(slash, menuEl) {
    slash.active = false;
    slash.items  = [];
    menuEl.style.display = 'none';
    menuEl.innerHTML     = '';
}

// ---------------------------------------------------------------------------
// Bubble menu
// ---------------------------------------------------------------------------

function buildBubbleMenuEl() {
    const el = document.createElement('div');
    el.className = 'post-editor-bubble-menu';
    el.style.display = 'none';
    el.setAttribute('role', 'toolbar');
    el.setAttribute('aria-label', 'Text formatting');

    [
        { label: 'Bold',          cmd: 'toggleBold',   mark: 'bold',   html: '<strong>B</strong>' },
        { label: 'Italic',        cmd: 'toggleItalic', mark: 'italic', html: '<em>I</em>' },
        { label: 'Strikethrough', cmd: 'toggleStrike', mark: 'strike', html: '<s>S</s>' },
        { label: 'Inline code',   cmd: 'toggleCode',   mark: 'code',   html: '<code>&lt;/&gt;</code>' },
    ].forEach(item => {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'post-editor-bubble-btn';
        btn.setAttribute('aria-label', item.label);
        btn.setAttribute('data-command', item.cmd);
        btn.setAttribute('data-mark', item.mark);
        btn.innerHTML = item.html;
        el.appendChild(btn);
    });

    return el;
}

function updateBubbleMenu(menuEl, editor) {
    const { empty } = editor.state.selection;
    if (empty || editor.isActive('codeBlock')) { hideBubbleMenu(menuEl); return; }

    const { from } = editor.state.selection;
    const coords   = editor.view.coordsAtPos(from);
    menuEl.style.display  = 'flex';
    menuEl.style.position = 'fixed';
    menuEl.style.left     = coords.left + 'px';
    menuEl.style.top      = (coords.top - 46) + 'px';

    menuEl.querySelectorAll('button[data-mark]').forEach(btn =>
        btn.classList.toggle('is-active', editor.isActive(btn.dataset.mark))
    );
}

function hideBubbleMenu(menuEl) { menuEl.style.display = 'none'; }

function wireBubbleMenuButtons(menuEl, editor) {
    menuEl.addEventListener('mousedown', e => {
        const btn = e.target.closest('button[data-command]');
        if (!btn) return;
        e.preventDefault();
        editor.chain().focus()[btn.dataset.command]().run();
    });
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function openEditorImageModal() {
    const modalEl = document.getElementById('editorImageInsert-modal');
    const bodyEl  = document.getElementById('editorImageInsert-modal-body');
    if (!modalEl || !bodyEl) return;
    window.htmx?.ajax(
        'GET',
        '/admin/Media?handler=MediaPicker&targetInputId=editorImageInsert&modalId=editorImageInsert-modal',
        { target: '#editorImageInsert-modal-body', swap: 'innerHTML' }
    );
    window.bootstrap?.Modal?.getOrCreateInstance(modalEl).show();
}
