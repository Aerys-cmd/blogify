(function () {
    'use strict';

    var form = document.querySelector('[data-post-editor-page]');
    if (!form) return;

    var mode = form.dataset.editorMode;
    var drawer = document.getElementById('postSettingsDrawer');
    var openButton = form.querySelector('.pe-settings-open');
    var closeButtons = form.querySelectorAll('[data-settings-close]');
    var titleInput = document.getElementById('postTitle');
    var slugInput = document.getElementById('postSlug');
    var slugDisplay = document.getElementById('slugDisplay');
    var slugDisplayWrap = document.getElementById('slugDisplayWrap');
    var slugEditWrap = document.getElementById('slugEditWrap');
    var editorHidden = document.getElementById('postEditorHidden');
    var saveStatus = document.getElementById('peSaveStatus');
    var lastDrawerTrigger = null;
    var initialEditorContent = null;
    var slugManuallyEdited = mode === 'edit' || Boolean(slugInput && slugInput.value);

    function openDrawer(focusTarget) {
        if (!drawer) return;
        lastDrawerTrigger = document.activeElement;
        form.classList.add('pe-settings-visible');
        drawer.removeAttribute('inert');
        drawer.setAttribute('aria-hidden', 'false');
        if (openButton) openButton.setAttribute('aria-expanded', 'true');
        window.requestAnimationFrame(function () {
            (focusTarget || drawer.querySelector('button, input, textarea, select, [tabindex]:not([tabindex="-1"])'))?.focus();
        });
    }

    function closeDrawer() {
        if (!drawer || !form.classList.contains('pe-settings-visible')) return;
        form.classList.remove('pe-settings-visible');
        drawer.setAttribute('inert', '');
        drawer.setAttribute('aria-hidden', 'true');
        if (openButton) openButton.setAttribute('aria-expanded', 'false');
        if (lastDrawerTrigger instanceof HTMLElement) lastDrawerTrigger.focus();
    }

    openButton?.addEventListener('click', function () { openDrawer(); });
    closeButtons.forEach(function (button) { button.addEventListener('click', closeDrawer); });
    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') closeDrawer();
        if (event.key !== 'Tab' || !drawer || !form.classList.contains('pe-settings-visible')) return;
        var focusable = Array.from(drawer.querySelectorAll('button:not([disabled]), input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'));
        if (!focusable.length) return;
        var first = focusable[0];
        var last = focusable[focusable.length - 1];
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    });

    function resizeTitle() {
        if (!titleInput) return;
        titleInput.style.height = 'auto';
        titleInput.style.height = titleInput.scrollHeight + 'px';
    }

    function generateSlug(text) {
        return text.toLowerCase().trim()
            .replace(/[^a-z0-9\s-]/g, '')
            .replace(/\s+/g, '-')
            .replace(/-+/g, '-')
            .replace(/^-|-$/g, '');
    }

    function updateSlugDisplay() {
        if (slugDisplay && slugInput) slugDisplay.textContent = slugInput.value || '\u2026';
        updateSerp();
    }

    document.getElementById('slugEditBtn')?.addEventListener('click', function () {
        openDrawer(slugInput);
    });
    document.getElementById('slugDoneBtn')?.addEventListener('click', function () {
        updateSlugDisplay();
        closeDrawer();
    });

    slugInput?.addEventListener('keydown', function (event) {
        if (event.key === 'Enter') event.preventDefault();
    });
    slugInput?.addEventListener('input', function () {
        slugManuallyEdited = true;
        updateSlugDisplay();
    });
    titleInput?.addEventListener('input', function () {
        resizeTitle();
        if (mode === 'create' && slugInput && !slugManuallyEdited) slugInput.value = generateSlug(titleInput.value);
        updateSlugDisplay();
    });

    function extractText(blocks) {
        return blocks.reduce(function (text, block) {
            var ownText = Array.isArray(block.content)
                ? block.content.filter(function (item) { return item.type === 'text'; }).map(function (item) { return item.text; }).join(' ')
                : '';
            var childText = Array.isArray(block.children) ? extractText(block.children) : '';
            return text + ' ' + ownText + ' ' + childText;
        }, '');
    }

    function updateWordCount(json) {
        var output = document.getElementById('wordCountDisplay');
        if (!output) return;
        var text = '';
        try {
            var blocks = JSON.parse(json || '[]');
            if (Array.isArray(blocks)) text = extractText(blocks);
        } catch (_) { }
        var words = text.trim() ? text.trim().split(/\s+/).length : 0;
        output.textContent = words + ' words \u00b7 ' + Math.max(1, Math.ceil(words / 238)) + ' min read';
    }

    function markUnsaved() {
        if (!saveStatus) return;
        saveStatus.className = 'pe-save-status pe-unsaved';
        saveStatus.textContent = '\u25cf ' + (form.dataset.unsavedLabel || 'Unsaved changes');
    }

    if (editorHidden) {
        var descriptor = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value');
        if (descriptor) {
            Object.defineProperty(editorHidden, 'value', {
                get: function () { return descriptor.get.call(this); },
                set: function (value) {
                    descriptor.set.call(this, value);
                    updateWordCount(value);
                    if (initialEditorContent === null) initialEditorContent = value;
                    else if (value !== initialEditorContent) markUnsaved();
                }
            });
        }
        updateWordCount(editorHidden.value);
    }

    function counter(inputId, outputId, max) {
        var input = document.getElementById(inputId);
        var output = document.getElementById(outputId);
        if (!input || !output) return;
        function update() {
            output.textContent = input.value.length + '/' + max;
            output.classList.toggle('warn', input.value.length > max * 0.8 && input.value.length < max);
            output.classList.toggle('over', input.value.length >= max);
        }
        input.addEventListener('input', update);
        update();
    }

    function updateSerp() {
        var metaTitle = document.getElementById('metaTitleInput');
        var metaDescription = document.getElementById('metaDescInput');
        var serpSlug = document.getElementById('serpSlug');
        var serpTitle = document.getElementById('serpTitle');
        var serpDescription = document.getElementById('serpDesc');
        if (serpSlug) serpSlug.textContent = slugInput?.value || '\u2026';
        if (serpTitle) serpTitle.textContent = metaTitle?.value || titleInput?.value || 'Your post title';
        if (serpDescription) serpDescription.textContent = metaDescription?.value || 'Your meta description will appear here\u2026';
    }

    ['metaTitleInput', 'metaDescInput'].forEach(function (id) {
        document.getElementById(id)?.addEventListener('input', updateSerp);
    });
    counter('excerptInput', 'excerptCount', 500);
    counter('metaTitleInput', 'metaTitleCount', 60);
    counter('metaDescInput', 'metaDescCount', 160);

    var checkboxHost = document.getElementById('categoryCheckboxes');
    var pills = document.getElementById('categoryPills');
    var search = document.getElementById('categorySearch');
    var dropdown = document.getElementById('categoryDropdown');
    var badge = document.getElementById('categoriesBadge');
    if (checkboxHost && pills && search && dropdown) {
        function categories() {
            return Array.from(checkboxHost.querySelectorAll('input[type="checkbox"]')).map(function (checkbox) {
                return { checkbox: checkbox, name: checkboxHost.querySelector('label[for="' + checkbox.id + '"]')?.textContent.trim() || checkbox.value };
            });
        }
        function renderPills() {
            pills.innerHTML = '';
            var selected = categories().filter(function (category) { return category.checkbox.checked; });
            selected.forEach(function (category) {
                var pill = document.createElement('span');
                pill.className = 'pe-category-pill';
                pill.append(document.createTextNode(category.name + ' '));
                var remove = document.createElement('button');
                remove.type = 'button';
                remove.className = 'pe-category-pill-remove';
                remove.setAttribute('aria-label', (form.dataset.removeLabel || 'Remove') + ' ' + category.name);
                remove.textContent = '\u00d7';
                remove.addEventListener('click', function () { category.checkbox.checked = false; renderPills(); renderOptions(''); markUnsaved(); });
                pill.append(remove);
                pills.append(pill);
            });
            if (badge) {
                badge.textContent = selected.length;
                badge.hidden = selected.length === 0;
            }
        }
        function renderOptions(query) {
            dropdown.innerHTML = '';
            categories().filter(function (category) {
                return !category.checkbox.checked && category.name.toLowerCase().includes(query.toLowerCase());
            }).forEach(function (category) {
                var option = document.createElement('button');
                option.type = 'button';
                option.className = 'pe-category-option';
                option.setAttribute('role', 'option');
                option.textContent = category.name;
                option.addEventListener('click', function () { category.checkbox.checked = true; search.value = ''; dropdown.classList.remove('show'); renderPills(); markUnsaved(); });
                dropdown.append(option);
            });
            dropdown.classList.toggle('show', dropdown.childElementCount > 0);
        }
        search.addEventListener('input', function () { renderOptions(search.value); });
        search.addEventListener('focus', function () { renderOptions(search.value); });
        search.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') { event.preventDefault(); dropdown.querySelector('button')?.click(); }
        });
        document.addEventListener('click', function (event) {
            if (!event.target.closest('.pe-category-search-wrap')) dropdown.classList.remove('show');
        });
        renderPills();
    }

    form.addEventListener('input', markUnsaved);
    form.addEventListener('change', markUnsaved);
    form.addEventListener('invalid', function (event) {
        if (drawer?.contains(event.target)) openDrawer(event.target);
    }, true);
    if (window.jQuery) {
        window.jQuery(form).on('invalid-form.validate', function (_, validator) {
            var field = validator?.errorList?.[0]?.element;
            if (!field || !drawer?.contains(field)) return;
            var section = field.closest('.collapse');
            if (section && window.bootstrap) window.bootstrap.Collapse.getOrCreateInstance(section, { toggle: false }).show();
            openDrawer(field);
        });
    }

    function revealServerError() {
        var error = drawer?.querySelector('.field-validation-error');
        if (!error) {
            var pageError = form.querySelector('.field-validation-error');
            var pageField = pageError?.previousElementSibling?.matches('input, textarea, select')
                ? pageError.previousElementSibling
                : pageError?.parentElement?.querySelector('input, textarea, select, button');
            pageField?.focus();
            return;
        }
        var section = error.closest('.collapse');
        if (section && window.bootstrap) window.bootstrap.Collapse.getOrCreateInstance(section, { toggle: false }).show();
        var field = error.previousElementSibling?.matches('input, textarea, select')
            ? error.previousElementSibling
            : error.closest('.pe-settings-body')?.querySelector('input, textarea, select, button');
        openDrawer(field);
    }

    if (mode === 'create') {
        var draftKey = 'pe_draft_create';
        var draftTimer;
        try {
            var savedDraft = JSON.parse(localStorage.getItem(draftKey) || 'null');
            if (savedDraft?.title && savedDraft.savedAt && Date.now() - savedDraft.savedAt < 86400000 && !titleInput?.value) {
                var banner = document.createElement('div');
                banner.className = 'pe-draft-recovery';
                banner.setAttribute('role', 'status');
                var draftMessage = document.createElement('span');
                draftMessage.textContent = (form.dataset.draftLabel || 'Unsaved local draft') + ': "' + savedDraft.title.substring(0, 60) + '"';
                var restore = document.createElement('button');
                restore.type = 'button';
                restore.className = 'btn btn-sm btn-outline-secondary';
                restore.textContent = form.dataset.restoreLabel || 'Restore draft';
                restore.addEventListener('click', function () {
                    titleInput.value = savedDraft.title;
                    if (slugInput) slugInput.value = savedDraft.slug || '';
                    slugManuallyEdited = Boolean(savedDraft.slug);
                    resizeTitle();
                    updateSlugDisplay();
                    markUnsaved();
                    banner.remove();
                    localStorage.removeItem(draftKey);
                });
                banner.append(draftMessage, restore);
                form.prepend(banner);
            }
        } catch (_) { }
        titleInput?.addEventListener('input', function () {
            window.clearTimeout(draftTimer);
            draftTimer = window.setTimeout(function () {
                try { localStorage.setItem(draftKey, JSON.stringify({ title: titleInput.value, slug: slugInput?.value || '', savedAt: Date.now() })); } catch (_) { }
            }, 2000);
        });
        form.addEventListener('submit', function () { localStorage.removeItem(draftKey); });
    }

    resizeTitle();
    updateSlugDisplay();
    updateSerp();
    revealServerError();
    if (form.dataset.showSaved === 'true' && saveStatus) {
        saveStatus.className = 'pe-save-status pe-saved';
        saveStatus.textContent = form.dataset.savedLabel || 'Saved';
    }
    var toast = document.getElementById('saveToast');
    if (toast && window.bootstrap) window.bootstrap.Toast.getOrCreateInstance(toast).show();
})();
